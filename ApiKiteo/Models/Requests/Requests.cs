using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiKiteo.API.Models.Requests;

// ─── Auth ────────────────────────────────────────────────────────────────────

/// <summary>POST /auth/login</summary>
public sealed record AuthLoginRequest(
    [Required] string Username,
    [Required] string Password
);

// ─── Semanas ──────────────────────────────────────────────────────────────────

// GET /semanas?cliente=TBB&tipo=CEA   (query params, no body)
// GET /semanas_pendientes              (sin parámetros)

/// <summary>POST /semana_grp_faltantes</summary>
public sealed record SemanaGrpFaltantesRequest(
    [Required] string Wkname,
    string? Det,
    [Required] List<string> Grupos
);

// ─── Escaneo ──────────────────────────────────────────────────────────────────

/// <summary>POST /vin_to_adjust</summary>
public sealed record VinToAdjustRequest(
    [Required] string Wkname,
    [Required] string Item,
    [Required] string Empleado
);

/// <summary>POST /escanear_ajuste</summary>
public sealed record EscanearAjusteRequest(
    [Required] string Wkname,
    [Required] string Item,
    [Required] string Empleado,
    [Required] List<string> Vines
);

/// <summary>POST /escanear</summary>
public sealed record EscanearRequest(
    [Required] string Wkname,
    [Required] string Item,
    [Required] int Cantidad,
    [Required] string Empleado
);

/// <summary>POST /semana_vines_entrega</summary>
public sealed record SemanaVinesEntregaRequest(
    [Required] string Wkname,
    [Required] string Empleado,
    [Required] List<string> Vines,
    string? Comentario,
    string? Supervisor
);

// ─── Admin ────────────────────────────────────────────────────────────────────

/// <summary>POST /api/semanas/aprobar</summary>
public sealed record AprobarSemanaRequest(
    [Required] string Wkname,
    [Required] string AprobadoPor
);

/// <summary>
/// POST /api/semanas/crear
/// wknamerename es opcional — si viene, el SP renombra el wkname después de insertar.
/// usuario registra quién ejecutó kit_vin_crea_db.
/// </summary>
public sealed record CrearDbRequest(
    [Required] string Wkname,
    string? Wknamerename,
    string? Usuario
);

/// <summary>
/// GET /api/macro/export
/// Todos los filtros son opcionales.
/// Sin filtros → últimas 4 semanas por recorddate.
/// wknames: lista separada por comas — ej: wk22_196_CEA,wk21_142_CEA
/// </summary>
public sealed record MacroExportRequest(
    string? Wknames,    // CSV string → se parsea en el controller
    string? Tipo,
    string? Cliente,
    DateOnly? Desde,
    DateOnly? Hasta
);

/// <summary>POST /escanear_bulk — uso temporal para carga masiva</summary>
public sealed record EscanearBulkRequest(
    [Required] string Wkname,
    [Required] string Empleado,
    [Required] List<EscanearBulkItem> Items
);

public sealed record EscanearBulkItem(
    [Required] string Item,
    [Required] int Cantidad
);

// ─── Admin — Roles ────────────────────────────────────────────────────────────

/// <summary>POST /api/roles</summary>
public sealed record RoleAddRequest(
    [Required] string Username,
    [Required] string FullName,
    [Required] string Access,   // LPaccess | FAaccess | IPaccess | SCHaccess
    [Required] string Site,
    [Required] string CreatedBy
);

/// <summary>DELETE /api/roles/{id}</summary>
public sealed record RoleRemoveRequest(
    [Required] string RemovedBy
);

/// <summary>PUT /api/roles/{id}</summary>
public sealed record RoleUpdateRequest(
    [Required] string Access,   // LPaccess | FAaccess | IPaccess | SCHaccess
    [Required] string UpdatedBy
);


// ─── MandarFinal ──────────────────────────────────────────────────────────────


/// POST /mandar_final/add
/// sitio opcional — si viene, el SP valida items contra CNDetalle
/// usando el lunes de producción calculado internamente.
public sealed record MandarFinalAddRequest(
    [Required] List<string> Items,
    [Required] string Usuario,
    string? Sitio
);

/// POST /mandar_final/remove
public sealed record MandarFinalRemoveRequest(
    [Required] List<string> Items,
    [Required] string Usuario
);

// ─── Wks ──────────────────────────────────────────────────────────────────────

/// <summary>
/// POST /wks/status_board
/// El SP espera {"wkname": ["wk20_108_CEA", "wk20_111_ZC/ZD", ...]}.
/// Cada wkname tiene formato: {semana}_{vinCant}_{tipo}.
/// Un wkname con tipo compuesto (ZC/ZD) se expande internamente en 2 filas por el SP.
/// </summary>
public sealed record WksStatusBoardRequest(
    [Required][MinLength(1)] List<string> Wknames
);


// ─── Wks — cache cleanup ──────────────────────────────────────────────────────

/// <summary>POST /wks/cache/cleanup</summary>
public sealed record WksCacheCleanupRequest(
    int SemanasRetener = 8,   // cuántas semanas retener en cache
    int HorasCompletadas = 48   // horas antes de borrar semanas al 100%
);
/// <summary>GET /api/liberacion/semanas y POST /api/liberacion/resumen y /detalle</summary>
public sealed record LiberacionRequest(
    [Required][MinLength(1)] List<string> Wknames,
    [Required] string Username,
    string Cliente = "TODOS"   // TODOS | TBB | BB
);
/// <summary>POST /api/liberacion/corte/ingresar</summary>
public sealed record CorteIngresarRequest(
    [Required] int LoteId,
    [Required] string Wkname,
    [Required] string Fechacorte,   // ej: "262026"
    [Required] string Username
);
/// <summary>
/// POST /api/liberacion/crear
/// Si sobreescribir=false y hay lote activo → SP devuelve 400 DUPLICADA.
/// El WinForm pregunta al usuario y re-llama con sobreescribir=true.
/// </summary>
public sealed record LiberacionCrearRequest(
    [Required] string Username,
    [Required] List<string> Wknames,
    bool Sobreescribir = false
);