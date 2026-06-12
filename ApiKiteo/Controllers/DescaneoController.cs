using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>Descaneo — Supervisores WinForms pueden desescanear circuitos.</summary>
[Route("descaneo")]
[Produces("application/json")]
public sealed class DescaneoController : KiteoBaseController
{
    private readonly IDescaneoService _service;

    public DescaneoController(IDescaneoService service) => _service = service;

    // ── GET /descaneo/buscar ──────────────────────────────────────────────────

    /// <summary>
    /// Busca items de VinBusiness_DB_macro para descaneo.
    /// modo: 1=escaneados (default) | 2=sin escanear | 3=todos.
    /// Enriquecido con modelo, co_num y secuencia de Vines.
    /// </summary>
    [HttpGet("buscar")]
    [ProducesResponseType(typeof(DescanBuscarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Buscar(
        [FromQuery] string?   wkname,
        [FromQuery] string?   vin,
        [FromQuery] string?   item,
        [FromQuery] string?   operador,
        [FromQuery] string?   cliente,
        [FromQuery] DateOnly? fechaDesde,
        [FromQuery] DateOnly? fechaHasta,
        [FromQuery] byte      modo = 1,
        CancellationToken     ct   = default)
    {
        if (modo is < 1 or > 3)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'modo' debe ser 1 (escaneados), 2 (sin escanear) o 3 (todos).",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.BuscarAsync(
            new DescanBuscarRequest(
                wkname, vin, item, operador, cliente,
                fechaDesde, fechaHasta, modo),
            ct));
    }

    // ── POST /descaneo/aplicar ────────────────────────────────────────────────

    /// <summary>
    /// Descanea un item específico por su id.
    /// El motivo es obligatorio — queda registrado en Boss_transactions.
    /// No se puede desescanear si el kit ya fue entregado.
    /// </summary>
    [HttpPost("aplicar")]
    [ProducesResponseType(typeof(DescaneoAplicarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Aplicar(
        [FromBody] DescaneoAplicarRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Motivo))
            return BadRequest(ErrorResponse.Create(
                "El motivo es obligatorio.", ErrorCodes.Kiteo400));

        return FromResult(await _service.AplicarAsync(request, ct));
    }
}
