using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers.Admin;

/// <summary>
/// Admin — Roles de usuario. Gestión de accesos en Central_Access para KiteoApp.
/// </summary>
[Route("api/roles")]
[Produces("application/json")]
public sealed class AdminRolesController : KiteoBaseController
{
    private readonly IAdminRolesService _service;

    public AdminRolesController(IAdminRolesService service) => _service = service;

    /// <summary>
    /// Lista los roles activos (o todos) con filtros opcionales de site y tipo de access.
    /// </summary>
    /// <param name="site">Filtro por sitio (ej: "TBB"). Vacío = todos los sitios.</param>
    /// <param name="access">Filtro por tipo de access (ej: "LPaccess"). Vacío = todos.</param>
    /// <param name="includeInactive">true para incluir roles con Estatus = 0.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(
        [FromQuery] string? site,
        [FromQuery] string? access,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        return FromResult(await _service.GetRolesAsync(
            site?.Trim() ?? string.Empty,
            access?.Trim() ?? string.Empty,
            includeInactive,
            ct));
    }

    /// <summary>
    /// Asigna un rol a un usuario en Central_Access.
    /// Valores válidos para access: LPaccess | FAaccess | IPaccess | SCHaccess.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddRole(
        [FromBody] RoleAddRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Access)
            || string.IsNullOrWhiteSpace(request.Site)
            || string.IsNullOrWhiteSpace(request.CreatedBy))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'username', 'fullName', 'access', 'site' y 'createdBy' son requeridos.",
                ErrorCodes.Admin400));

        return FromResult(await _service.AddRoleAsync(request, ct));
    }

    /// <summary>
    /// Soft-delete de un rol (Estatus 1 → 0). No borra el registro físicamente.
    /// </summary>
    /// <param name="id">id_num del registro en Central_Access.</param>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveRole(
        [FromRoute] int id,
        [FromBody] RoleRemoveRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (id <= 0)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'id' debe ser un entero positivo.",
                ErrorCodes.Admin400));

        if (string.IsNullOrWhiteSpace(request.RemovedBy))
            return BadRequest(ErrorResponse.Create(
                "El campo 'removedBy' es requerido.",
                ErrorCodes.Admin400));

        return FromResult(await _service.RemoveRoleAsync(id, request.RemovedBy.Trim(), ct));
    }

    /// <summary>
    /// Cambia el tipo de access de un registro activo en Central_Access.
    /// Valores válidos: LPaccess | FAaccess | IPaccess | SCHaccess.
    /// </summary>
    /// <param name="id">id_num del registro en Central_Access.</param>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRole(
        [FromRoute] int id,
        [FromBody] RoleUpdateRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (id <= 0)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'id' debe ser un entero positivo.",
                ErrorCodes.Admin400));

        if (string.IsNullOrWhiteSpace(request.Access)
            || string.IsNullOrWhiteSpace(request.UpdatedBy))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'access' y 'updatedBy' son requeridos.",
                ErrorCodes.Admin400));

        return FromResult(await _service.UpdateRoleAsync(id, request, ct));
    }
}