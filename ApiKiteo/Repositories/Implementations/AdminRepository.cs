using Dapper;
using Microsoft.Extensions.Options;
using KiteoAdmin.API.Configuration;
using KiteoAdmin.API.Infrastructure.Database;
using KiteoAdmin.API.Repositories.Interfaces;

namespace KiteoAdmin.API.Repositories.Implementations;

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
}
