namespace ApiKiteo.API.Configuration;

/// <summary>
/// Nombres de los Stored Procedures configurados en appsettings.json
/// sección "StoredProcedures". Nunca hardcodeados en código.
/// </summary>
public sealed class StoredProceduresOptions
{
    public const string SectionName = "StoredProcedures";

    // ── Auth ──────────────────────────────────────────────────────────────────
    public string GetUserAccess { get; init; } = string.Empty;

    // ── Semanas ───────────────────────────────────────────────────────────────
    public string GetSemanas { get; init; } = string.Empty;
    public string GetSemanasPendientes { get; init; } = string.Empty;

    // ── Empleados ─────────────────────────────────────────────────────────────
    public string CheckEmpleado { get; init; } = string.Empty;

    // ── VINs ──────────────────────────────────────────────────────────────────
    public string GetSemanaLoc { get; init; } = string.Empty;
    public string GetSemanaGrpStatus { get; init; } = string.Empty;
    public string GetSemanaGrpFaltantes { get; init; } = string.Empty;
    public string GetSemanaVinStatus { get; init; } = string.Empty;

    // ── Escaneo ───────────────────────────────────────────────────────────────
    public string GetVinToAdjust { get; init; } = string.Empty;
    public string EscanearAjuste { get; init; } = string.Empty;
    public string Escanear { get; init; } = string.Empty;
    public string EntregarVines { get; init; } = string.Empty;

    // ── Admin ─────────────────────────────────────────────────────────────────
    public string AprobarSemana { get; init; } = string.Empty;

    // ── Admin — Roles ─────────────────────────────────────────────────────────
    public string GetAdminRolesList { get; init; } = string.Empty;
    public string AdminRoleAdd { get; init; } = string.Empty;
    public string AdminRoleRemove { get; init; } = string.Empty;
    public string AdminRoleUpdate { get; init; } = string.Empty;
    // ── MandarFinal ────────────────────────────────────────────────────────────
    public string MandarFinalList { get; init; } = string.Empty;
    public string MandarFinalAdd { get; init; } = string.Empty;
    public string MandarFinalRemove { get; init; } = string.Empty;
    public string MandarFinalParents { get; init; } = string.Empty;
    public string MandarFinalPorParent { get; init; } = string.Empty;
    // ── Wks ────────────────────────────────────────────────────────────────────
    public string WksStatusBoard { get; init; } = string.Empty;

    public string BuscarCircuito { get; init; } = string.Empty;
    public string PreviewSemana { get; init; } = string.Empty;
    public string CrearDb { get; init; } = string.Empty;
    // ── liberacion ─────────────────────────────────────────────────────────────────
    public string LiberacionSemanas { get; init; } = string.Empty;
    public string WksLiberacion { get; init; } = string.Empty;  // Kit_vin_wks_liberacion
    public string LiberacionCrear { get; init; } = string.Empty;  // Kit_vin_liberacion_crear
    public string LiberacionGet { get; init; } = string.Empty;  // Kit_vin_liberacion_get
    public string CorteIngresar { get; init; } = string.Empty;  // Kit_vin_corte_ingresar
    public string LiberacionList { get; init; } = string.Empty;

    public string Scheduling { get; init; } = string.Empty;

    // ── Descaneo ──────────────────────────────────────────────────────────────────
    public string DescanBuscar { get; init; } = string.Empty;  // Kit_vin_descan_buscar
    public string DescaneoAplicar { get; init; } = string.Empty;  // Kit_vin_descaneo_aplicar

     //── Lista Viva ────────────────────────────────────────────────────────────────
     public string ListasActivas { get; init; } = string.Empty;  // Kit_listas_activas
    public string ListaGuardar { get; init; } = string.Empty;  // Kit_lista_guardar
    public string ListaDetalle { get; init; } = string.Empty;  // Kit_lista_detalle
    public string ListaAgregar { get; init; } = string.Empty;  // Kit_lista_agregar
    public string ListaNota { get; init; } = string.Empty;  // Kit_lista_nota
    public string ListaQuitarItem { get; init; } = string.Empty;  // Kit_lista_quitar_item
    public string ListaEliminar { get; init; } = string.Empty;  // Kit_lista_eliminar

    // ── Expeditados ───────────────────────────────────────────────────────────
    public string ExpeditadosDetectar { get; init; } = string.Empty;
    public string ExpeditadosMover { get; init; } = string.Empty;
    public string ExpeditadosIgnorar { get; init; } = string.Empty;

    // ── Comunización ──────────────────────────────────────────────────────────
    public string ValidarComunizacion { get; init; } = string.Empty;

}
