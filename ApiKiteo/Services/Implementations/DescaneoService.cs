using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class DescaneoService : IDescaneoService
{
    private readonly IDescaneoRepository     _repo;
    private readonly IWksRepository          _wksRepo;
    private readonly ILogger<DescaneoService> _logger;

    public DescaneoService(
        IDescaneoRepository repo,
        IWksRepository wksRepo,
        ILogger<DescaneoService> logger)
    {
        _repo    = repo;
        _wksRepo = wksRepo;
        _logger  = logger;
    }

    // ── GET /descaneo/buscar ──────────────────────────────────────────────────

    public async Task<ServiceResult<DescanBuscarResponse>> BuscarAsync(
        DescanBuscarRequest request, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.BuscarAsync(
                request.Wkname,
                request.Vin,
                request.Item,
                request.Operador,
                request.Cliente,
                request.FechaDesde,
                request.FechaHasta,
                request.Modo,
                ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new DescanBuscarItem
                {
                    Id                  = d.GetInt("id") ?? 0,
                    Wkname              = d.GetStr("WkName")              ?? string.Empty,
                    Cliente             = d.GetStr("Cliente")             ?? string.Empty,
                    Tipo                = d.GetStr("tipo")                ?? string.Empty,
                    Vin                 = d.GetStr("Vin")                 ?? string.Empty,
                    Modelo              = d.GetStr("MODELO"),
                    CoNum               = d.GetStr("co_num"),
                    Secuencia           = d.GetStr("secuencia"),
                    Grupo               = d.GetStr("Grupo"),
                    Vindesc             = d.GetStr("vindesc"),
                    Locacion            = d.GetInt("Locacion"),
                    LocacionDesc        = d.GetStr("Locacion_Descripcion"),
                    Item                = d.GetStr("item")                ?? string.Empty,
                    ItemDescripcion     = d.GetStr("item_Descripcion"),
                    Operador            = d.GetStr("Operador"),
                    EscaneadoEn         = d.GetStr("escaneado_en"),
                    Entregado           = d.GetStr("Entregado"),
                    EntregadoPor        = d.GetStr("Entregado_por"),
                    Bloqueado           = (d.GetInt("bloqueado") ?? 0) == 1
                })
                .ToList();

            return ServiceResult<DescanBuscarResponse>.Ok(
                new DescanBuscarResponse(true, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en DescanBuscar");
            return ServiceResult<DescanBuscarResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /descaneo/aplicar ────────────────────────────────────────────────

    public async Task<ServiceResult<DescaneoAplicarResponse>> AplicarAsync(
        DescaneoAplicarRequest request, CancellationToken ct = default)
    {
        try
        {
            var row = await _repo.AplicarAsync(request.Id, request.Username, request.Motivo, ct);

            if (row is null)
                return ServiceResult<DescaneoAplicarResponse>.Fail(
                    500, "El SP no devolvió respuesta.", ErrorCodes.Kiteo500);

            var d          = (IDictionary<string, object?>)row;
            var httpStatus = d.GetInt("http_status") ?? 500;
            var code       = d.GetStr("code")    ?? string.Empty;
            var message    = d.GetStr("message") ?? string.Empty;

            if (httpStatus != 200)
                return ServiceResult<DescaneoAplicarResponse>.Fail(httpStatus, message, code);

            var wkname = d.GetStr("wkname") ?? string.Empty;

            _logger.LogInformation(
                "Descaneo aplicado | id={I} wkname={W} operador_removido={O} by={U}",
                request.Id, wkname,
                d.GetStr("operador_removido"), request.Username);

            // Cache fire-and-forget — el descaneo cambia el % de la semana
            if (!string.IsNullOrWhiteSpace(wkname))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _wksRepo.RefreshStatusCacheAsync(wkname);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "RefreshStatusCache falló (descaneo) | wkname={W}", wkname);
                    }
                });
            }

            return ServiceResult<DescaneoAplicarResponse>.Ok(
                new DescaneoAplicarResponse(
                    Ok:               true,
                    Message:          message,
                    Id:               d.GetInt("id")         ?? request.Id,
                    Wkname:           wkname,
                    Vin:              d.GetStr("vin")         ?? string.Empty,
                    Item:             d.GetStr("item")        ?? string.Empty,
                    Locacion:         d.GetInt("locacion"),
                    OperadorRemovido: d.GetStr("operador_removido"),
                    EscaneadoEn:      d.GetStr("escaneado_en_original")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en DescaneoAplicar id={I}", request.Id);
            return ServiceResult<DescaneoAplicarResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }
}
