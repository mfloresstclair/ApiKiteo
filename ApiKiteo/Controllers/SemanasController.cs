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
    /// Obtiene la lista de semanas con estatus Pendiente.
    /// </summary>
    [HttpGet("semanas_pendientes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSemanasPendientes(CancellationToken ct)
    {
        var result = await _service.GetSemanasPendientesAsync(ct);
        return FromResult(result);
    }
}
