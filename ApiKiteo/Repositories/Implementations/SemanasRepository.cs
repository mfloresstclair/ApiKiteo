using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class SemanasRepository : ISemanasRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public SemanasRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    public async Task<IEnumerable<dynamic>> GetSemanasAsync(
        string cliente, string tipo, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.GetSemanas,
            new { cliente, tipo },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task<IEnumerable<dynamic>> GetSemanasPendientesAsync(
        byte filtro = 0, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // @filtro: 0 = todos (últimas 2 semanas), 1 = solo Pendiente, 2 = solo APROBADA
        return await conn.QueryAsync(
            _sp.GetSemanasPendientes,
            new { filtro },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }
}