namespace ApiKiteo.API.Configuration;

/// <summary>
/// Nombres de los Stored Procedures configurados en appsettings.json
/// sección "StoredProcedures". Nunca hardcodeados en código.
/// </summary>
public sealed class StoredProceduresOptions
{
    public const string SectionName = "StoredProcedures";

    // ── Auth ──────────────────────────────────────────────────────────────────
    public string GetUserAccess        { get; init; } = string.Empty;

    // ── Semanas ───────────────────────────────────────────────────────────────
    public string GetSemanas           { get; init; } = string.Empty;
    public string GetSemanasPendientes { get; init; } = string.Empty;

    // ── Empleados ─────────────────────────────────────────────────────────────
    public string CheckEmpleado        { get; init; } = string.Empty;

    // ── VINs ──────────────────────────────────────────────────────────────────
    public string GetSemanaLoc         { get; init; } = string.Empty;
    public string GetSemanaGrpStatus   { get; init; } = string.Empty;
    public string GetSemanaGrpFaltantes{ get; init; } = string.Empty;
    public string GetSemanaVinStatus   { get; init; } = string.Empty;

    // ── Escaneo ───────────────────────────────────────────────────────────────
    public string GetVinToAdjust       { get; init; } = string.Empty;
    public string EscanearAjuste       { get; init; } = string.Empty;
    public string Escanear             { get; init; } = string.Empty;
    public string EntregarVines        { get; init; } = string.Empty;

    // ── Admin ─────────────────────────────────────────────────────────────────
    public string AprobarSemana        { get; init; } = string.Empty;

    // ── Admin — Roles ─────────────────────────────────────────────────────────
    public string GetAdminRolesList { get; init; } = string.Empty;
    public string AdminRoleAdd { get; init; } = string.Empty;
    public string AdminRoleRemove { get; init; } = string.Empty;
    public string AdminRoleUpdate { get; init; } = string.Empty;
}
