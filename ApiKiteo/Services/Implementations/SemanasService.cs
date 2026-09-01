using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class SemanasService : ISemanasService
{
    private readonly ISemanasRepository _repo;
    private readonly ILogger<SemanasService> _logger;

    public SemanasService(ISemanasRepository repo, ILogger<SemanasService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<SemanaItem>>> GetSemanasAsync(
        string cliente, string tipo, CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("Query GetSemanas | cliente={Cliente} tipo={Tipo}", cliente, tipo);

            var rows = await _repo.GetSemanasAsync(cliente, tipo, ct);
            var list = rows.ToList();

            _logger.LogInformation(
                "GetSemanas | cliente={Cliente} tipo={Tipo} resultados={Count}",
                cliente, tipo, list.Count);

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

                return new SemanaItem
                {
                    Clave = clave,
                    Estatus = d.GetValueOrDefault("estatus")?.ToString()
                };
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
        byte filtro = 0, CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("Query GetSemanasPendientes | filtro={Filtro}", filtro);

            var rows = await _repo.GetSemanasPendientesAsync(filtro, ct);

            var result = rows
                .Select(r => (IDictionary<string, object?>)r)
                .Where(d => d.GetValueOrDefault("wkname") is not null)
                .Select(d => new SemanaPendienteItem
                {
                    Wkname = d["wkname"]!.ToString()!,
                    Estatus = d.GetValueOrDefault("estatus")?.ToString(),
                    AprobadoPor = d.GetValueOrDefault("aprobado_por")?.ToString(),
                    // filtro=3 devuelve creado_por; filtros 0-2 devuelven null
                    CreadoPor = d.GetValueOrDefault("creado_por")?.ToString(),

                    // MF 1/9/2026 — formato explícito: la columna es date en SQL
                    // y un .ToString() a secas daría "8/31/2026 12:00:00 AM"
                    // según la cultura del servidor. Mismo patrón que
                    // ExpeditadosService usa para comunizado_despues.
                    FechaCorte = d.GetValueOrDefault("fechacorte") is DateTime fc
                                 ? fc.ToString("yyyy-MM-dd")
                                 : null
                })
                .ToList();

            _logger.LogInformation(
                "GetSemanasPendientes | filtro={Filtro} resultados={Count}",
                filtro, result.Count);

            return ServiceResult<IReadOnlyList<SemanaPendienteItem>>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetSemanasPendientes filtro={Filtro}", filtro);
            return ServiceResult<IReadOnlyList<SemanaPendienteItem>>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }
}