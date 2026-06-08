using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers.Admin;

/// <summary>
/// Admin — Semanas. Grupo "Admin — Semanas" del Swagger.
/// </summary>
[Route("api/semanas")]
[Produces("application/json")]
public sealed class AdminSemanasController : KiteoBaseController
{
    private readonly IAdminService _service;
    private readonly ISchedulingService _scheduling;

    public AdminSemanasController(
        IAdminService service,
        ISchedulingService scheduling)
    {
        _service = service;
        _scheduling = scheduling;
    }

    /// <summary>
    /// Aprueba una semana y registra quién la aprobó.
    /// </summary>
    [HttpPost("aprobar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    /// </summary>
    [HttpGet("preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewSemana(
        [FromQuery] string? wkname, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wkname))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'wkname' es requerido.", ErrorCodes.Admin400));

        return FromResult(await _service.PreviewSemanaAsync(wkname.Trim(), ct));
    }

    /// <summary>
    /// Lista de VINs individuales de una semana — carga bajo demanda.
    /// </summary>
    [HttpGet("preview/vins")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewVins(
        [FromQuery] string? wkname, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wkname))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'wkname' es requerido.", ErrorCodes.Admin400));

        return FromResult(await _service.GetPreviewVinsAsync(wkname.Trim(), ct));
    }

    /// <summary>
    /// Crea los registros de VinBusiness_DB_macro para una semana.
    /// </summary>
    [HttpPost("crear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearDb(
        [FromBody] CrearDbRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname))
            return BadRequest(ErrorResponse.Create(
                "El campo 'wkname' es requerido.", ErrorCodes.Admin400));

        return FromResult(await _service.CrearDbAsync(request, ct));
    }

    /// <summary>
    /// Semanas activas para Scheduling — tiene items sin Entregado.
    /// Sin @wkname → solo selector (RS1).
    /// Con @wkname → selector + detalle de esa semana (RS1 + RS2).
    /// </summary>
    /// <param name="wkname">Opcional — detalle de la semana seleccionada.</param>
    /// <param name="cliente">TODOS (default) | TBB | BB</param>
    [HttpGet("scheduling")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Scheduling(
        [FromQuery] string? wkname = null,
        [FromQuery] string cliente = "TODOS",
        CancellationToken ct = default)
    {
        if (cliente is not ("TODOS" or "TBB" or "BB"))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'cliente' debe ser TODOS, TBB o BB.",
                ErrorCodes.Admin400));

        return FromResult(await _scheduling.GetAsync(
            string.IsNullOrWhiteSpace(wkname) ? null : wkname.Trim(),
            cliente, ct));
    }
}