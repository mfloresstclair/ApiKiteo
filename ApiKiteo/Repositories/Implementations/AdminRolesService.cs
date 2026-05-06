using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class AdminRolesService : IAdminRolesService
{
    private readonly IAdminRolesRepository _repo;
    private readonly ILogger<AdminRolesService> _logger;

    public AdminRolesService(IAdminRolesRepository repo, ILogger<AdminRolesService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // ── GET /api/roles ────────────────────────────────────────────────────────

    public async Task<ServiceResult<RolesListResponse>> GetRolesAsync(
        string site, string access, bool includeInactive,
        CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetRolesAsync(site, access, includeInactive, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new RoleItem
                {
                    IdNum = d.GetInt("id_num") ?? 0,
                    UserName = d.GetStr("UserName") ?? string.Empty,
                    FullName = d.GetStr("FullName") ?? string.Empty,
                    Access = d.GetStr("Access") ?? string.Empty,
                    Site = d.GetStr("Site") ?? string.Empty,
                    Estatus = d.GetInt("Estatus") ?? 1,
                    // Fechas como string para no depender del formato del cliente WPF
                    CreatedAt = d.GetValueOrDefault("created_at")?.ToString(),
                    LastUpdated = d.GetValueOrDefault("last_updated")?.ToString()
                })
                .ToList();

            _logger.LogInformation(
                "GetRoles | site={Site} access={Access} inactivos={Inc} → {Count} registros",
                site, access, includeInactive, resultados.Count);

            return ServiceResult<RolesListResponse>.Ok(
                new RolesListResponse(true, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetRoles site={Site} access={Access}", site, access);
            return ServiceResult<RolesListResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Admin500);
        }
    }

    // ── POST /api/roles ───────────────────────────────────────────────────────

    public async Task<ServiceResult<RoleAddResponse>> AddRoleAsync(
        RoleAddRequest request, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.AddRoleAsync(
                request.Username, request.FullName, request.Access,
                request.Site, request.CreatedBy, ct);

            var result = ParseSpResult(rows);
            if (result is not null) return result.Cast<RoleAddResponse>();

            // http_status = 200 → construir respuesta con los datos devueltos
            var d = rows
                .Select(r => (IDictionary<string, object?>)r)
                .First();

            var response = new RoleAddResponse(
                Ok: true,
                Mensaje: d.GetStr("message") ?? "Rol asignado correctamente.",
                IdNum: d.GetInt("id_num") ?? 0,
                Username: d.GetStr("username") ?? request.Username,
                FullName: d.GetStr("fullName") ?? request.FullName,
                Access: d.GetStr("access") ?? request.Access,
                Site: d.GetStr("site") ?? request.Site);

            _logger.LogInformation(
                "RoleAdd | username={U} access={A} site={S} → id={Id}",
                request.Username, request.Access, request.Site, response.IdNum);

            return ServiceResult<RoleAddResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AddRole username={U}", request.Username);
            return ServiceResult<RoleAddResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Admin500);
        }
    }

    // ── DELETE /api/roles/{id} ────────────────────────────────────────────────

    public async Task<ServiceResult<RoleRemoveResponse>> RemoveRoleAsync(
        int idNum, string removedBy, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.RemoveRoleAsync(idNum, removedBy, ct);

            var result = ParseSpResult(rows);
            if (result is not null) return result.Cast<RoleRemoveResponse>();

            var d = rows
                .Select(r => (IDictionary<string, object?>)r)
                .First();

            var response = new RoleRemoveResponse(
                Ok: true,
                Mensaje: d.GetStr("message") ?? "Rol removido correctamente.",
                IdNum: d.GetInt("id_num") ?? idNum,
                Username: d.GetStr("username") ?? string.Empty,
                Access: d.GetStr("access") ?? string.Empty);

            _logger.LogInformation(
                "RoleRemove | id={Id} removedBy={By} username={U}",
                idNum, removedBy, response.Username);

            return ServiceResult<RoleRemoveResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en RemoveRole id={Id}", idNum);
            return ServiceResult<RoleRemoveResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Admin500);
        }
    }

    // ── PUT /api/roles/{id} ───────────────────────────────────────────────────

    public async Task<ServiceResult<RoleUpdateResponse>> UpdateRoleAsync(
        int idNum, RoleUpdateRequest request, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.UpdateRoleAsync(idNum, request.Access, request.UpdatedBy, ct);

            var result = ParseSpResult(rows);
            if (result is not null) return result.Cast<RoleUpdateResponse>();

            var d = rows
                .Select(r => (IDictionary<string, object?>)r)
                .First();

            var response = new RoleUpdateResponse(
                Ok: true,
                Mensaje: d.GetStr("message") ?? "Rol actualizado correctamente.",
                IdNum: d.GetInt("id_num") ?? idNum,
                Username: d.GetStr("username") ?? string.Empty,
                AccessAnterior: d.GetStr("access_anterior") ?? string.Empty,
                AccessNuevo: d.GetStr("access_nuevo") ?? request.Access);

            _logger.LogInformation(
                "RoleUpdate | id={Id} {Prev} → {New} by={By}",
                idNum, response.AccessAnterior, response.AccessNuevo, request.UpdatedBy);

            return ServiceResult<RoleUpdateResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en UpdateRole id={Id}", idNum);
            return ServiceResult<RoleUpdateResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Admin500);
        }
    }

    // ── Helper: interpretar rowset http_status de los SPs de mutación ─────────
    //
    // Los SPs de add/remove/update devuelven SIEMPRE un row con:
    //   http_status  int        → 200 | 400 | 404 | 409 | 500
    //   code         varchar    → 'OK' | 'YA_EXISTE' | 'NOT_FOUND' | ...
    //   message      varchar    → texto legible para el usuario
    //
    // Retorna null si http_status = 200 (el caller debe procesar el row).
    // Retorna un ServiceResult de falla con el status/mensaje/código del SP si ≠ 200.

    private static SpFailResult? ParseSpResult(IEnumerable<dynamic> rows)
    {
        var first = rows
            .Select(r => (IDictionary<string, object?>)r)
            .FirstOrDefault();

        if (first is null)
            return new SpFailResult(500, "El SP no devolvió resultado.", ErrorCodes.Admin500);

        var rawStatus = first.GetValueOrDefault("http_status")
                     ?? first.GetValueOrDefault("httpStatus");

        if (rawStatus is null || !int.TryParse(rawStatus.ToString(), out var httpStatus))
            return null;   // Sin http_status → resultado exitoso directo

        if (httpStatus == 200) return null;   // Éxito → el caller procesa el row

        var mensaje = first.GetStr("message") ?? "Error en operación.";
        var codigo = MapCode(first.GetStr("code"), httpStatus);

        return new SpFailResult(httpStatus, mensaje, codigo);
    }

    // Convierte el código del SP al formato estándar de la API
    private static string MapCode(string? spCode, int httpStatus) => spCode switch
    {
        "YA_EXISTE" => ErrorCodes.Admin409,
        "NOT_FOUND" => ErrorCodes.Admin404,
        "PARAM_INVALIDO" => ErrorCodes.Admin400,
        "ACCESS_INVALIDO" => ErrorCodes.Admin400,
        "SIN_CAMBIO" => ErrorCodes.Admin400,
        "CONFLICT" => ErrorCodes.Admin409,
        _ => httpStatus switch
        {
            400 => ErrorCodes.Admin400,
            404 => ErrorCodes.Admin404,
            409 => ErrorCodes.Admin409,
            _ => ErrorCodes.Admin500
        }
    };

    // Tipo de retorno intermedio para el helper (evita dynamic)
    private sealed record SpFailResult(int HttpStatus, string Mensaje, string Codigo)
    {
        // Permite convertir el fallo a cualquier ServiceResult<T> sin duplicar código
        public ServiceResult<T> Cast<T>() =>
            ServiceResult<T>.Fail(HttpStatus, Mensaje, Codigo);
    }
}