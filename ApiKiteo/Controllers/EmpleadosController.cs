using Microsoft.AspNetCore.Mvc;
using KiteoAdmin.API.Common;
using KiteoAdmin.API.Services.Interfaces;

namespace KiteoAdmin.API.Controllers;

/// <summary>
/// Empleados — replica GET /empleado.
/// </summary>
[Produces("application/json")]
public sealed class EmpleadosController : KiteoBaseController
{
    private readonly IEmpleadosService _service;

    public EmpleadosController(IEmpleadosService service) => _service = service;

    /// <summary>
    /// Valida y retorna el nombre de un empleado por número.
    /// </summary>
    [HttpGet("empleado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmpleado(
        [FromQuery] string? empleado,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(empleado))
            return BadRequest(ErrorResponse.Create(
                "El parametro 'empleado' es requerido.",
                ErrorCodes.Kiteo400));

        var result = await _service.GetEmpleadoAsync(empleado.Trim(), ct);
        return FromResult(result);
    }
}
