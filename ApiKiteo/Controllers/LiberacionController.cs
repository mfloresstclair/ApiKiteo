using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Liberación de material para Corte.
///
/// Flujo completo:
///   1. Scheduling selecciona semanas en PendienteCorte
///   2. POST /api/liberacion/crear  → crea lote, devuelve lote_id (va al Excel)
///   3. POST /api/liberacion        → obtiene resumen + detalle para email/CSV
///   4. Corte recibe Excel con lote_id
///   5. GET  /api/liberacion/{loteId}           → Corte ve sus semanas
///   6. POST /api/liberacion/corte/ingresar     → Corte ingresa fechacorte por semana
///      → cuando todos ingresaron → automático a "Pendiente"
///   7. Scheduling aprueba → "APROBADA" → CrearDb
/// </summary>
[Route("api/liberacion")]
[Produces("application/json")]
public sealed class LiberacionController : KiteoBaseController
{
    private readonly ILiberacionService _service;

    public LiberacionController(ILiberacionService service) => _service = service;

    /// <summary>
    /// Semanas por estatus y cliente para el selector del form.
    /// </summary>
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
    /// Crea el lote de liberación y linkea las semanas seleccionadas.
    /// El lote_id del response es el número que va en el Excel para Corte.
    /// Si sobreescribir=false y hay lote activo → 400 code=DUPLICADA.
    /// El WinForm detecta ese code y pregunta si desea sobreescribir.
    /// </summary>
    [HttpPost("crear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearLote(
        [FromBody] LiberacionCrearRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.CrearLoteAsync(request, ct));
    }

    /// <summary>
    /// Devuelve el material a liberar — resumen (det=0) Y detalle (det=1) en una sola llamada.
    /// El WinForms usa el detalle para el CSV adjunto del email.
    /// Llamar DESPUÉS de /crear para asegurar que el lote ya existe.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMaterial(
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

        return FromResult(await _service.GetMaterialAsync(request, ct));
    }

    /// <summary>
    /// Busca un lote por ID — Corte usa el número del Excel.
    /// Devuelve resumen del lote + semanas con su fechacorte.
    /// </summary>
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
    /// Corte ingresa fechacorte para una semana de su lote.
    /// Cuando TODOS los wknames tienen fechacorte → pasan a "Pendiente" automáticamente.
    /// semanas_pendientes=0 en el response indica que el lote está completo.
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

        if (request.Semana < 1 || request.Semana > 53)
            return BadRequest(ErrorResponse.Create(
                "El campo 'semana' debe estar entre 1 y 53.", ErrorCodes.Kiteo400));

        if (request.Anio < 2020 || request.Anio > DateTime.Today.Year + 1)
            return BadRequest(ErrorResponse.Create(
                $"El campo 'anio' debe estar entre 2020 y {DateTime.Today.Year + 1}.",
                ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.IngresarCorteAsync(request, ct));
    }

    /// <summary>
    /// Lista lotes de la semana actual y la anterior.
    /// Usado por el panel izquierdo del form de Corte.
    /// </summary>
    /// <param name="cliente">TODOS (default) | TBB | BB</param>
    [HttpGet("list")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LiberacionList(
        [FromQuery] string cliente = "TODOS",
        CancellationToken ct = default)
    {
        if (cliente is not ("TODOS" or "TBB" or "BB"))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'cliente' debe ser TODOS, TBB o BB.", ErrorCodes.Kiteo400));

        return FromResult(await _service.LiberacionListAsync(cliente, ct));
    }
    /// <summary>
    /// Deriva la fechacorte consultando MAX(DateFetch) en BuildPlan.dbo.SytelineOut.
    /// El WinForms lo usa para pre-llenar y validar antes de dejar al ingeniero guardar.
    ///
    /// Blank4 formato: [semana sin cero][año] → "272026" = s27/2026 | "82026" = s8/2026
    ///
    /// Response ok=false + fechacorte=null → corte aún no ocurrió, bloquear en UI.
    /// </summary>
    [HttpGet("fechacorte")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFechaCorte(
        [FromQuery] int semana,
        [FromQuery] int anio,
        CancellationToken ct)
    {
        if (semana < 1 || semana > 53)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'semana' debe estar entre 1 y 53.",
                ErrorCodes.Kiteo400));

        if (anio < 2024 || anio > 2035)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'anio' debe estar entre 2024 y 2035.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GetFechaCorteAsync(semana, anio, ct));
    }



}