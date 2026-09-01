using ApiKiteo.API.Models.Responses;
using Azure.Core;

namespace ApiKiteo.API.Repositories.Interfaces;

// ─── Auth ─────────────────────────────────────────────────────────────────────

public interface IAuthRepository
{

    /// Ejecuta Kit_vin_User_Access y devuelve los permisos del usuario.

    Task<IEnumerable<UserAccessRow>> GetUserAccessAsync(
        string username, CancellationToken ct = default);
}

// ─── Semanas ──────────────────────────────────────────────────────────────────

public interface ISemanasRepository
{
    Task<IEnumerable<dynamic>> GetSemanasAsync(
        string cliente, string tipo, CancellationToken ct = default);

    Task<IEnumerable<dynamic>> GetSemanasPendientesAsync(
        byte filtro = 0, CancellationToken ct = default);
}

// ─── Empleados ────────────────────────────────────────────────────────────────

public interface IEmpleadosRepository
{
    Task<string?> GetNombreEmpleadoAsync(
        string empleado, CancellationToken ct = default);
}

// ─── VINs ─────────────────────────────────────────────────────────────────────

public interface IVinsRepository
{
    Task<IEnumerable<dynamic>> GetSemanaLocAsync(
        string wkname, CancellationToken ct = default);

    Task<IEnumerable<dynamic>> GetSemanaGrpStatusAsync(
        string wkname, CancellationToken ct = default);

    Task<IEnumerable<dynamic>> GetSemanaGrpFaltantesAsync(
        string wkname, string jsonGrupos, string det,
        string? descripcion = null,           // NUEVO
        CancellationToken ct = default);
    Task<IEnumerable<dynamic>> GetSemanaVinStatusAsync(
        string wkname, string cliente, string tipo,
        byte modo = 1,    // 1=pending | 2=delivered | 3=all
        CancellationToken ct = default);

    /// <summary>
    /// Busca filas en VinBusiness_DB_macro por item o overlay (búsqueda parcial).
    /// soloFaltantes: '0' = todos, '1' = solo sin operador (Pendiente).
    /// Ejecuta: Kit_vin_buscar_circuito
    /// </summary>
    Task<IEnumerable<dynamic>> BuscarCircuitoAsync(
        string wkname, string circuito, string soloFaltantes,
        CancellationToken ct = default);
}

// ─── Escaneo ──────────────────────────────────────────────────────────────────

public interface IEscaneoRepository
{
    Task<IEnumerable<dynamic>> GetVinToAdjustAsync(
        string wkname, string item, string empleado, CancellationToken ct = default);

    Task<IEnumerable<dynamic>> EscanearAjusteAsync(
        string wkname, string item, string jsonVines, string empleado,
        CancellationToken ct = default);

    Task<IEnumerable<dynamic>> EscanearAsync(
        string wkname, string item, int cantidad, string empleado,
        CancellationToken ct = default);

    Task<IEnumerable<dynamic>> EntregarVinesAsync(
        string wkname, string jsonVines, string empleado,
        string comentario, string supervisor, CancellationToken ct = default);
}

// ─── Admin ────────────────────────────────────────────────────────────────────

public interface IAdminRepository
{
    Task<IEnumerable<dynamic>> AprobarSemanaAsync(
        string wkname, string aprobadoPor, CancellationToken ct = default);

    /// <summary>
    /// Ejecuta Kit_vin_wk_preview y devuelve dos result sets:
    /// Item1 = resumen general (1 fila o fila de error).
    /// Item2 = detalle por grupo (vacío si hubo error en Item1).
    /// Usa GridReader — los SPs con múltiples result sets lo requieren.
    /// </summary>
    Task<(IEnumerable<dynamic> Resumen, IEnumerable<dynamic> Detalle)> PreviewSemanaAsync(
        string wkname, CancellationToken ct = default);

    /// <summary>
    /// Verifica si ya existen filas para ese wkname en VinBusiness_DB_macro.
    /// Se usa como guarda antes de ejecutar kit_vin_crea_db.
    /// </summary>
    Task<bool> WkNameExistsInMacroAsync(
        string wkname, CancellationToken ct = default);

    /// <summary>
    /// Ejecuta kit_vin_crea_db y devuelve dos result sets:
    /// Item1 = metadata resuelta (1 fila: wkname, wknamedata, descripcion, cliente, tipo).
    /// Item2 = registros creados en VinBusiness_DB_macro.
    /// wknamerename es opcional — si viene, el SP renombra el wkname antes del SELECT final.
    /// Usa GridReader por los múltiples result sets.
    /// </summary>
    Task<(IEnumerable<dynamic> Metadata, IEnumerable<dynamic> Registros)> CrearDbAsync(
        string wkname, string? wknamerename, string? usuario, CancellationToken ct = default);

    /// <summary>
    /// Lista de VINs individuales de una semana para el preview de admin.
    /// SQL inline justificado — query simple de lectura sobre Vines sin SP.
    /// </summary>
    Task<IEnumerable<dynamic>> GetPreviewVinsAsync(
        string wkname, CancellationToken ct = default);
}

// ─── Admin — Roles ────────────────────────────────────────────────────────────

public interface IAdminRolesRepository
{

    /// Llama Kit_vin_admin_roles_list.
    /// Devuelve el resultset completo de Central_Access para KiteoApp.

    Task<IEnumerable<dynamic>> GetRolesAsync(
        string site, string access, bool includeInactive,
        CancellationToken ct = default);


    /// Llama Kit_vin_admin_role_add.
    /// Devuelve un rowset con http_status / code / message + datos del nuevo registro.

    Task<IEnumerable<dynamic>> AddRoleAsync(
        string username, string fullName, string access,
        string site, string createdBy,
        CancellationToken ct = default);


    /// Llama Kit_vin_admin_role_remove.
    /// Devuelve un rowset con http_status / code / message.

    Task<IEnumerable<dynamic>> RemoveRoleAsync(
        int idNum, string removedBy,
        CancellationToken ct = default);


    /// Llama Kit_vin_admin_role_update.
    /// Devuelve un rowset con http_status / code / message + access anterior/nuevo.

    Task<IEnumerable<dynamic>> UpdateRoleAsync(
        int idNum, string access, string updatedBy,
        CancellationToken ct = default);
}

// ─── MandarFinal ──────────────────────────────────────────────────────────────


public interface IMandarFinalRepository
{




    /// Devuelve TOP 20 ParentItems de CNDetalle para la semana en curso,
    /// opcionalmente filtrados por búsqueda parcial.
    /// Ejecuta: Kit_vin_mandar_final_parents

    Task<IEnumerable<dynamic>> GetParentsAsync(
        string sitio, string search, CancellationToken ct = default);


    /// Devuelve los items hijo de un ParentItem para la semana en curso,
    /// con overlay y flag de presencia en la lista de mandar_a_final.
    /// Ejecuta: Kit_vin_mandar_final_por_parent

    Task<IEnumerable<dynamic>> GetPorParentAsync(
        string sitio, string parentItem, CancellationToken ct = default);


    /// Devuelve todos los items registrados en VinBusiness_DB_macro_Mandar_a_final.
    /// Ejecuta: Kit_vin_mandar_final_list

    Task<IEnumerable<dynamic>> GetListAsync(
        bool includeInactive, CancellationToken ct = default);


    /// Agrega o reactiva items en VinBusiness_DB_macro_Mandar_a_final.
    /// El SP espera @jsonItems = {"items":["ITEM1","ITEM2"]}.
    /// Ejecuta: Kit_vin_mandar_final_add

    Task<IEnumerable<dynamic>> AddItemsAsync(
        string jsonItems, string usuario, string sitio,
        CancellationToken ct = default);


    /// Soft-delete (Estatus = 0) de items en VinBusiness_DB_macro_Mandar_a_final.
    /// El SP espera @jsonItems = {"items":["ITEM1","ITEM2"]}.
    /// Ejecuta: Kit_vin_mandar_final_remove

    Task<IEnumerable<dynamic>> RemoveItemsAsync(
        string jsonItems, string usuario, CancellationToken ct = default);
}

public interface IWksRepository
{
    /// <summary>
    /// Obtiene el estado de kits por semana y tipo para una lista de wknames.
    /// El SP expande internamente wknames con tipo compuesto (ZC/ZD) en filas separadas.
    /// El SP espera @jsonWkname = {"wkname": ["wk20_108_CEA", ...]}.
    /// Ejecuta: kit_vin_wks_status_board
    /// </summary>
    Task<IEnumerable<dynamic>> GetStatusBoardAsync(
        string jsonWkname, CancellationToken ct = default);

    /// <summary>
    /// Recalcula el cache de status board para un wkname específico.
    /// Fire-and-forget — llamar después de escanear, entregar o crear_db.
    /// SQL inline en C# — sin SP.
    /// </summary>
    Task RefreshStatusCacheAsync(
        string wkname, CancellationToken ct = default);

    /// <summary>
    /// Limpia entradas del cache según límites configurables.
    /// Devuelve el número de filas eliminadas.
    /// </summary>
    Task<int> CacheCleanupAsync(
        int semanasRetener, int horasCompletadas, CancellationToken ct = default);
}

// ─── Macro Export ─────────────────────────────────────────────────────────────

public interface IMacroRepository
{
    /// <summary>
    /// Ejecuta una consulta paginada/filtrada sobre VinBusiness_DB_macro
    /// y pasa el IEnumerable al delegate <paramref name="process"/> mientras
    /// la conexión sigue abierta (patrón callback para streaming seguro).
    ///
    /// Sin filtros → últimas 4 semanas por recorddate.
    /// wknames vacío = sin filtro por semana.
    ///
    /// EXCEPCIÓN DE STACK: SQL inline justificado — no existe SP para esta consulta
    /// y el query es 100% parameterizado (sin concatenación de strings de usuario).
    /// </summary>
    Task StreamMacroAsync(
        IReadOnlyList<string> wknames,
        string? tipo,
        string? cliente,
        DateOnly? desde,
        DateOnly? hasta,
        Func<IEnumerable<dynamic>, Task> process,
        CancellationToken ct = default);
}




public interface ILiberacionRepository
{
    /// <summary>
    /// Semanas por estatus y cliente para el selector del form.
    /// Ejecuta: Kit_vin_wks_semanas_liberacion
    /// </summary>
    Task<IEnumerable<dynamic>> GetSemanasAsync(
        string estatus = "PendienteCorte", string cliente = "TODOS",
        CancellationToken ct = default);

    /// <summary>
    /// Crea un lote de liberación y linkea las semanas.
    /// Con sobreescribir=false devuelve 400 si hay lote activo.
    /// Con sobreescribir=true elimina el lote anterior y crea uno nuevo.
    /// Ejecuta: Kit_vin_liberacion_crear
    /// </summary>
    Task<IEnumerable<dynamic>> CrearLoteAsync(
        string jsonWknames, string username, bool sobreescribir,
        CancellationToken ct = default);

    /// <summary>
    /// Devuelve SIEMPRE 2 result sets via GridReader:
    ///   RS1: resumen  — item | Cant | cliente
    ///   RS2: detalle  — wkname | tipo | item | qty_ordered | cliente | vin
    /// Sin lote_id — la creación del lote es responsabilidad de CrearLoteAsync.
    /// Ejecuta: Kit_vin_wks_liberacion
    /// </summary>
    Task<(IEnumerable<dynamic> Resumen, IEnumerable<dynamic> Detalle)> GetMaterialAsync(
        string jsonWknames, string username, string cliente,
        CancellationToken ct = default);

    /// <summary>
    /// Busca un lote por ID — 2 result sets via GridReader.
    ///   RS1: LoteData (resumen del lote)
    ///   RS2: WkData   (semanas con fechacorte e ingresado flag)
    /// Ejecuta: Kit_vin_liberacion_get
    /// </summary>
    Task<(IEnumerable<dynamic> Lote, IEnumerable<dynamic> Semanas)> GetLoteAsync(
        int loteId, CancellationToken ct = default);

    /// <summary>
    /// Corte ingresa fechacorte para una semana.
    /// Cuando todos tienen fechacorte → estatus = PENDIENTE automáticamente.
    /// Ejecuta: Kit_vin_corte_ingresar
    /// </summary>
    Task<IEnumerable<dynamic>> IngresarCorteAsync(
        int loteId, string wkname, int semana, int anio, string username,
        CancellationToken ct = default);

    /// <summary>
    /// Lista lotes de la semana actual y la anterior.
    /// Ejecuta: Kit_vin_liberacion_list
    /// </summary>
    Task<IEnumerable<dynamic>> LiberacionListAsync(
        string cliente = "TODOS", CancellationToken ct = default);

    /// <summary>
    /// Busca MAX(DateFetch) en BuildPlan.dbo.SytelineOut para semana+año.
    /// Inline SQL — no SP (cross-database, solo lectura).
    /// </summary>
    Task<DateOnly?> GetFechaCorteAsync(
        int semana, int anio, CancellationToken ct = default);

    /// <summary>
    /// Congela el resumen que se envió a Corte y marca el lote como Enviado.
    /// Ejecuta: Kit_vin_liberacion_snapshot_guardar
    /// </summary>
    Task<IEnumerable<dynamic>> GuardarSnapshotAsync(
        int loteId, string username, string jsonResumen,
        string? destinatarios, string? wkEtiqueta, string? cliente, string? archivo,
        CancellationToken ct = default);

    /// <summary>
    /// Lee un lote enviado — 3 result sets via GridReader:
    ///   RS1: cabecera   RS2: resumen congelado   RS3: semanas
    /// Sin JOIN a Vines: un lote de hace meses se lee igual que el de hoy.
    /// Ejecuta: Kit_vin_liberacion_snapshot_get
    /// </summary>
    Task<(IEnumerable<dynamic> Lote, IEnumerable<dynamic> Resumen, IEnumerable<dynamic> Semanas)>
        GetSnapshotAsync(int loteId, CancellationToken ct = default);

    /// <summary>
    /// Lotes ya enviados, para el selector de reimpresión.
    /// Ejecuta: Kit_vin_liberacion_historial
    /// </summary>
    Task<IEnumerable<dynamic>> HistorialAsync(
        string cliente = "TODOS", int top = 50, CancellationToken ct = default);
}
public interface ISchedulingRepository
{
    /// <summary>
    /// Semanas activas (con items sin Entregado) + detalle opcional.
    /// @wkname null  → solo RS1 (selector)
    /// @wkname valor → RS1 + RS2 (selector + detalle)
    /// Ejecuta: Kit_vin_scheduling
    /// </summary>
    Task<(IEnumerable<dynamic> Semanas, IEnumerable<dynamic>? Detalle)> GetAsync(
        string? wkname, string cliente, CancellationToken ct = default);
}

public interface IDescaneoRepository
{
    /// <summary>
    /// Busca items en VinBusiness_DB_macro con filtros opcionales.
    /// modo: 1=escaneados | 2=sin escanear | 3=todos.
    /// Ejecuta: Kit_vin_descan_buscar
    /// </summary>
    Task<IEnumerable<dynamic>> BuscarAsync(
        string? wkname,
        string? vin,
        string? item,
        string? operador,
        string? cliente,
        DateOnly? fechaDesde,
        DateOnly? fechaHasta,
        byte modo,
        CancellationToken ct = default);

    /// <summary>
    /// Descanea un item por su id. Registra auditoría en Boss_transactions.
    /// Devuelve 1 fila con http_status, code, message [+ datos del item].
    /// Ejecuta: Kit_vin_descaneo_aplicar
    /// </summary>
    Task<dynamic?> AplicarAsync(
        int id, string username, string motivo,
        CancellationToken ct = default);
}
/// <summary>
/// Listas de prioridad — modelo de 3 niveles.
///   Nivel 1  kit_lista_prioridad · Nivel 2  kit_lista · Nivel 3  kit_lista_item
/// </summary>
public interface IListasRepository
{
    // ── Nivel 1: contenedor ───────────────────────────────────────────────
    Task<dynamic?> PrioridadCrearAsync(
        string wkname, string cliente, string tipo, string nombre, string creadoPor,
        CancellationToken ct = default);

    Task<IEnumerable<dynamic>> PrioridadListAsync(
        string wkname, string cliente, string tipo,
        CancellationToken ct = default);

    // ── Nivel 2: listas ───────────────────────────────────────────────────
    Task<IEnumerable<dynamic>> GetActivasAsync(
        int prioridadId, CancellationToken ct = default);

    Task<dynamic?> CrearAsync(
        int prioridadId, string nombre, string colorHex,
        string? filtrosJson, string? asignadoA, string creadoPor,
        string? jsonItems, int? orden,
        CancellationToken ct = default);

    /// <summary>asignadoA == "" borra el asignado; null lo deja como está.</summary>
    Task<dynamic?> ActualizarAsync(
        int listaId, string? nombre, string? colorHex, string? asignadoA, string username,
        CancellationToken ct = default);

    /// <summary>direccion: -1 sube (más prioridad), 1 baja.</summary>
    Task<dynamic?> ReordenarAsync(
        int listaId, short direccion, string username,
        CancellationToken ct = default);

    Task<dynamic?> EliminarAsync(
        int listaId, string username, CancellationToken ct = default);

    // ── Nivel 3: circuitos ────────────────────────────────────────────────
    Task<(dynamic? Header, IEnumerable<dynamic> Items)> GetDetalleAsync(
        int listaId, CancellationToken ct = default);

    Task<dynamic?> AgregarItemsAsync(
        int listaId, string jsonItems, string? jsonGrupos, string? etiqueta,
        string? creadoPor, CancellationToken ct = default);

    // `username` va a Boss_transactions: vaciar una lista item por item no
    // dejaba ningún rastro, a diferencia de borrarla completa.
    Task<dynamic?> ActualizarNotaAsync(
        int listaId, int itemId, string? notaArea, string? username,
        CancellationToken ct = default);

    Task<dynamic?> QuitarItemAsync(
        int listaId, int itemId, string? username, CancellationToken ct = default);

    // ── Panel F6: franja de color por grupo ───────────────────────────────
    Task<IEnumerable<dynamic>> GruposMarcadosAsync(
        int prioridadId, CancellationToken ct = default);

    /// <summary>
    /// Una fila por PIEZA (VIN) que la lista todavia tiene que surtir.
    /// Es la otra unidad de la misma lista: `GetDetalleAsync` devuelve
    /// CIRCUITOS, esto devuelve VINs. Mezclarlas fue el bug del "43 vs 53".
    /// </summary>
    Task<IEnumerable<dynamic>> PiezasAsync(int listaId, CancellationToken ct = default);

    /// <summary>
    /// Inline SQL — valida LPaccess en Central_Access.
    /// No tiene SP equivalente.
    /// </summary>
    Task<bool> HasLpAccessAsync(
        string username, CancellationToken ct = default);
}

public interface IExpeditadosRepository
{
    /// <summary>Ejecuta Kit_vin_expeditados_detectar. RS1=resumen, RS2=pendientes.</summary>
    Task<(IEnumerable<dynamic> Resumen, IEnumerable<dynamic> Pendientes)>
        DetectarAsync(bool soloReportar, CancellationToken ct = default);

    /// <summary>Ejecuta Kit_vin_expeditados_mover. RS1=resultado, RS2=vins movidos.</summary>
    Task<(IEnumerable<dynamic> Resultado, IEnumerable<dynamic> Vins)>
        MoverAsync(string ids, string username, CancellationToken ct = default);

    /// <summary>Ejecuta Kit_vin_expeditados_ignorar.</summary>
    Task<IEnumerable<dynamic>> IgnorarAsync(
        string ids, string username, string? motivo, CancellationToken ct = default);
}