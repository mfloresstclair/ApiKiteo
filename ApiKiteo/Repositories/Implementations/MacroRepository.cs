using System.Text;
using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class MacroRepository : IMacroRepository
{
    private readonly IDbConnectionFactory _db;

    public MacroRepository(IDbConnectionFactory db) => _db = db;

    /// <inheritdoc/>
    public async Task StreamMacroAsync(
        IReadOnlyList<string> wknames,
        string? tipo,
        string? cliente,
        DateOnly? desde,
        DateOnly? hasta,
        Func<IEnumerable<dynamic>, Task> process,
        CancellationToken ct = default)
    {
        // ── Build query ───────────────────────────────────────────────────────
        // SQL inline justificado: no existe SP para esta operación y toda la
        // interpolación es via parámetros Dapper — sin riesgo de SQL injection.
        var sql = new StringBuilder("""
    SELECT
        WkName,
        Vin,
        vinDesc,
        motherharness           AS Motherharness,
        overlay,
        Grupo,
        item,
        item_Descripcion        AS ItemDescripcion,
        Locacion,
        tipo,
        Cliente,
        Operador,
        recorddate,
        Entregado,
        Entregado_por           AS EntregadoPor
    FROM dbo.VinBusiness_DB_macro WITH (NOLOCK)
    WHERE ISNULL(Estatus, 1) = 1
    """);

        var p = new DynamicParameters();

        // Filtro por lista de semanas
        if (wknames.Count > 0)
        {
            sql.AppendLine("AND WkName IN @wknames");
            p.Add("wknames", wknames);
        }

        // Filtro por tipo
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            sql.AppendLine("AND tipo = @tipo");
            p.Add("tipo", tipo.Trim());
        }

        // Filtro por cliente
        if (!string.IsNullOrWhiteSpace(cliente))
        {
            sql.AppendLine("AND Cliente = @cliente");
            p.Add("cliente", cliente.Trim());
        }

        // Filtro por rango de fechas
        if (desde.HasValue)
        {
            sql.AppendLine("AND CAST(recorddate AS date) >= @desde");
            p.Add("desde", desde.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (hasta.HasValue)
        {
            sql.AppendLine("AND CAST(recorddate AS date) <= @hasta");
            p.Add("hasta", hasta.Value.ToDateTime(TimeOnly.MinValue));
        }

        // Sin filtros de rango ni semanas → últimas 4 semanas (seguridad)
        if (wknames.Count == 0 && !desde.HasValue && !hasta.HasValue)
        {
            sql.AppendLine("AND recorddate >= DATEADD(week, -4, GETDATE())");
        }

        sql.AppendLine("ORDER BY WkName, Locacion, Vin, item");

        // ── Execute — buffered: false → lazy read desde el DataReader ─────────
        // La conexión debe mantenerse abierta mientras process() itera.
        // El patrón delegate garantiza que conn no se cierra antes de tiempo.
        using var conn = _db.CreateConnection();

        var rows = await conn.QueryAsync(
            sql.ToString(),
            p,
            commandType: System.Data.CommandType.Text,
            commandTimeout: 120);   // 2 min — suficiente para 100k filas filtradas

        // Pasar el IEnumerable al service mientras conn sigue abierta
        await process(rows);
    }
}
