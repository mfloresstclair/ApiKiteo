using Microsoft.AspNetCore.Mvc;
using KiteoAdmin.API.Models.Requests;
using KiteoAdmin.API.Services.Interfaces;

namespace KiteoAdmin.API.Controllers;

/// <summary>
/// Autenticación — replica el endpoint Python /auth/login.
/// </summary>
[Route("auth")]
[Produces("application/json")]
public sealed class AuthController : KiteoBaseController
{
    private readonly IAuthService _service;

    public AuthController(IAuthService service) => _service = service;

    /// <summary>
    /// Autentica un usuario via Active Directory y valida su acceso en SQL Server.
    /// </summary>
    /// <remarks>
    /// Devuelve access = "LPaccess" | "FAaccess" según los permisos en Kit_vin_User_Access.
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login(
        [FromBody] AuthLoginRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _service.LoginAsync(request, ct);
        return FromResult(result);
    }
}
