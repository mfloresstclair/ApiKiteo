using KiteoAdmin.API.Common;
using KiteoAdmin.API.Infrastructure.Ldap;
using KiteoAdmin.API.Models.Requests;
using KiteoAdmin.API.Models.Responses;
using KiteoAdmin.API.Repositories.Interfaces;
using KiteoAdmin.API.Services.Interfaces;

namespace KiteoAdmin.API.Services.Implementations;

/// <summary>
/// Orquesta: LDAP auth → Kit_vin_User_Access → construir respuesta.
/// Replica exactamente el flujo Python del endpoint /auth/login.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly ILdapAuthProvider _ldap;
    private readonly IAuthRepository   _repo;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ILdapAuthProvider ldap,
        IAuthRepository repo,
        ILogger<AuthService> logger)
    {
        _ldap   = ldap;
        _repo   = repo;
        _logger = logger;
    }

    public async Task<ServiceResult<AuthLoginResponse>> LoginAsync(
        AuthLoginRequest request, CancellationToken ct = default)
    {
        // 1. Autenticar contra Active Directory
        var (adOk, adMsg) = await _ldap.AuthenticateAsync(request.Username, request.Password, ct);
        if (!adOk)
            return ServiceResult<AuthLoginResponse>.Fail(401, adMsg, ErrorCodes.Auth401);

        // 2. Consultar permisos en SQL Server
        try
        {
            var rows = await _repo.GetUserAccessAsync(request.Username, ct);

            bool hasLp = false, hasFa = false;

            foreach (var row in rows)
            {
                // El SP puede devolver la columna "access" con un string,
                // o columnas booleanas LPaccess / FAaccess — soportamos ambas.
                var accStr = row.Access?.ToString()?.Trim().ToLowerInvariant();
                if (accStr == "lpaccess") hasLp = true;
                if (accStr == "faaccess") hasFa = true;

                if (row.LPaccess == true) hasLp = true;
                if (row.FAaccess == true) hasFa = true;
            }

            if (!hasLp && !hasFa)
                return ServiceResult<AuthLoginResponse>.Fail(
                    401, "Usuario sin acceso a la aplicacion.", ErrorCodes.AuthNoAccess);

            var access = hasLp ? "LPaccess" : "FAaccess";

            return ServiceResult<AuthLoginResponse>.Ok(
                new AuthLoginResponse(true, request.Username, access));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando acceso para {User}", request.Username);
            return ServiceResult<AuthLoginResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Auth500);
        }
    }
}
