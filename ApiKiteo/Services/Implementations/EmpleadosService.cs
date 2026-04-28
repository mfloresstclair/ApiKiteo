using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Services.Implementations;

public sealed class EmpleadosService : IEmpleadosService
{
    private readonly IEmpleadosRepository _repo;
    private readonly ILogger<EmpleadosService> _logger;

    public EmpleadosService(IEmpleadosRepository repo, ILogger<EmpleadosService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<ServiceResult<EmpleadoResponse>> GetEmpleadoAsync(
        string empleado, CancellationToken ct = default)
    {
        try
        {
            var nombre = await _repo.GetNombreEmpleadoAsync(empleado, ct);

            if (nombre is null)
                return ServiceResult<EmpleadoResponse>.Fail(
                    404, "Empleado no encontrado.", ErrorCodes.Kiteo404);

            return ServiceResult<EmpleadoResponse>.Ok(new EmpleadoResponse(nombre));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GetEmpleado {Emp}", empleado);
            return ServiceResult<EmpleadoResponse>.Fail(
                500, "Error interno. Contacta a soporte.", ErrorCodes.Kiteo500);
        }
    }
}
