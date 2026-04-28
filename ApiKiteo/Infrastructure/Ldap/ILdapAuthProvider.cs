namespace ApiKiteo.API.Infrastructure.Ldap;

/// <summary>
/// Contrato para autenticación contra Active Directory / LDAP.
/// </summary>
public interface ILdapAuthProvider
{
    /// <summary>
    /// Autentica un usuario contra el directorio activo.
    /// </summary>
    /// <returns>(true, "OK") si las credenciales son válidas; (false, mensaje) si no.</returns>
    Task<(bool Success, string Message)> AuthenticateAsync(
        string username,
        string password,
        CancellationToken ct = default);
}
