using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Wks — estado de semanas de producción (pizarrón live).
/// </summary>
[Route("wks")]
[Produces("application/json")]
public sealed class WksController : KiteoBaseController
{
    private readonly IWksService _service;

    public WksController(IWksService service) => _service = service;

    // ── POST /wks/status_board ────────────────────────────────────────────────

    /// <summary>
    /// Devuelve el estado de kits por semana y tipo para una lista de wknames.
    /// Reemplaza el pizarrón físico de la planta — datos en tiempo real.
    /// </summary>
    /// <remarks>
    /// Formato de wkname: {semana}_{vinCant}_{tipo}
    /// Un tipo compuesto como ZC/ZD genera 2 filas en la respuesta (una por tipo).
    ///
    /// Ejemplo de body:
    /// ```json
    /// {
    ///   "wknames": [
    ///     "wk20_108_CEA",
    ///     "wk20_111_ZC/ZD",
    ///     "wk21_138_ZA"
    ///   ]
    /// }
    /// ```
    /// </remarks>
    [HttpPost("status_board")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStatusBoard(
        [FromBody] WksStatusBoardRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.Wknames is null || request.Wknames.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "El campo 'wknames' debe ser una lista con al menos 1 elemento.",
                ErrorCodes.Kiteo400));

        // Descartar entradas vacías o con solo espacios antes de llegar al SP
        var wknamesSanitizados = request.Wknames
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Distinct()
            .ToList();

        if (wknamesSanitizados.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "Todos los wknames enviados están vacíos.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GetStatusBoardAsync(
            request with { Wknames = wknamesSanitizados }, ct));
    }
}
