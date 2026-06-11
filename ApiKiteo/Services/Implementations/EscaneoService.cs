using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;
using System.Text.Json;

namespace ApiKiteo.API.Services.Implementations;

public sealed class EscaneoService : IEscaneoService
{
    private readonly IEscaneoRepository _repo;
    private readonly IWksRepository _wksRepo;
    private readonly ILogger<EscaneoService> _logger;

    public EscaneoService(
        IEscaneoRepository repo,
        IWksRepository wksRepo,
        ILogger<EscaneoService> logger)
    {
        _repo = repo;
        _wksRepo = wksRepo;
        _logger = logger;
    }

    // ── /vin_to_adjust ────────────────────────────────────────────────────────

    public async Task<ServiceResult<VinToAdjustResponse>> GetVinToAdjustAsync(
        VinToAdjustRequest request, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetVinToAdjustAsync(
                request.Wkname, request.Item, request.Empleado, ct);

            var vines = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Where(d => d.GetStr("vin") is not null)
                .Select(d => new VinItem
                {
                    Vin = d.GetStr("vin"),
                    Loc = d.GetValueOrDefault("Loc")
                          ?? d.GetValueOrDefault("locacion")
                          ?? d.GetValueOrDefault("Locacion"),
                    Grupo = d.GetStr("Grupo") ?? d.GetStr("grupo"),
                    Item = d.GetStr("item") ?? request.Item
                })
                .ToList();

            return ServiceResult<VinToAdjustResponse>.Ok(
                new VinToAdjustResponse(
                    true, request.Wkname, request.Item, vines.Count, vines));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en GetVinToAdjust {Wk}/{Item}", request.Wkname, request.Item);
            return ServiceResult<VinToAdjustResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── /escanear_ajuste ──────────────────────────────────────────────────────

    public async Task<ServiceResult<EscanearAjusteResponse>> EscanearAjusteAsync(
        EscanearAjusteRequest request, CancellationToken ct = default)
    {
        try
        {
            var jsonVines = JsonSerializer.Serialize(new { vines = request.Vines });

            var rows = await _repo.EscanearAjusteAsync(
                request.Wkname, request.Item, jsonVines, request.Empleado, ct);

            EscaneoEvento? evento = null;
            var vines = new List<VinItem>();

            foreach (var r in rows)
            {
                var d = (IDictionary<string, object?>)r;
                var tipo = d.GetStr("Tipo");

                if (tipo?.Equals("EvtData", StringComparison.OrdinalIgnoreCase) == true
                    && evento is null)
                {
                    evento = BuildEvento(d);
                    continue;
                }

                var vin = d.GetStr("vin") ?? d.GetStr("Vin");
                if (vin is not null)
                {
                    vines.Add(new VinItem
                    {
                        Vin = vin,
                        Loc = d.GetValueOrDefault("loc") ?? d.GetValueOrDefault("Locacion"),
                        Grupo = d.GetStr("grupo") ?? d.GetStr("Grupo"),
                        Item = d.GetStr("item") ?? request.Item
                    });
                }
            }

            // Actualizar cache después del ajuste
            _ = Task.Run(async () =>
            {
                try
                {
                    await _wksRepo.RefreshStatusCacheAsync(request.Wkname);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "RefreshStatusCache falló (ajuste) | wkname={W}", request.Wkname);
                }
            });

            return ServiceResult<EscanearAjusteResponse>.Ok(
                new EscanearAjusteResponse(
                    true, request.Wkname, request.Item, vines.Count, evento, vines));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en EscanearAjuste {Wk}/{Item}", request.Wkname, request.Item);
            return ServiceResult<EscanearAjusteResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── /escanear ─────────────────────────────────────────────────────────────

    public async Task<ServiceResult<EscanearResponse>> EscanearAsync(
        EscanearRequest request, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.EscanearAsync(
                request.Wkname, request.Item, request.Cantidad, request.Empleado, ct);

            EscaneoEvento? evento = null;
            var vines = new List<VinItem>();
            var gruposMap = new Dictionary<string, Dictionary<string, object?>>();
            decimal? weekPerc = null;

            foreach (var r in rows)
            {
                var d = (IDictionary<string, object?>)r;
                var tipo = d.GetStr("Tipo") ?? d.GetStr("tipo") ?? string.Empty;

                if (tipo.Equals("EvtData", StringComparison.OrdinalIgnoreCase) && evento is null)
                {
                    evento = BuildEvento(d);
                    continue;
                }

                if (tipo.Equals("VinData", StringComparison.OrdinalIgnoreCase))
                {
                    var vin = d.GetStr("vin") ?? d.GetStr("Vin");
                    if (vin is not null)
                        vines.Add(new VinItem
                        {
                            Vin = vin,
                            Loc = d.GetValueOrDefault("loc") ?? d.GetValueOrDefault("Loc"),
                            Grupo = d.GetStr("grupo") ?? d.GetStr("Grupo"),
                            Item = request.Item
                        });
                    continue;
                }

                if (tipo.Equals("WeekPerc", StringComparison.OrdinalIgnoreCase))
                {
                    weekPerc = d.GetDecimal("Porcentaje") ?? d.GetDecimal("porcentaje");
                    continue;
                }

                if (tipo.Equals("GrpData", StringComparison.OrdinalIgnoreCase))
                {
                    var grupo = d.GetStr("grupo") ?? d.GetStr("Grupo");
                    var porcentaje = d.GetValueOrDefault("Porcentaje")
                                  ?? d.GetValueOrDefault("porcentaje");
                    if (porcentaje is not null && grupo is not null
                        && !gruposMap.ContainsKey(grupo))
                    {
                        gruposMap[grupo] = new Dictionary<string, object?>
                        {
                            ["grupo"] = grupo,
                            ["Porcentaje"] = porcentaje.ToString()
                        };
                    }
                }
            }

            // FIX: _wksRepo (antes decía _wksRepository → NullReferenceException silenciosa)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _wksRepo.RefreshStatusCacheAsync(request.Wkname);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "RefreshStatusCache falló (escanear) | wkname={W}", request.Wkname);
                }
            });

            return ServiceResult<EscanearResponse>.Ok(
                new EscanearResponse(
                    true,
                    request.Wkname,
                    request.Item,
                    vines.Count,
                    evento,
                    vines,
                    gruposMap.Values.ToList(),
                    weekPerc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en Escanear {Wk}/{Item}", request.Wkname, request.Item);
            return ServiceResult<EscanearResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── /semana_vines_entrega ─────────────────────────────────────────────────

    public async Task<ServiceResult<SemanaVinesEntregaResponse>> EntregarVinesAsync(
        SemanaVinesEntregaRequest request, CancellationToken ct = default)
    {
        try
        {
            var jsonVines = JsonSerializer.Serialize(new { vines = request.Vines });

            var rows = await _repo.EntregarVinesAsync(
                request.Wkname,
                jsonVines,
                request.Empleado,
                request.Comentario ?? string.Empty,
                request.Supervisor ?? string.Empty,
                ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => d.ToDictionary(k => k.Key, v => v.Value))
                .ToList();

            // FIX: try/catch — antes sin manejo de errores, fallaba silenciosamente
            _ = Task.Run(async () =>
            {
                try
                {
                    await _wksRepo.RefreshStatusCacheAsync(request.Wkname);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "RefreshStatusCache falló (entrega) | wkname={W}", request.Wkname);
                }
            });

            return ServiceResult<SemanaVinesEntregaResponse>.Ok(
                new SemanaVinesEntregaResponse(
                    true,
                    request.Wkname,
                    request.Empleado,
                    request.Vines.Count,
                    resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en EntregarVines {Wk}", request.Wkname);
            return ServiceResult<SemanaVinesEntregaResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static EscaneoEvento BuildEvento(IDictionary<string, object?> d) =>
        new()
        {
            Mensaje = d.GetStr("mensaje"),
            Actualizados = d.GetInt("ajustados") ?? d.GetInt("actualizados"),
            Pendientes = d.GetInt("disponibles_para_ajuste") ?? d.GetInt("pendientes"),
            Requested = d.GetInt("solicitado") ?? d.GetInt("requested"),
            TotalItem = d.GetInt("total_item"),
            Excedente = d.GetInt("excedente"),
            Faltante = d.GetInt("faltante"),
            LocacionesAjustadas = d.GetStr("locaciones_ajustadas")
        };
}