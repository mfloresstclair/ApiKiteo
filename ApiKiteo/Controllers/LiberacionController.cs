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
        [FromQuery] bool incluirSinCargaActual = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(estatus))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'estatus' es requerido.", ErrorCodes.Kiteo400));

        if (cliente is not ("TODOS" or "TBB" or "BB"))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'cliente' debe ser TODOS, TBB o BB.", ErrorCodes.Kiteo400));

        // MF 4/11/2026 — incluirSinCargaActual: en false (el default) el
        // resultado es el de siempre. En true cada semana se mide contra su
        // ULTIMA carga y las que no tienen ninguna aparecen igual. Hace falta
        // para ver la papelera: una semana IGNORADA ya no se carga, y sin esto
        // el filtro de carga de referencia la deja fuera del result set.
        return FromResult(await _service.GetSemanasAsync(
            estatus.Trim(), cliente, incluirSinCargaActual, ct));
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
    /// Renombra una semana al nombre que le corresponde por numero de camiones.
    ///
    /// El nombre nuevo NO se recibe: lo calcula el SP con la formula del loader.
    /// confirmar=false devuelve code=SIMULACION con nombre_nuevo y no escribe.
    ///
    /// Codigos que el front debe distinguir: SIMULACION, OK, SIN_CAMBIO (200);
    /// PARAM_INVALIDO (400); NO_EXISTE (404); FUERA_DE_VENTANA, MACRO_ACTIVA,
    /// SIN_CARGA, NOMBRE_RARO, DESTINO_OCUPADO (409).
    /// </summary>
    [HttpPost("wk/renombrar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WkRenombrar(
        [FromBody] WkRenombrarRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname))
            return BadRequest(ErrorResponse.Create(
                "El campo 'wkname' es requerido.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.WkRenombrarAsync(request, ct));
    }

    /// <summary>
    /// Manda una semana a la papelera (estatus IGNORADA) o la restaura.
    ///
    /// Nunca borra y nunca cambia el wkname — por eso no deja Vines huerfanas.
    /// confirmar=false simula. Si la semana tiene macro generada, la simulacion
    /// regresa code=TIENE_MACRO con requiere_confirmacion_fuerte=true y http 200:
    /// es un alto, no un error, y el front tiene que preguntar otra vez.
    /// </summary>
    [HttpPost("wk/papelera")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WkPapelera(
        [FromBody] WkPapeleraRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname))
            return BadRequest(ErrorResponse.Create(
                "El campo 'wkname' es requerido.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        // Se valida aqui ademas del SP: un typo en accion no merece un viaje
        // a la base, y el mensaje de 400 dice las dos opciones.
        if (request.Accion is not ("MOVER" or "RESTAURAR"))
            return BadRequest(ErrorResponse.Create(
                "El campo 'accion' debe ser MOVER o RESTAURAR.", ErrorCodes.Kiteo400));

        return FromResult(await _service.WkPapeleraAsync(request, ct));
    }

    /// <summary>
    /// Junta dos semanas de Pre-Corte antes de liberarlas (6/11/2026 MF).
    ///
    /// Repunta los Vines de las dos —de todas sus cargas— a un solo nombre y
    /// deja un header; el otro queda IGNORADA, sin borrarse. No toca macro:
    /// si alguna ya esta generada contesta YA_GENERADA. confirmar=false simula
    /// y devuelve wknameFinal.
    ///
    /// Codigos: SIMULACION, OK (200); PARAM_INVALIDO (400); NO_EXISTE (404);
    /// FUERA_DE_VENTANA, YA_GENERADA, YA_CORTADOS, TIPO_DISTINTO, SIN_CARGA,
    /// CARGAS_DISTINTAS, TRASLAPE, NOMBRE_RARO, NOMBRE_OCUPADO (409).
    /// </summary>
    [HttpPost("wk/premerge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WkPremerge(
        [FromBody] WkPremergeRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.WkBase) || string.IsNullOrWhiteSpace(request.WkMerge))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'wkBase' y 'wkMerge' son requeridos.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.WkPremergeAsync(request, ct));
    }

    /// <summary>
    /// Saca de una semana de Pre-Corte los VINs que ya tienen macro en otra
    /// semana (6/11/2026 MF). Regresan a la semana donde se cortaron; se
    /// repuntan Vines de todas sus cargas. confirmar=false simula.
    ///
    /// Codigos: SIMULACION, OK, SIN_CAMBIO (200); PARAM_INVALIDO (400);
    /// NO_EXISTE (404); FUERA_DE_VENTANA, YA_GENERADA, SIN_CARGA, ES_FANTASMA (409).
    /// </summary>
    [HttpPost("wk/sacar-cortados")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WkSacarCortados(
        [FromBody] WkSacarCortadosRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname))
            return BadRequest(ErrorResponse.Create(
                "El campo 'wkname' es requerido.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.WkSacarCortadosAsync(request, ct));
    }

    /// <summary>
    /// Pasa camiones (o masters) de una semana de Pre-Corte a otra existente
    /// del mismo tipo, o a una nueva (merge parcial; 9/11/2026 MF). Repunta
    /// Vines de todas las cargas, recalcula los nombres y renumera GRUPO.
    /// confirmar=false simula y trae las hermanas.
    ///
    /// Codigos: SIMULACION, OK (200); PARAM_INVALIDO (400); NO_EXISTE (404);
    /// FUERA_DE_VENTANA, YA_GENERADA, SIN_CARGA, NOMBRE_RARO, TIPO_DISTINTO,
    /// CARGAS_DISTINTAS, NO_ENCONTRADO, TODA_LA_SEMANA, HERMANA_AMBIGUA,
    /// YA_CORTADOS, NOMBRE_OCUPADO (409).
    /// </summary>
    [HttpPost("wk/mover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WkMover(
        [FromBody] WkMoverRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Origen))
            return BadRequest(ErrorResponse.Create(
                "El campo 'origen' es requerido.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        if (request.Ids is null || !request.Ids.Any(s => !string.IsNullOrWhiteSpace(s)))
            return BadRequest(ErrorResponse.Create(
                "Hay que escoger al menos un camion o master ('ids').", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Destino) == string.IsNullOrWhiteSpace(request.SemanaNueva))
            return BadRequest(ErrorResponse.Create(
                "Va 'destino' (semana existente) o 'semanaNueva' (wkNN), uno de los dos.", ErrorCodes.Kiteo400));

        return FromResult(await _service.WkMoverAsync(request, ct));
    }

    /// <summary>
    /// Camiones y masters de la ultima carga de una semana: de donde escoge
    /// la pantalla de mover. Semana inexistente → lista vacia.
    /// </summary>
    [HttpGet("wk/camiones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> WkCamiones(
        [FromQuery] string wkname,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(wkname))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'wkname' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.WkCamionesAsync(wkname.Trim(), ct));
    }

    /// <summary>
    /// Quien y cuando cargo DevTest: el job semanal (Update_DevTest) y el
    /// microservicio (boton Ejecutar Carga). Para el aviso de Pre-Corte.
    /// </summary>
    [HttpGet("ultima-carga")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UltimaCarga(CancellationToken ct = default)
        => FromResult(await _service.UltimaCargaAsync(ct));

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

    // ═══════════════════════════════════════════════════════════════════════════
    // Trazabilidad — Fase 4
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Congela el resumen enviado a Corte y marca el lote como Enviado.
    /// </summary>
    /// <remarks>
    /// Lo llama el WinForm DESPUÉS de que el correo sale, con el mismo resumen
    /// que usó para armar el Excel. El servidor NO lo recalcula: lo que se
    /// congela es lo que Corte recibió, no lo que la query diría hoy.
    /// El orden de 'items' es el orden de las filas del Excel y se conserva.
    /// </remarks>
    [HttpPost("{loteId:int}/snapshot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GuardarSnapshot(
        [FromRoute] int loteId,
        [FromBody] LiberacionSnapshotRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (loteId <= 0)
            return BadRequest(ErrorResponse.Create(
                "loteId debe ser mayor a 0.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "El resumen viene vacío — no hay nada que congelar.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GuardarSnapshotAsync(loteId, request, ct));
    }

    /// <summary>
    /// Devuelve el lote tal como se envió: cabecera, resumen congelado y semanas.
    /// </summary>
    /// <remarks>
    /// No consulta Vines — un lote de hace meses se lee igual que el de hoy.
    /// </remarks>
    [HttpGet("{loteId:int}/snapshot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSnapshot(
        [FromRoute] int loteId, CancellationToken ct)
    {
        if (loteId <= 0)
            return BadRequest(ErrorResponse.Create(
                "loteId debe ser mayor a 0.", ErrorCodes.Kiteo400));

        return FromResult(await _service.GetSnapshotAsync(loteId, ct));
    }

    /// <summary>
    /// Lotes ya enviados, más recientes primero. Alimenta el selector de
    /// reimpresión del WinForm.
    /// </summary>
    [HttpGet("historial")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Historial(
        [FromQuery] string? cliente,
        [FromQuery] int top = 50,
        CancellationToken ct = default)
    {
        var filtro = string.IsNullOrWhiteSpace(cliente) ? "TODOS" : cliente.Trim();
        if (top < 1) top = 1;
        if (top > 500) top = 500;

        return FromResult(await _service.HistorialAsync(filtro, top, ct));
    }
}