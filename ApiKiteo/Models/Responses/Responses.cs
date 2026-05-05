namespace ApiKiteo.API.Models.Responses;

// ─── Auth ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Respuesta de /auth/login.
/// Contrato fijo con KiteoApp WPF — no cambiar nombres de propiedades.
/// </summary>
public sealed record AuthLoginResponse(
    bool   Ok,
    string Username,
    string Access           // "LPaccess" | "FAaccess"
);

/// <summary>
/// Row interno devuelto por Kit_vin_User_Access.
/// </summary>
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

public sealed record SemanaPendienteItem(string Wkname);

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

/// <summary>
/// Fila de evento devuelta por el SP con Tipo = "EvtData".
/// </summary>
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