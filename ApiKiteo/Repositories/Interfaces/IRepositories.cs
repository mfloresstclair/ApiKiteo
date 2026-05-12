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
        CancellationToken ct = default);
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
