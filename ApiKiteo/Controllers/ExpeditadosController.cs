using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Expeditados — VINs que requieren atención antes de que el bus se quede sin kit.
///
/// El detector marca tres motivos:
///   · FUERA_MACRO  llegó después de que la macro de su semana ya se generó
///   · RE_REPEDIDO  re-pedido de una pieza perdida (motherharness %REBB)
///   · SIN_WKNAME   no recibió semana: no entra a NINGUNA macro y no aparece en
///                  ninguna pantalla. Es el más grave y no se puede mover a EXP
///                  (el destino se deriva de la semana origen) — hay que
///                  asignarle wkname primero.
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
    // OJO: esto corre el detector completo (solo_reportar=1, no escribe). Si el
    // front lo llama seguido, vale separar un endpoint que solo lea la tabla.
    public async Task<IActionResult> Listar(CancellationToken ct)
        => FromResult(await _service.DetectarAsync(soloReportar: true, ct));

    /// <summary>
    /// Corre el detector y registra los nuevos como PENDIENTE.
    /// Idempotente por VIN mientras siga PENDIENTE (índice filtrado
    /// UQ_exp_vin_pendiente). Una reincidencia posterior a MOVIDO/IGNORADO sí se
    /// inserta de nuevo: el wkname se reescribe en cada carga, así que el mismo
    /// VIN reaparece con nombre distinto y hay motherharness que van en RE15BB.
    /// </summary>
    [HttpPost("detectar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Detectar(CancellationToken ct)
        => FromResult(await _service.DetectarAsync(soloReportar: false, ct));

    /// <summary>
    /// Mueve los VINs seleccionados a su propia semana EXP.
    ///   400 MIXTO            distintas semanas origen (⇒ distinto tipo)
    ///   400 SIN_ORIGEN       algún VIN sin semana (SIN_WKNAME): no se puede derivar el tipo
    ///   400 ORIGEN_INVALIDO  el wkname origen no tiene formato wkNN_cant_tipo
    ///   409 SNAPSHOT_CAMBIO  el wkname se reescribió desde la detección
    ///   409 YA_EXISTE        el wkname destino ya existe
    ///   409 NADA_MOVIDO      el UPDATE no afectó filas (cinturón del anterior)
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