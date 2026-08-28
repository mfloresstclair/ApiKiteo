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

    /// <summary>
    /// "release" o "debug". Solo las release cuentan para promover
    /// `version_reco`: una build de desarrollo reporta 1.0.0.9999 y dejaria a
    /// toda la planta con el aviso de actualizar puesto para siempre.
    ///
    /// Un cliente que no lo mande se trata como debug — o sea, no promueve.
    /// La duda no cambia nada, igual que en todo lo demas.
    /// </summary>
    public const string HeaderBuild = "X-Kiteo-Build";

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
        _log = log;
    }

    /// <summary>
    /// Rutas que el guard NUNCA toca.
    ///
    /// /version y /health porque un cliente bloqueado tiene que poder
    /// preguntar que necesita, y porque tumbarle el health-check al monitoreo
    /// convierte "falta correr un script" en "la API se cayo" — y alguien se
    /// levanta de madrugada con el diagnostico equivocado.
    ///
    /// /api/health y /api/metrics porque ESOS son los que se monitorean de
    /// verdad (MetricsMiddleware los excluye por esos prefijos exactos).
    ///
    /// /swagger y la raiz porque Swagger vive en RoutePrefix vacio: apagarlo
    /// deja sin herramientas justo a quien viene a diagnosticar.
    /// </summary>
    private static readonly string[] Exentas =
    {
        "/version", "/health", "/api/health", "/api/metrics", "/swagger", "/weekboard"
    };

    private static bool EsExenta(PathString ruta)
    {
        // La raiz exacta es Swagger UI. Un StartsWithSegments("/") daria true
        // para TODO.
        if (!ruta.HasValue || ruta == "/") return true;
        foreach (var e in Exentas)
            if (ruta.StartsWithSegments(e, StringComparison.OrdinalIgnoreCase)) return true;
        // Cualquier archivo estatico (.js, .css, .html del weekboard).
        return ruta.Value!.Contains('.', StringComparison.Ordinal);
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (EsExenta(ctx.Request.Path))
        {
            await _next(ctx);
            return;
        }

        // Resolucion MANUAL y tolerante.
        //
        // Con inyeccion por metodo, olvidar el AddSingleton no daba un guard
        // inerte: daba InvalidOperationException en CADA peticion, o sea la API
        // entera caida por un error de registro. La duda de configuracion
        // tampoco bloquea.
        var cat = ctx.RequestServices.GetService<CatalogoVersiones>();
        if (cat is null)
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
            // Debug y no Error: el estado ya se logueo como Error UNA vez, al
            // cambiar, desde CatalogoVersiones. Aqui seria una linea por
            // peticion — con 40 estaciones sondeando, cientos de miles al dia,
            // y el sink de archivo deja de escribir al llegar a 1 GB.
            _log.LogDebug("503 por esquema atrasado | SQL={A} requerido={R} | {Ruta}",
                cat.EsquemaActual, CatalogoVersiones.EsquemaRequerido, ctx.Request.Path);

            if (!ctx.Response.HasStarted) ctx.Response.Headers["Retry-After"] = "60";
            await Responder(ctx, 503,
                "El servidor esta en mantenimiento: falta aplicar una " +
                "actualizacion de base de datos. Avisa a sistemas.",
                ErrorCodes.Kiteo503Esquema);
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
        var v = CatalogoVersiones.Leer(version);

        // Version ilegible: se registra y se deja pasar. Bloquear por no saber
        // parsear un header seria el guard causando la falla.
        if (v is null)
        {
            LogUnaVez($"ilegible|{Corto(ctx, HeaderEstacion)}|{version}",
                () => _log.LogWarning("Version de cliente ilegible '{V}' | estacion={E} usuario={U}",
                    version, Corto(ctx, HeaderEstacion), Corto(ctx, HeaderUsuario)));
            await _next(ctx);
            return;
        }

        if (pol.Minima is not null && v < pol.Minima)
        {
            // Una vez por (estacion, usuario, version), no por peticion: una
            // estacion bloqueada sigue sondeando cada 20 s y el cliente no
            // tiene backoff. Es un estado, no un evento.
            LogUnaVez(
                $"426|{Corto(ctx, HeaderEstacion)}|{Corto(ctx, HeaderUsuario)}|{v}",
                () => _log.LogWarning(
                    "426 cliente viejo | version={V} minima={M} estacion={E} usuario={U}",
                    v, pol.Minima, Corto(ctx, HeaderEstacion), Corto(ctx, HeaderUsuario)));

            await Responder(ctx, 426,
                string.IsNullOrWhiteSpace(pol.Mensaje)
                    ? $"Esta version ({v}) ya no esta soportada. Se necesita la {pol.Minima} o mayor."
                    : pol.Mensaje,
                ErrorCodes.Kiteo426,
                new
                {
                    versionActual = v.ToString(),
                    versionMinima = pol.Minima.ToString(),
                    rutaInstalador = pol.RutaInstalador
                });
            return;
        }

        // Se registra la version para que el servidor sepa que hay una nueva
        // publicada, sin que nadie tenga que decirselo. Va DESPUES del 426: una
        // version rechazada no promueve nada.
        cat.ObservarCliente(v, ctx.Request.Headers[HeaderBuild] == "release",
            $"{Corto(ctx, HeaderEstacion)}/{Corto(ctx, HeaderUsuario)}");

        // Al corriente para trabajar pero hay una mas nueva: se avisa por
        // header en CADA respuesta. El cliente decide si lo enseña; la API no
        // interrumpe a nadie por esto.
        if (pol.Reco is not null && v < pol.Reco)
            ctx.Response.Headers[HeaderAviso] = pol.Reco.ToString();

        await _next(ctx);
    }

    // Dedupe de log. Acotado a 500 claves: es (estacion, usuario, version) y
    // en la planta hay decenas, pero un cliente que mande basura variable en el
    // header no puede hacer crecer esto sin limite.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _vistos = new();

    private static void LogUnaVez(string clave, Action escribir)
    {
        if (_vistos.Count > 500) _vistos.Clear();
        if (_vistos.TryAdd(clave, 0)) escribir();
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

        ctx.Response.StatusCode = status;
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