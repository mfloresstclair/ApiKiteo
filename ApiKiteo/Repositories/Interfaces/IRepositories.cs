using ApiKiteo.API.Models.Responses;

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
        string wkname, string jsonGrupos, string det, CancellationToken ct = default);

    Task<IEnumerable<dynamic>> GetSemanaVinStatusAsync(
        string wkname, string cliente, string tipo, CancellationToken ct = default);

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