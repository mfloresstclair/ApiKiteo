using Microsoft.AspNetCore.Mvc;
using ApiKiteo.API.Infrastructure.Versionado;

namespace ApiKiteo.API.Controllers;

/// <summary>
/// Diagnostico de versiones. NUNCA lo bloquea el guard — es la unica puerta
/// que le queda a un cliente rechazado para saber que necesita.
/// </summary>
[ApiController]
[Route("version")]
public sealed class VersionController : ControllerBase
{
    private readonly CatalogoVersiones _cat;

    public VersionController(CatalogoVersiones cat) => _cat = cat;

    /// <summary>
    /// GET /version — que corre aqui y que se le exige al cliente.
    ///
    /// Con esto se contesta sin entrar a SQL la pregunta que mas se hace en
    /// una falla de piso: "¿la estacion esta vieja o el servidor esta atras?".
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        var pol = _cat.Cliente;

        return Ok(new
        {
            ok  = true,
            api = new
            {
                version = typeof(VersionController).Assembly.GetName().Version?.ToString(3)
                          ?? "0.0.0",
                maquina = Environment.MachineName
            },
            sql = new
            {
                esquemaActual    = _cat.EsquemaActual,      // -1 = todavia no se lee
                esquemaRequerido = CatalogoVersiones.EsquemaRequerido,
                atrasado         = _cat.EsquemaAtrasado
            },
            cliente = new
            {
                minima         = pol.Minima?.ToString(),    // null = guard inerte
                recomendada    = pol.Reco?.ToString(),
                mensaje        = pol.Mensaje,
                rutaInstalador = pol.RutaInstalador
            },
            // false = todavia no hubo una lectura buena de kit_app_version.
            // Si esto se queda en false, el guard NO esta protegiendo nada:
            // o falta correr version-guard.sql, o el usuario de la API no tiene
            // permiso de SELECT sobre las dos tablas.
            catalogoCargado = _cat.Cargado
        });
    }
}
