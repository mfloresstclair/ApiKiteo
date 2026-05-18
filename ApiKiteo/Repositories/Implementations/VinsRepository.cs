using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class VinsRepository : IVinsRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public VinsRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    public async Task<IEnumerable<dynamic>> GetSemanaLocAsync(
        string wkname, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.GetSemanaLoc,
            new { wkname },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task<IEnumerable<dynamic>> GetSemanaGrpStatusAsync(
        string wkname, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.GetSemanaGrpStatus,
            new { wkname },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task<IEnumerable<dynamic>> GetSemanaGrpFaltantesAsync(
        string wkname, string jsonGrupos, string det, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // El SP espera: @wkname, @jsonGrupos, @det
        return await conn.QueryAsync(
            _sp.GetSemanaGrpFaltantes,
            new { wkname, jsonGrupos, det },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 60);   // puede tomar más tiempo (cálculo de faltantes)
    }

    public async Task<IEnumerable<dynamic>> GetSemanaVinStatusAsync(
        string wkname, string cliente, string tipo, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.GetSemanaVinStatus,
            new { wkname, cliente, tipo },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }
    public async Task<IEnumerable<dynamic>> BuscarCircuitoAsync(
        string wkname, string circuito, string soloFaltantes,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // soloFaltantes es varchar(1) en el SP: '0' = todos, '1' = solo Pendiente.
        return await conn.QueryAsync(
            _sp.BuscarCircuito,
            new { wkname, circuito, soloFaltantes },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }
}
