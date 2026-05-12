using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// MandarFinal — gestión de la lista de mandar_a_final.
/// Flujo típico: parents → por_parent → add/remove → candidatos / list para verificar.
/// La semana de producción (lunes) la calculan todos los SPs internamente.
/// </summary>
[Route("mandar_final")]
[Produces("application/json")]
public sealed class MandarFinalController : KiteoBaseController
{
    private readonly IMandarFinalService _service;

    public MandarFinalController(IMandarFinalService service) => _service = service;

    // ── GET /mandar_final/parents ─────────────────────────────────────────────

    /// <summary>
    /// Devuelve los ParentItems de CNDetalle para la semana en curso (TOP 20).
    /// Punto de entrada del flujo — el usuario elige un parent antes de ver sus items.
    /// </summary>
    /// <remarks>
    /// Ejemplo: GET /mandar_final/parents?sitio=TBB
    ///          GET /mandar_final/parents?sitio=TBB&amp;search=CEA
    /// </remarks>
    [HttpGet("parents")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetParents(
        [FromQuery] string? sitio,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sitio))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'sitio' es requerido.",
                ErrorCodes.Kiteo400));

        var sitioTrim = sitio.Trim();
        if (sitioTrim.Length > 3)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'sitio' debe tener máximo 3 caracteres.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GetParentsAsync(sitioTrim, search, ct));
    }

    // ── GET /mandar_final/por_parent ──────────────────────────────────────────

    /// <summary>
    /// Devuelve los items hijo de un ParentItem para la semana en curso,
    /// con overlay y flag de presencia en la lista de mandar_a_final.
    /// </summary>
    /// <remarks>
    /// Ejemplo: GET /mandar_final/por_parent?sitio=TBB&amp;parentItem=CEEA+
    /// </remarks>
    [HttpGet("por_parent")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPorParent(
        [FromQuery] string? sitio,
        [FromQuery] string? parentItem,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sitio) || string.IsNullOrWhiteSpace(parentItem))
            return BadRequest(ErrorResponse.Create(
                "Los parámetros 'sitio' y 'parentItem' son requeridos.",
                ErrorCodes.Kiteo400));

        var sitioTrim = sitio.Trim();
        if (sitioTrim.Length > 3)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'sitio' debe tener máximo 3 caracteres.",
                ErrorCodes.Kiteo400));

        return FromResult(
            await _service.GetPorParentAsync(sitioTrim, parentItem.Trim(), ct));
    }


    // ── GET /mandar_final ─────────────────────────────────────────────────────

    /// <summary>
    /// Items registrados en VinBusiness_DB_macro_Mandar_a_final.
    /// Por defecto solo devuelve los activos (Estatus = 1).
    /// </summary>
    /// <remarks>
    /// Ejemplo: GET /mandar_final
    ///          GET /mandar_final?includeInactive=true
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        return FromResult(await _service.GetListAsync(includeInactive, ct));
    }

    // ── POST /mandar_final/add ────────────────────────────────────────────────

    /// <summary>
    /// Agrega o reactiva items en la lista de mandar_a_final.
    /// Si se incluye sitio, el SP valida que los items existan en CNDetalle
    /// para el lunes de la semana de producción calculado internamente.
    /// </summary>
    [HttpPost("add")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddItems(
        [FromBody] MandarFinalAddRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Usuario))
            return BadRequest(ErrorResponse.Create(
                "El campo 'usuario' es requerido.",
                ErrorCodes.Kiteo400));

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "El campo 'items' debe ser una lista con al menos 1 elemento.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.AddItemsAsync(request, ct));
    }

    // ── POST /mandar_final/remove ─────────────────────────────────────────────

    /// <summary>
    /// Desactiva (soft-delete) items de la lista de mandar_a_final.
    /// Solo afecta items con Estatus = 1; los ya inactivos se ignoran sin error.
    /// </summary>
    [HttpPost("remove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveItems(
        [FromBody] MandarFinalRemoveRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Usuario))
            return BadRequest(ErrorResponse.Create(
                "El campo 'usuario' es requerido.",
                ErrorCodes.Kiteo400));

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "El campo 'items' debe ser una lista con al menos 1 elemento.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.RemoveItemsAsync(request, ct));
    }
}