using System.Text.Json;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class MandarFinalService : IMandarFinalService
{
    private readonly IMandarFinalRepository _repo;
    private readonly ILogger<MandarFinalService> _logger;

    public MandarFinalService(IMandarFinalRepository repo, ILogger<MandarFinalService> logger)
    {
        _repo = repo;
        _logger = logger;
    }


    // ── GET /mandar_final/parents ─────────────────────────────────────────────

    public async Task<ServiceResult<MandarFinalParentsResponse>> GetParentsAsync(
        string sitio, string? search, CancellationToken ct = default)
    {
        try
        {
            // search vacío desactiva el filtro en el SP
            var searchParam = search?.Trim() ?? string.Empty;

            var rows = await _repo.GetParentsAsync(sitio, searchParam, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new MandarFinalParentItem
                {
                    ParentItem = d.GetStr("ParentItem") ?? string.Empty,
                    TotalCircuitos = d.GetInt("total_circuitos") ?? 0,
                    TieneActivosEnLista = (d.GetInt("tiene_activos_en_lista") ?? 0) == 1,
                    FechaSemana = FormatDate(d.GetValueOrDefault("fecha_semana"))
                })
                .ToList();

            return ServiceResult<MandarFinalParentsResponse>.Ok(
                new MandarFinalParentsResponse(
                    true,
                    sitio,
                    string.IsNullOrEmpty(searchParam) ? null : searchParam,
                    resultados.Count,
                    resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetParents sitio={Sitio} search={Search}", sitio, search);
            return ServiceResult<MandarFinalParentsResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── GET /mandar_final/por_parent ──────────────────────────────────────────

    public async Task<ServiceResult<MandarFinalPorParentResponse>> GetPorParentAsync(
        string sitio, string parentItem, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetPorParentAsync(sitio, parentItem, ct);
            var list = rows.Select(r => (IDictionary<string, object?>)r).ToList();

            // El SP devuelve fila de error si sitio o parentItem están vacíos
            if (list.Count == 1)
            {
                var spError = TryExtractSpError<MandarFinalPorParentResponse>(list[0]);
                if (spError is not null) return spError;
            }

            var resultados = list
                .Select(d => new MandarFinalPorParentItem
                {
                    ParentItem = d.GetStr("ParentItem") ?? parentItem,
                    Item = d.GetStr("Item") ?? string.Empty,
                    Description = d.GetStr("Description"),
                    Circuits = d.GetStr("Circuits"),
                    Splices = d.GetStr("Splices"),
                    Twists = d.GetStr("Twists"),
                    Overlay = d.GetStr("overlay"),
                    FechaSemana = FormatDate(d.GetValueOrDefault("fecha_semana")),
                    YaEnLista = (d.GetInt("ya_en_lista") ?? 0) == 1,
                    AgregadoPor = d.GetStr("agregado_por"),
                    FechaAgregado = FormatDate(d.GetValueOrDefault("fecha_agregado"))
                })
                .ToList();

            return ServiceResult<MandarFinalPorParentResponse>.Ok(
                new MandarFinalPorParentResponse(true, sitio, parentItem, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en GetPorParent sitio={Sitio} parentItem={Parent}", sitio, parentItem);
            return ServiceResult<MandarFinalPorParentResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── GET /mandar_final ─────────────────────────────────────────────────────

    public async Task<ServiceResult<MandarFinalListResponse>> GetListAsync(
        bool includeInactive, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetListAsync(includeInactive, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new MandarFinalListItem
                {
                    Id = d.GetInt("id") ?? 0,
                    Item = d.GetStr("item") ?? string.Empty,
                    Usuario = d.GetStr("Usuario"),
                    Recorddate = FormatDate(d.GetValueOrDefault("recorddate")),
                    Estatus = d.GetInt("Estatus") ?? 1
                })
                .ToList();

            return ServiceResult<MandarFinalListResponse>.Ok(
                new MandarFinalListResponse(true, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetList includeInactive={Flag}", includeInactive);
            return ServiceResult<MandarFinalListResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /mandar_final/add ────────────────────────────────────────────────

    public async Task<ServiceResult<MandarFinalAddResponse>> AddItemsAsync(
        MandarFinalAddRequest request, CancellationToken ct = default)
    {
        try
        {
            var jsonItems = JsonSerializer.Serialize(new { items = request.Items });

            var rows = await _repo.AddItemsAsync(
                jsonItems,
                request.Usuario,
                request.Sitio ?? string.Empty,
                ct);

            var list = rows.Select(r => (IDictionary<string, object?>)r).ToList();

            if (list.Count == 0)
                return ServiceResult<MandarFinalAddResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var d = list[0];

            var spError = TryExtractSpError<MandarFinalAddResponse>(d);
            if (spError is not null) return spError;

            return ServiceResult<MandarFinalAddResponse>.Ok(
                new MandarFinalAddResponse(
                    Ok: true,
                    Mensaje: d.GetStr("message") ?? "Operación completada.",
                    Solicitados: d.GetInt("solicitados") ?? request.Items.Count,
                    Insertados: d.GetInt("insertados") ?? 0,
                    Reactivados: d.GetInt("reactivados") ?? 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AddItems usuario={User}", request.Usuario);
            return ServiceResult<MandarFinalAddResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /mandar_final/remove ─────────────────────────────────────────────

    public async Task<ServiceResult<MandarFinalRemoveResponse>> RemoveItemsAsync(
        MandarFinalRemoveRequest request, CancellationToken ct = default)
    {
        try
        {
            var jsonItems = JsonSerializer.Serialize(new { items = request.Items });

            var rows = await _repo.RemoveItemsAsync(jsonItems, request.Usuario, ct);

            var list = rows.Select(r => (IDictionary<string, object?>)r).ToList();

            if (list.Count == 0)
                return ServiceResult<MandarFinalRemoveResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var d = list[0];

            var spError = TryExtractSpError<MandarFinalRemoveResponse>(d);
            if (spError is not null) return spError;

            return ServiceResult<MandarFinalRemoveResponse>.Ok(
                new MandarFinalRemoveResponse(
                    Ok: true,
                    Mensaje: d.GetStr("message") ?? "Operación completada.",
                    Solicitados: d.GetInt("solicitados") ?? request.Items.Count,
                    Removidos: d.GetInt("removidos") ?? 0,
                    // Columna renombrada en el SP: no_encontrados_o_ya_inactivos → no_encontrados
                    NoEncontrados: d.GetInt("no_encontrados") ?? 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en RemoveItems usuario={User}", request.Usuario);
            return ServiceResult<MandarFinalRemoveResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lee http_status del rowset del SP.
    /// Devuelve null si es 200 (éxito) o si la columna no existe (resultado normal).
    /// Devuelve un ServiceResult de falla para cualquier otro código.
    /// </summary>
    private static ServiceResult<T>? TryExtractSpError<T>(IDictionary<string, object?> d)
    {
        var rawStatus = d.GetValueOrDefault("http_status");
        if (rawStatus is null) return null;

        if (!int.TryParse(rawStatus.ToString(), out var httpStatus)) return null;
        if (httpStatus == 200) return null;

        var mensaje = d.GetStr("message") ?? "Error al procesar la solicitud.";
        var codigo = d.GetStr("code") ?? ErrorCodes.Kiteo500;

        return ServiceResult<T>.Fail(httpStatus, mensaje, codigo);
    }

    /// <summary>
    /// Convierte un valor de fecha (DateTime, DateOnly, string) a ISO 8601 yyyy-MM-dd.
    /// Devuelve null si el valor es nulo o no parseable.
    /// </summary>
    private static string? FormatDate(object? val)
    {
        if (val is null || val is DBNull) return null;

        return val switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            DateOnly dOnly => dOnly.ToString("yyyy-MM-dd"),
            _ => DateTime.TryParse(val.ToString(), out var parsed)
                                  ? parsed.ToString("yyyy-MM-dd")
                                  : val.ToString()
        };
    }
}