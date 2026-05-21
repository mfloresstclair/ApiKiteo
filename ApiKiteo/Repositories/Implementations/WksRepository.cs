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

    // ── Límites del auto-cleanup del cache ────────────────────────────────────
    // Se leen de appsettings.json sección "CacheSettings".
    // Para cambiarlos editar appsettings.json y reiniciar el servicio.
    // Defaults: 8 semanas de historial, completadas se borran a las 48h.
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

        // Leer con fallback a defaults seguros si la sección no existe
        _semanasRetener = config.GetValue<int>("CacheSettings:SemanasRetener", 8);
        _horasCompletadas = config.GetValue<int>("CacheSettings:HorasCompletadas", 48);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> GetStatusBoardAsync(
        string jsonWkname, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // Lee del cache Kit_vin_wks_status_cache — respuesta <10ms.
        // El cache se actualiza via RefreshStatusCacheAsync (fire-and-forget).
        return await conn.QueryAsync(
            _sp.WksStatusBoard,
            new { jsonWkname },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    /// <inheritdoc/>
    public async Task RefreshStatusCacheAsync(
        string wkname, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("RefreshCache start | wkname={Wk}", wkname);

        // Parsear wkname → wk + vinCant + tipo(s)
        // Formato: wk21_142_CEA | wk20_111_ZC/ZD
        var partes = wkname.Split('_');
        if (partes.Length < 3) return;

        var wk = partes[0];
        var vinCant = int.TryParse(partes[1], out var v) ? v : 0;
        var typeRaw = string.Join("_", partes[2..]);
        var tipos = typeRaw.Split('/');   // ZC/ZD → ["ZC", "ZD"]

        using var conn = _db.CreateConnection();

        foreach (var tipo in tipos)
        {
            var tipoTrim = tipo.Trim();

            // 1) Porcentaje escaneado
            // Para ZC/ZD filtra por vinDesc; para el resto usa todos los rows del wkname
            const string sqlPorc = """
                SELECT
                    100.0 * SUM(CASE WHEN Operador IS NOT NULL THEN 1 ELSE 0 END)
                          / NULLIF(COUNT(*), 0)
                FROM dbo.VinBusiness_DB_macro WITH (NOLOCK)
                WHERE WkName = @wkname
                  AND (
                        (@tipo IN ('ZC','ZD')
                         AND vinDesc LIKE 'BodyCV' + @tipo + '\_%' ESCAPE '\')
                     OR (@tipo NOT IN ('ZC','ZD'))
                  )
                """;

            var porc = await conn.ExecuteScalarAsync<decimal?>(
                sqlPorc, new { wkname, tipo = tipoTrim }) ?? 0m;

            // 2) Kits completos kiteados vs entregados
            // WHERE WkName = @wkname filtra antes del GROUP BY — no full scan
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
                            (@tipo IN ('ZC','ZD')
                             AND vinDesc LIKE 'BodyCV' + @tipo + '\_%' ESCAPE '\')
                         OR (@tipo NOT IN ('ZC','ZD'))
                      )
                    GROUP BY Vin
                ) x
                """;

            var kits = (IDictionary<string, object?>?)await conn.QuerySingleOrDefaultAsync(
                sqlKits, new { wkname, tipo = tipoTrim });

            var kitsComp = Convert.ToInt32(kits?.GetValueOrDefault("kitsComp") ?? 0);
            var kitsCompFinal = Convert.ToInt32(kits?.GetValueOrDefault("KitsCompFinal") ?? 0);
            var kitsCompTot = kitsComp + kitsCompFinal;

            // 3) UPSERT — DELETE + INSERT (más simple que MERGE en C#)
            const string sqlDelete = """
                DELETE FROM dbo.Kit_vin_wks_status_cache
                WHERE wkname = @wkname AND tipo = @tipo
                """;

            const string sqlInsert = """
                INSERT INTO dbo.Kit_vin_wks_status_cache
                    (wkname, wk, tipo, VinCant, kitsComp, KitsCompFinal,
                     KitsCompTot, Porc, updated_at)
                VALUES
                    (@wkname, @wk, @tipo, @vinCant, @kitsComp, @kitsCompFinal,
                     @kitsCompTot, @porc, GETUTCDATE())
                """;

            await conn.ExecuteAsync(sqlDelete, new { wkname, tipo = tipoTrim });
            await conn.ExecuteAsync(sqlInsert, new
            {
                wkname,
                wk,
                tipo = tipoTrim,
                vinCant,
                kitsComp,
                kitsCompFinal,
                kitsCompTot,
                porc = Math.Round(porc, 2)
            });
        }

        // 4) Auto-cleanup al final de cada refresh
        // Límites leídos de appsettings.json → CacheSettings
        // Borra: entradas viejas O semanas completas sin cambios recientes
        const string sqlCleanup = """
            DELETE FROM dbo.Kit_vin_wks_status_cache
            WHERE updated_at < DATEADD(week, -@semanasRetener, GETUTCDATE())
               OR (KitsCompTot >= VinCant
                   AND VinCant > 0
                   AND updated_at < DATEADD(hour, -@horasCompletadas, GETUTCDATE()))
            """;

        await conn.ExecuteAsync(sqlCleanup, new
        {
            semanasRetener = _semanasRetener,
            horasCompletadas = _horasCompletadas
        });

        sw.Stop();
        _logger.LogDebug(
            "RefreshCache done | wkname={Wk} tipos={T} elapsed={E}ms",
            wkname, string.Join("/", tipos), sw.ElapsedMilliseconds);
    }

    /// <inheritdoc/>
    public async Task<int> CacheCleanupAsync(
        int semanasRetener, int horasCompletadas, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // @@ROWCOUNT captura las filas afectadas por el DELETE inmediatamente anterior
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