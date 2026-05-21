using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class AdminService : IAdminService
{
    private readonly IAdminRepository _repo;
    private readonly IWksRepository _wksRepo;   // para RefreshStatusCache
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IAdminRepository repo,
        IWksRepository wksRepo,
        ILogger<AdminService> logger)
    {
        _repo = repo;
        _wksRepo = wksRepo;
        _logger = logger;
    }

    public async Task<ServiceResult<AprobarSemanaResponse>> AprobarSemanaAsync(
        AprobarSemanaRequest request, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.AprobarSemanaAsync(request.Wkname, request.AprobadoPor, ct);
            var list = rows.ToList();

            if (list.Count > 0)
            {
                var d = (IDictionary<string, object?>)list[0];

                var rawStatus = d.GetValueOrDefault("http_status")
                             ?? d.GetValueOrDefault("httpStatus");

                if (rawStatus is not null && int.TryParse(rawStatus.ToString(), out var httpStatus))
                {
                    if (httpStatus != 200)
                    {
                        var msg = d.GetStr("message") ?? "Error al aprobar la semana.";
                        var code = d.GetStr("code") ?? ErrorCodes.Admin500;
                        return ServiceResult<AprobarSemanaResponse>.Fail(httpStatus, msg, code);
                    }
                }
            }

            return ServiceResult<AprobarSemanaResponse>.Ok(
                new AprobarSemanaResponse(true, "Semana aprobada"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AprobarSemana {Wk}", request.Wkname);
            return ServiceResult<AprobarSemanaResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Admin500);
        }
    }

    public async Task<ServiceResult<WkPreviewResponse>> PreviewSemanaAsync(
        string wkname, CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("PreviewSemana | wkname={Wk}", wkname);

            var (resumenRows, detalleRows) = await _repo.PreviewSemanaAsync(wkname, ct);

            var resumenList = resumenRows
                .Select(r => (IDictionary<string, object?>)r)
                .ToList();

            if (resumenList.Count == 0)
                return ServiceResult<WkPreviewResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var primera = resumenList[0];
            if (primera.ContainsKey("http_status"))
            {
                if (!int.TryParse(primera["http_status"]?.ToString(), out var httpStatus))
                    httpStatus = 500;

                var mensaje = primera.GetValueOrDefault("message")?.ToString()
                              ?? "Error al procesar la solicitud.";
                var codigo = primera.GetValueOrDefault("code")?.ToString()
                              ?? ErrorCodes.Kiteo500;

                return ServiceResult<WkPreviewResponse>.Fail(httpStatus, mensaje, codigo);
            }

            var resumen = new WkPreviewResumen
            {
                Wkname = primera.GetStr("wkname") ?? wkname,
                Tipo = primera.GetStr("tipo"),
                FechaSemana = FormatDate(primera.GetValueOrDefault("fecha_semana")),
                TotalVins = primera.GetInt("total_vins") ?? 0,
                TotalGrupos = primera.GetInt("total_grupos") ?? 0,
                DueDateMin = FormatDate(primera.GetValueOrDefault("due_date_min")),
                DueDateMax = FormatDate(primera.GetValueOrDefault("due_date_max")),
                EstatusHeader = primera.GetStr("estatus_header"),
                YaCargada = (primera.GetInt("ya_cargada") ?? 0) == 1
            };

            var detalle = detalleRows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new WkPreviewGrupo
                {
                    Grupo = d.GetStr("GRUPO"),
                    TotalVins = d.GetInt("total_vins") ?? 0,
                    Descripcion = d.GetStr("descripcion"),
                    Modelo = d.GetStr("modelo"),
                    Motherharness = d.GetStr("motherharness"),
                    DueDateMin = FormatDate(d.GetValueOrDefault("due_date_min")),
                    DueDateMax = FormatDate(d.GetValueOrDefault("due_date_max")),
                    HorasPromedio = d.GetDecimal("horas_promedio") ?? 0m
                })
                .ToList();

            _logger.LogInformation(
                "PreviewSemana | wkname={Wk} grupos={G} vins={V}",
                wkname, detalle.Count, resumen.TotalVins);

            return ServiceResult<WkPreviewResponse>.Ok(
                new WkPreviewResponse(true, wkname, resumen, detalle));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en PreviewSemana wkname={Wk}", wkname);
            return ServiceResult<WkPreviewResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<CrearDbResponse>> CrearDbAsync(
        CrearDbRequest request, CancellationToken ct = default)
    {
        var wkname = request.Wkname.Trim();
        var wknamerename = string.IsNullOrWhiteSpace(request.Wknamerename)
            ? null : request.Wknamerename.Trim();

        try
        {
            _logger.LogInformation(
                "CrearDb inicio | wkname={Wk} rename={Rename}", wkname, wknamerename);

            var wknameAVerificar = wknamerename ?? wkname;

            var yaExiste = await _repo.WkNameExistsInMacroAsync(wknameAVerificar, ct);
            if (yaExiste)
            {
                _logger.LogWarning(
                    "CrearDb rechazado — ya existe | wkname={Wk}", wknameAVerificar);
                return ServiceResult<CrearDbResponse>.Fail(
                    409,
                    $"La semana '{wknameAVerificar}' ya está cargada en VinBusiness_DB_macro.",
                    ErrorCodes.Admin409);
            }

            var (metadataRows, registrosRows) = await _repo.CrearDbAsync(
                wkname, wknamerename, request.Usuario, ct);

            var metadataList = metadataRows.Select(r => (IDictionary<string, object?>)r).ToList();
            var registrosList = registrosRows.Select(r => (IDictionary<string, object?>)r).ToList();

            if (metadataList.Count == 0)
                return ServiceResult<CrearDbResponse>.Fail(
                    500, "El SP no devolvió respuesta de metadata.", ErrorCodes.Kiteo500);

            var meta = metadataList[0];
            var totalLineas = registrosList.Count;
            var totalVins = registrosList
                .Select(d => d.GetStr("vin") ?? d.GetStr("VIN"))
                .Where(v => v is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var wknameEfectivo = wknamerename ?? wkname;

            _logger.LogInformation(
                "CrearDb completado | wkname={Wk} → {Ef} | vins={V} lineas={L}",
                wkname, wknameEfectivo, totalVins, totalLineas);

            // Cache fire-and-forget — inicializa el status board para esta semana nueva
            _ = Task.Run(() =>
                _wksRepo.RefreshStatusCacheAsync(wknameEfectivo, CancellationToken.None));

            return ServiceResult<CrearDbResponse>.Ok(
                new CrearDbResponse(
                    Ok: true,
                    Wkname: wkname,
                    WknameEfectivo: wknameEfectivo,
                    Wknamedata: meta.GetStr("wknamedata"),
                    Descripcion: meta.GetStr("descripcion"),
                    Cliente: meta.GetStr("cliente"),
                    Tipo: meta.GetStr("tipo"),
                    TotalVins: totalVins,
                    TotalLineas: totalLineas));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CrearDb wkname={Wk}", wkname);
            return ServiceResult<CrearDbResponse>.Fail(
                500, "Error interno al crear la semana. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<WkPreviewVinsResponse>> GetPreviewVinsAsync(
        string wkname, CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("GetPreviewVins | wkname={Wk}", wkname);

            var rows = await _repo.GetPreviewVinsAsync(wkname, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new WkPreviewVinItem
                {
                    Vin = d.GetStr("VIN"),
                    Semana = d.GetStr("semana"),
                    Grupo = d.GetStr("grupo"),
                    Descripcion = d.GetStr("descripcion"),
                    Modelo = d.GetStr("modelo"),
                    Motherharness = d.GetStr("motherharness"),
                    Tipo = d.GetStr("tipo"),
                    DueDate = FormatDate(d.GetValueOrDefault("due_date")),
                    Horas = GetDecimal(d.GetValueOrDefault("horas"))
                })
                .ToList();

            _logger.LogInformation(
                "GetPreviewVins | wkname={Wk} total={T}", wkname, resultados.Count);

            return ServiceResult<WkPreviewVinsResponse>.Ok(
                new WkPreviewVinsResponse(true, wkname, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetPreviewVins wkname={Wk}", wkname);
            return ServiceResult<WkPreviewVinsResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private static decimal GetDecimal(object? val)
    {
        if (val is null || val is DBNull) return 0m;
        return val switch
        {
            decimal d => Math.Round(d, 2),
            double v => Math.Round((decimal)v, 2),
            float f => Math.Round((decimal)f, 2),
            _ => decimal.TryParse(val.ToString(), out var p) ? Math.Round(p, 2) : 0m
        };
    }
}