using System.Text.Json;
using ApiKiteo.API.Common;

namespace ApiKiteo.API.Infrastructure.Versionado;

/// <summary>
/// Rechaza clientes por debajo del minimo, y rechaza a TODOS si SQL esta
/// atras de lo que esta API necesita.
///
/// ── Solo mira requests que se identifican ─────────────────────────────────
/// Si no viene `X-Kiteo-Cliente`, pasa de largo. Eso resuelve de un golpe
/// Swagger (que vive en la raiz), el weekboard, /health y cualquier curl de
/// diagnostico, sin una lista de rutas exentas que se desactualiza sola.
///
/// El costo honesto de esa decision: los clientes ANTERIORES a este guard no
/// mandan el header y por lo tanto NO se pueden bloquear. A esos los actualiza
/// ClickOnce al relanzar, no este codigo. El guard empieza a morder con la
/// primera version que manda el header.
///
/// ── Que codigo se devuelve ────────────────────────────────────────────────
/// 426 Upgrade Required. NO 403: en esta API 403 ya significa "te falta
/// LPaccess" y el cliente tiene un camino para eso; mandar 403 aqui haria que
/// le pida credenciales al operador para un problema que no se arregla con
/// credenciales.
/// </summary>
public sealed class VersionGuardMiddleware
{
    public const string HeaderCliente = "X-Kiteo-Cliente";
    public const string HeaderEstacion = "X-Kiteo-Estacion";
    public const string HeaderUsuario = "X-Kiteo-Usuario";

    /// <summary>Se le pone a las respuestas OK de un cliente que ya se quedo atras.</summary>
    public const string HeaderAviso = "X-Kiteo-Actualizar";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<VersionGuardMiddleware> _log;

    public VersionGuardMiddleware(RequestDelegate next, ILogger<VersionGuardMiddleware> log)
    {
        _next = next;
        _log  = log;
    }

    public async Task InvokeAsync(HttpContext ctx, CatalogoVersiones cat)
    {
        var ruta = ctx.Request.Path.Value ?? string.Empty;

        // /version SIEMPRE contesta. Un cliente bloqueado tiene que poder
        // preguntar QUE version necesita y DE DONDE bajarla; si el guard tapara
        // tambien esa puerta, el mensaje de error no podria decir nada util.
        if (ruta.StartsWith("/version", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        // ── SQL atras de la API ───────────────────────────────────────────
        // Esta rama sí aplica a todos, con header o sin el: el problema no es
        // del cliente. 503 + Retry-After, que es lo que significa: "vuelve,
        // esto se arregla del lado del servidor".
        if (cat.EsquemaAtrasado)
        {
            _log.LogError("503 por esquema atrasado | SQL={A} requerido={R} | {Ruta}",
                cat.EsquemaActual, CatalogoVersiones.EsquemaRequerido, ruta);

            ctx.Response.Headers["Retry-After"] = "60";
            await Responder(ctx, 503,
                "El servidor esta en mantenimiento: falta aplicar una " +
                "actualizacion de base de datos. Avisa a sistemas.",
                "KITEO_503_ESQUEMA");
            return;
        }

        var version = ctx.Request.Headers[HeaderCliente].ToString();

        // Sin header: no es un cliente que sepamos evaluar. Pasa.
        if (string.IsNullOrWhiteSpace(version))
        {
            await _next(ctx);
            return;
        }

        var pol = cat.Cliente;
        var v   = CatalogoVersiones.Leer(version);

        // Version ilegible: se registra y se deja pasar. Bloquear por no saber
        // parsear un header seria el guard causando la falla.
        if (v is null)
        {
            _log.LogWarning("Version de cliente ilegible '{V}' | estacion={E} usuario={U}",
                version, Corto(ctx, HeaderEstacion), Corto(ctx, HeaderUsuario));
            await _next(ctx);
            return;
        }

        if (pol.Minima is not null && v < pol.Minima)
        {
            _log.LogWarning(
                "426 cliente viejo | version={V} minima={M} estacion={E} usuario={U} ruta={R}",
                v, pol.Minima, Corto(ctx, HeaderEstacion), Corto(ctx, HeaderUsuario), ruta);

            await Responder(ctx, 426,
                string.IsNullOrWhiteSpace(pol.Mensaje)
                    ? $"Esta version ({v}) ya no esta soportada. Se necesita la {pol.Minima} o mayor."
                    : pol.Mensaje,
                "KITEO_426",
                new
                {
                    versionActual   = v.ToString(),
                    versionMinima   = pol.Minima.ToString(),
                    rutaInstalador  = pol.RutaInstalador
                });
            return;
        }

        // Al corriente para trabajar pero hay una mas nueva: se avisa por
        // header en CADA respuesta. El cliente decide si lo enseña; la API no
        // interrumpe a nadie por esto.
        if (pol.Reco is not null && v < pol.Reco)
            ctx.Response.Headers[HeaderAviso] = pol.Reco.ToString();

        await _next(ctx);
    }

    private static string Corto(HttpContext ctx, string header)
    {
        var s = ctx.Request.Headers[header].ToString();
        return string.IsNullOrWhiteSpace(s) ? "?" : (s.Length > 40 ? s[..40] : s);
    }

    private static async Task Responder(
        HttpContext ctx, int status, string mensaje, string codigo, object? extra = null)
    {
        // Si algo ya empezo a escribir la respuesta, tocar los headers lanza y
        // el 500 resultante taparia el motivo real.
        if (ctx.Response.HasStarted) return;

        ctx.Response.StatusCode  = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";

        // Misma forma que ErrorResponse — { exito, mensaje, codigo } — para que
        // el manejo de errores que ya tiene el cliente no necesite un caso nuevo
        // solo para leer el texto.
        var cuerpo = extra is null
            ? (object)ErrorResponse.Create(mensaje, codigo)
            : new { exito = false, mensaje, codigo, detalle = extra };

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(cuerpo, Json));
    }
}
