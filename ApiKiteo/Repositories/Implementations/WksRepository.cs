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
    private readonly int _horasCompletadas;

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
        _horasCompletadas = config.GetValue<int>("CacheSettings:HorasCompletadas", 48);
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
            const string sqlDelete = """
                DELETE FROM dbo.Kit_vin_wks_status_cache
                WHERE wkname = @wkname AND tipo = @tipoTrim
                """;

            const string sqlInsert = """
                INSERT INTO dbo.Kit_vin_wks_status_cache
                    (wkname, wk, tipo, VinCant, kitsComp, KitsCompFinal,
                     KitsCompTot, Porc, updated_at)
                VALUES
                    (@wkname, @wk, @tipoTrim, @vinCant, @kitsComp, @kitsCompFinal,
                     @kitsCompTot, @porc, GETUTCDATE())
                """;

            await conn.ExecuteAsync(sqlDelete, new { wkname, tipoTrim });
            await conn.ExecuteAsync(sqlInsert, new
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
        const string sqlCleanup = """
            DELETE FROM dbo.Kit_vin_wks_status_cache
            WHERE updated_at < DATEADD(week, -@semanasRetener, GETUTCDATE())
               OR (KitsCompTot >= VinCant
                   AND VinCant > 0
                   AND updated_at < DATEADD(hour, -@horasCompletadas, GETUTCDATE()))
            """;

        await conn.ExecuteAsync(sqlCleanup,
            new { semanasRetener = _semanasRetener, horasCompletadas = _horasCompletadas });
    }

    public async Task<int> CacheCleanupAsync(
        int semanasRetener, int horasCompletadas, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        const string sql = """
            DELETE FROM dbo.Kit_vin_wks_status_cache
            WHERE updated_at < DATEADD(week, -@semanasRetener, GETUTCDATE())
               OR (KitsCompTot >= VinCant
                   AND VinCant > 0
                   AND updated_at < DATEADD(hour, -@horasCompletadas, GETUTCDATE()));

            SELECT @@ROWCOUNT;
            """;

        return await conn.ExecuteScalarAsync<int>(
            sql, new { semanasRetener, horasCompletadas });
    }
}