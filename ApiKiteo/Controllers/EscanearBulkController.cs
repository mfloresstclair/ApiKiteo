using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Endpoint temporal para carga masiva desde Excel.
/// Llama internamente a EscanearAsync por cada ítem.
/// </summary>
[Produces("application/json")]
public sealed class EscanearBulkController : KiteoBaseController
{
    private readonly IEscaneoService _service;
    private readonly ILogger<EscanearBulkController> _logger;

    public EscanearBulkController(
        IEscaneoService service,
        ILogger<EscanearBulkController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta escanear para una lista de ítems en secuencia.
    /// Úsalo desde Swagger pegando el JSON con todos los ítems.
    /// </summary>
    [HttpPost("escanear_bulk")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EscanearBulk(
        [FromBody] EscanearBulkRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Wkname)
            || string.IsNullOrWhiteSpace(request.Empleado))
            return BadRequest(ErrorResponse.Create(
                "Los campos 'wkname' y 'empleado' son requeridos.",
                ErrorCodes.Kiteo400));

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(ErrorResponse.Create(
                "El campo 'items' debe tener al menos 1 elemento.",
                ErrorCodes.Kiteo400));

        var resultados = new List<EscanearBulkItemResult>();

        foreach (var it in request.Items)
        {
            // Respetar cancelación si el cliente corta la conexión
            if (ct.IsCancellationRequested) break;

            var req = new EscanearRequest(
                request.Wkname,
                it.Item,
                it.Cantidad,
                request.Empleado);

            var svc = await _service.EscanearAsync(req, ct);

            if (svc.IsSuccess)
            {
                var evt = svc.Value?.Evento;
                resultados.Add(new EscanearBulkItemResult(
                    it.Item,
                    it.Cantidad,
                    true,
                    evt?.Mensaje,
                    evt?.Actualizados,
                    evt?.Pendientes));
            }
            else
            {
                _logger.LogWarning(
                    "Bulk: fallo en ítem {Item} — {Msg}", it.Item, svc.Mensaje);

                resultados.Add(new EscanearBulkItemResult(
                    it.Item,
                    it.Cantidad,
                    false,
                    svc.Mensaje,
                    null,
                    null));
            }
        }

        var exitosos = resultados.Count(r => r.Ok);
        var fallidos = resultados.Count(r => !r.Ok);

        return Ok(new EscanearBulkResponse(
            true,
            request.Wkname,
            resultados.Count,
            exitosos,
            fallidos,
            resultados));
    }
}