using Microsoft.AspNetCore.Mvc;
using KiteoAdmin.API.Common;
using KiteoAdmin.API.Models.Requests;
using KiteoAdmin.API.Services.Interfaces;

namespace KiteoAdmin.API.Controllers.Admin;

/// <summary>
/// Admin — Semanas. Grupo "Admin — Semanas" del Swagger v3.0.
/// </summary>
[Route("api/semanas")]
[Produces("application/json")]
public sealed class AdminSemanasController : KiteoBaseController
{
    private readonly IAdminService _service;

    public AdminSemanasController(IAdminService service) => _service = service;

    /// <summary>
    /// Aprueba una semana y registra quién la aprobó.
    /// </summary>
    [HttpPost("aprobar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AprobarSemana(
        [FromBody] AprobarSemanaRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname)
            || string.IsNullOrWhiteSpace(request.AprobadoPor))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'wkname' y 'aprobadoPor' son requeridos.",
                ErrorCodes.Admin400));

        return FromResult(await _service.AprobarSemanaAsync(request, ct));
    }
}
