using System.Text.Json;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class WksService : IWksService
{
    private readonly IWksRepository _repo;
    private readonly ILogger<WksService> _logger;

    public WksService(IWksRepository repo, ILogger<WksService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // ── POST /wks/status_board ────────────────────────────────────────────────

    public async Task<ServiceResult<WksStatusBoardResponse>> GetStatusBoardAsync(
        WksStatusBoardRequest request, CancellationToken ct = default)
    {
        try
        {
            var jsonWkname = JsonSerializer.Serialize(new { wkname = request.Wknames });

            var rows = await _repo.GetStatusBoardAsync(jsonWkname, ct);

            var resultados = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Select(d => new WksStatusBoardRow
                {
                    Wk = d.GetStr("wk") ?? string.Empty,
                    Tipo = d.GetStr("tipo") ?? string.Empty,
                    Cliente = d.GetStr("cliente") ?? string.Empty,
                    VinCant = d.GetInt("VinCant") ?? 0,
                    KitsComp = d.GetInt("KitsComp") ?? 0,
                    KitCompFinal = d.GetInt("kitCompFinal") ?? 0,
                    KitsCompTot = d.GetInt("KitsCompTot") ?? 0,
                    Porc = GetDecimal(d.GetValueOrDefault("Porc"))
                })
                .ToList();

            return ServiceResult<WksStatusBoardResponse>.Ok(
                new WksStatusBoardResponse(true, resultados.Count, resultados));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en GetStatusBoard wknames={Count}", request.Wknames.Count);
            return ServiceResult<WksStatusBoardResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── POST /wks/cache/cleanup ───────────────────────────────────────────────

    public async Task<ServiceResult<WksCacheCleanupResponse>> CacheCleanupAsync(
        int semanasRetener, int horasCompletadas, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "CacheCleanup manual | semanas={S} horas={H}",
                semanasRetener, horasCompletadas);

            var eliminadas = await _repo.CacheCleanupAsync(
                semanasRetener, horasCompletadas, ct);

            _logger.LogInformation("CacheCleanup | eliminadas={N}", eliminadas);

            return ServiceResult<WksCacheCleanupResponse>.Ok(
                new WksCacheCleanupResponse(
                    Ok: true,
                    TotalEliminadas: eliminadas,
                    SemanasRetener: semanasRetener,
                    HorasCompletadas: horasCompletadas));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CacheCleanup");
            return ServiceResult<WksCacheCleanupResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static decimal GetDecimal(object? val)
    {
        if (val is null || val is DBNull) return 0m;
        return val switch
        {
            decimal d => Math.Round(d, 2),
            double v => Math.Round((decimal)v, 2),
            float f => Math.Round((decimal)f, 2),
            _ => decimal.TryParse(val.ToString(), out var parsed)
                             ? Math.Round(parsed, 2)
                             : 0m
        };
    }
}