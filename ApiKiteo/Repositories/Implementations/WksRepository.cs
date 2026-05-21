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

    // ── Límites del auto-cleanup (CacheSettings en appsettings) ──────────────
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

        // Lee del cache Kit_vin_wks_status_cache — respuesta <10ms.
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

        // ── Parsear wkname ────────────────────────────────────────────────────
        // Formato: wk21_142_CEA | wk20_111_ZC/ZD | wk23_134_ZA
        var partes = wkname.Split('_');
        if (partes.Length < 3) return;

        var wk = partes[0];
        var vinCant = int.TryParse(partes[1], out var v) ? v : 0;
        var typeRaw = string.Join("_", partes[2..]);

        // Tipos compuestos (ZC/ZD) se expanden en filas separadas del cache.
        // El '/' es la señal de tipo compuesto — no se hardcodea ningún nombre.
        var tipos = typeRaw.Split('/');
        var esCompuesto = tipos.Length > 1;

        using var conn = _db.CreateConnection();

        // ── Cliente desde VinBusiness_DB_macro ────────────────────────────────
        // Lectura directa del dato real en BD — sin mapping en config.
        // Si el wkname aún no tiene rows (semana vacía), devuelve string.Empty.
        var cliente = await conn.ExecuteScalarAsync<string?>(
            "SELECT TOP 1 CLIENTE FROM dbo.VinBusiness_DB_macro WITH (NOLOCK) WHERE WkName = @wkname",
            new { wkname }) ?? string.Empty;

        foreach (var tipo in tipos)
        {
            var tipoTrim = tipo.Trim();

            // ── Filtro dinámico por tipo compuesto ────────────────────────────
            // Tipos compuestos (ZC/ZD) comparten wkname en VinBusiness_DB_macro.
            // Se distinguen por vinDesc: BodyCVZC_% / BodyCVZD_%.
            // Tipos simples (CEA, ZA, C2...) → todos los rows del wkname son del mismo tipo.
            // La regla del '/' elimina el hardcoding de ('ZC','ZD') — cualquier
            // tipo compuesto futuro funciona automáticamente.
            var filtroVinDesc = esCompuesto
                ? "AND vinDesc LIKE 'BodyCV' + @tipo + '\\_%' ESCAPE '\\'"
                : string.Empty;

            // 1) Porcentaje escaneado
            var sqlPorc = $"""
                SELECT
                    100.0 * SUM(CASE WHEN Operador IS NOT NULL THEN 1 ELSE 0 END)
                          / NULLIF(COUNT(*), 0)
                FROM dbo.VinBusiness_DB_macro WITH (NOLOCK)
                WHERE WkName = @wkname
                {filtroVinDesc}
                """;

            var porc = await conn.ExecuteScalarAsync<decimal?>(
                sqlPorc, new { wkname, tipo = tipoTrim }) ?? 0m;

            // 2) Kits completos kiteados vs entregados
            var sqlKits = $"""
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
                    {filtroVinDesc}
                    GROUP BY Vin
                ) x
                """;

            var kits = (IDictionary<string, object?>?)await conn.QuerySingleOrDefaultAsync(
                sqlKits, new { wkname, tipo = tipoTrim });

            var kitsComp = Convert.ToInt32(kits?.GetValueOrDefault("kitsComp") ?? 0);
            var kitsCompFinal = Convert.ToInt32(kits?.GetValueOrDefault("KitsCompFinal") ?? 0);
            var kitsCompTot = kitsComp + kitsCompFinal;

            // 3) UPSERT — DELETE + INSERT
            const string sqlDelete = """
                DELETE FROM dbo.Kit_vin_wks_status_cache
                WHERE wkname = @wkname AND tipo = @tipo
                """;

            const string sqlInsert = """
                INSERT INTO dbo.Kit_vin_wks_status_cache
                    (wkname, wk, tipo, cliente, VinCant, kitsComp, KitsCompFinal,
                     KitsCompTot, Porc, updated_at)
                VALUES
                    (@wkname, @wk, @tipo, @cliente, @vinCant, @kitsComp, @kitsCompFinal,
                     @kitsCompTot, @porc, GETUTCDATE())
                """;

            await conn.ExecuteAsync(sqlDelete, new { wkname, tipo = tipoTrim });
            await conn.ExecuteAsync(sqlInsert, new
            {
                wkname,
                wk,
                tipo = tipoTrim,
                cliente,
                vinCant,
                kitsComp,
                kitsCompFinal,
                kitsCompTot,
                porc = Math.Round(porc, 2)
            });
        }

        // 4) Auto-cleanup — límites desde CacheSettings en appsettings
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
            "RefreshCache done | wkname={Wk} tipos={T} cliente={C} elapsed={E}ms",
            wkname, string.Join("/", tipos), cliente, sw.ElapsedMilliseconds);
    }

    /// <inheritdoc/>
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