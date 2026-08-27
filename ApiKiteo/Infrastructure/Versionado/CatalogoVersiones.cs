using Dapper;
using ApiKiteo.API.Infrastructure.Database;

namespace ApiKiteo.API.Infrastructure.Versionado;

/// <summary>Lo que la tabla dice sobre el cliente WPF. Todo nullable = sin politica.</summary>
public sealed record PoliticaCliente(
    Version? Minima,
    Version? Reco,
    string   Mensaje,
    string?  RutaInstalador)
{
    /// <summary>Sin politica: nada se bloquea. Es el estado inicial y el de falla.</summary>
    public static readonly PoliticaCliente Inerte = new(null, null, string.Empty, null);
}

/// <summary>
/// Lee `kit_app_version` y `kit_meta_version` en segundo plano y sirve la
/// ultima lectura buena.
///
/// ── Por que en segundo plano y no por request ─────────────────────────────
/// Un guard que consulta SQL en la ruta caliente le suma una ida a la base a
/// CADA escaneo, y peor: si SQL se pone lento, el guard convierte lentitud en
/// bloqueo. Aqui la lectura vive en un timer y el middleware solo lee un campo
/// en memoria. Costo por request: cero.
///
/// ── Por que nunca lanza ───────────────────────────────────────────────────
/// Si la tabla no existe, si el usuario de la API no tiene permiso, si SQL no
/// contesta — se queda con lo ultimo bueno, o con `Inerte` si nunca hubo nada.
/// Un guard de version que se cae con la base apagaria la planta DOS veces.
/// </summary>
public sealed class CatalogoVersiones : BackgroundService
{
    /// <summary>
    /// Nivel de esquema que ESTA compilacion de la API necesita en SQL.
    ///
    /// SUBIR ESTE NUMERO ES PARTE DEL CAMBIO, no un tramite aparte: si agregas
    /// una columna a un SP y la lees aqui, el numero sube en el mismo commit
    /// que el script. Es lo unico que evita repetir 'Cannot insert the value
    /// NULL into column es_final' — que fue exactamente esto: API adelante,
    /// SP atras, y el error se lo comio la operadora.
    ///
    ///   1 = listas-v3-UNICO   2 = listas-v3-FIX1   3 = listas-v3-FIX2
    /// </summary>
    public const int EsquemaRequerido = 3;

    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(60);

    private readonly IDbConnectionFactory      _db;
    private readonly ILogger<CatalogoVersiones> _log;

    // volatile: lo escribe el timer, lo leen los hilos de request.
    private volatile PoliticaCliente _cliente = PoliticaCliente.Inerte;
    private volatile bool _leidoAlgunaVez;
    private int _esquemaActual = -1;   // -1 = desconocido → no se bloquea

    public CatalogoVersiones(IDbConnectionFactory db, ILogger<CatalogoVersiones> log)
    {
        _db  = db;
        _log = log;
    }

    /// <summary>Ultima politica buena. Nunca null.</summary>
    public PoliticaCliente Cliente => _cliente;

    /// <summary>Nivel de esquema leido de SQL. -1 si todavia no se sabe.</summary>
    public int EsquemaActual => Volatile.Read(ref _esquemaActual);

    /// <summary>
    /// true solo cuando se SABE que SQL esta atras. Desconocido devuelve false:
    /// la duda no bloquea.
    /// </summary>
    public bool EsquemaAtrasado
    {
        get { var a = EsquemaActual; return a >= 0 && a < EsquemaRequerido; }
    }

    /// <summary>Para el endpoint de diagnostico: ¿ya hubo una lectura buena?</summary>
    public bool Cargado => _leidoAlgunaVez;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Primera lectura inmediata: para cuando entre el primer request la
        // politica ya esta puesta. Sin esto habria una ventana de 60 s en cada
        // reinicio donde el guard no existe.
        await RefrescarAsync(ct);

        using var timer = new PeriodicTimer(Intervalo);
        while (await timer.WaitForNextTickAsync(ct))
            await RefrescarAsync(ct);
    }

    private async Task RefrescarAsync(CancellationToken ct)
    {
        try
        {
            using var conn = _db.CreateConnection();

            // SQL inline, sin SP: es una lectura de una fila y meterla en un SP
            // obligaria a un script mas para poder cambiar el guard. Mismo
            // criterio que /api/semanas/preview/vins.
            var fila = await conn.QueryFirstOrDefaultAsync(new CommandDefinition(@"
                SELECT version_minima, version_reco, mensaje, ruta_instalador
                  FROM dbo.kit_app_version
                 WHERE app = 'EstacionKiteo';",
                commandTimeout: 5, cancellationToken: ct));

            if (fila is not null)
            {
                var d = (IDictionary<string, object?>)fila;
                _cliente = new PoliticaCliente(
                    Leer(d["version_minima"] as string),
                    Leer(d["version_reco"]   as string),
                    d["mensaje"] as string ?? string.Empty,
                    d["ruta_instalador"] as string);
            }

            var esq = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(@"
                SELECT version FROM dbo.kit_meta_version WHERE componente = 'listas';",
                commandTimeout: 5, cancellationToken: ct));

            Volatile.Write(ref _esquemaActual, esq ?? -1);

            if (!_leidoAlgunaVez)
            {
                _leidoAlgunaVez = true;
                _log.LogInformation(
                    "Guard de version activo | minima={M} reco={R} | esquema SQL={E} requerido={Q}",
                    _cliente.Minima?.ToString() ?? "(sin minimo)",
                    _cliente.Reco?.ToString()   ?? "(sin reco)",
                    EsquemaActual, EsquemaRequerido);
            }

            if (EsquemaAtrasado)
                _log.LogError(
                    "ESQUEMA SQL ATRASADO — la base esta en {A} y esta API necesita {R}. " +
                    "Faltan scripts de migracion por correr.",
                    EsquemaActual, EsquemaRequerido);
        }
        catch (Exception ex)
        {
            // Warning, no Error: que la tabla no exista es el estado NORMAL
            // antes de correr version-guard.sql, y no es una falla de la API.
            _log.LogWarning(ex,
                "No se pudo leer el catalogo de versiones. Se conserva la ultima " +
                "lectura buena y NO se bloquea a nadie.");
        }
    }

    /// <summary>
    /// Tolerante a proposito: lo que no se puede leer vale null, y null nunca
    /// bloquea. Acepta la cuarta parte de ClickOnce y la descarta — esa parte
    /// se incrementa en cada publicacion aunque el codigo no cambie.
    /// </summary>
    internal static Version? Leer(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var p = s.Trim().Split('.');
        if (p.Length is < 2 or > 4) return null;

        int[] n = new int[3];
        for (int i = 0; i < p.Length; i++)
        {
            if (!int.TryParse(p[i], System.Globalization.NumberStyles.None,
                              System.Globalization.CultureInfo.InvariantCulture, out var v) || v < 0)
                return null;
            if (i < 3) n[i] = v;
        }
        return new Version(n[0], n[1], n[2]);
    }
}
