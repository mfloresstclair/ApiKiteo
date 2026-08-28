using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class ExpeditadosService : IExpeditadosService
{
    private readonly IExpeditadosRepository _repo;
    private readonly ILogger<ExpeditadosService> _logger;

    public ExpeditadosService(
        IExpeditadosRepository repo,
        ILogger<ExpeditadosService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // ── GET /api/expeditados  |  POST /api/expeditados/detectar ──────────────

    public async Task<ServiceResult<ExpeditadosListResponse>> DetectarAsync(
        bool soloReportar, CancellationToken ct = default)
    {
        try
        {
            var (_, pendientes) = await _repo.DetectarAsync(soloReportar, ct);

            var lista = pendientes
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new ExpeditadoItem
                {
                    Id = Convert.ToInt32(d.GetValueOrDefault("id") ?? 0),
                    Vin = d.GetStr("vin") ?? string.Empty,
                    // NO se normaliza a string.Empty: un SIN_WKNAME tiene wkname_origen
                    // NULL de verdad (no tiene semana), y el front lo usa para
                    // deshabilitar el boton Mover.
                    WknameOrigen = d.GetStr("wkname_origen"),
                    Tipo = d.GetStr("tipo"),
                    DueDate = d.GetStr("due_date"),
                    DetectadoEn = d.GetStr("detectado_en"),
                    Resolucion = d.GetStr("resolucion") ?? string.Empty,

                    // Detector v2 (8/2026)
                    MotivoDeteccion = d.GetStr("motivo_deteccion"),
                    DiasVencido = ToNullableInt(d.GetValueOrDefault("dias_vencido")),
                    DiasPendiente = ToNullableInt(d.GetValueOrDefault("dias_pendiente"))
                })
                .ToList();

            if (lista.Count > 0)
            {
                var sinSemana = lista.Count(x => x.MotivoDeteccion == "SIN_WKNAME");
                var vencidos = lista.Count(x => x.DiasVencido > 0);

                _logger.LogWarning(
                    "Expeditados PENDIENTES: {N} (sin semana: {S}, vencidos: {V})",
                    lista.Count, sinSemana, vencidos);

                // Un VIN sin semana no entra a NINGUNA macro y no aparece en ninguna
                // pantalla de piso: se loguea aparte para que no pase inadvertido.
                foreach (var x in lista.Where(x => x.MotivoDeteccion == "SIN_WKNAME"))
                    _logger.LogWarning(
                        "VIN sin semana asignada: {Vin} (tipo {Tipo}, due {Due})",
                        x.Vin, x.Tipo, x.DueDate);
            }

            return ServiceResult<ExpeditadosListResponse>.Ok(
                new ExpeditadosListResponse(true, lista.Count, lista));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detectando expeditados");
            return ServiceResult<ExpeditadosListResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /api/expeditados/mover ─────────────────────────────────────────

    public async Task<ServiceResult<ExpeditadosMoverResponse>> MoverAsync(
        ExpeditadosMoverRequest request, CancellationToken ct = default)
    {
        try
        {
            var ids = string.Join(',', request.Ids);
            _logger.LogInformation(
                "Expeditados mover | ids={Ids} usuario={U}", ids, request.Username);

            var (resultado, vinsRows) = await _repo.MoverAsync(ids, request.Username, ct);

            var primera = resultado.Select(r => (IDictionary<string, object?>)r).FirstOrDefault();
            if (primera is null)
                return ServiceResult<ExpeditadosMoverResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var httpStatus = Convert.ToInt32(primera.GetValueOrDefault("http_status") ?? 500);
            var code = primera.GetStr("code") ?? string.Empty;
            var mensaje = primera.GetStr("message") ?? string.Empty;

            // MIXTO / SNAPSHOT_CAMBIO / YA_EXISTE / NOT_FOUND — el front los muestra tal cual
            if (httpStatus != 200)
            {
                _logger.LogWarning("Expeditados mover {Code}: {Msg}", code, mensaje);
                return ServiceResult<ExpeditadosMoverResponse>.Fail(httpStatus, mensaje, code);
            }

            var destino = primera.GetStr("wkname_destino");
            var vins = vinsRows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => d.GetStr("vin") ?? string.Empty)
                .Where(v => v.Length > 0)
                .ToList();

            _logger.LogInformation(
                "Expeditados movidos a {Destino} | {N} vins", destino, vins.Count);

            return ServiceResult<ExpeditadosMoverResponse>.Ok(
                new ExpeditadosMoverResponse(true, mensaje, destino, vins));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moviendo expeditados usuario={U}", request.Username);
            return ServiceResult<ExpeditadosMoverResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /api/expeditados/ignorar ───────────────────────────────────────

    public async Task<ServiceResult<ExpeditadosIgnorarResponse>> IgnorarAsync(
        ExpeditadosIgnorarRequest request, CancellationToken ct = default)
    {
        try
        {
            var ids = string.Join(',', request.Ids);
            _logger.LogInformation(
                "Expeditados ignorar | ids={Ids} usuario={U} motivo={M}",
                ids, request.Username, request.Motivo ?? "(sin motivo)");

            var rows = await _repo.IgnorarAsync(ids, request.Username, request.Motivo, ct);

            var primera = rows.Select(r => (IDictionary<string, object?>)r).FirstOrDefault();
            if (primera is null)
                return ServiceResult<ExpeditadosIgnorarResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var httpStatus = Convert.ToInt32(primera.GetValueOrDefault("http_status") ?? 500);
            var mensaje = primera.GetStr("message") ?? string.Empty;

            if (httpStatus != 200)
                return ServiceResult<ExpeditadosIgnorarResponse>.Fail(
                    httpStatus, mensaje, primera.GetStr("code") ?? "ERROR");

            var afectados = Convert.ToInt32(primera.GetValueOrDefault("afectados") ?? 0);

            return ServiceResult<ExpeditadosIgnorarResponse>.Ok(
                new ExpeditadosIgnorarResponse(true, mensaje, afectados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ignorando expeditados usuario={U}", request.Username);
            return ServiceResult<ExpeditadosIgnorarResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }

    // Los conteos calculados del SP pueden venir NULL (no vencido / sin fecha).
    private static int? ToNullableInt(object? v)
        => v is null || v is DBNull ? null : Convert.ToInt32(v);

    // ── GET /api/comunizacion/validar ───────────────────────────────────────

    public async Task<ServiceResult<ComunizacionValidarResponse>> ValidarComunizacionAsync(
        int semana, int anio, DateOnly fechacorte, CancellationToken ct = default)
    {
        try
        {
            // Blank4: [semana sin cero a la izquierda][anio] → "322026", "82026"
            var blank4 = semana.ToString() + anio.ToString();

            _logger.LogInformation(
                "Validar comunización | blank4={B} fechacorte={F}", blank4, fechacorte);

            var rows = await _repo.ValidarComunizacionAsync(blank4, fechacorte, ct);

            var gaps = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new ComunizacionGapItem
                {
                    Harness = d.GetStr("harness") ?? string.Empty,
                    CircuitosCorte = Convert.ToInt32(d.GetValueOrDefault("circuitos_corte") ?? 0),
                    VinsAfectados = Convert.ToInt32(d.GetValueOrDefault("vins_afectados") ?? 0),
                    ComunizadoDespues = d.GetValueOrDefault("comunizado_despues") is DateTime dt
    ? dt.ToString("yyyy-MM-dd") : null
                })
                .ToList();

            var mensaje = gaps.Count == 0
                ? "Sin gaps de comunización. La semana se puede generar."
                : $"{gaps.Count} harness(es) que las órdenes piden y no están comunizados. " +
                  "La macro saldrá incompleta.";

            if (gaps.Count > 0)
                _logger.LogWarning(
                    "Comunización semana {S}/{A}: {N} gaps críticos", semana, anio, gaps.Count);

            return ServiceResult<ComunizacionValidarResponse>.Ok(
                new ComunizacionValidarResponse(true, gaps.Count, mensaje, gaps));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando comunización semana={S}/{A}", semana, anio);
            return ServiceResult<ComunizacionValidarResponse>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }
}