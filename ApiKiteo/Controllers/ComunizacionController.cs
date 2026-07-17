
using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Common;
using ApiKiteo.API.Services.Interfaces;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Validador de comunización — correr ANTES de aprobar/generar.
///
/// Detecta harnesses que las órdenes de la semana piden pero que no están
/// comunizados en CNDetalle. Sin ellos, kit_vin_crea_db no genera sus items
/// y la macro sale incompleta EN SILENCIO.
///
/// Caso real wk32 (16/07/2026): 6 harnesses sin comunizar = 98 líneas perdidas.
/// Se descubrió el viernes comparando Excel. Este endpoint lo dice el lunes.
/// </summary>
[Route("api/comunizacion")]
[Produces("application/json")]
[Tags("Comunización")]
public sealed class ComunizacionController : KiteoBaseController
{
    private readonly IExpeditadosService _service;

    public ComunizacionController(IExpeditadosService service) => _service = service;

    /// <summary>
    /// Gaps de comunización de una semana. Vacío = se puede generar sin riesgo.
    /// La fechacorte es la del header (la que el SP derivó al ingresar el corte).
    /// </summary>
    [HttpGet("validar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validar(
        [FromQuery] int semana,
        [FromQuery] int anio,
        [FromQuery] DateOnly fechacorte,
        CancellationToken ct)
    {
        if (semana < 1 || semana > 53)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'semana' debe estar entre 1 y 53.", ErrorCodes.Kiteo400));

        if (anio < 2024 || anio > 2035)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'anio' no es válido.", ErrorCodes.Kiteo400));

        if (fechacorte == default)
            return BadRequest(ErrorResponse.Create(
                "El parámetro 'fechacorte' es requerido (yyyy-MM-dd).", ErrorCodes.Kiteo400));

        return FromResult(await _service.ValidarComunizacionAsync(semana, anio, fechacorte, ct));
    }
}