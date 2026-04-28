using ApiKiteo.API.Models.Responses;

namespace ApiKiteo.API.Repositories.Interfaces;

// ─── Auth ─────────────────────────────────────────────────────────────────────

public interface IAuthRepository
{
    /// <summary>
    /// Ejecuta Kit_vin_User_Access y devuelve los permisos del usuario.
    /// </summary>
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
