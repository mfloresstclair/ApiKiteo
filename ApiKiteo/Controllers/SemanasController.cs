using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Semanas — /semanas y /semanas_pendientes.
/// </summary>
[Produces("application/json")]
public sealed class SemanasController : KiteoBaseController
{
    private readonly ISemanasService _service;

    public SemanasController(ISemanasService service) => _service = service;

    /// <summary>
    /// Obtiene las semanas disponibles para un cliente y tipo.
    /// </summary>
    [HttpGet("semanas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSemanas(
        [FromQuery] string? cliente,
        [FromQuery] string? tipo,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cliente) || string.IsNullOrWhiteSpace(tipo))
            return BadRequest(ErrorResponse.Create(
                "Los parametros 'cliente' y 'tipo' son requeridos.",
                ErrorCodes.Kiteo400));

        var result = await _service.GetSemanasAsync(cliente.Trim(), tipo.Trim(), ct);
        return FromResult(result);
    }

    /// <summary>
    /// Obtiene semanas con su estatus desde Kit_vin_wk_header.
    /// filtro:
    ///   0 = todos los estatus (últimas 2 semanas, sin creado_por)
    ///   1 = solo Pendiente (esperando aprobación de Scheduling)
    ///   2 = solo APROBADA (aprobadas, listas para CrearDb)
    ///   3 = solo creadas en DBMacro (creado_por IS NOT NULL, últimas 4 semanas)
    ///   4 = solo PendienteCorte (esperando aprobación de Corte)
    /// </summary>
    [HttpGet("semanas_pendientes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSemanasPendientes(
        [FromQuery] byte filtro = 0,
        CancellationToken ct = default)
    {
        if (filtro > 4)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'filtro' acepta: 0 (todos), 1 (Pendiente), " +
                "2 (APROBADA), 3 (creadas en DBMacro), 4 (PendienteCorte).",
                ErrorCodes.Kiteo400));

        var result = await _service.GetSemanasPendientesAsync(filtro, ct);
        return FromResult(result);
    }
}