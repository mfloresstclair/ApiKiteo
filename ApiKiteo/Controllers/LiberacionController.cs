using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Liberación de material para Corte.
///
/// Flujo completo:
///   1. Scheduling carga wknames → estatus: PendienteCorte
///   2. POST /api/liberacion/resumen → crea lote_id → va al Excel para Corte
///   3. POST /api/liberacion/detalle → det=1 para CSV adjunto del email
///   4. Corte busca su lote: GET /api/liberacion/{loteId}
///   5. Corte ingresa fechacorte: POST /api/liberacion/corte/ingresar
///      → cuando todos ingresaron → wknames pasan a "Pendiente"
///   6. Scheduling aprueba → "APROBADA" → CrearDb
/// </summary>
[Route("api/liberacion")]
[Produces("application/json")]
public sealed class LiberacionController : KiteoBaseController
{
    private readonly ILiberacionService _service;

    public LiberacionController(ILiberacionService service) => _service = service;

    /// <summary>
    /// Semanas por estatus y cliente — para el selector del form.
    /// </summary>
    /// <param name="estatus">PendienteCorte (default) | Pendiente | APROBADA</param>
    /// <param name="cliente">TODOS (default) | TBB | BB</param>
    [HttpGet("semanas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSemanas(
        [FromQuery] string estatus = "PendienteCorte",
        [FromQuery] string cliente = "TODOS",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(estatus))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'estatus' es requerido.", ErrorCodes.Kiteo400));

        if (cliente is not ("TODOS" or "TBB" or "BB"))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'cliente' debe ser TODOS, TBB o BB.", ErrorCodes.Kiteo400));

        return FromResult(await _service.GetSemanasAsync(estatus.Trim(), cliente, ct));
    }

    /// <summary>
    /// Resumen de material a liberar (det=0).
    /// Crea un lote en Kit_vin_liberacion y asigna lote_id a las semanas.
    /// El LoteId del response es el número que va en el Excel para Corte.
    /// Devuelve 409 si alguna semana ya tiene una liberación activa.
    /// </summary>
    [HttpPost("resumen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetResumen(
        [FromBody] LiberacionRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        if (request.Cliente is not ("TODOS" or "TBB" or "BB"))
            return BadRequest(ErrorResponse.Create(
                "El campo 'cliente' debe ser TODOS, TBB o BB.", ErrorCodes.Kiteo400));

        return FromResult(await _service.GetResumenAsync(request, ct));
    }

    /// <summary>
    /// Detalle completo (det=1) — todas las filas para CSV adjunto del email.
    /// El WinForms pagina localmente con VirtualMode.
    /// No duplica la creación del lote — usar siempre DESPUÉS de /resumen.
    /// </summary>
    [HttpPost("detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDetalle(
        [FromBody] LiberacionRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        if (request.Cliente is not ("TODOS" or "TBB" or "BB"))
            return BadRequest(ErrorResponse.Create(
                "El campo 'cliente' debe ser TODOS, TBB o BB.", ErrorCodes.Kiteo400));

        return FromResult(await _service.GetDetalleAsync(request, ct));
    }

    /// <summary>
    /// Busca un lote por ID — Corte lo usa para ver sus semanas pendientes.
    /// Devuelve resumen del lote + lista de semanas con su fechacorte.
    /// </summary>
    /// <param name="loteId">ID del lote (del Excel que recibió Corte)</param>
    [HttpGet("{loteId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLote(
        [FromRoute] int loteId,
        CancellationToken ct)
    {
        if (loteId <= 0)
            return BadRequest(ErrorResponse.Create(
                "loteId debe ser mayor a 0.", ErrorCodes.Kiteo400));

        return FromResult(await _service.GetLoteAsync(loteId, ct));
    }

    /// <summary>
    /// Corte ingresa la fechacorte para una semana de su lote.
    /// Cuando TODOS los wknames del lote tienen fechacorte,
    /// el sistema los mueve automáticamente a estatus "Pendiente".
    /// </summary>
    [HttpPost("corte/ingresar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IngresarCorte(
        [FromBody] CorteIngresarRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.LoteId <= 0)
            return BadRequest(ErrorResponse.Create(
                "loteId debe ser mayor a 0.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Wkname))
            return BadRequest(ErrorResponse.Create(
                "El campo 'wkname' es requerido.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Fechacorte))
            return BadRequest(ErrorResponse.Create(
                "El campo 'fechacorte' es requerido.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.IngresarCorteAsync(request, ct));
    }
}