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
    Hoja = d.GetStr("hoja") ?? string.Empty,     // ← NUEVO
    Tipo = d.GetStr("tipo") ?? string.Empty,     // ← NUEVO
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
                    Vin = d.GetStr("vin") ?? string.Empty,
                    Hoja = d.GetStr("hoja") ?? string.Empty
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
                "Corte ingresar | lote={L} wkname={W} semana={S} anio={A} usuario={U}",
                request.LoteId, request.Wkname,
                request.Semana, request.Anio, request.Username);

            // El SP Kit_vin_liberacion_ingresar_corte recibe @semana + @anio
            // y consulta SytelineOut (Blank4='312026') internamente
            var rows = await _repo.IngresarCorteAsync(
                request.LoteId, request.Wkname,
                request.Semana, request.Anio,
                request.Username, ct);

            var primera = rows
                .Select(r => (IDictionary<string, object?>)r)
                .FirstOrDefault();

            if (primera is null)
                return ServiceResult<CorteIngresarResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var httpStatus = Convert.ToInt32(primera.GetValueOrDefault("http_status") ?? 500);
            var mensaje = primera.GetStr("message") ?? string.Empty;

            if (httpStatus != 200)
                return ServiceResult<CorteIngresarResponse>.Fail(httpStatus, mensaje, "CORTE_ERR");

            var pendientes = Convert.ToInt32(primera.GetValueOrDefault("semanas_pendientes") ?? -1);
            var fechaDerivada = primera.GetStr("fechacorte"); // el SP la devuelve si quieres

            return ServiceResult<CorteIngresarResponse>.Ok(
                new CorteIngresarResponse(
                    Ok: true,
                    Mensaje: mensaje,
                    SemanasPendientes: pendientes,
                    Fechacorte: fechaDerivada));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error IngresarCorte lote={L} wkname={W}", request.LoteId, request.Wkname);
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
    // ── GET /api/liberacion/fechacorte ────────────────────────────────────────

    public async Task<ServiceResult<FechaCorteDerivadaResponse>> GetFechaCorteAsync(
        int semana, int anio, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "GetFechaCorte | semana={S} anio={A}", semana, anio);

            var fecha = await _repo.GetFechaCorteAsync(semana, anio, ct);

            if (fecha is null)
            {
                _logger.LogInformation(
                    "GetFechaCorte | Sin corte para semana={S} anio={A}", semana, anio);

                return ServiceResult<FechaCorteDerivadaResponse>.Ok(
                    new FechaCorteDerivadaResponse(
                        Ok: false,
                        Semana: semana,
                        Anio: anio,
                        Fechacorte: null,
                        Mensaje: $"Sin corte en SytelineOut para semana {semana} / {anio}. " +
                                    "El corte aún no ha ocurrido."));
            }

            string fechaStr = fecha.Value.ToString("yyyy-MM-dd");

            _logger.LogInformation(
                "GetFechaCorte OK | semana={S} anio={A} fecha={F}",
                semana, anio, fechaStr);

            return ServiceResult<FechaCorteDerivadaResponse>.Ok(
                new FechaCorteDerivadaResponse(
                    Ok: true,
                    Semana: semana,
                    Anio: anio,
                    Fechacorte: fechaStr,
                    Mensaje: $"FechaCorte derivada de SytelineOut: {fechaStr}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error GetFechaCorte semana={S} anio={A}", semana, anio);

            return ServiceResult<FechaCorteDerivadaResponse>.Fail(
                500, "Error al consultar SytelineOut.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /api/liberacion/{loteId}/snapshot ────────────────────────────────

    public async Task<ServiceResult<LiberacionSnapshotGuardarResponse>> GuardarSnapshotAsync(
        int loteId, LiberacionSnapshotRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Snapshot liberación | lote={L} usuario={U} items={N}",
                loteId, request.Username, request.Items.Count);

            // El ORDEN del array ES el orden de las filas del Excel — el SP lo
            // conserva con el [key] de OPENJSON. No reordenar aquí.
            var json = JsonSerializer.Serialize(new
            {
                items = request.Items.Select(i => new
                {
                    hoja    = i.Hoja,
                    tipo    = i.Tipo ?? string.Empty,
                    item    = i.Item,
                    cant    = i.Cant,
                    cliente = i.Cliente
                })
            });

            var rows = await _repo.GuardarSnapshotAsync(
                loteId, request.Username, json,
                request.Destinatarios, request.WkEtiqueta,
                request.Cliente, request.Archivo, ct);

            var primera = rows.Select(r => (IDictionary<string, object?>)r).FirstOrDefault();
            if (primera is null)
                return ServiceResult<LiberacionSnapshotGuardarResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var httpStatus = Convert.ToInt32(primera.GetValueOrDefault("http_status") ?? 500);
            var code       = primera.GetStr("code") ?? string.Empty;
            var mensaje    = primera.GetStr("message") ?? string.Empty;
            var total      = Convert.ToInt32(primera.GetValueOrDefault("total_items") ?? 0);

            if (httpStatus != 200)
            {
                _logger.LogWarning(
                    "Snapshot {Code} | lote={L} usuario={U}", code, loteId, request.Username);
                return ServiceResult<LiberacionSnapshotGuardarResponse>.Fail(
                    httpStatus, mensaje, code);
            }

            _logger.LogInformation(
                "Snapshot OK | lote={L} items={N} archivo={A}",
                loteId, total, request.Archivo);

            return ServiceResult<LiberacionSnapshotGuardarResponse>.Ok(
                new LiberacionSnapshotGuardarResponse(true, code, mensaje, total));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando snapshot lote={L}", loteId);
            return ServiceResult<LiberacionSnapshotGuardarResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // ── GET /api/liberacion/{loteId}/snapshot ─────────────────────────────────

    public async Task<ServiceResult<LiberacionSnapshotGetResponse>> GetSnapshotAsync(
        int loteId, CancellationToken ct = default)
    {
        try
        {
            var (loteRows, resumenRows, semanaRows) = await _repo.GetSnapshotAsync(loteId, ct);

            var primera = loteRows
                .Select(r => (IDictionary<string, object?>)r)
                .FirstOrDefault();

            if (primera is null || primera.ContainsKey("http_status"))
                return ServiceResult<LiberacionSnapshotGetResponse>.Fail(
                    404, "Lote no encontrado.", "LIB_404");

            var cabecera = new LiberacionSnapshotCabecera
            {
                LoteId        = Convert.ToInt32(primera.GetValueOrDefault("lote_id") ?? 0),
                LiberadoPor   = primera.GetStr("liberado_por") ?? string.Empty,
                LiberadoEn    = primera.GetValueOrDefault("liberado_en")?.ToString(),
                EnviadoEn     = primera.GetValueOrDefault("enviado_en")?.ToString(),
                EnviadoPor    = primera.GetStr("enviado_por"),
                Destinatarios = primera.GetStr("destinatarios"),
                WkEtiqueta    = primera.GetStr("wk_etiqueta"),
                Cliente       = primera.GetStr("cliente"),
                Archivo       = primera.GetStr("archivo"),
                TotalItems    = Convert.ToInt32(primera.GetValueOrDefault("total_items") ?? 0),
                Estatus       = primera.GetStr("estatus") ?? string.Empty,
                TotalSemanas  = Convert.ToInt32(primera.GetValueOrDefault("total_semanas") ?? 0)
            };

            var resumen = resumenRows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new LiberacionSnapshotItem
                {
                    Hoja    = d.GetStr("hoja") ?? string.Empty,
                    Tipo    = d.GetStr("tipo") ?? string.Empty,
                    Item    = d.GetStr("item") ?? string.Empty,
                    Cant    = Convert.ToInt32(d.GetValueOrDefault("cant") ?? 0),
                    Cliente = d.GetStr("cliente") ?? string.Empty
                })
                .ToList();

            var semanas = semanaRows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new LiberacionSnapshotSemana
                {
                    Wkname     = d.GetStr("wkname") ?? string.Empty,
                    Estatus    = d.GetStr("estatus") ?? string.Empty,
                    Fechacorte = d.GetStr("fechacorte")
                })
                .ToList();

            return ServiceResult<LiberacionSnapshotGetResponse>.Ok(
                new LiberacionSnapshotGetResponse(true, cabecera, resumen, semanas));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo snapshot lote={L}", loteId);
            return ServiceResult<LiberacionSnapshotGetResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // ── GET /api/liberacion/historial ─────────────────────────────────────────

    public async Task<ServiceResult<LiberacionHistorialResponse>> HistorialAsync(
        string cliente, int top, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.HistorialAsync(cliente, top, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new LiberacionHistorialItem
                {
                    LoteId       = Convert.ToInt32(d.GetValueOrDefault("lote_id") ?? 0),
                    WkEtiqueta   = d.GetStr("wk_etiqueta"),
                    Cliente      = d.GetStr("cliente"),
                    EnviadoEn    = d.GetValueOrDefault("enviado_en")?.ToString(),
                    EnviadoPor   = d.GetStr("enviado_por"),
                    TotalItems   = Convert.ToInt32(d.GetValueOrDefault("total_items") ?? 0),
                    Archivo      = d.GetStr("archivo"),
                    Estatus      = d.GetStr("estatus") ?? string.Empty,
                    TotalSemanas = Convert.ToInt32(d.GetValueOrDefault("total_semanas") ?? 0)
                })
                .ToList();

            return ServiceResult<LiberacionHistorialResponse>.Ok(
                new LiberacionHistorialResponse(true, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en historial de liberaciones cliente={C}", cliente);
            return ServiceResult<LiberacionHistorialResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }
}