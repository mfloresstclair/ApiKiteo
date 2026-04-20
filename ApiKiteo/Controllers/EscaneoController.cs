using Microsoft.AspNetCore.Mvc;
using KiteoAdmin.API.Common;
using KiteoAdmin.API.Models.Requests;
using KiteoAdmin.API.Services.Interfaces;

namespace KiteoAdmin.API.Controllers;

/// <summary>
/// Escaneo — replica /vin_to_adjust, /escanear_ajuste,
///            /escanear y /semana_vines_entrega.
/// </summary>
[Produces("application/json")]
public sealed class EscaneoController : KiteoBaseController
{
    private readonly IEscaneoService _service;

    public EscaneoController(IEscaneoService service) => _service = service;

    /// <summary>
    /// Obtiene los VINs pendientes de ajuste para un ítem y empleado.
    /// </summary>
    [HttpPost("vin_to_adjust")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VinToAdjust(
        [FromBody] VinToAdjustRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname)
            || string.IsNullOrWhiteSpace(request.Item)
            || string.IsNullOrWhiteSpace(request.Empleado))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'wkname', 'item' y 'empleado' son requeridos.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GetVinToAdjustAsync(request, ct));
    }

    /// <summary>
    /// Ejecuta el ajuste sobre una lista de VINs específicos.
    /// </summary>
    [HttpPost("escanear_ajuste")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EscanearAjuste(
        [FromBody] EscanearAjusteRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname)
            || string.IsNullOrWhiteSpace(request.Item)
            || string.IsNullOrWhiteSpace(request.Empleado))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'wkname', 'item' y 'empleado' no pueden ir vacios.",
                ErrorCodes.Kiteo400));

        if (request.Vines is null || request.Vines.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "El campo 'vines' debe ser una lista con al menos 1 VIN.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.EscanearAjusteAsync(request, ct));
    }

    /// <summary>
    /// Escanea una cantidad de ítems para una semana (flujo normal, sin ajuste).
    /// </summary>
    [HttpPost("escanear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Escanear(
        [FromBody] EscanearRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname)
            || string.IsNullOrWhiteSpace(request.Item)
            || string.IsNullOrWhiteSpace(request.Empleado))
            return BadRequest(ErrorResponse.Create(
                "Faltan campos requeridos: wkname, item, empleado.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.EscanearAsync(request, ct));
    }

    /// <summary>
    /// Registra la entrega final de VINs de una semana.
    /// </summary>
    [HttpPost("semana_vines_entrega")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SemanaVinesEntrega(
        [FromBody] SemanaVinesEntregaRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname)
            || string.IsNullOrWhiteSpace(request.Empleado))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'wkname' y 'empleado' son requeridos.",
                ErrorCodes.Kiteo400));

        if (request.Vines is null || request.Vines.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "El campo 'vines' debe ser una lista.", ErrorCodes.Kiteo400));

        return FromResult(await _service.EntregarVinesAsync(request, ct));
    }
}
