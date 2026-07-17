
using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Expeditados — VINs que llegan a DevTest DESPUÉS de que la macro de su semana
/// ya se generó. Sin esto, nadie se entera y el bus se queda sin kit.
///
/// Flujo:
///   1. El Loader corre y reporta cuántos detectó
///   2. GET /api/expeditados → Scheduling ve los PENDIENTE
///   3. Decide:
///      - POST /mover   → crea wkNN_n_tipo_EXP1, de ahí el flujo normal
///                        (lote → corte → aprobar → generar)
///      - POST /ignorar → los descarta con motivo
///
/// El sistema reporta, el humano decide. Nunca renombra solo.
/// </summary>
[Route("api/expeditados")]
[Produces("application/json")]
[Tags("Expeditados")]
public sealed class ExpeditadosController : KiteoBaseController
{
    private readonly IExpeditadosService _service;

    public ExpeditadosController(IExpeditadosService service) => _service = service;

    /// <summary>
    /// Lista los expeditados PENDIENTE. No modifica nada (solo_reportar=1).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => FromResult(await _service.DetectarAsync(soloReportar: true, ct));

    /// <summary>
    /// Corre el detector y registra los nuevos como PENDIENTE.
    /// Idempotente — el UNIQUE (vin, wkname_origen) evita duplicados.
    /// </summary>
    [HttpPost("detectar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Detectar(CancellationToken ct)
        => FromResult(await _service.DetectarAsync(soloReportar: false, ct));

    /// <summary>
    /// Mueve los VINs seleccionados a su propia semana EXP.
    /// Todos deben ser de la MISMA semana origen (mismo tipo) → si no, 400 MIXTO.
    /// Si el snapshot cambió desde la detección → 409 SNAPSHOT_CAMBIO.
    /// </summary>
    [HttpPost("mover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Mover(
        [FromBody] ExpeditadosMoverRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.Ids is null || request.Ids.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "Selecciona al menos un VIN.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.MoverAsync(request, ct));
    }

    /// <summary>
    /// Marca los expeditados como IGNORADO — no vuelven a aparecer en la lista.
    /// </summary>
    [HttpPost("ignorar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ignorar(
        [FromBody] ExpeditadosIgnorarRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.Ids is null || request.Ids.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "Selecciona al menos un VIN.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.IgnorarAsync(request, ct));
    }
}
