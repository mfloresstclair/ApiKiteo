using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

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
        int loteId, string wkname, string fechacorte, string username,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.CorteIngresar,
            new { lote_id = loteId, wkname, fechacorte, username },
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

}