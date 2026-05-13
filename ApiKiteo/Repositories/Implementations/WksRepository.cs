using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class WksRepository : IWksRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public WksRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> GetStatusBoardAsync(
        string jsonWkname, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // El SP hace varios UPDATEs sobre table variables internamente,
        // por lo que puede tardar más de lo usual en listas grandes.
        // Timeout generoso: 60s.
        return await conn.QueryAsync(
            _sp.WksStatusBoard,
            new { jsonWkname },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 60);
    }
}
