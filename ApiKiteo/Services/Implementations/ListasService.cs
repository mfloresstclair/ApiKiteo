using System.Text.Json;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class ListasService : IListasService
{
    private readonly IListasRepository      _repo;
    private readonly ILogger<ListasService> _logger;

    public ListasService(IListasRepository repo, ILogger<ListasService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    // ── GET /listas ───────────────────────────────────────────────────────────

    public async Task<ServiceResult<ListasActivasResponse>> GetActivasAsync(
        string? wkname, string? cliente, string? tipo,
        CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetActivasAsync(wkname, cliente, tipo, ct);

            var listas = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new ListaActivaItem
                {
                    Id             = d.GetInt("id")              ?? 0,
                    Wkname         = d.GetStr("wkname")          ?? string.Empty,
                    Cliente        = d.GetStr("cliente")         ?? string.Empty,
                    Tipo           = d.GetStr("tipo")            ?? string.Empty,
                    GruposJson     = d.GetStr("grupos_json"),
                    Det            = d.GetStr("det"),
                    FiltroLoc      = d.GetStr("filtro_loc"),
                    TextoBusqueda  = d.GetStr("texto_busqueda"),
                    CreadoPor      = d.GetStr("creado_por"),
                    CreatedAt      = d.GetStr("created_at"),
                    PendienteActual = d.GetInt("pendiente_actual") ?? 0,
                    TotalItems     = d.GetInt("total_items")      ?? 0
                })
                .ToList();

            return ServiceResult<ListasActivasResponse>.Ok(
                new ListasActivasResponse(true, listas));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetActivas wkname={W}", wkname);
            return ServiceResult<ListasActivasResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /listas ──────────────────────────────────────────────────────────

    public async Task<ServiceResult<ListaGuardarResponse>> GuardarAsync(
        ListaGuardarRequest request, CancellationToken ct = default)
    {
        try
        {
            // FIX 1: array raíz, camelCase, sin wrapper {"items":...}
            var jsonItems = JsonSerializer.Serialize(request.Items, _camelCase);

            var row = await _repo.GuardarAsync(
                request.Wkname, request.Cliente, request.Tipo,
                request.GruposJson, request.Det, request.FiltroLoc,
                request.TextoBusqueda, request.CreadoPor, jsonItems, ct);

            if (row is null)
                return ServiceResult<ListaGuardarResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var d = (IDictionary<string, object?>)row;
            var httpStatus = d.GetInt("http_status") ?? 500;
            var message = d.GetStr("message") ?? string.Empty;

            if (httpStatus != 200)
                return ServiceResult<ListaGuardarResponse>.Fail(
                    httpStatus, message, d.GetStr("code") ?? string.Empty);

            // FIX 2: probar "id" y "lista_id" — el SP devuelve "lista_id"
            var id = d.GetInt("id") ?? d.GetInt("lista_id") ?? 0;

            _logger.LogInformation(
                "Lista guardada | id={Id} wkname={W} creado_por={U}",
                id, request.Wkname, request.CreadoPor);

            return ServiceResult<ListaGuardarResponse>.Ok(
                new ListaGuardarResponse(true, id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Guardar wkname={W}", request.Wkname);
            return ServiceResult<ListaGuardarResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── GET /listas/:id ───────────────────────────────────────────────────────

    public async Task<ServiceResult<ListaDetalleResponse>> GetDetalleAsync(
        int listaId, CancellationToken ct = default)
    {
        try
        {
            var (header, itemRows) = await _repo.GetDetalleAsync(listaId, ct);

            if (header is null)
                return ServiceResult<ListaDetalleResponse>.Fail(
                    404, "Lista no encontrada.", ErrorCodes.Kiteo404);

            var h = (IDictionary<string, object?>)header;

            var lista = new ListaHeaderItem
            {
                Id         = h.GetInt("id")          ?? listaId,
                Wkname     = h.GetStr("wkname")      ?? string.Empty,
                Cliente    = h.GetStr("cliente")     ?? string.Empty,
                Tipo       = h.GetStr("tipo")        ?? string.Empty,
                GruposJson = h.GetStr("grupos_json"),
                Det        = h.GetStr("det"),
                FiltroLoc  = h.GetStr("filtro_loc"),
                CreadoPor  = h.GetStr("creado_por"),
                CreatedAt  = h.GetStr("created_at")
            };

            var items = itemRows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new ListaDetalleItem
                {
                    Id              = d.GetInt("id")               ?? 0,
                    Item            = d.GetStr("item")             ?? string.Empty,
                    Locacion        = d.GetStr("locacion"),
                    Etiqueta = d.GetStr("etiqueta"),
                    CreadoPor = d.GetStr("creado_por"),
                    CreatedAt = d.GetStr("created_at"),  // o FormatDateTime si existe el helper
                    NotaArea        = d.GetStr("nota_area"),
                    PendienteActual = d.GetInt("pendiente_actual") ?? 0,
                    TrabajadoActual = d.GetInt("trabajado_actual") ?? 0
                })
                .ToList();

            return ServiceResult<ListaDetalleResponse>.Ok(
                new ListaDetalleResponse(true, lista, items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetDetalle id={Id}", listaId);
            return ServiceResult<ListaDetalleResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /listas/:id/items ────────────────────────────────────────────────

    public async Task<ServiceResult<ListaAgregarResponse>> AgregarItemsAsync(
        int listaId, ListaAgregarRequest request, CancellationToken ct = default)
    {
        try
        {
            // FIX 1: mismo patrón — array raíz, camelCase, sin wrapper
            var jsonItems = JsonSerializer.Serialize(request.Items, _camelCase);

            var row = await _repo.AgregarItemsAsync(listaId, jsonItems, request.Etiqueta, request.CreadoPor, ct);

            if (row is null)
                return ServiceResult<ListaAgregarResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var d = (IDictionary<string, object?>)row;
            var httpStatus = d.GetInt("http_status") ?? 500;
            var message = d.GetStr("message") ?? string.Empty;

            if (httpStatus != 200)
                return ServiceResult<ListaAgregarResponse>.Fail(
                    httpStatus, message, d.GetStr("code") ?? string.Empty);

            return ServiceResult<ListaAgregarResponse>.Ok(
                new ListaAgregarResponse(true, d.GetInt("insertados") ?? 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AgregarItems lista={Id}", listaId);
            return ServiceResult<ListaAgregarResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── PATCH /listas/:id/items/:item_id/nota ────────────────────────────────

    public async Task<ServiceResult<ListaOkResponse>> ActualizarNotaAsync(
        int listaId, int itemId, string? notaArea,
        CancellationToken ct = default)
    {
        try
        {
            var row = await _repo.ActualizarNotaAsync(listaId, itemId, notaArea, ct);
            return MapSimpleResult(row, "ActualizarNota", listaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en ActualizarNota lista={L} item={I}", listaId, itemId);
            return ServiceResult<ListaOkResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── DELETE /listas/:id/items/:item_id ─────────────────────────────────────

    public async Task<ServiceResult<ListaOkResponse>> QuitarItemAsync(
        int listaId, int itemId, CancellationToken ct = default)
    {
        try
        {
            var row = await _repo.QuitarItemAsync(listaId, itemId, ct);
            return MapSimpleResult(row, "QuitarItem", listaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en QuitarItem lista={L} item={I}", listaId, itemId);
            return ServiceResult<ListaOkResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── DELETE /listas/:id ────────────────────────────────────────────────────

    public async Task<ServiceResult<ListaOkResponse>> EliminarAsync(
        int listaId, string username, CancellationToken ct = default)
    {
        try
        {
            // Verificar LPaccess antes de ejecutar
            var tieneAcceso = await _repo.HasLpAccessAsync(username, ct);
            if (!tieneAcceso)
                return ServiceResult<ListaOkResponse>.Fail(
                    403, "Requiere LPaccess para eliminar listas.", ErrorCodes.Kiteo403);

            var row = await _repo.EliminarAsync(listaId, username, ct);   // ← FIX: faltaba username

            _logger.LogInformation(
                "Lista eliminada | id={Id} por={U}", listaId, username);

            return MapSimpleResult(row, "Eliminar", listaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Eliminar lista={Id}", listaId);
            return ServiceResult<ListaOkResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static ServiceResult<ListaOkResponse> MapSimpleResult(
        dynamic? row, string operacion, int listaId)
    {
        if (row is null)
            return ServiceResult<ListaOkResponse>.Fail(
                404, $"Lista {listaId} no encontrada.", ErrorCodes.Kiteo404);

        var d          = (IDictionary<string, object?>)row;
        var httpStatus = d.GetInt("http_status") ?? 500;
        var message    = d.GetStr("message") ?? string.Empty;

        return httpStatus == 200
            ? ServiceResult<ListaOkResponse>.Ok(new ListaOkResponse(true))
            : ServiceResult<ListaOkResponse>.Fail(
                httpStatus, message, d.GetStr("code") ?? string.Empty);
    }
    private static readonly JsonSerializerOptions _camelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
