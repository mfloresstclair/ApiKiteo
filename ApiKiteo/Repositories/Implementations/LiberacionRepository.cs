using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;
using Dapper;
using Microsoft.Extensions.Options;
using System.Data;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class LiberacionRepository : ILiberacionRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public LiberacionRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> GetSemanasAsync(
        string estatus = "PendienteCorte", string cliente = "TODOS",
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.LiberacionSemanas,
            new { estatus, cliente },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> CrearLoteAsync(
        string jsonWknames, string username, bool sobreescribir,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.LiberacionCrear,
            new { username, jsonwks = jsonWknames, sobreescribir },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<dynamic> Resumen, IEnumerable<dynamic> Detalle)> GetMaterialAsync(
        string jsonWknames, string username, string cliente,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // El SP ahora SIEMPRE devuelve 2 result sets — GridReader requerido.
        // Timeout generoso — incluye carga del lado TBB (CombinedOverlays).
        using var grid = await conn.QueryMultipleAsync(
            _sp.WksLiberacion,
            new { username, jsonwks = jsonWknames, cliente },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 120);

        var resumen = await grid.ReadAsync<dynamic>();
        var detalle = await grid.ReadAsync<dynamic>();

        return (resumen, detalle);
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<dynamic> Lote, IEnumerable<dynamic> Semanas)> GetLoteAsync(
        int loteId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        using var grid = await conn.QueryMultipleAsync(
            _sp.LiberacionGet,
            new { lote_id = loteId },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);

        var lote = await grid.ReadAsync<dynamic>();
        var semanas = await grid.ReadAsync<dynamic>();

        return (lote, semanas);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> IngresarCorteAsync(
        int loteId, string wkname, int semana, int anio, string username,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.CorteIngresar,                              // ← _sp.CorteIngresar
            new { lote_id = loteId, wkname, semana, anio, username },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> LiberacionListAsync(
        string cliente = "TODOS", CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.LiberacionList,
            new { cliente },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task<DateOnly?> GetFechaCorteAsync(
        int semana, int anio, CancellationToken ct = default)
    {
        // Inline SQL — BuildPlan.dbo.SytelineOut está en el mismo SQL Server.
        // Blank4 formato: [semana sin cero][año 4 dígitos]
        // "272026" = semana 27 año 2026 | "82026" = semana 8 año 2026
        // Fecha DOMINANTE (día con más registros = corte real), no MAX:
        // un ajuste tardío de pocos registros no debe mover la fechacorte.
        const string sql = @"SELECT TOP 1 CAST(DateFetch AS DATE)
FROM BuildPlan.dbo.SytelineOut
WHERE LTRIM(RTRIM(Blank4)) = @blank4
GROUP BY CAST(DateFetch AS DATE)
ORDER BY COUNT(*) DESC;";

        // Blank4 = semana SIN cero a la izquierda + año 4 dígitos
        var blank4 = semana.ToString() + anio.ToString();   // 31 + 2026 = "312026", 8 + 2026 = "82026"

        using var conn = _db.CreateConnection();
        var result = await conn.QueryFirstOrDefaultAsync<DateTime?>(
            sql,
            new { blank4 },                                  // ← ahora sí pasa @blank4
            commandTimeout: 15);

        if (result is null) return null;
        return DateOnly.FromDateTime(result.Value);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> GuardarSnapshotAsync(
        int loteId, string username, string jsonResumen,
        string? destinatarios, string? wkEtiqueta, string? cliente, string? archivo,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.LiberacionSnapshotGuardar,
            new
            {
                lote_id     = loteId,
                username,
                jsonresumen = jsonResumen,
                destinatarios,
                wk_etiqueta = wkEtiqueta,
                cliente,
                archivo
            },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<dynamic> Lote, IEnumerable<dynamic> Resumen, IEnumerable<dynamic> Semanas)>
        GetSnapshotAsync(int loteId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        using var grid = await conn.QueryMultipleAsync(
            _sp.LiberacionSnapshotGet,
            new { lote_id = loteId },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);

        var lote = (await grid.ReadAsync<dynamic>()).ToList();

        // Si el SP hizo RETURN temprano (404) no existen RS2 ni RS3 — leerlos
        // lanzaría excepción. Mismo patrón que AdminRepository.PreviewSemanaAsync.
        var primera = lote.Count > 0 ? (IDictionary<string, object?>)lote[0] : null;
        if (primera?.ContainsKey("http_status") == true)
            return (lote, Enumerable.Empty<dynamic>(), Enumerable.Empty<dynamic>());

        var resumen = (await grid.ReadAsync<dynamic>()).ToList();
        var semanas = (await grid.ReadAsync<dynamic>()).ToList();

        return (lote, resumen, semanas);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> HistorialAsync(
        string cliente = "TODOS", int top = 50, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.LiberacionHistorial,
            new { cliente, top },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }
}