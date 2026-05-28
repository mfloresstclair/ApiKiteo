using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
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
        byte filtro = 0, CancellationToken ct = default);
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

    Task<ServiceResult<BuscarCircuitoResponse>> BuscarCircuitoAsync(
        string wkname, string circuito, bool soloFaltantes,
        CancellationToken ct = default);
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

    Task<ServiceResult<WkPreviewResponse>> PreviewSemanaAsync(
        string wkname, CancellationToken ct = default);

    Task<ServiceResult<CrearDbResponse>> CrearDbAsync(
        CrearDbRequest request, CancellationToken ct = default);

    /// <summary>
    /// Lista de VINs individuales de una semana para el preview de admin.
    /// Lazy — se carga solo cuando el usuario solicita ver los VINs.
    /// </summary>
    Task<ServiceResult<WkPreviewVinsResponse>> GetPreviewVinsAsync(
        string wkname, CancellationToken ct = default);
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
    Task<ServiceResult<MandarFinalParentsResponse>> GetParentsAsync(
        string sitio, string? search, CancellationToken ct = default);

    Task<ServiceResult<MandarFinalPorParentResponse>> GetPorParentAsync(
        string sitio, string parentItem, CancellationToken ct = default);

    Task<ServiceResult<MandarFinalListResponse>> GetListAsync(
        bool includeInactive, CancellationToken ct = default);

    Task<ServiceResult<MandarFinalAddResponse>> AddItemsAsync(
        MandarFinalAddRequest request, CancellationToken ct = default);

    Task<ServiceResult<MandarFinalRemoveResponse>> RemoveItemsAsync(
        MandarFinalRemoveRequest request, CancellationToken ct = default);
}

// ─── Wks ──────────────────────────────────────────────────────────────────────

public interface IWksService
{
    Task<ServiceResult<WksStatusBoardResponse>> GetStatusBoardAsync(
        WksStatusBoardRequest request, CancellationToken ct = default);

    /// <summary>
    /// Limpia el cache con límites configurables.
    /// Devuelve cuántas filas fueron eliminadas.
    /// </summary>
    Task<ServiceResult<WksCacheCleanupResponse>> CacheCleanupAsync(
        int semanasRetener, int horasCompletadas, CancellationToken ct = default);
}

// ─── Macro Export ─────────────────────────────────────────────────────────────

public interface IMacroService
{
    Task StreamCsvAsync(
        IReadOnlyList<string> wknames,
        string? tipo,
        string? cliente,
        DateOnly? desde,
        DateOnly? hasta,
        Stream output,
        CancellationToken ct = default);
}
public interface ILiberacionService
{
    Task<ServiceResult<LiberacionSemanasResponse>> GetSemanasAsync(
        string estatus, string cliente, CancellationToken ct = default);

    Task<ServiceResult<LiberacionResumenResponse>> GetResumenAsync(
        LiberacionRequest request, CancellationToken ct = default);

    Task<ServiceResult<LiberacionDetalleResponse>> GetDetalleAsync(
        LiberacionRequest request, CancellationToken ct = default);
}
