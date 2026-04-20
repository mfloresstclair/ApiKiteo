using Microsoft.AspNetCore.Mvc;
using KiteoAdmin.API.Common;

namespace KiteoAdmin.API.Controllers;

/// <summary>
/// Base de todos los controllers.
/// Provee el helper <see cref="FromResult{T}"/> que traduce
/// <see cref="ServiceResult{T}"/> → <see cref="IActionResult"/>.
/// </summary>
[ApiController]
public abstract class KiteoBaseController : ControllerBase
{
    /// <summary>
    /// Convierte un ServiceResult en la respuesta HTTP correspondiente.
    /// Éxito → 200 con el value.
    /// Falla → status con { exito, mensaje, codigo }.
    /// </summary>
    protected IActionResult FromResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return StatusCode(
            result.HttpStatus,
            ErrorResponse.Create(result.Mensaje, result.Codigo));
    }
}
