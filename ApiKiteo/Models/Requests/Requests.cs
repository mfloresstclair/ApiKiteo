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
    string Wkname,
    IReadOnlyList<string> Grupos,
    string? Det,
    string? Descripcion   // NUEVO: "BodyCVZC" | "BodyCVZD" | null
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
    [Required] int Semana,    // 1-53 — el WinForms lo pre-llena del wkname
    [Required] int Anio,      // ej. 2026
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
public sealed record WksCacheRefreshRequest(
    [Required] string Wkname
);

public sealed record DescanBuscarRequest(
    string? Wkname,
    string? Vin,
    string? Item,
    string? Operador,
    string? Cliente,
    DateOnly? FechaDesde,
    DateOnly? FechaHasta,
    byte Modo = 1
);

public sealed record DescaneoAplicarRequest(
    [Required] int Id,
    [Required] string Username,
    [Required] string Motivo
);

// ─── Listas de prioridad ──────────────────────────────────────────────────
// Nivel 1: kit_lista_prioridad · Nivel 2: kit_lista · Nivel 3: kit_lista_item

// Los [StringLength] no son decorado: SQL Server TRUNCA un parametro
// demasiado largo SIN error. Una nota de 700 caracteres se guardaba a 500
// y la API devolvia 200. Peor con VARCHAR(20): un usuario de dominio largo
// quedaba truncado en asignado_a y ya no cruzaba contra Central_Access.

/// <summary>Un circuito. `Grupo` es lo que permite pintar la tarjeta del panel F6.</summary>
public sealed record ListaItemInput(
    [Required][StringLength(100)] string Item,
    [StringLength(50)]  string? Locacion,
    [StringLength(50)]  string? Grupo = null,
    [StringLength(100)] string? Etiqueta = null
);

/// <summary>POST /listas/prioridades — crea el contenedor de la semana.</summary>
public sealed record ListaPrioridadCrearRequest(
    [Required][StringLength(50)]  string Wkname,
    [Required][StringLength(10)]  string Cliente,
    [Required][StringLength(50)]  string Tipo,
    [Required][StringLength(100)] string Nombre,
    [Required][StringLength(20)]  string CreadoPor
);

/// <summary>POST /listas — crea una lista de prioridad dentro del contenedor.</summary>
public sealed record ListaCrearRequest(
    [Required] int PrioridadId,
    [Required][StringLength(100)] string Nombre,
    // '#RRGGBB'. El SP lo valida con un CHECK; aqui se valida antes de viajar.
    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "El color debe ser #RRGGBB.")]
    string ColorHex,
    [Required][StringLength(20)] string CreadoPor,
    // Como se armo: grupos, det, filtroLoc, texto, chips. Se guarda tal cual.
    string? FiltrosJson = null,
    [StringLength(20)] string? AsignadoA = null,
    // 1 = mas prioridad. null = al final. 0 empujaba TODAS las listas y
    // dejaba una "Prioridad 0" con un hueco que ya no se podia cerrar.
    [Range(1, int.MaxValue, ErrorMessage = "El orden empieza en 1. Omítelo para ir al final.")]
    int? Orden = null,
    List<ListaItemInput>? Items = null
);

/// <summary>PATCH /listas/{id} — cualquier campo en null se deja como está.</summary>
public sealed record ListaActualizarRequest(
    [Required][StringLength(20)] string Username,
    [StringLength(100)] string? Nombre = null,
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "El color debe ser #RRGGBB.")]
    string? ColorHex = null,
    // Cadena vacia borra el asignado; null lo deja igual.
    [StringLength(20)] string? AsignadoA = null
);

/// <summary>POST /listas/{id}/reordenar — -1 sube (más prioridad), 1 baja.</summary>
public sealed record ListaReordenarRequest(
    [Required] short Direccion,
    [Required][StringLength(20)] string Username
);

/// <summary>
/// POST /listas/{id}/items. `Etiqueta` es solo el DEFAULT para los items que
/// no traen la suya — cada ListaItemInput puede llevar la propia.
/// </summary>
public sealed record ListaAgregarRequest(
    [Required] List<ListaItemInput> Items,
    [StringLength(100)] string? Etiqueta = null,
    [StringLength(20)]  string? CreadoPor = null
);

public sealed record ListaNotaRequest(
    [StringLength(500)] string? NotaArea,
    // Quien la escribio. Va a Boss_transactions.
    [StringLength(20)] string? Username = null
);

public sealed record ListaEliminarRequest(
    [Required] string Username
);

/// <summary>POST /api/expeditados/mover — crea semana EXP con los VINs seleccionados.</summary>
public sealed record ExpeditadosMoverRequest(
    [Required] IReadOnlyList<int> Ids,      // ids de Kit_vin_expeditados
    [Required] string Username
);

/// <summary>POST /api/expeditados/ignorar — descarta sin mover.</summary>
public sealed record ExpeditadosIgnorarRequest(
    [Required] IReadOnlyList<int> Ids,
    [Required] string Username,
    string? Motivo = null
);
