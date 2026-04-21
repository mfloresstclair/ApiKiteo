using System.DirectoryServices.AccountManagement;
using Microsoft.Extensions.Options;
using KiteoAdmin.API.Configuration;

namespace KiteoAdmin.API.Infrastructure.Ldap;

/// <summary>
/// Autenticación contra Active Directory usando PrincipalContext
/// (System.DirectoryServices.AccountManagement — API nativa de Windows).
///
/// Estrategia: dominio null → Windows auto-detecta el DC de la máquina.
/// Fallback: nombre de dominio configurado en LdapOptions.
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

    /// <inheritdoc/>
    public async Task<(bool Success, string Message)> AuthenticateAsync(
        string username,
        string password,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            // Intento 1: null → Windows auto-detecta el Domain Controller
            // Es el método más confiable en servidores unidos al dominio
            if (TryValidate(null, username, password, out var err1))
            {
                _logger.LogInformation("AD auth exitoso para {User} (auto-detect DC)", username);
                return (true, "OK");
            }

            _logger.LogDebug("Auto-detect DC falló para {User}: {Err} — intentando con dominio configurado", username, err1);

            // Intento 2: dominio explícito desde appsettings (STCLAIRTECH)
            if (!string.IsNullOrWhiteSpace(_options.Domain))
            {
                if (TryValidate(_options.Domain, username, password, out var err2))
                {
                    _logger.LogInformation("AD auth exitoso para {User} (dominio={Dom})", username, _options.Domain);
                    return (true, "OK");
                }

                _logger.LogDebug("Dominio configurado falló para {User}: {Err}", username, err2);
            }

            // Intento 3: dominio de la máquina actual (Environment.UserDomainName)
            var machineDomain = Environment.UserDomainName;
            if (!string.IsNullOrWhiteSpace(machineDomain)
                && machineDomain != _options.Domain
                && machineDomain != "WORKGROUP")
            {
                if (TryValidate(machineDomain, username, password, out var err3))
                {
                    _logger.LogInformation("AD auth exitoso para {User} (UserDomainName={Dom})", username, machineDomain);
                    return (true, "OK");
                }

                _logger.LogWarning("Todos los intentos AD fallaron para {User}. Último error: {Err}", username, err3);
            }

            return (false, "Credenciales invalidas o no autorizado.");

        }, ct);
    }

    // ── Helper: intenta ValidateCredentials y captura cualquier excepción ─────
    private bool TryValidate(string? domain, string username, string password, out string errorMsg)
    {
        try
        {
            _logger.LogDebug("PrincipalContext dominio={Dom} usuario={User}", domain ?? "(null)", username);

            using var pc = new PrincipalContext(ContextType.Domain, domain);
            var result = pc.ValidateCredentials(username, password);

            errorMsg = result ? string.Empty : "Credenciales incorrectas";
            return result;
        }
        catch (PrincipalServerDownException ex)
        {
            errorMsg = $"ServerDown: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            errorMsg = ex.Message;
            return false;
        }
    }
}