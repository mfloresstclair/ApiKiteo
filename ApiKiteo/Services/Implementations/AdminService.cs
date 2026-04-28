using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class AdminService : IAdminService
{
    private readonly IAdminRepository _repo;
    private readonly ILogger<AdminService> _logger;

    public AdminService(IAdminRepository repo, ILogger<AdminService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<ServiceResult<AprobarSemanaResponse>> AprobarSemanaAsync(
        AprobarSemanaRequest request, CancellationToken ct = default)
    {
        try
        {
            var rows = await _repo.AprobarSemanaAsync(request.Wkname, request.AprobadoPor, ct);
            var list = rows.ToList();

            // Opción A: SP devuelve rowset con http_status / message / code
            if (list.Count > 0)
            {
                var d = (IDictionary<string, object?>)list[0];

                var rawStatus = d.GetValueOrDefault("http_status")
                             ?? d.GetValueOrDefault("httpStatus");

                if (rawStatus is not null && int.TryParse(rawStatus.ToString(), out var httpStatus))
                {
                    if (httpStatus != 200)
                    {
                        var msg  = d.GetStr("message") ?? "Error al aprobar la semana.";
                        var code = d.GetStr("code")    ?? ErrorCodes.Admin500;
                        return ServiceResult<AprobarSemanaResponse>.Fail(httpStatus, msg, code);
                    }
                }
            }

            // Opción B: SP no devuelve rowset → éxito si no lanzó excepción
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
}
