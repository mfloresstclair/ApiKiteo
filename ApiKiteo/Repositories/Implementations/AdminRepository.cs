using ApiKiteo.API.Common;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;
using Dapper;
using Microsoft.Extensions.Options;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class AdminRepository : IAdminRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public AdminRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    public async Task<IEnumerable<dynamic>> AprobarSemanaAsync(
        string wkname, string aprobadoPor, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SP espera: @wkname, @aprobadoPor
        // Puede devolver rowset con http_status/message/code O nada (éxito silencioso)
        return await conn.QueryAsync(
            _sp.AprobarSemana,
            new { wkname, aprobadoPor },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task<(IEnumerable<dynamic> Resumen, IEnumerable<dynamic> Detalle)>
        PreviewSemanaAsync(string wkname, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // QueryMultipleAsync es el único método de Dapper que maneja múltiples result sets.
        // El GridReader debe consumirse en orden — primero resumen, luego detalle.
        using var multi = await conn.QueryMultipleAsync(
            _sp.PreviewSemana,
            new { wkname },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);

        // Result set 1: resumen (1 fila) — o fila de error (400/404)
        var resumen = (await multi.ReadAsync<dynamic>()).ToList();

        // Solo leer el result set 2 si el SP no devolvió error en el primero.
        // Si hay http_status en la primera fila, el SP hizo RETURN temprano
        // y no existe un segundo result set — intentar leerlo lanzaría excepción.
        var primeraFila = resumen.Count > 0
            ? (IDictionary<string, object?>)resumen[0]
            : null;

        var esError = primeraFila?.ContainsKey("http_status") == true;

        var detalle = esError
            ? Enumerable.Empty<dynamic>()
            : (await multi.ReadAsync<dynamic>()).ToList();

        return (resumen, detalle);
    }

    public async Task<bool> WkNameExistsInMacroAsync(
        string wkname, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // Consulta directa sin SP — simple COUNT para la guarda previa.
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.VinBusiness_DB_macro WITH (NOLOCK)
            WHERE WkName = @wkname
            """;

        var count = await conn.ExecuteScalarAsync<int>(sql, new { wkname });
        return count > 0;
    }

    public async Task<(IEnumerable<dynamic> Metadata, IEnumerable<dynamic> Registros)>
        CrearDbAsync(string wkname, string? wknamerename, string? usuario,
            CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // Timeout generoso: 300s — el SP hace CTEs pesadas + INSERT masivo.
        // Result set 1: metadata (wkname, wknamedata, descripcion, cliente, tipo) — 1 fila.
        // Result set 2: SELECT final desde VinBusiness_DB_macro — se cuentan en el service,
        //               no se devuelven completas al cliente para evitar timeout de red.
        using var multi = await conn.QueryMultipleAsync(
            _sp.CrearDb,
            new
            {
                wkname,
                wknamerename = string.IsNullOrWhiteSpace(wknamerename) ? null : wknamerename,
                usuario
            },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 300);

        var metadata = (await multi.ReadAsync<dynamic>()).ToList();
        var registros = (await multi.ReadAsync<dynamic>()).ToList();

        return (metadata, registros);
    }
    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> GetPreviewVinsAsync(
        string wkname, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SQL inline justificado: query de solo lectura sin lógica de negocio.
        // No existe SP para esta operación — query 100% parameterizado.
        //
        // MF 1/9/2026 — filtro de fecha. Sin él, la consulta devolvía UNA FILA POR
        // CARGA: Vines guarda un renglón por VIN por lunes de producción, así que
        // wk36_1_Body_RE1 (1 VIN, dos cargas) salía como "2 VINs" y wk36_30_Body
        // habría salido como 120. El preview mostraba cargas, no VINs.
        //
        // DISTINCT no sirve aquí: las filas NO son idénticas. Medido sobre
        // 2665979-BODYRE, HORASTOT viene 15.928 en una carga y 15.928000000000003
        // en la otra — artefacto de float. DISTINCT las dejaría pasar las dos y
        // parecería que el arreglo no hizo nada.
        //
        // MAX(fecha) es lo que lee el generador: kit_vin_crea_db usa
        // "WHERE v.fecha = @bldwkdate AND v.wkname = @wkname", y @bldwkdate es el
        // lunes de producción — que para una semana viva es justamente el máximo.
        // Un preview que no filtra enseña filas que la generación nunca va a leer.
        const string sql = """
            SELECT
                VIN,
                wkname          AS semana,
                GRUPO           AS grupo,
                Descripcion     AS descripcion,
                MODELO          AS modelo,
                motherharness,
                tipo,
                CAST(due_date AS date)      AS due_date,
                ISNULL(HORASTOT, 0)         AS horas
            FROM dbo.Vines WITH (NOLOCK)
            WHERE wkname = @wkname
              AND fecha = (SELECT MAX(fecha) FROM dbo.Vines WITH (NOLOCK)
                           WHERE wkname = @wkname)
            ORDER BY GRUPO, VIN
            """;

        return await conn.QueryAsync(sql, new { wkname }, commandTimeout: 30);
    }
    public async Task RefreshStatusCacheAsync(
        string wkname, CancellationToken ct = default)
    {
        // ── Parsear wkname en C# ──────────────────────────────────────────────
        // Formato: wk21_142_CEA | wk20_111_ZC/ZD | wk36_1_Body_RE1
        //
        // MF 31/8/2026 — se parsea sobre la BASE, no sobre el wkname crudo.
        // Antes esto era wkname.Split('_') directo, y con las semanas de
        // reordenados el tipo salía mal:
        //   wk36_1_Body_RE1    → typeRaw "Body_RE1"  (un tipo que no existe,
        //                        y así se guardaba en Kit_vin_wks_status_cache)
        //   wk20_111_ZC/ZD_RE1 → tipos ["ZC", "ZD_RE1"], y el filtro
        //                        vinDesc LIKE 'BodyCVZD_RE1\_%' no empata con
        //                        nada → Porc y kits en 0, en silencio.
        //
        // OJO: solo el TIPO se deriva de la base. Las queries de abajo y la
        // fila del cache siguen usando el wkname COMPLETO — la semana _RE1 es
        // una semana propia, con su macro y su escaneo aparte, y tiene que
        // seguir contando por separado de su base.
        var wknameBase = WknameParser.Base(wkname);

        var partes = wknameBase.Split('_');
        if (partes.Length < 3) return;

        var wk = partes[0];
        var vinCant = int.TryParse(partes[1], out var v) ? v : 0;
        var typeRaw = string.Join("_", partes[2..]);

        // Expandir tipo compuesto ZC/ZD → ["ZC", "ZD"]
        var tipos = typeRaw.Split('/');

        using var conn = _db.CreateConnection();

        foreach (var tipo in tipos)
        {
            var tipoTrim = tipo.Trim();

            // ── 1) Calcular Porc (% escaneado) ───────────────────────────────
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

            // ── 2) Calcular kitsComp / KitsCompFinal ─────────────────────────
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

            // ── 3) UPSERT — DELETE + INSERT (más limpio que MERGE en C#) ─────
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

        // ── 4) Cleanup — entradas > 8 semanas O completadas > 48h ────────────
        const string sqlCleanup = """
            DELETE FROM dbo.Kit_vin_wks_status_cache
            WHERE updated_at < DATEADD(week, -8, GETUTCDATE())
               OR (KitsCompTot >= VinCant
                   AND VinCant > 0
                   AND updated_at < DATEADD(hour, -48, GETUTCDATE()))
            """;

        await conn.ExecuteAsync(sqlCleanup);
    }
}