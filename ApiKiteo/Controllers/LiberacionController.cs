using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Models.Requests;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Liberación de material para Corte.
/// Flujo: Scheduling selecciona semanas PendienteCorte → genera resumen y detalle
/// → envía correo a sí mismo con det=0 (body) y det=1 (CSV adjunto).
/// </summary>
[Route("api/liberacion")]
[Produces("application/json")]
public sealed class LiberacionController : KiteoBaseController
{
    private readonly ILiberacionService _service;

    public LiberacionController(ILiberacionService service) => _service = service;

    /// <summary>
    /// Semanas en estado PendienteCorte con su cliente (TBB/BB).
    /// Usadas para el selector del form de liberación.
    /// </summary>
    /// <param name="cliente">TODOS | TBB | BB — filtra por cliente.</param>
    /// <param name="estatus">PendienteCorte (default) | Pendiente | APROBADA</param>
    /// <param name="cliente">TODOS (default) | TBB | BB</param>
    [HttpGet("semanas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSemanas(
        [FromQuery] string estatus = "PendienteCorte",
        [FromQuery] string cliente = "TODOS",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(estatus))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'estatus' es requerido.",
                ErrorCodes.Kiteo400));

        if (cliente is not ("TODOS" or "TBB" or "BB"))
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'cliente' debe ser TODOS, TBB o BB.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GetSemanasAsync(estatus.Trim(), cliente, ct));
    }

    /// <summary>
    /// Resumen de material a liberar (det=0): item + cantidad + cliente.
    /// Registra la ejecución en Boss_transactions.
    /// </summary>
    [HttpPost("resumen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetResumen(
        [FromBody] LiberacionRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.",
                ErrorCodes.Kiteo400));

        if (request.Cliente is not ("TODOS" or "TBB" or "BB"))
            return BadRequest(ErrorResponse.Create(
                "El campo 'cliente' debe ser TODOS, TBB o BB.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GetResumenAsync(request, ct));
    }

    /// <summary>
    /// Detalle completo de material a liberar (det=1): una fila por wkname/tipo/item/vin.
    /// Devuelve todas las filas — el cliente (WinForms) pagina localmente con VirtualMode.
    /// NO duplica el log de Boss_transactions (ya lo hizo /resumen).
    /// </summary>
    [HttpPost("detalle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDetalle(
        [FromBody] LiberacionRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(ErrorResponse.Create(
                "El campo 'username' es requerido.",
                ErrorCodes.Kiteo400));

        if (request.Cliente is not ("TODOS" or "TBB" or "BB"))
            return BadRequest(ErrorResponse.Create(
                "El campo 'cliente' debe ser TODOS, TBB o BB.",
                ErrorCodes.Kiteo400));

        return FromResult(await _service.GetDetalleAsync(request, ct));
    }
}