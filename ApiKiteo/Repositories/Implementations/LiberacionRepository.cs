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
    public async Task<IEnumerable<dynamic>> GetResumenAsync(
        string jsonWknames, string username, string cliente,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        // El SP valida duplicados y registra en Kit_vin_liberacion.
        // Timeout generoso — incluye la carga del lado TBB (CombinedOverlays).
        return await conn.QueryAsync(
            _sp.Liberacion,
            new { username, jsonwks = jsonWknames, detail = "0", cliente },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 120);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> GetDetalleAsync(
        string jsonWknames, string username, string cliente,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        // @detail='1' — el lote ya fue creado por GetResumenAsync.
        // Ambas llamadas registran en Boss_transactions (doble log aceptado).
        return await conn.QueryAsync(
            _sp.Liberacion,
            new { username, jsonwks = jsonWknames, detail = "1", cliente },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 120);
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<dynamic> Lote, IEnumerable<dynamic> Semanas)> GetLoteAsync(
        int loteId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // 2 result sets — GridReader requerido
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
}