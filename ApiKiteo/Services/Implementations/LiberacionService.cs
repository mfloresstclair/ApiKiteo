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

    // ── POST /api/liberacion/crear ────────────────────────────────────────────

    public async Task<ServiceResult<LiberacionCrearResponse>> CrearLoteAsync(
        LiberacionCrearRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Liberación crear | usuario={U} semanas={N} sobreescribir={S}",
                request.Username, request.Wknames.Count, request.Sobreescribir);

            var json = JsonSerializer.Serialize(new { wkname = request.Wknames });
            var rows = await _repo.CrearLoteAsync(json, request.Username, request.Sobreescribir, ct);

            var primera = rows.Select(r => (IDictionary<string, object?>)r).FirstOrDefault();
            if (primera is null)
                return ServiceResult<LiberacionCrearResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var httpStatus = Convert.ToInt32(primera.GetValueOrDefault("http_status") ?? 500);
            var code = primera.GetStr("code") ?? string.Empty;
            var mensaje = primera.GetStr("message") ?? string.Empty;
            var loteId = Convert.ToInt32(primera.GetValueOrDefault("lote_id") ?? 0);

            if (httpStatus != 200)
            {
                _logger.LogWarning(
                    "Liberación crear {Code} | usuario={U}", code, request.Username);
                return ServiceResult<LiberacionCrearResponse>.Fail(httpStatus, mensaje, code);
            }

            _logger.LogInformation(
                "Liberación crear OK | lote_id={L} usuario={U}", loteId, request.Username);

            return ServiceResult<LiberacionCrearResponse>.Ok(
                new LiberacionCrearResponse(true, code, mensaje, loteId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Liberación crear usuario={U}", request.Username);
            return ServiceResult<LiberacionCrearResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /api/liberacion ──────────────────────────────────────────────────

    public async Task<ServiceResult<LiberacionMaterialResponse>> GetMaterialAsync(
        LiberacionRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Liberación material | usuario={U} semanas={N} cliente={C}",
                request.Username, request.Wknames.Count, request.Cliente);

            var json = JsonSerializer.Serialize(new { wkname = request.Wknames });
            var (resumenRows, detalleRows) = await _repo.GetMaterialAsync(
                json, request.Username, request.Cliente, ct);

            var resumen = resumenRows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new LiberacionResumenItem
                {
                    Item = d.GetStr("item") ?? string.Empty,
                    Cant = GetDecimal(d.GetValueOrDefault("Cant")),
                    Cliente = d.GetStr("cliente") ?? string.Empty
                })
                .ToList();

            var detalle = detalleRows
                .Select(r => (IDictionary<string, object?>)r)
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

            return ServiceResult<LiberacionMaterialResponse>.Ok(
                new LiberacionMaterialResponse(
                    Ok: true,
                    TotalResumen: resumen.Count,
                    TotalDetalle: detalle.Count,
                    Resumen: resumen,
                    Detalle: detalle));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Liberación material usuario={U}", request.Username);
            return ServiceResult<LiberacionMaterialResponse>.Fail(
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

            var loteLista = loteRows.Select(r => (IDictionary<string, object?>)r).ToList();
            var primeraFila = loteLista.FirstOrDefault();

            if (primeraFila is not null && primeraFila.ContainsKey("http_status"))
                return ServiceResult<LiberacionGetResponse>.Fail(
                    404, "Liberación no encontrada.", "LIB_404");

            LoteResumenItem? lote = null;
            if (primeraFila is not null)
            {
                lote = new LoteResumenItem
                {
                    LoteId = Convert.ToInt32(primeraFila.GetValueOrDefault("lote_id") ?? 0),
                    LiberadoPor = primeraFila.GetStr("liberado_por") ?? string.Empty,
                    LiberadoEn = primeraFila.GetValueOrDefault("liberado_en")?.ToString(),
                    Cliente = primeraFila.GetStr("cliente") ?? string.Empty,
                    TotalSemanas = Convert.ToInt32(primeraFila.GetValueOrDefault("total_semanas") ?? 0),
                    Pendientes = Convert.ToInt32(primeraFila.GetValueOrDefault("pendientes") ?? 0),
                    Estatus = primeraFila.GetStr("estatus") ?? string.Empty
                };
            }

            var semanas = semanaRows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new WkLoteItem
                {
                    Wkname = d.GetStr("wkname") ?? string.Empty,
                    Estatus = d.GetStr("estatus") ?? string.Empty,
                    Fechacorte = d.GetStr("fechacorte"),
                    Cliente = d.GetStr("cliente") ?? string.Empty,
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

            var primera = rows.Select(r => (IDictionary<string, object?>)r).FirstOrDefault();
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


    // ── GET /api/liberacion/list ──────────────────────────────────────────────

    public async Task<ServiceResult<LiberacionListResponse>> LiberacionListAsync(
        string cliente, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.LiberacionListAsync(cliente, ct);
            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new LoteResumenItem
                {
                    LoteId = Convert.ToInt32(d.GetValueOrDefault("lote_id") ?? 0),
                    LiberadoPor = d.GetStr("liberado_por") ?? string.Empty,
                    LiberadoEn = d.GetValueOrDefault("liberado_en")?.ToString(),
                    Cliente = d.GetStr("cliente") ?? string.Empty,
                    TotalSemanas = Convert.ToInt32(d.GetValueOrDefault("total_semanas") ?? 0),
                    Pendientes = Convert.ToInt32(d.GetValueOrDefault("pendientes") ?? 0),
                    Estatus = d.GetStr("estatus") ?? string.Empty
                })
                .ToList();

            return ServiceResult<LiberacionListResponse>.Ok(
                new LiberacionListResponse(true, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error LiberacionList cliente={C}", cliente);
            return ServiceResult<LiberacionListResponse>.Fail(
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