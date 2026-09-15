using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;
using System.Globalization;

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

            // MF 1/9/2026 — antes esto devolvia HTTP 401 (con codigo Kiteo400, que
            // ya no cuadraba entre si) cuando simplemente no habia semanas.
            //
            // Una lista vacia no es un fallo de autenticacion: es una lista vacia.
            // Y del lado del cliente el 401 hacia que GetStringAsync lanzara
            // HttpRequestException, que cae en el catch de "Error de conexion" — o
            // sea que al operador se le mostraba un problema de red por un
            // resultado normal. Un cliente que ademas trate 401 como sesion
            // expirada lo desloguearia.
            //
            // Ahora se devuelve 200 con la lista vacia y el consumidor decide.

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

                    // DESPUÉS
                    // MF 1/9/2026 — formato explícito: mismo patrón que ExpeditadosService
                    // usa para comunizado_despues.
                    // MF 14/9/2026 — fechacorte es varchar(20) en Kit_vin_wk_header, NO date.
                    // El "is DateTime" nunca empataba y la columna salía siempre null aunque
                    // el SP ya la traía. Se acepta string o DateTime y se normaliza a
                    // yyyy-MM-dd; un texto que no parsea se pasa crudo — mejor un valor raro
                    // a la vista que ninguno.
                    FechaCorte = d.GetValueOrDefault("fechacorte") switch
                    {
                        DateTime fc => fc.ToString("yyyy-MM-dd"),
                        string s when DateTime.TryParse(s, CultureInfo.InvariantCulture,
                                                        DateTimeStyles.None, out var p)
                                    => p.ToString("yyyy-MM-dd"),
                        string s when !string.IsNullOrWhiteSpace(s) => s.Trim(),
                        _ => null
                    }
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