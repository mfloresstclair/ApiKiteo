using Novell.Directory.Ldap;
using Microsoft.Extensions.Options;
using KiteoAdmin.API.Configuration;

namespace KiteoAdmin.API.Infrastructure.Ldap;

/// <summary>
/// Proveedor de autenticación LDAP contra Active Directory.
/// Equivalente a la función Python ad_authenticate().
/// 
/// Estrategia: bind directo con credenciales del usuario.
/// Si el bind tiene éxito → usuario válido en AD.
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
        _logger  = logger;
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string Message)> AuthenticateAsync(
        string username,
        string password,
        CancellationToken ct = default)
    {
        // Formato DOMAIN\user (idéntico al Python: f"{domain}\\{username}")
        var userDn = $"{_options.Domain}\\{username}";

        return await Task.Run(() =>
        {
            try
            {
                using var conn = new LdapConnection();

                if (_options.UseSsl)
                    conn.SecureSocketLayer = true;

                conn.Connect(_options.Host, _options.Port);
                conn.Bind(userDn, password);        // lanza LdapException si falla
                conn.Disconnect();

                return (true, "OK");
            }
            catch (LdapException ex)
            {
                _logger.LogWarning("LDAP bind fallido para {User}: {Msg}", username, ex.Message);
                return (false, "Credenciales invalidas o no autorizado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en LDAP para {User}", username);
                return (false, "Error de autenticacion. Contacta a soporte.");
            }
        }, ct);
    }
}
