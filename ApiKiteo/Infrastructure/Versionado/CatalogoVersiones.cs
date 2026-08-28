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
    private volatile bool _avisoEsquema;      // ultimo estado logueado
    private volatile bool _avisoPromocion;    // ya se aviso que falta GRANT UPDATE
    private int _esquemaActual  = -1;         // -1 = desconocido → no se bloquea
    private int _fallosSeguidos;              // lecturas fallidas consecutivas

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

    // La version RELEASE mas alta que se ha visto reportar. Es asi como el
    // servidor se entera de que publicaste: no lo infiere del share (el master
    // es un contenedor Linux y no lo alcanza) ni espera a que alguien corra un
    // UPDATE a mano — en cuanto UNA estacion relanza y ClickOnce la actualiza,
    // esa estacion empieza a reportar el numero nuevo y aqui queda registrado.
    private Version? _vistaMasAlta;
    private readonly object _candado = new();

    /// <summary>
    /// Registra la version que reporto un cliente. Se llama desde el camino
    /// caliente, asi que NO toca la base: solo mueve un maximo en memoria.
    /// Persistir es trabajo del timer.
    ///
    /// `esRelease` no es opcional a proposito: las builds de desarrollo
    /// reportan 1.0.0.9999 —va asi en el csproj para no bloquear el depurador—
    /// y si contaran, `reco` se iria a 9999 y TODAS las estaciones verian el
    /// aviso de actualizar para siempre, sin forma de quitarlo.
    /// </summary>
    public void ObservarCliente(Version? v, bool esRelease)
    {
        if (v is null || !esRelease) return;
        lock (_candado)
            if (_vistaMasAlta is null || v > _vistaMasAlta) _vistaMasAlta = v;
    }

    /// <summary>
    /// Para el diagnostico: ¿el guard esta protegiendo ALGO?
    ///
    /// No es "la consulta funciono": es "hay una politica que puede bloquear".
    /// Una tabla que existe pero esta vacia da una consulta perfecta y un guard
    /// que no protege nada — reportar true ahi seria el peor diagnostico
    /// posible, porque dice "cubierto" justo cuando no lo estas.
    /// </summary>
    public bool Cargado => _leidoAlgunaVez && _cliente.Minima is not null;

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

            // Sin fila NO se conserva la politica anterior: una consulta que
            // funciono y devolvio cero filas es evidencia POSITIVA de que no
            // hay politica. Conservarla dejaba un agujero feo: con la planta
            // parada por un minimo mal capturado, alguien entra en panico y
            // hace DELETE en vez del UPDATE documentado — y el bloqueo seguia
            // hasta reiniciar el servicio.
            if (fila is null)
            {
                _cliente = PoliticaCliente.Inerte;
            }
            else
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
            Volatile.Write(ref _fallosSeguidos, 0);

            // En su propio try: un UPDATE sin permiso NO puede contaminar el
            // estado de las lecturas. Sin esto, subia por el catch de abajo,
            // contaba como lectura fallida y a los 3 ciclos ponia el esquema en
            // -1 — con las lecturas funcionando perfectamente. Falla seguro,
            // pero /version quedaria mintiendo sobre lo que si puede leer.
            try { await PromoverRecoAsync(conn, ct); }
            catch (Exception exUp)
            {
                if (!_avisoPromocion)
                {
                    _avisoPromocion = true;
                    _log.LogWarning(exUp,
                        "No se pudo subir version_reco sola. Falta GRANT UPDATE ON " +
                        "dbo.kit_app_version a la cuenta del servicio. Todo lo demas " +
                        "sigue funcionando; el aviso de version nueva hay que ponerlo " +
                        "a mano con un UPDATE.");
                }
            }

            if (!_leidoAlgunaVez)
            {
                _leidoAlgunaVez = true;
                _log.LogInformation(
                    "Guard de version activo | minima={M} reco={R} | esquema SQL={E} requerido={Q}",
                    _cliente.Minima?.ToString() ?? "(sin minimo)",
                    _cliente.Reco?.ToString()   ?? "(sin reco)",
                    EsquemaActual, EsquemaRequerido);
            }

            // Solo al CAMBIAR de estado. Antes iba un LogError por ciclo: un
            // esquema atrasado un fin de semana llenaba el archivo del dia, y
            // el sink de Serilog deja de escribir al llegar a 1 GB — o sea que
            // el log se pierde justo durante el incidente.
            var atrasado = EsquemaAtrasado;
            if (atrasado != _avisoEsquema)
            {
                _avisoEsquema = atrasado;
                if (atrasado)
                    _log.LogError(
                        "ESQUEMA SQL ATRASADO — la base esta en {A} y esta API necesita {R}. " +
                        "Faltan scripts de migracion por correr. Se responde 503.",
                        EsquemaActual, EsquemaRequerido);
                else
                    _log.LogInformation("Esquema SQL al corriente ({A}). Se reanuda el servicio.",
                        EsquemaActual);
            }
        }
        catch (Exception ex)
        {
            // Un valor viejo que BLOQUEA es un estado de duda, y la duda no
            // bloquea. Si dejamos de poder leer, a los 3 ciclos (3 min) se
            // vuelve a "desconocido" y la API deja de responder 503 — si no,
            // un permiso revocado dejaba la API 503eando para siempre, sin
            // poder leer nunca el UPDATE que lo arreglaria.
            var fallos = Interlocked.Increment(ref _fallosSeguidos);
            if (fallos >= 3 && EsquemaActual >= 0)
            {
                Volatile.Write(ref _esquemaActual, -1);
                _log.LogWarning(
                    "{N} lecturas seguidas fallidas: el nivel de esquema vuelve a " +
                    "desconocido y se DEJA DE BLOQUEAR.", fallos);
            }

            // Warning y no Error: que la tabla no exista es el estado NORMAL
            // antes de correr version-guard.sql, y no es una falla de la API.
            // Y solo el primero de la racha, por lo mismo del archivo de log.
            if (fallos == 1)
                _log.LogWarning(ex,
                    "No se pudo leer el catalogo de versiones. Se conserva la ultima " +
                    "lectura buena y NO se bloquea a nadie.");
        }
    }

    /// <summary>
    /// Si algun cliente reporto una version mas alta que la `reco` guardada, la
    /// sube. Aqui y no en el middleware: un UPDATE por peticion le pondria una
    /// escritura a la base en el camino de cada escaneo.
    ///
    /// Solo toca `version_reco` — el aviso blando. `version_minima` NO se toca
    /// jamas por este camino: publicar bloquearia en el acto a toda estacion
    /// que todavia no actualizo. Esa sigue siendo una decision humana.
    ///
    /// Requiere UPDATE sobre kit_app_version. Si solo hay SELECT, esto falla,
    /// el catch de RefrescarAsync lo registra y el guard sigue funcionando —
    /// nada mas que sin promover sola.
    /// </summary>
    private async Task PromoverRecoAsync(
        Microsoft.Data.SqlClient.SqlConnection conn, CancellationToken ct)
    {
        Version? vista;
        lock (_candado) vista = _vistaMasAlta;

        var pol = _cliente;
        if (vista is null || (pol.Reco is not null && vista <= pol.Reco)) return;

        // No promover por debajo del minimo: si alguien capturo un minimo mas
        // alto que lo que hay en piso, `reco` no debe contradecirlo.
        if (pol.Minima is not null && vista < pol.Minima) return;

        var n = await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE dbo.kit_app_version
               SET version_reco = @v, actualizado_por = 'auto', updated_at = GETDATE()
             WHERE app = 'EstacionKiteo';",
            new { v = vista.ToString() }, commandTimeout: 5, cancellationToken: ct));

        if (n > 0)
        {
            _cliente = pol with { Reco = vista };
            _log.LogInformation(
                "Version nueva en piso: {V}. version_reco se actualizo sola " +
                "(la reporto una estacion).", vista);
        }
    }

    /// <summary>
    /// CUATRO componentes, no tres. El cliente publica como
    /// `ApplicationVersion = 1.0.0.*`: todo el versionado vive en la cuarta
    /// parte. Descartarla —que es lo que hace casi cualquier comparador de
    /// versiones semanticas— dejaria a todas las estaciones llamandose "1.0.0"
    /// y al guard incapaz de distinguir una de otra: instalado, en verde, y
    /// protegiendo nada.
    ///
    /// Tiene que quedar IDENTICO a VersionApp.Leer del cliente. Si los dos
    /// lados normalizan distinto, el servidor bloquea a alguien que del lado
    /// del cliente se veia al corriente.
    ///
    /// Tolerante a proposito: lo que no se puede leer vale null, y null nunca
    /// bloquea. Las partes que falten valen 0, para que un minimo capturado
    /// como "1.0.0" compare igual que "1.0.0.0".
    /// </summary>
    internal static Version? Leer(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        var p = s.Trim().Split('.');
        if (p.Length is < 2 or > 4) return null;

        var n = new int[4];
        for (int i = 0; i < p.Length; i++)
        {
            if (!int.TryParse(p[i], System.Globalization.NumberStyles.None,
                              System.Globalization.CultureInfo.InvariantCulture, out var v) || v < 0)
                return null;
            n[i] = v;
        }
        return new Version(n[0], n[1], n[2], n[3]);
    }
}
