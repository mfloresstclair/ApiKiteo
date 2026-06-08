using ApiKiteo.API.Common;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class SchedulingService : ISchedulingService
{
    private readonly ISchedulingRepository _repo;
    private readonly ILogger<SchedulingService> _logger;

    public SchedulingService(
        ISchedulingRepository repo,
        ILogger<SchedulingService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<ServiceResult<object>> GetAsync(
        string? wkname, string cliente, CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug(
                "Scheduling | wkname={W} cliente={C}", wkname ?? "NULL", cliente);

            var (semanas, detalle) = await _repo.GetAsync(wkname, cliente, ct);

            // Pass-through — Dapper devuelve ExpandoObject, System.Text.Json
            // lo serializa con los alias del SP tal como están.
            // Si el SP cambia un alias → solo cambia el SP, nada en C#.
            return ServiceResult<object>.Ok(new
            {
                ok = true,
                semanas = semanas,
                detalle = detalle   // null cuando @wkname no se pasó
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Scheduling wkname={W}", wkname);
            return ServiceResult<object>.Fail(
                500, "Error interno.", ErrorCodes.Kiteo500);
        }
    }
}