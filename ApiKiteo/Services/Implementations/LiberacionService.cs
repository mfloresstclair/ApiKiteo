using System.Text.Json;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class LiberacionService : ILiberacionService
{
    private readonly ILiberacionRepository _repo;
    private readonly ILogger<LiberacionService> _logger;

    public LiberacionService(
        ILiberacionRepository repo,
        ILogger<LiberacionService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // ── GET /api/liberacion/semanas ───────────────────────────────────────────

    public async Task<ServiceResult<LiberacionSemanasResponse>> GetSemanasAsync(
        string estatus, string cliente, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetSemanasAsync(estatus, cliente, ct);
            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new LiberacionSemanaItem
                {
                    Wkname = d.GetStr("wkname") ?? string.Empty,
                    Estatus = d.GetStr("estatus") ?? string.Empty,
                    Cliente = d.GetStr("cliente") ?? string.Empty,
                    CreadoEn = d.GetValueOrDefault("creado_en")?.ToString()
                })
                .ToList();

            return ServiceResult<LiberacionSemanasResponse>.Ok(
                new LiberacionSemanasResponse(true, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error GetSemanas liberación cliente={C}", cliente);
            return ServiceResult<LiberacionSemanasResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /api/liberacion/resumen ──────────────────────────────────────────

    public async Task<ServiceResult<LiberacionResumenResponse>> GetResumenAsync(
        LiberacionRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Liberación resumen | usuario={U} semanas={N} cliente={C}",
                request.Username, request.Wknames.Count, request.Cliente);

            var json = JsonSerializer.Serialize(new { wkname = request.Wknames });
            var rows = await _repo.GetResumenAsync(json, request.Username, request.Cliente, ct);

            var lista = rows.Select(r => (IDictionary<string, object?>)r).ToList();

            // El SP puede devolver una fila de error {http_status=400} si hay duplicado
            var primera = lista.FirstOrDefault();
            if (primera is not null && primera.ContainsKey("http_status"))
            {
                var status = Convert.ToInt32(primera["http_status"] ?? 500);
                var mensaje = primera.GetValueOrDefault("message")?.ToString()
                              ?? "Error en el SP.";
                return ServiceResult<LiberacionResumenResponse>.Fail(status, mensaje, "LIB_ERR");
            }

            // Extraer lote_id del primer row (el SP lo repite en cada fila)
            var loteId = Convert.ToInt32(primera?.GetValueOrDefault("lote_id") ?? 0);

            var resultados = lista
                .Select(d => new LiberacionResumenItem
                {
                    Item = d.GetStr("item") ?? string.Empty,
                    Cant = GetDecimal(d.GetValueOrDefault("Cant")),
                    Cliente = d.GetStr("cliente") ?? string.Empty
                })
                .ToList();

            _logger.LogInformation(
                "Liberación resumen OK | lote_id={L} items={N}",
                loteId, resultados.Count);

            return ServiceResult<LiberacionResumenResponse>.Ok(
                new LiberacionResumenResponse(true, loteId, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Liberación resumen usuario={U}", request.Username);
            return ServiceResult<LiberacionResumenResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /api/liberacion/detalle ──────────────────────────────────────────

    public async Task<ServiceResult<LiberacionDetalleResponse>> GetDetalleAsync(
        LiberacionRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Liberación detalle | usuario={U} semanas={N} cliente={C}",
                request.Username, request.Wknames.Count, request.Cliente);

            var json = JsonSerializer.Serialize(new { wkname = request.Wknames });
            var rows = await _repo.GetDetalleAsync(json, request.Username, request.Cliente, ct);

            var lista = rows.Select(r => (IDictionary<string, object?>)r).ToList();

            // Verificar error del SP
            var primera = lista.FirstOrDefault();
            if (primera is not null && primera.ContainsKey("http_status"))
            {
                var status = Convert.ToInt32(primera["http_status"] ?? 500);
                var mensaje = primera.GetValueOrDefault("message")?.ToString() ?? "Error en el SP.";
                return ServiceResult<LiberacionDetalleResponse>.Fail(status, mensaje, "LIB_ERR");
            }

            var loteId = Convert.ToInt32(primera?.GetValueOrDefault("lote_id") ?? 0);

            // Devuelve todo — el WinForms pagina localmente con VirtualMode
            var resultados = lista
                .Select(d => new LiberacionDetalleItem
                {
                    Wkname = d.GetStr("wkname") ?? string.Empty,
                    Tipo = d.GetStr("tipo") ?? string.Empty,
                    Item = d.GetStr("item") ?? string.Empty,
                    QtyOrdered = GetDecimal(d.GetValueOrDefault("qty_ordered")),
                    Cliente = d.GetStr("cliente") ?? string.Empty,
                    Vin = d.GetStr("vin") ?? string.Empty
                })
                .ToList();

            return ServiceResult<LiberacionDetalleResponse>.Ok(
                new LiberacionDetalleResponse(true, loteId, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Liberación detalle usuario={U}", request.Username);
            return ServiceResult<LiberacionDetalleResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // ── GET /api/liberacion/{loteId} ──────────────────────────────────────────

    public async Task<ServiceResult<LiberacionGetResponse>> GetLoteAsync(
        int loteId, CancellationToken ct = default)
    {
        try
        {
            var (loteRows, semanaRows) = await _repo.GetLoteAsync(loteId, ct);

            // RS1: verificar 404
            var loteLista = loteRows.Select(r => (IDictionary<string, object?>)r).ToList();
            var primeraFila = loteLista.FirstOrDefault();

            if (primeraFila is not null && primeraFila.ContainsKey("http_status"))
            {
                return ServiceResult<LiberacionGetResponse>.Fail(
                    404, "Liberación no encontrada.", "LIB_404");
            }

            LoteResumenItem? lote = null;
            if (primeraFila is not null)
            {
                lote = new LoteResumenItem
                {
                    LoteId = Convert.ToInt32(primeraFila.GetValueOrDefault("lote_id") ?? 0),
                    LiberadoPor = primeraFila.GetStr("liberado_por") ?? string.Empty,
                    LiberadoEn = primeraFila.GetValueOrDefault("liberado_en")?.ToString(),
                    TotalSemanas = Convert.ToInt32(primeraFila.GetValueOrDefault("total_semanas") ?? 0),
                    Pendientes = Convert.ToInt32(primeraFila.GetValueOrDefault("pendientes") ?? 0),
                    Estatus = primeraFila.GetStr("estatus") ?? string.Empty
                };
            }

            // RS2: semanas del lote
            var semanas = semanaRows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new WkLoteItem
                {
                    Wkname = d.GetStr("wkname") ?? string.Empty,
                    Estatus = d.GetStr("estatus") ?? string.Empty,
                    Fechacorte = d.GetStr("fechacorte"),
                    Ingresado = Convert.ToInt32(d.GetValueOrDefault("ingresado") ?? 0) == 1
                })
                .ToList();

            return ServiceResult<LiberacionGetResponse>.Ok(
                new LiberacionGetResponse(true, lote, semanas));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error GetLote lote_id={L}", loteId);
            return ServiceResult<LiberacionGetResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /api/liberacion/corte/ingresar ───────────────────────────────────

    public async Task<ServiceResult<CorteIngresarResponse>> IngresarCorteAsync(
        CorteIngresarRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Corte ingresar | lote={L} wkname={W} fecha={F} usuario={U}",
                request.LoteId, request.Wkname, request.Fechacorte, request.Username);

            var rows = await _repo.IngresarCorteAsync(
                request.LoteId, request.Wkname,
                request.Fechacorte, request.Username, ct);

            var lista = rows.Select(r => (IDictionary<string, object?>)r).ToList();
            var primera = lista.FirstOrDefault();
            if (primera is null)
                return ServiceResult<CorteIngresarResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var httpStatus = Convert.ToInt32(primera.GetValueOrDefault("http_status") ?? 500);
            var mensaje = primera.GetStr("message") ?? string.Empty;

            if (httpStatus != 200)
                return ServiceResult<CorteIngresarResponse>.Fail(httpStatus, mensaje, "CORTE_ERR");

            var pendientes = Convert.ToInt32(primera.GetValueOrDefault("semanas_pendientes") ?? 0);

            _logger.LogInformation(
                "Corte ingresar OK | lote={L} wkname={W} pendientes={P}",
                request.LoteId, request.Wkname, pendientes);

            return ServiceResult<CorteIngresarResponse>.Ok(
                new CorteIngresarResponse(
                    Ok: true,
                    Mensaje: mensaje,
                    LoteId: request.LoteId,
                    Wkname: request.Wkname,
                    Fechacorte: request.Fechacorte,
                    SemanasPendientes: pendientes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error CorteIngresar lote={L}", request.LoteId);
            return ServiceResult<CorteIngresarResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static decimal GetDecimal(object? val)
    {
        if (val is null || val is DBNull) return 0m;
        return val switch
        {
            decimal d => d,
            double v => (decimal)v,
            float f => (decimal)f,
            _ => decimal.TryParse(val.ToString(), out var p) ? p : 0m
        };
    }
}