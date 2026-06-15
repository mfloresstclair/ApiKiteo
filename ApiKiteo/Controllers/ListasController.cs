using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Lista Viva — lista de trabajo con notas de área y tracking automático.
/// Pase de turno implícito vía Operador IS NULL en VinBusiness_DB_macro.
/// </summary>
[Route("listas")]
[Produces("application/json")]
public sealed class ListasController : KiteoBaseController
{
    private readonly IListasService _service;

    public ListasController(IListasService service) => _service = service;

    // ── GET /listas ───────────────────────────────────────────────────────────

    /// <summary>
    /// Listas vigentes con conteo live de pendientes y total de items.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ListasActivasResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivas(
        [FromQuery] string? wkname,
        [FromQuery] string? cliente,
        [FromQuery] string? tipo,
        CancellationToken   ct = default)
    {
        return FromResult(await _service.GetActivasAsync(wkname, cliente, tipo, ct));
    }

    // ── POST /listas ──────────────────────────────────────────────────────────

    /// <summary>
    /// Crea una lista nueva con sus items.
    /// Invalida automáticamente la lista anterior del mismo wkname+cliente+tipo.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ListaGuardarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Guardar(
        [FromBody] ListaGuardarRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname)
            || string.IsNullOrWhiteSpace(request.CreadoPor))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'wkname' y 'creadoPor' son requeridos.",
                ErrorCodes.Kiteo400));

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "La lista debe tener al menos 1 item.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GuardarAsync(request, ct));
    }

    // ── GET /listas/:id ───────────────────────────────────────────────────────

    /// <summary>
    /// Header de la lista + items con conteos live.
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
    /// Agrega items a una lista existente (dedup interno en el SP).
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

    // ── PATCH /listas/:id/items/:item_id/nota ─────────────────────────────────

    /// <summary>
    /// Actualiza la nota_area de un item de la lista.
    /// </summary>
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

        return FromResult(await _service.ActualizarNotaAsync(id, itemId, request.NotaArea, ct));
    }

    // ── DELETE /listas/:id/items/:item_id ─────────────────────────────────────

    /// <summary>
    /// Soft-delete de un item de la lista.
    /// </summary>
    [HttpDelete("{id:int}/items/{itemId:int}")]
    [ProducesResponseType(typeof(ListaOkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QuitarItem(
        [FromRoute] int id,
        [FromRoute] int itemId,
        CancellationToken ct)
    {
        return FromResult(await _service.QuitarItemAsync(id, itemId, ct));
    }

    // ── DELETE /listas/:id ────────────────────────────────────────────────────

    /// <summary>
    /// Soft-delete de una lista completa.
    /// Requiere LPaccess — se valida contra Central_Access.
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
}
