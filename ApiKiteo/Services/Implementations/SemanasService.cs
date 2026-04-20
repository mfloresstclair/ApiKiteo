using KiteoAdmin.API.Common;
using KiteoAdmin.API.Models.Responses;
using KiteoAdmin.API.Repositories.Interfaces;
using KiteoAdmin.API.Services.Interfaces;

namespace KiteoAdmin.API.Services.Implementations;

public sealed class SemanasService : ISemanasService
{
    private readonly ISemanasRepository _repo;
    private readonly ILogger<SemanasService> _logger;

    public SemanasService(ISemanasRepository repo, ILogger<SemanasService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<SemanaItem>>> GetSemanasAsync(
        string cliente, string tipo, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetSemanasAsync(cliente, tipo, ct);
            var list = rows.ToList();

            if (list.Count == 0)
                return ServiceResult<IReadOnlyList<SemanaItem>>.Fail(
                    401,
                    "No hay semanas cargadas para este cliente y tipo seleccionado.",
                    ErrorCodes.Kiteo400);

            var result = list.Select(r =>
            {
                var d = (IDictionary<string, object?>)r;

                // El SP puede devolver "clave" o "wkname" — soportamos ambos
                var clave = d.GetValueOrDefault("clave")?.ToString()
                         ?? d.GetValueOrDefault("wkname")?.ToString()
                         ?? string.Empty;

                var estatus = d.GetValueOrDefault("estatus")?.ToString();

                return new SemanaItem { Clave = clave, Estatus = estatus };
            }).ToList();

            return ServiceResult<IReadOnlyList<SemanaItem>>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetSemanas cliente={C} tipo={T}", cliente, tipo);
            return ServiceResult<IReadOnlyList<SemanaItem>>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }

    public async Task<ServiceResult<IReadOnlyList<SemanaPendienteItem>>> GetSemanasPendientesAsync(
        CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.GetSemanasPendientesAsync(ct);

            var result = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Where(d => d.GetValueOrDefault("wkname") is not null)
                .Select(d => new SemanaPendienteItem(d["wkname"]!.ToString()!))
                .ToList();

            return ServiceResult<IReadOnlyList<SemanaPendienteItem>>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetSemanasPendientes");
            return ServiceResult<IReadOnlyList<SemanaPendienteItem>>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }
}
