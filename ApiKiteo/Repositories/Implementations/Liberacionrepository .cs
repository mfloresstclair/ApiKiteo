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

        // @log=1 — escribe Boss_transactions (primera llamada)
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

        // @log=0 — NO escribe Boss_transactions (segunda llamada, ya se registró en resumen)
        return await conn.QueryAsync(
            _sp.Liberacion,
            new { username, jsonwks = jsonWknames, detail = "1", cliente },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 120);
    }
}