using Microsoft.AspNetCore.Mvc;
using KiteoAdmin.API.Common;
using KiteoAdmin.API.Models.Requests;
using KiteoAdmin.API.Services.Interfaces;

namespace KiteoAdmin.API.Controllers;

/// <summary>
/// VINs — replica /semana_loc, /semana_grp_status,
///         /semana_grp_faltantes y /semana_vin_status.
/// </summary>
[Produces("application/json")]
public sealed class VinsController : KiteoBaseController
{
    private readonly IVinsService _service;

    public VinsController(IVinsService service) => _service = service;

    /// <summary>
    /// Obtiene los VINs y locaciones de una semana.
    /// </summary>
    [HttpGet("semana_loc")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSemanaLoc(
        [FromQuery] string? wkname, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wkname))
            return BadRequest(ErrorResponse.Create(
                "El parametro 'wkname' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.GetSemanaLocAsync(wkname.Trim(), ct));
    }

    /// <summary>
    /// Obtiene el progreso por grupo de una semana.
    /// </summary>
    [HttpGet("semana_grp_status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSemanaGrpStatus(
        [FromQuery] string? wkname, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wkname))
            return BadRequest(ErrorResponse.Create(
                "El parametro 'wkname' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.GetSemanaGrpStatusAsync(wkname.Trim(), ct));
    }

    /// <summary>
    /// Obtiene los VINs faltantes agrupados por grupo.
    /// </summary>
    [HttpPost("semana_grp_faltantes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSemanaGrpFaltantes(
        [FromBody] SemanaGrpFaltantesRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname))
            return BadRequest(ErrorResponse.Create(
                "El campo 'wkname' es requerido.", ErrorCodes.Kiteo400));

        if (request.Grupos is null || request.Grupos.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "El campo 'grupos' debe ser una lista con al menos un elemento.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GetSemanaGrpFaltantesAsync(request, ct));
    }

    /// <summary>
    /// Obtiene el estatus de VINs por semana con porcentaje de completado.
    /// </summary>
    [HttpGet("semana_vin_status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSemanaVinStatus(
        [FromQuery] string? wkname,
        [FromQuery] string? cliente,
        [FromQuery] string? tipo,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wkname)
            || string.IsNullOrWhiteSpace(cliente)
            || string.IsNullOrWhiteSpace(tipo))
            return BadRequest(ErrorResponse.Create(
                "Faltan parametros requeridos (wkname, cliente, tipo).",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GetSemanaVinStatusAsync(
            wkname.Trim(), cliente.Trim(), tipo.Trim(), ct));
    }
}
