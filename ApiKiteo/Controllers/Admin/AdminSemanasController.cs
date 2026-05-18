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

    /// <summary>
    /// Crea los registros de VinBusiness_DB_macro para una semana de producción.
    /// Verifica que la semana exista en Vines y que no haya sido creada ya.
    /// Si wknamerename viene informado, el wkname se renombra después de insertar.
    /// </summary>
    /// <remarks>
    /// Ejemplo mínimo:
    /// ```json
    /// { "wkname": "wk22_196_CEA" }
    /// ```
    /// Con renombre:
    /// ```json
    /// { "wkname": "wk22_196_CEA", "wknamerename": "wk22_196_CEA_v2" }
    /// ```
    /// </remarks>
    [HttpPost("crear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CrearDb(
        [FromBody] CrearDbRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname))
            return BadRequest(ErrorResponse.Create(
                "El campo 'wkname' es requerido.",
                ErrorCodes.Admin400));

        return FromResult(await _service.CrearDbAsync(request, ct));
    }
}