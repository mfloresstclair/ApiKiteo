using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>Wks — Pizarrón live de planta y gestión del cache de status.</summary>
[Route("wks")]
[Produces("application/json")]
public sealed class WksController : KiteoBaseController
{
    private readonly IWksService _service;
    public WksController(IWksService service) => _service = service;

    // ── POST /wks/status_board ────────────────────────────────────────────────

    /// <summary>
    /// Estado de kits por semana y tipo. Lee del cache — respuesta en menos de 10ms.
    /// Un wkname con tipo ZC/ZD genera 2 filas: una para ZC y otra para ZD.
    /// El cache se actualiza automáticamente después de cada escaneo y entrega.
    /// </summary>
    [HttpPost("status_board")]
    [ProducesResponseType(typeof(WksStatusBoardResponse), StatusCodes.Status200OK)]
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

        var wknamesSanitizados = request.Wknames
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Distinct()
            .ToList();

        if (wknamesSanitizados.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "Todos los wknames enviados están vacíos.", ErrorCodes.Kiteo400));

        return FromResult(await _service.GetStatusBoardAsync(
            request with { Wknames = wknamesSanitizados }, ct));
    }

    // ── POST /wks/cache/cleanup ───────────────────────────────────────────────

    /// <summary>
    /// Limpia manualmente el cache de status board con límites configurables.
    /// </summary>
    [HttpPost("cache/cleanup")]
    [ProducesResponseType(typeof(WksCacheCleanupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CacheCleanup(
        [FromBody] WksCacheCleanupRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.SemanasRetener < 1 || request.SemanasRetener > 52)
            return BadRequest(ErrorResponse.Create(
                "semanasRetener debe estar entre 1 y 52.", ErrorCodes.Kiteo400));

        if (request.HorasCompletadas < 1 || request.HorasCompletadas > 8760)
            return BadRequest(ErrorResponse.Create(
                "horasCompletadas debe estar entre 1 y 8760.", ErrorCodes.Kiteo400));

        return FromResult(await _service.CacheCleanupAsync(
            request.SemanasRetener, request.HorasCompletadas, ct));
    }
}