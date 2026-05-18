namespace ApiKiteo.API.Models.Responses;

// ─── Auth ─────────────────────────────────────────────────────────────────────


/// Respuesta de /auth/login.
/// Contrato fijo con KiteoApp WPF — no cambiar nombres de propiedades.

public sealed record AuthLoginResponse(
    bool Ok,
    string Username,
    string Access           // "LPaccess" | "FAaccess"
);


/// Row interno devuelto por Kit_vin_User_Access.

public sealed record UserAccessRow
{
    public string? Access { get; init; }
    public bool? LPaccess { get; init; }
    public bool? FAaccess { get; init; }
}

// ─── Semanas ──────────────────────────────────────────────────────────────────

public sealed record SemanaItem
{
    public string Clave { get; init; } = string.Empty;
    public string? Estatus { get; init; }
}

public sealed record SemanaPendienteItem
{
    public string Wkname { get; init; } = string.Empty;
    public string? Estatus { get; init; }   // "Pendiente" | "APROBADA"
    public string? AprobadoPor { get; init; }   // null si está pendiente
}

// ─── Empleados ────────────────────────────────────────────────────────────────

public sealed record EmpleadoResponse(string Nombre);

// ─── VINs — semana_loc ────────────────────────────────────────────────────────

public sealed record SemanaLocItem
{
    public string? Vin { get; init; }
    public int? Locacion { get; init; }
    public string? Grupo { get; init; }
    public string? Item { get; init; }
    public string? Descripcion { get; init; }
}

public sealed record SemanaLocResponse(
    bool Ok,
    string Wkname,
    int Total,
    IReadOnlyList<SemanaLocItem> Resultados
);

// ─── VINs — semana_grp_status ─────────────────────────────────────────────────

public sealed record SemanaGrpStatusItem
{
    public string Grupo { get; init; } = string.Empty;
    public string? Vindesc { get; init; }   // ventana/window normalizada — ej: "10WDO", "BodyCVZC"
    public int Vines { get; init; }
    public decimal Porcentaje { get; init; }
}

public sealed record SemanaGrpStatusResponse(
    bool Ok,
    string Wkname,
    int Total,
    IReadOnlyList<SemanaGrpStatusItem> Resultados
);

// ─── VINs — semana_grp_faltantes ─────────────────────────────────────────────

// El SP devuelve columnas dinámicas (det="1" = resumen, det="CEA" = detalle),
// se usan Dictionary<string,object?> para no romper ante cambios de schema.
public sealed record SemanaGrpFaltantesResponse(
    bool Ok,
    string Wkname,
    string Det,
    int Total,
    IReadOnlyList<Dictionary<string, object?>> Resultados
);

// ─── VINs — semana_vin_status ─────────────────────────────────────────────────

public sealed record SemanaVinStatusItem
{
    public int? Locacion { get; init; }
    public string? Vin { get; init; }
    public string? Vindesc { get; init; }   // ventana/window normalizada — ej: "10WDO", "BodyCVZC"
    public decimal Porcentaje { get; init; }
}

public sealed record SemanaVinStatusResponse(
    bool Ok,
    string Wkname,
    int Total,
    IReadOnlyList<SemanaVinStatusItem> Resultados
);

// ─── Escaneo — vin_to_adjust ──────────────────────────────────────────────────

public sealed record VinItem
{
    public string? Vin { get; init; }
    public object? Loc { get; init; }   // puede ser int o string según SP
    public string? Grupo { get; init; }
    public string? Item { get; init; }
}

public sealed record VinToAdjustResponse(
    bool Ok,
    string Wkname,
    string Item,
    int Total,
    IReadOnlyList<VinItem> Vines
);

// ─── Escaneo — escanear / escanear_ajuste ─────────────────────────────────────


/// Fila de evento devuelta por el SP con Tipo = "EvtData".

public sealed record EscaneoEvento
{
    public string? Mensaje { get; init; }
    public int? Actualizados { get; init; }
    public int? Pendientes { get; init; }
    public int? Requested { get; init; }
    public int? TotalItem { get; init; }
    public int? Excedente { get; init; }
    public int? Faltante { get; init; }
    public string? LocacionesAjustadas { get; init; }
}

public sealed record EscanearAjusteResponse(
    bool Ok,
    string Wkname,
    string Item,
    int Total,
    EscaneoEvento? Evento,
    IReadOnlyList<VinItem> Vines
);

public sealed record EscanearResponse(
    bool Ok,
    string Wkname,
    string Item,
    int Total,
    EscaneoEvento? Evento,
    IReadOnlyList<VinItem> Vines,
    IReadOnlyList<Dictionary<string, object?>> GruposProgreso,
    decimal? WeekPerc       // % total de la semana para este item
);

// ─── Escaneo — semana_vines_entrega ──────────────────────────────────────────

public sealed record SemanaVinesEntregaResponse(
    bool Ok,
    string Wkname,
    string Empleado,
    int TotalActualizados,
    IReadOnlyList<Dictionary<string, object?>> VinesActualizados
);

// ─── Admin — semanas ──────────────────────────────────────────────────────────

public sealed record AprobarSemanaResponse(
    bool Ok,
    string Mensaje
);

/// <summary>
/// Respuesta de POST /api/semanas/crear.
/// Solo devuelve conteos — no los datos completos para evitar timeout.
/// WknameEfectivo = wknamerename si hubo renombre, si no = wkname original.
/// </summary>
public sealed record CrearDbResponse(
    bool Ok,
    string Wkname,
    string WknameEfectivo,   // el wkname final después del posible renombre
    string? Wknamedata,       // sufijo del wkname: CEA, ZC/ZD, C2, etc.
    string? Descripcion,      // p.ej "BodyCVZC ,BodyCVZD" para ZC/ZD, null para CEA/C2
    string? Cliente,          // "TBB" | "BB"
    string? Tipo,             // "CEA" | "ZC/ZD" | "ZACV" | "T3" | "C2" | "BODYHDX"
    int TotalVins,        // COUNT(DISTINCT VIN) de las filas insertadas
    int TotalLineas       // COUNT(*) total de filas insertadas en VinBusiness_DB_macro
);

public sealed record EscanearBulkResponse(
    bool Ok,
    string Wkname,
    int Total,
    int Exitosos,
    int Fallidos,
    IReadOnlyList<EscanearBulkItemResult> Resultados
);

public sealed record EscanearBulkItemResult(
    string Item,
    int Cantidad,
    bool Ok,
    string? Mensaje,
    int? Actualizados,
    int? Pendientes
);
// ─── Admin — Roles ────────────────────────────────────────────────────────────


/// Fila devuelta por Kit_vin_admin_roles_list.
/// Mapea directamente desde Central_Access.

public sealed record RoleItem
{
    public int IdNum { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Access { get; init; } = string.Empty;
    public string Site { get; init; } = string.Empty;
    public int Estatus { get; init; }
    public string? CreatedAt { get; init; }
    public string? LastUpdated { get; init; }
}

public sealed record RolesListResponse(
    bool Ok,
    int Total,
    IReadOnlyList<RoleItem> Resultados
);


/// Respuesta de Kit_vin_admin_role_add cuando http_status = 200.

public sealed record RoleAddResponse(
    bool Ok,
    string Mensaje,
    int IdNum,
    string Username,
    string FullName,
    string Access,
    string Site
);


/// Respuesta de Kit_vin_admin_role_remove cuando http_status = 200.

public sealed record RoleRemoveResponse(
    bool Ok,
    string Mensaje,
    int IdNum,
    string Username,
    string Access
);


/// Respuesta de Kit_vin_admin_role_update cuando http_status = 200.

public sealed record RoleUpdateResponse(
    bool Ok,
    string Mensaje,
    int IdNum,
    string Username,
    string AccessAnterior,
    string AccessNuevo
);

// ─── MandarFinal — candidatos ─────────────────────────────────────────────────



/// Fila devuelta por Kit_vin_mandar_final_candidatos.
/// El SP hace UNION ALL de dos fuentes:
///   - Fuente "CNDetalle"   : items de la semana actual para el sitio dado.
///   - Fuente "MandarFinal" : items activos en la lista que ya no están en CNDetalle esta semana.

public sealed record MandarFinalCandidatoItem
{
    public string ParentItem { get; init; } = string.Empty;
    public string Item { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Circuits { get; init; }
    public string? Splices { get; init; }
    public string? Twists { get; init; }
    public string? FechaSemana { get; init; }   // lunes calculado por el SP (yyyy-MM-dd)
    public bool YaEnLista { get; init; }
    public string? AgregadoPor { get; init; }
    public string? FechaAgregado { get; init; }   // DateTime → string ISO
    public string? Origen { get; init; }   // "CNDetalle" | "MandarFinal"
}


// ─── MandarFinal — parents ────────────────────────────────────────────────────


/// Fila devuelta por Kit_vin_mandar_final_parents.
/// TOP 20 ParentItems de CNDetalle para la semana en curso.

public sealed record MandarFinalParentItem
{
    public string ParentItem { get; init; } = string.Empty;
    public int TotalCircuitos { get; init; }
    public bool TieneActivosEnLista { get; init; }
    public string? FechaSemana { get; init; }   // lunes calculado (yyyy-MM-dd)
}

public sealed record MandarFinalParentsResponse(
    bool Ok,
    string Sitio,
    string? Search,
    int Total,
    IReadOnlyList<MandarFinalParentItem> Resultados
);

// ─── MandarFinal — por_parent ─────────────────────────────────────────────────


/// Fila devuelta por Kit_vin_mandar_final_por_parent.
/// Items hijo de un ParentItem con datos de circuito y flag de lista.

public sealed record MandarFinalPorParentItem
{
    public string ParentItem { get; init; } = string.Empty;
    public string Item { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Circuits { get; init; }
    public string? Splices { get; init; }
    public string? Twists { get; init; }
    public string? Overlay { get; init; }   // overlay real desde VinBusiness_DB_macro
    public string? FechaSemana { get; init; }   // lunes calculado (yyyy-MM-dd)
    public bool YaEnLista { get; init; }
    public string? AgregadoPor { get; init; }
    public string? FechaAgregado { get; init; }   // DateTime → string ISO
}

public sealed record MandarFinalPorParentResponse(
    bool Ok,
    string Sitio,
    string ParentItem,
    int Total,
    IReadOnlyList<MandarFinalPorParentItem> Resultados
);

// ─── MandarFinal — list ───────────────────────────────────────────────────────


/// Fila devuelta por Kit_vin_mandar_final_list.

public sealed record MandarFinalListItem
{
    public int Id { get; init; }
    public string Item { get; init; } = string.Empty;
    public string? Usuario { get; init; }
    public string? Recorddate { get; init; }   // DateTime → string ISO
    public int Estatus { get; init; }
}

public sealed record MandarFinalListResponse(
    bool Ok,
    int Total,
    IReadOnlyList<MandarFinalListItem> Resultados
);

// ─── MandarFinal — add ────────────────────────────────────────────────────────


/// Respuesta de Kit_vin_mandar_final_add.
/// ya_activos y fecha_semana eliminados — el SP ya no los devuelve.

public sealed record MandarFinalAddResponse(
    bool Ok,
    string Mensaje,
    int Solicitados,
    int Insertados,
    int Reactivados
);

// ─── MandarFinal — remove ─────────────────────────────────────────────────────


/// Respuesta de Kit_vin_mandar_final_remove.
/// Columna renombrada: no_encontrados_o_ya_inactivos → no_encontrados.

public sealed record MandarFinalRemoveResponse(
    bool Ok,
    string Mensaje,
    int Solicitados,
    int Removidos,
    int NoEncontrados
);

// ─── Wks — status board ───────────────────────────────────────────────────────

/// <summary>
/// Fila devuelta por kit_vin_wks_status_board.
/// Un wkname con tipo compuesto (ZC/ZD) produce 2 filas — una por tipo.
/// Los nombres de columna respetan exactamente los del SP para evitar confusión.
/// </summary>
public sealed record WksStatusBoardRow
{
    /// <summary>Semana de producción. Ejemplo: "wk20", "wk21".</summary>
    public string Wk { get; init; } = string.Empty;

    /// <summary>Tipo de circuito. Ejemplo: "CEA", "ZC", "ZD", "C2", "BodyT3".</summary>
    public string Tipo { get; init; } = string.Empty;

    /// <summary>Cantidad de VINs programados para esta semana/tipo.</summary>
    public int VinCant { get; init; }

    /// <summary>Kits completos kiteados pero aún NO entregados.</summary>
    public int KitsComp { get; init; }

    /// <summary>Kits completos kiteados Y entregados.</summary>
    public int KitCompFinal { get; init; }

    /// <summary>KitsComp + KitCompFinal.</summary>
    public int KitsCompTot { get; init; }

    /// <summary>Porcentaje de VINs escaneados (0–100). Dos decimales.</summary>
    public decimal Porc { get; init; }
}

public sealed record WksStatusBoardResponse(
    bool Ok,
    int Total,
    IReadOnlyList<WksStatusBoardRow> Resultados
);

// ─── Admin — semana preview ───────────────────────────────────────────────────

/// <summary>
/// Result set 1: resumen general de la semana (1 fila).
/// estatus_header: "SIN_HEADER" | "Pendiente" | "APROBADA"
/// ya_cargada: true si el Job ya corrió crea_db para esta semana.
/// </summary>
public sealed record WkPreviewResumen
{
    public string Wkname { get; init; } = string.Empty;
    public string? Tipo { get; init; }
    public string? FechaSemana { get; init; }   // date → string ISO yyyy-MM-dd
    public int TotalVins { get; init; }
    public int TotalGrupos { get; init; }
    public string? DueDateMin { get; init; }   // date → string ISO yyyy-MM-dd
    public string? DueDateMax { get; init; }
    public string? EstatusHeader { get; init; }   // "SIN_HEADER" | "Pendiente" | "APROBADA"
    public bool YaCargada { get; init; }   // ya existe en VinBusiness_DB_macro
}

/// <summary>
/// Result set 2: detalle por grupo (1 fila por grupo), ordenado por total_vins DESC.
/// </summary>
public sealed record WkPreviewGrupo
{
    public string? Grupo { get; init; }
    public int TotalVins { get; init; }
    public string? Descripcion { get; init; }
    public string? Modelo { get; init; }
    public string? Motherharness { get; init; }
    public string? DueDateMin { get; init; }   // date → string ISO yyyy-MM-dd
    public string? DueDateMax { get; init; }
    public decimal HorasPromedio { get; init; }
}

public sealed record WkPreviewResponse(
    bool Ok,
    string Wkname,
    WkPreviewResumen Resumen,
    IReadOnlyList<WkPreviewGrupo> Detalle
);

/// <summary>
/// Fila devuelta por Kit_vin_buscar_circuito.
/// Busca por item exacto, overlay completo o coincidencia parcial en ambos campos.
/// estado: "PENDIENTE" | "KITEADO" | "ENTREGADO"
/// </summary>
public sealed record BuscarCircuitoItem
{
    public int? Locacion { get; init; }
    public string? Vin { get; init; }
    public string? Grupo { get; init; }
    public string? Vindesc { get; init; }
    public string? Overlay { get; init; }
    public string? Item { get; init; }
    public string? Descripcion { get; init; }
    public string? Estado { get; init; }   // "PENDIENTE" | "KITEADO" | "ENTREGADO"
    public string? Operador { get; init; }
    public string? Entregado { get; init; }   // DateTime → string ISO, null si no entregado
    public string? EntregadoPor { get; init; }
    public bool EsMandarAFinal { get; init; }   // Locacion = 0
}

public sealed record BuscarCircuitoResponse(
    bool Ok,
    string Wkname,
    string Circuito,
    bool SoloFaltantes,
    int Total,
    IReadOnlyList<BuscarCircuitoItem> Resultados
);