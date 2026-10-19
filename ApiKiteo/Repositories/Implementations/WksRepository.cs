using ApiKiteo.API.Common;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class WksRepository : IWksRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;
    private readonly ILogger<WksRepository> _logger;
    private readonly int _semanasRetener;
    // MF 19/10/2026 — se quito _horasCompletadas: la condicion que lo usaba ya
    // no existe (ver SqlCleanup). La clave CacheSettings:HorasCompletadas puede
    // quedarse en appsettings sin efecto, o borrarse; no la lee nadie mas.

    // ─────────────────────────────────────────────────────────────────────────
    // MF 19/10/2026 — LA REGLA DE LIMPIEZA, UNA SOLA VEZ, Y MAS CORTA.
    //
    // Estaba escrita dos veces (el auto-cleanup de RefreshStatusCacheAsync y
    // CacheCleanupAsync) y las dos tenian el mismo defecto. Una regla copiada
    // en dos lugares es dos bugs esperando turno. Si se cambia, se cambia aqui.
    //
    // SE QUITO la purga de semanas terminadas:
    //     OR (KitsCompTot >= VinCant AND VinCant > 0
    //         AND updated_at < DATEADD(hour, -@horasCompletadas, GETUTCDATE()))
    //
    // POR QUE. Esa condicion borraba el renglon de una semana "terminada" a las
    // 48 horas, y el board SOLO LEE este cache: sin renglon, la pantalla no
    // muestra porcentaje. Fue la causa de la mitad de los reportes de las
    // ultimas tres semanas:
    //     wk42_41_BodyT3   12/10  rack completo, 29 finales,  99.41%  -> borrada
    //     wk43_170_ZA      19/10  rack completo,  3 finales,  99.97%  -> borrada
    //                             (3 circuitos de 11,511; Alejandro la reporto)
    // y ademas, una semana que llega de verdad al 100% quedaba sin porcentaje
    // para siempre, que para quien mira la pantalla es el mismo sintoma.
    //
    // El motivo original era velocidad. Medido el 19/10: la tabla tiene 41
    // renglones y 8 KB de datos (144 KB asignados, 112 sin usar). Sin esa
    // condicion, el regimen proyectado a 8 semanas es de 91 renglones — ni
    // siquiera pide una extension nueva. La purga no compraba velocidad: lo que
    // la compra es que el board lea esta tabla en vez de agregar
    // VinBusiness_DB_macro, y eso no cambia.
    //
    // La retencion por antiguedad SI se queda: es el control de tamaño real.
    // ─────────────────────────────────────────────────────────────────────────
    private const string SqlCleanup = """
        DELETE FROM dbo.Kit_vin_wks_status_cache
        WHERE updated_at < DATEADD(week, -@semanasRetener, GETUTCDATE())
        """;

    public WksRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp,
        IConfiguration config,
        ILogger<WksRepository> logger)
    {
        _db = db;
        _sp = sp.Value;
        _logger = logger;
        _semanasRetener = config.GetValue<int>("CacheSettings:SemanasRetener", 8);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> GetStatusBoardAsync(
        string jsonWkname, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.WksStatusBoard,
            new { jsonWkname },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task RefreshStatusCacheAsync(
        string wkname, CancellationToken ct = default)
    {
        var partes = wkname.Split('_');
        if (partes.Length < 3) return;

        var wk = partes[0];
        var vinCant = int.TryParse(partes[1], out var v) ? v : 0;
        var typeRaw = string.Join("_", partes[2..]);

        bool esCompuesto = typeRaw.Contains('/');
        var tipos = typeRaw.Split('/');

        using var conn = _db.CreateConnection();

        foreach (var tipo in tipos)
        {
            var tipoTrim = tipo.Trim();

            // ── 1) Porcentaje escaneado ───────────────────────────────────────
            // FIX: SIN AND Locacion <> 0 — incluye MANDAR A FINAL en el %
            //      alinea con lo que el piso cuenta (total de la semana)
            const string sqlPorc = """
                SELECT
                    100.0 * SUM(CASE WHEN Operador IS NOT NULL THEN 1 ELSE 0 END)
                          / NULLIF(COUNT(*), 0)
                FROM dbo.VinBusiness_DB_macro WITH (NOLOCK)
                WHERE WkName = @wkname
                  AND (
                        (@esCompuesto = 1
                         AND vinDesc LIKE 'BodyCV' + @tipoTrim + '%')
                     OR (@esCompuesto = 0)
                  )
                  AND ISNULL(Estatus, 1) = 1
                """;

            var porc = await conn.ExecuteScalarAsync<decimal?>(
                sqlPorc,
                new { wkname, tipoTrim, esCompuesto = esCompuesto ? 1 : 0 }) ?? 0m;

            // ── 2) Kits completos — MANTIENE Locacion <> 0 ───────────────────
            // Un VIN "completo" = todos sus items de RACK escaneados.
            // MANDAR A FINAL no bloquea el conteo de kits completos.
            const string sqlKits = """
                SELECT
                    SUM(CASE WHEN TodoKiteado = 1 AND FueEntregado = 0 THEN 1 ELSE 0 END) AS kitsComp,
                    SUM(CASE WHEN TodoKiteado = 1 AND FueEntregado = 1 THEN 1 ELSE 0 END) AS KitsCompFinal
                FROM (
                    SELECT
                        Vin,
                        MIN(CASE WHEN Operador  IS NOT NULL THEN 1 ELSE 0 END) AS TodoKiteado,
                        MAX(CASE WHEN Entregado IS NOT NULL THEN 1 ELSE 0 END) AS FueEntregado
                    FROM dbo.VinBusiness_DB_macro WITH (NOLOCK)
                    WHERE WkName = @wkname
                      AND (
                            (@esCompuesto = 1
                             AND vinDesc LIKE 'BodyCV' + @tipoTrim + '%')
                         OR (@esCompuesto = 0)
                      )
                      AND Locacion <> 0
                      AND ISNULL(Estatus, 1) = 1
                    GROUP BY Vin
                ) x
                """;

            var kits = (IDictionary<string, object?>?)await conn.QuerySingleOrDefaultAsync(
                sqlKits, new { wkname, tipoTrim, esCompuesto = esCompuesto ? 1 : 0 });

            var kitsComp = Convert.ToInt32(kits?.GetValueOrDefault("kitsComp") ?? 0);
            var kitsCompFinal = Convert.ToInt32(kits?.GetValueOrDefault("KitsCompFinal") ?? 0);
            var kitsCompTot = kitsComp + kitsCompFinal;

            // ── UPSERT ────────────────────────────────────────────────────────
            // MF 19/10/2026 — antes eran DELETE + INSERT en dos ExecuteAsync
            // sueltos. Dos escaneos simultaneos sobre la misma semana corrian el
            // mismo par y uno perdia:
            //     Violation of PRIMARY KEY constraint 'PK_wks_status_cache'.
            //     The duplicate key value is (wk33_187_ZC/ZD, ZC).
            // Visto seis veces en el mismo segundo. El renglon sobrevivia —ganaba
            // el otro— pero la actualizacion se perdia y el log se llenaba de
            // errores que tapan los de verdad.
            //
            // LA TRANSACCION EXPLICITA NO ES OPCIONAL, Y LOS HINTS TAMPOCO.
            // En autocommit cada sentencia es su propia transaccion, asi que el
            // lock de rango que toma el UPDATE se LIBERA al terminar el UPDATE,
            // antes de que corra el INSERT — y la carrera queda igual de abierta.
            // Los hints solo valen dentro de la transaccion que los sostiene:
            //   UPDLOCK      toma el lock de escritura desde el UPDATE
            //   SERIALIZABLE sostiene el rango de la llave cuando el renglon NO
            //                existe, que es la ventana donde las dos tareas
            //                decidian insertar a la vez
            // Quitar el BEGIN TRAN, o quitar los hints, reintroduce el bug.
            //
            // XACT_ABORT ON: si el INSERT falla por cualquier otra razon, la
            // transaccion se deshace sola en vez de quedarse abierta en una
            // tarea fire-and-forget que nadie esta mirando.
            const string sqlUpsert = """
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                    UPDATE dbo.Kit_vin_wks_status_cache WITH (UPDLOCK, SERIALIZABLE)
                       SET wk            = @wk,
                           VinCant       = @vinCant,
                           kitsComp      = @kitsComp,
                           KitsCompFinal = @kitsCompFinal,
                           KitsCompTot   = @kitsCompTot,
                           Porc          = @porc,
                           updated_at    = GETUTCDATE()
                     WHERE wkname = @wkname AND tipo = @tipoTrim;

                    IF @@ROWCOUNT = 0
                        INSERT INTO dbo.Kit_vin_wks_status_cache
                            (wkname, wk, tipo, VinCant, kitsComp, KitsCompFinal,
                             KitsCompTot, Porc, updated_at)
                        VALUES
                            (@wkname, @wk, @tipoTrim, @vinCant, @kitsComp, @kitsCompFinal,
                             @kitsCompTot, @porc, GETUTCDATE());

                COMMIT TRANSACTION;
                """;

            await conn.ExecuteAsync(sqlUpsert, new
            {
                wkname,
                wk,
                tipoTrim,
                vinCant,
                kitsComp,
                kitsCompFinal,
                kitsCompTot,
                porc = Math.Round(porc, 2)
            });

            _logger.LogDebug(
                "RefreshCache OK | wkname={W} tipo={T} porc={P}% kitsComp={K}",
                wkname, tipoTrim, Math.Round(porc, 2), kitsComp);
        }

        // ── Auto-cleanup ──────────────────────────────────────────────────────
        // MF 19/10/2026 — la sentencia vive en SqlCleanup, arriba. Antes estaba
        // duplicada aqui y en CacheCleanupAsync.
        await conn.ExecuteAsync(SqlCleanup, new { semanasRetener = _semanasRetener });
    }

    public async Task<int> CacheCleanupAsync(
        int semanasRetener, int horasCompletadas, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // MF 19/10/2026 — misma sentencia que el auto-cleanup, mas el ROWCOUNT
        // que este metodo devuelve. La regla no se vuelve a escribir.
        var sql = SqlCleanup + ";\n\nSELECT @@ROWCOUNT;";

        // horasCompletadas se conserva en la firma —la expone el endpoint
        // POST /wks/cache/cleanup— pero ya no participa en el DELETE.
        return await conn.ExecuteScalarAsync<int>(sql, new { semanasRetener });
    }
}