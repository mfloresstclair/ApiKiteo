using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers.Admin;

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

    /// <summary>
    /// Previsualiza el contenido de una semana antes de aprobarla.
    /// Devuelve resumen general + detalle por grupo. Solo lectura.
    /// </summary>
    /// <remarks>
    /// Ejemplo: GET /api/semanas/preview?wkname=wk22_196_CEA
    /// </remarks>
    [HttpGet("preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewSemana(
        [FromQuery] string? wkname,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wkname))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'wkname' es requerido.",
                ErrorCodes.Admin400));

        return FromResult(await _service.PreviewSemanaAsync(wkname.Trim(), ct));
    }
}
