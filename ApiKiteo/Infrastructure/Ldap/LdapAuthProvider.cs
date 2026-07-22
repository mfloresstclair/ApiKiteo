using Novell.Directory.Ldap;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;

namespace ApiKiteo.API.Infrastructure.Ldap;

/// <summary>
/// Autenticación LDAP cross-platform usando Novell.Directory.Ldap.NETStandard.
/// Reemplaza PrincipalContext (Windows-only) — funciona en Linux con SSSD/AD.
///
/// Dominio: STCLAIR.STCLAIRTECH.COM
/// SSSD ya tiene línea de visión al DC — Novell usa el mismo host.
///
/// Orden de intentos:
///   1. UPN:     mflores@stclair.stclairtech.com  (formato moderno, más confiable)
///   2. NetBIOS: STCLAIR\mflores                  (formato legacy Windows)
///   3. Bare:    mflores                           (fallback)
/// </summary>
public sealed class LdapAuthProvider : ILdapAuthProvider
{
    private readonly LdapOptions _options;
    private readonly ILogger<LdapAuthProvider> _logger;

    public LdapAuthProvider(
        IOptions<LdapOptions> options,
        ILogger<LdapAuthProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> AuthenticateAsync(
        string username, string password, CancellationToken ct = default)
    {
        // UPN primero — no depende del NetBIOS name, funciona siempre con AD moderno
        var bindCandidates = new List<string>
        {
            $"{username}@{_options.DomainFqdn}",      // mflores@stclair.stclairtech.com
            $"{_options.Domain}\\{username}",          // STCLAIR\mflores
            username                                   // bare fallback
        };

        foreach (var bindDn in bindCandidates
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var (ok, errMsg) = await TryBindAsync(bindDn, password, ct);

            if (ok)
            {
                _logger.LogInformation(
                    "LDAP auth exitoso | user={U} bind={B} host={H}:{P}",
                    username, bindDn, _options.Host, _options.Port);
                return (true, "OK");
            }

            _logger.LogDebug(
                "LDAP bind falló | bind={B} error={E}", bindDn, errMsg);
        }

        _logger.LogWarning(
            "LDAP auth fallido para {User} — todos los intentos de bind fallaron", username);
        return (false, "Credenciales inválidas o no autorizado.");
    }

    private async Task<(bool Ok, string Error)> TryBindAsync(
        string bindDn, string password, CancellationToken ct)
    {
        try
        {
            using var conn = new LdapConnection { SecureSocketLayer = _options.UseSsl };

            // Novell es síncrono — correr en thread pool para no bloquear ASP.NET
            await Task.Run(() =>
            {
                conn.Connect(_options.Host, _options.Port);
                conn.Bind(bindDn, password);   // lanza LdapException si falla
            }, ct);

            return (true, string.Empty);
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            return (false, "InvalidCredentials");
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.NoSuchObject)
        {
            return (false, "UserNotFound");
        }
        catch (LdapException ex)
        {
            _logger.LogWarning(
                "LdapException | code={Code} msg={Msg} bind={Dn}",
                ex.ResultCode, ex.Message, bindDn);
            return (false, $"LDAP {ex.ResultCode}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en bind para {Dn}", bindDn);
            return (false, ex.Message);
        }
    }
}