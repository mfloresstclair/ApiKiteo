using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Models.Responses;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public AuthRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    public async Task<IEnumerable<UserAccessRow>> GetUserAccessAsync(
        string username, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<UserAccessRow>(
            _sp.GetUserAccess,
            new { username },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }
}
