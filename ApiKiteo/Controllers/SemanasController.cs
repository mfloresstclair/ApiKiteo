using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Semanas — replica /semanas y /semanas_pendientes.
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
    /// Obtiene semanas con su estatus (Pendiente / APROBADA).
    /// filtro: 0 = todos (últimas 2 semanas), 1 = solo pendientes, 2 = solo aprobadas.
    /// </summary>
    [HttpGet("semanas_pendientes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSemanasPendientes(
        [FromQuery] byte filtro = 0,
        CancellationToken ct = default)
    {
        if (filtro > 2)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'filtro' debe ser 0 (todos), 1 (pendientes) o 2 (aprobadas).",
                ErrorCodes.Kiteo400));

        var result = await _service.GetSemanasPendientesAsync(filtro, ct);
        return FromResult(result);
    }
}