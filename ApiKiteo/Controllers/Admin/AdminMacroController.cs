using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers.Admin;

/// <summary>
/// Admin — Macro export. Exportación de VinBusiness_DB_macro en CSV.
/// </summary>
[Route("api/macro")]
[Produces("application/json")]
public sealed class AdminMacroController : KiteoBaseController
{
    private readonly IMacroService _service;

    public AdminMacroController(IMacroService service) => _service = service;

    // ── GET /api/macro/export ─────────────────────────────────────────────────

    /// <summary>
    /// Exporta VinBusiness_DB_macro como CSV descargable.
    /// Sin filtros → últimas 4 semanas por recorddate.
    /// Todos los filtros son opcionales y combinables.
    /// </summary>
    /// <remarks>
    /// Ejemplos:
    ///   GET /api/macro/export                                       (últimas 4 semanas)
    ///   GET /api/macro/export?wknames=wk22_196_CEA,wk21_142_CEA
    ///   GET /api/macro/export?tipo=CEA&amp;cliente=TBB
    ///   GET /api/macro/export?desde=2026-04-01&amp;hasta=2026-05-15
    /// </remarks>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task Export(
        [FromQuery] string?  wknames,
        [FromQuery] string?  tipo,
        [FromQuery] string?  cliente,
        [FromQuery] string?  desde,
        [FromQuery] string?  hasta,
        CancellationToken ct)
    {
        // ── Parsear wknames (CSV string → lista) ──────────────────────────────
        var wknameList = string.IsNullOrWhiteSpace(wknames)
            ? Array.Empty<string>()
            : wknames
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        // ── Parsear fechas opcionales ─────────────────────────────────────────
        DateOnly? desdeDate = null;
        DateOnly? hastaDate = null;

        if (!string.IsNullOrWhiteSpace(desde))
        {
            if (!DateOnly.TryParse(desde, out var d))
            {
                Response.StatusCode = 400;
                await Response.WriteAsJsonAsync(ErrorResponse.Create(
                    "El parámetro 'desde' debe tener formato yyyy-MM-dd.", ErrorCodes.Kiteo400), ct);
                return;
            }
            desdeDate = d;
        }

        if (!string.IsNullOrWhiteSpace(hasta))
        {
            if (!DateOnly.TryParse(hasta, out var h))
            {
                Response.StatusCode = 400;
                await Response.WriteAsJsonAsync(ErrorResponse.Create(
                    "El parámetro 'hasta' debe tener formato yyyy-MM-dd.", ErrorCodes.Kiteo400), ct);
                return;
            }
            hastaDate = h;
        }

        if (desdeDate.HasValue && hastaDate.HasValue && desdeDate > hastaDate)
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(ErrorResponse.Create(
                "'desde' no puede ser mayor que 'hasta'.", ErrorCodes.Kiteo400), ct);
            return;
        }

        // ── Nombre del archivo con timestamp ──────────────────────────────────
        var filename = $"macro_export_{DateTime.Now:yyyyMMdd_HHmm}.csv";

        // ── Headers de descarga ───────────────────────────────────────────────
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{filename}\"");
        Response.Headers.Append("X-Export-Filename", filename);

        // ── Streaming directo a Response.Body — no carga todo en memoria ──────
        await _service.StreamCsvAsync(
            wknameList, tipo, cliente, desdeDate, hastaDate,
            Response.Body, ct);
    }
}
