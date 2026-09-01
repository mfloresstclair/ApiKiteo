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
        string wkname, string cliente, string tipo,
        byte modo = 1,
        CancellationToken ct = default);

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

    /// <summary>
    /// Recalcula el cache para un wkname específico.
    /// Llamado desde POST /wks/cache/refresh para correcciones manuales post-deploy.
    /// </summary>
    Task RefreshCacheAsync(string wkname, CancellationToken ct = default);
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

    /// <summary>
    /// Crea el lote de liberación en BD y linkea las semanas.
    /// Si sobreescribir=false y hay duplicado → Fail(400, "DUPLICADA").
    /// El WinForm detecta code="DUPLICADA" y pregunta si desea sobreescribir.
    /// </summary>
    Task<ServiceResult<LiberacionCrearResponse>> CrearLoteAsync(
        LiberacionCrearRequest request, CancellationToken ct = default);

    /// <summary>
    /// Devuelve resumen Y detalle del material a liberar en una sola llamada.
    /// Usa GridReader — Kit_vin_wks_liberacion siempre devuelve 2 result sets.
    /// </summary>
    Task<ServiceResult<LiberacionMaterialResponse>> GetMaterialAsync(
        LiberacionRequest request, CancellationToken ct = default);

    Task<ServiceResult<LiberacionGetResponse>> GetLoteAsync(
        int loteId, CancellationToken ct = default);

    Task<ServiceResult<CorteIngresarResponse>> IngresarCorteAsync(
        CorteIngresarRequest request, CancellationToken ct = default);

    Task<ServiceResult<LiberacionListResponse>> LiberacionListAsync(
        string cliente, CancellationToken ct = default);
    Task<ServiceResult<FechaCorteDerivadaResponse>> GetFechaCorteAsync(
    int semana, int anio, CancellationToken ct = default);

    /// <summary>
    /// Congela el resumen enviado a Corte. Lo llama el WinForm DESPUÉS de que
    /// el correo sale, con el mismo resumen que usó para armar el Excel.
    /// </summary>
    Task<ServiceResult<LiberacionSnapshotGuardarResponse>> GuardarSnapshotAsync(
        int loteId, LiberacionSnapshotRequest request, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el lote tal como se envió: cabecera, resumen congelado y semanas.
    /// </summary>
    Task<ServiceResult<LiberacionSnapshotGetResponse>> GetSnapshotAsync(
        int loteId, CancellationToken ct = default);

    /// <summary>Lotes ya enviados, para el selector de reimpresión.</summary>
    Task<ServiceResult<LiberacionHistorialResponse>> HistorialAsync(
        string cliente, int top, CancellationToken ct = default);
}
public interface ISchedulingService
{
    /// <summary>
    /// Semanas activas + detalle opcional — pass-through de dynamic.
    /// wkname null  → solo selector (RS1)
    /// wkname valor → selector + detalle (RS1 + RS2)
    /// Los campos del response son los alias del SP directamente.
    /// </summary>
    Task<ServiceResult<object>> GetAsync(
        string? wkname, string cliente, CancellationToken ct = default);
}
public interface IDescaneoService
{
    Task<ServiceResult<DescanBuscarResponse>> BuscarAsync(
        DescanBuscarRequest request, CancellationToken ct = default);

    Task<ServiceResult<DescaneoAplicarResponse>> AplicarAsync(
        DescaneoAplicarRequest request, CancellationToken ct = default);
}
/// <summary>
/// Listas de prioridad — modelo de 3 niveles.
/// El `orden` de la lista ES la prioridad: 1 va primero.
/// </summary>
public interface IListasService
{
    // ── Nivel 1: contenedor ───────────────────────────────────────────────
    Task<ServiceResult<ListaPrioridadListResponse>> GetPrioridadesAsync(
        string wkname, string cliente, string tipo, CancellationToken ct = default);

    Task<ServiceResult<ListaPrioridadCrearResponse>> CrearPrioridadAsync(
        ListaPrioridadCrearRequest request, CancellationToken ct = default);

    // ── Nivel 2: listas ───────────────────────────────────────────────────
    Task<ServiceResult<ListasActivasResponse>> GetActivasAsync(
        int prioridadId, CancellationToken ct = default);

    Task<ServiceResult<ListaCrearResponse>> CrearAsync(
        ListaCrearRequest request, CancellationToken ct = default);

    Task<ServiceResult<ListaOkResponse>> ActualizarAsync(
        int listaId, ListaActualizarRequest request, CancellationToken ct = default);

    Task<ServiceResult<ListaReordenarResponse>> ReordenarAsync(
        int listaId, ListaReordenarRequest request, CancellationToken ct = default);

    Task<ServiceResult<ListaOkResponse>> EliminarAsync(
        int listaId, string username, CancellationToken ct = default);

    // ── Nivel 3: circuitos ────────────────────────────────────────────────
    Task<ServiceResult<ListaDetalleResponse>> GetDetalleAsync(
        int listaId, CancellationToken ct = default);

    Task<ServiceResult<ListaAgregarResponse>> AgregarItemsAsync(
        int listaId, ListaAgregarRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<ListaOkResponse>> ActualizarNotaAsync(
        int listaId, int itemId, string? notaArea, string? username,
        CancellationToken ct = default);

    Task<ServiceResult<ListaOkResponse>> QuitarItemAsync(
        int listaId, int itemId, string? username, CancellationToken ct = default);

    // ── Panel F6 ──────────────────────────────────────────────────────────
    Task<ServiceResult<GruposMarcadosResponse>> GetGruposMarcadosAsync(
        int prioridadId, CancellationToken ct = default);

    /// <summary>
    /// Las PIEZAS (VINs) que la lista todavia tiene que surtir, con los
    /// circuitos que le faltan a cada una.
    /// </summary>
    Task<ServiceResult<ListaPiezasResponse>> GetPiezasAsync(
        int listaId, CancellationToken ct = default);
}


public interface IExpeditadosService
{
    Task<ServiceResult<ExpeditadosListResponse>> DetectarAsync(
        bool soloReportar, CancellationToken ct = default);

    Task<ServiceResult<ExpeditadosMoverResponse>> MoverAsync(
        ExpeditadosMoverRequest request, CancellationToken ct = default);

    Task<ServiceResult<ExpeditadosIgnorarResponse>> IgnorarAsync(
        ExpeditadosIgnorarRequest request, CancellationToken ct = default);
}