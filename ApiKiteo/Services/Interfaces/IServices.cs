using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;

namespace ApiKiteo.API.Services.Interfaces;

// ─── Auth ─────────────────────────────────────────────────────────────────────

public interface IAuthService
{
    Task<ServiceResult<AuthLoginResponse>> LoginAsync(
        AuthLoginRequest request, CancellationToken ct = default);
}

// ─── Semanas ──────────────────────────────────────────────────────────────────

public interface ISemanasService
{
    Task<ServiceResult<IReadOnlyList<SemanaItem>>> GetSemanasAsync(
        string cliente, string tipo, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<SemanaPendienteItem>>> GetSemanasPendientesAsync(
        CancellationToken ct = default);
}

// ─── Empleados ────────────────────────────────────────────────────────────────

public interface IEmpleadosService
{
    Task<ServiceResult<EmpleadoResponse>> GetEmpleadoAsync(
        string empleado, CancellationToken ct = default);
}

// ─── VINs ─────────────────────────────────────────────────────────────────────

public interface IVinsService
{
    Task<ServiceResult<SemanaLocResponse>> GetSemanaLocAsync(
        string wkname, CancellationToken ct = default);

    Task<ServiceResult<SemanaGrpStatusResponse>> GetSemanaGrpStatusAsync(
        string wkname, CancellationToken ct = default);

    Task<ServiceResult<SemanaGrpFaltantesResponse>> GetSemanaGrpFaltantesAsync(
        SemanaGrpFaltantesRequest request, CancellationToken ct = default);

    Task<ServiceResult<SemanaVinStatusResponse>> GetSemanaVinStatusAsync(
        string wkname, string cliente, string tipo, CancellationToken ct = default);
}

// ─── Escaneo ──────────────────────────────────────────────────────────────────

public interface IEscaneoService
{
    Task<ServiceResult<VinToAdjustResponse>> GetVinToAdjustAsync(
        VinToAdjustRequest request, CancellationToken ct = default);

    Task<ServiceResult<EscanearAjusteResponse>> EscanearAjusteAsync(
        EscanearAjusteRequest request, CancellationToken ct = default);

    Task<ServiceResult<EscanearResponse>> EscanearAsync(
        EscanearRequest request, CancellationToken ct = default);

    Task<ServiceResult<SemanaVinesEntregaResponse>> EntregarVinesAsync(
        SemanaVinesEntregaRequest request, CancellationToken ct = default);
}

// ─── Admin ────────────────────────────────────────────────────────────────────

public interface IAdminService
{
    Task<ServiceResult<AprobarSemanaResponse>> AprobarSemanaAsync(
        AprobarSemanaRequest request, CancellationToken ct = default);
}

// ─── Admin — Roles ────────────────────────────────────────────────────────────

public interface IAdminRolesService
{
    Task<ServiceResult<RolesListResponse>> GetRolesAsync(
        string site, string access, bool includeInactive,
        CancellationToken ct = default);

    Task<ServiceResult<RoleAddResponse>> AddRoleAsync(
        RoleAddRequest request, CancellationToken ct = default);

    Task<ServiceResult<RoleRemoveResponse>> RemoveRoleAsync(
        int idNum, string removedBy, CancellationToken ct = default);

    Task<ServiceResult<RoleUpdateResponse>> UpdateRoleAsync(
        int idNum, RoleUpdateRequest request, CancellationToken ct = default);
}
// ─── MandarFinal ──────────────────────────────────────────────────────────────

public interface IMandarFinalService
{


    /// TOP 20 ParentItems de CNDetalle para la semana en curso.
    /// search es opcional — filtra por coincidencia parcial en ParentItem.

    Task<ServiceResult<MandarFinalParentsResponse>> GetParentsAsync(
        string sitio, string? search, CancellationToken ct = default);


    /// Items hijo de un ParentItem para la semana en curso,
    /// con overlay y flag de presencia en la lista de mandar_a_final.

    Task<ServiceResult<MandarFinalPorParentResponse>> GetPorParentAsync(
        string sitio, string parentItem, CancellationToken ct = default);


    /// Items registrados en mandar_a_final, filtrados o no por Estatus = 1.

    Task<ServiceResult<MandarFinalListResponse>> GetListAsync(
        bool includeInactive, CancellationToken ct = default);


    /// Agrega o reactiva items en la lista de mandar_a_final.
    /// Si viene sitio, el SP valida contra CNDetalle con el lunes calculado.

    Task<ServiceResult<MandarFinalAddResponse>> AddItemsAsync(
        MandarFinalAddRequest request, CancellationToken ct = default);


    /// Soft-delete de items (Estatus = 0) en la lista de mandar_a_final.

    Task<ServiceResult<MandarFinalRemoveResponse>> RemoveItemsAsync(
        MandarFinalRemoveRequest request, CancellationToken ct = default);
}


// ─── Wks ──────────────────────────────────────────────────────────────────────

public interface IWksService
{
    /// <summary>
    /// Devuelve el estado de kits (porcentaje, completos, entregados) por semana y tipo
    /// para la lista de wknames recibida.
    /// Tipos compuestos (ZC/ZD) se expanden en filas separadas por el SP.
    /// </summary>
    Task<ServiceResult<WksStatusBoardResponse>> GetStatusBoardAsync(
        WksStatusBoardRequest request, CancellationToken ct = default);
}