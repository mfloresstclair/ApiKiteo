using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Listas de prioridad — modelo de 3 niveles.
///
///   NIVEL 1  /listas/prioridades      el contenedor de la semana
///   NIVEL 2  /listas                  la lista, con su color y su orden
///   NIVEL 3  /listas/{id}/items       el circuito, con su etiqueta
///
/// El `orden` de la lista ES la prioridad: 1 va primero. Un circuito que
/// aparece en dos listas le cuenta a la de mayor prioridad; en la otra sale
/// atenuado como "cedido".
///
/// El pase de turno sigue siendo implícito vía Operador IS NULL en
/// VinBusiness_DB_macro — los conteos son live, no se guardan.
/// </summary>
[Route("listas")]
[Produces("application/json")]
public sealed class ListasController : KiteoBaseController
{
    private readonly IListasService _service;

    public ListasController(IListasService service) => _service = service;

    // ══════════════════════════════════════════════════════════════════════════
    //  NIVEL 1 — contenedor
    // ══════════════════════════════════════════════════════════════════════════

    // ── GET /listas/prioridades ───────────────────────────────────────────────

    /// <summary>
    /// Contenedores vigentes de la semana, con cuántas listas e items tiene cada uno.
    /// Puede haber varios por semana: eso es justo lo que pedían.
    /// </summary>
    [HttpGet("prioridades")]
    [ProducesResponseType(typeof(ListaPrioridadListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPrioridades(
        [FromQuery] string? wkname,
        [FromQuery] string? cliente,
        [FromQuery] string? tipo,
        CancellationToken   ct = default)
    {
        if (string.IsNullOrWhiteSpace(wkname)
            || string.IsNullOrWhiteSpace(cliente)
            || string.IsNullOrWhiteSpace(tipo))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'wkname', 'cliente' y 'tipo' son requeridos.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GetPrioridadesAsync(
            wkname.Trim(), cliente.Trim(), tipo.Trim(), ct));
    }

    // ── POST /listas/prioridades ──────────────────────────────────────────────

    /// <summary>
    /// Crea un contenedor nuevo. NO invalida los anteriores — conviven.
    /// </summary>
    [HttpPost("prioridades")]
    [ProducesResponseType(typeof(ListaPrioridadCrearResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearPrioridad(
        [FromBody] ListaPrioridadCrearRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname)
            || string.IsNullOrWhiteSpace(request.Nombre)
            || string.IsNullOrWhiteSpace(request.CreadoPor))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'wkname', 'nombre' y 'creadoPor' son requeridos.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.CrearPrioridadAsync(request, ct));
    }

    // ── GET /listas/prioridades/:id/grupos ────────────────────────────────────

    /// <summary>
    /// Lo que pinta la franja de color del panel de grupos (F6):
    /// una fila por (grupo, lista), ya ordenada por prioridad.
    /// Un grupo puede salir varias veces — la app dibuja la franja segmentada.
    /// </summary>
    [HttpGet("prioridades/{prioridadId:int}/grupos")]
    [ProducesResponseType(typeof(GruposMarcadosResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGruposMarcados(
        [FromRoute] int prioridadId,
        CancellationToken ct)
    {
        return FromResult(await _service.GetGruposMarcadosAsync(prioridadId, ct));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NIVEL 2 — listas
    // ══════════════════════════════════════════════════════════════════════════

    // ── GET /listas?prioridadId= ──────────────────────────────────────────────

    /// <summary>
    /// Listas de un contenedor, ordenadas por prioridad (orden ASC).
    /// `pendienteEfectivo` descuenta los circuitos cedidos a una lista de
    /// mayor prioridad; `itemsCedidos` dice cuántos son.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ListasActivasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetActivas(
        [FromQuery] int prioridadId,
        CancellationToken ct = default)
    {
        if (prioridadId <= 0)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'prioridadId' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.GetActivasAsync(prioridadId, ct));
    }

    // ── POST /listas ──────────────────────────────────────────────────────────

    /// <summary>
    /// Crea una lista dentro de un contenedor, con su color y (opcionalmente)
    /// sus items. Sin `orden` se va al final. El color es '#RRGGBB' — es lo que
    /// marca la lista en el panel de grupos.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ListaCrearResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Crear(
        [FromBody] ListaCrearRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.PrioridadId <= 0)
            return BadRequest(ErrorResponse.Create(
                "El campo 'prioridadId' es requerido.", ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Nombre)
            || string.IsNullOrWhiteSpace(request.CreadoPor))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'nombre' y 'creadoPor' son requeridos.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.CrearAsync(request, ct));
    }

    // ── PATCH /listas/:id ─────────────────────────────────────────────────────

    /// <summary>
    /// Cambia nombre, color o asignado. Lo que venga en null se deja igual;
    /// `asignadoA: ""` borra el asignado.
    /// </summary>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(typeof(ListaOkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(
        [FromRoute] int id,
        [FromBody] ListaActualizarRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        if (request.Nombre is null && request.ColorHex is null && request.AsignadoA is null)
            return BadRequest(ErrorResponse.Create(
                "No hay nada que actualizar.", ErrorCodes.Kiteo400));

        return FromResult(await _service.ActualizarAsync(id, request, ct));
    }

    // ── POST /listas/:id/reordenar ────────────────────────────────────────────

    /// <summary>
    /// Sube (-1) o baja (1) la lista una posición, intercambiándola con su
    /// vecina. Si ya está en el extremo devuelve 200 sin cambios.
    /// </summary>
    [HttpPost("{id:int}/reordenar")]
    [ProducesResponseType(typeof(ListaReordenarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reordenar(
        [FromRoute] int id,
        [FromBody] ListaReordenarRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.Direccion != -1 && request.Direccion != 1)
            return BadRequest(ErrorResponse.Create(
                "El campo 'direccion' debe ser -1 (subir) o 1 (bajar).",
                ErrorCodes.Kiteo400));

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.ReordenarAsync(id, request, ct));
    }

    // ── DELETE /listas/:id ────────────────────────────────────────────────────

    /// <summary>
    /// Soft-delete de una lista completa. Cierra el hueco de `orden` de las que
    /// quedan. Requiere LPaccess.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ListaOkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(
        [FromRoute] int id,
        [FromBody] ListaEliminarRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.", ErrorCodes.Kiteo400));

        return FromResult(await _service.EliminarAsync(id, request.Username.Trim(), ct));
    }

    // ── GET /listas/:id/piezas ────────────────────────────────────────────────

    /// <summary>
    /// Las PIEZAS (VINs) que esta lista todavia tiene que surtir, una por fila,
    /// con los circuitos que le faltan a cada una.
    ///
    /// Es la MISMA lista que GET /listas/:id, contada en otra unidad. La
    /// pantalla de circuitos dice "53"; esta dice "45". Las dos son correctas
    /// y por eso las dos tienen que estar a la vista: mostrar una sola sin
    /// decir cual es el bug que la operadora reporto como "43 vs 53".
    /// </summary>
    [HttpGet("{id:int}/piezas")]
    [ProducesResponseType(typeof(ListaPiezasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPiezas(
        [FromRoute] int id,
        CancellationToken ct)
    {
        return FromResult(await _service.GetPiezasAsync(id, ct));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NIVEL 3 — circuitos
    // ══════════════════════════════════════════════════════════════════════════

    // ── GET /listas/:id ───────────────────────────────────────────────────────

    /// <summary>
    /// Header de la lista + items agrupados por etiqueta, con conteos live.
    /// `cedidoAOrden` no es null cuando el circuito ya vive en una lista de
    /// mayor prioridad: la UI lo pinta atenuado.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ListaDetalleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetalle(
        [FromRoute] int id,
        CancellationToken ct)
    {
        return FromResult(await _service.GetDetalleAsync(id, ct));
    }

    // ── POST /listas/:id/items ────────────────────────────────────────────────

    /// <summary>
    /// Agrega circuitos a una lista. Cada item puede traer su propia `etiqueta`;
    /// la `etiqueta` del request es solo el default para los que no la traen.
    ///
    /// Respuesta: { ok, insertados, movidos, yaEnPrioridadMayor }.
    ///   `movidos` NO mueve nada de otra lista — cuenta los circuitos que YA
    ///   estaban en ESTA lista y cambiaron de etiqueta (el nombre es histórico).
    ///   `yaEnPrioridadMayor` sí mira las otras listas: "12 de 47 ya están en
    ///   Prioridad 1".
    /// </summary>
    [HttpPost("{id:int}/items")]
    [ProducesResponseType(typeof(ListaAgregarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AgregarItems(
        [FromRoute] int id,
        [FromBody] ListaAgregarRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "Debe enviar al menos 1 item.", ErrorCodes.Kiteo400));

        return FromResult(await _service.AgregarItemsAsync(id, request, ct));
    }

    // ── PUT /listas/:id/items/:item_id/nota ───────────────────────────────────

    /// <summary>
    /// Actualiza la nota_area de un item.
    /// </summary>
    [HttpPut("{id:int}/items/{itemId:int}/nota")]
    [HttpPatch("{id:int}/items/{itemId:int}/nota")]
    [ProducesResponseType(typeof(ListaOkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActualizarNota(
        [FromRoute] int id,
        [FromRoute] int itemId,
        [FromBody] ListaNotaRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        return FromResult(await _service.ActualizarNotaAsync(
            id, itemId, request.NotaArea, request.Username, ct));
    }

    // ── DELETE /listas/:id/items/:item_id ─────────────────────────────────────

    /// <summary>
    /// Soft-delete de un item de la lista. `username` va como query string
    /// porque DELETE con body no es universal; queda en Boss_transactions.
    /// </summary>
    [HttpDelete("{id:int}/items/{itemId:int}")]
    [ProducesResponseType(typeof(ListaOkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QuitarItem(
        [FromRoute] int id,
        [FromRoute] int itemId,
        [FromQuery] string? username,
        CancellationToken ct)
    {
        return FromResult(await _service.QuitarItemAsync(id, itemId, username?.Trim(), ct));
    }
}
