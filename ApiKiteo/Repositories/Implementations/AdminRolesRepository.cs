using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class AdminRolesRepository : IAdminRolesRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public AdminRolesRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    public async Task<IEnumerable<dynamic>> GetRolesAsync(
        string site, string access, bool includeInactive,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SP espera: @site varchar(3), @access varchar(50),
        //            @includeInactive tinyint
        return await conn.QueryAsync(
            _sp.GetAdminRolesList,
            new
            {
                site,
                access,
                includeInactive = includeInactive ? 1 : 0
            },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task<IEnumerable<dynamic>> AddRoleAsync(
        string username, string fullName, string access,
        string site, string createdBy,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SP espera: @username, @fullname, @access, @site, @createdBy
        // Devuelve: http_status, code, message [, id_num, username, fullName, access, site]
        return await conn.QueryAsync(
            _sp.AdminRoleAdd,
            new { username, fullName, access, site, createdBy },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task<IEnumerable<dynamic>> RemoveRoleAsync(
        int idNum, string removedBy,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SP espera: @id_num int, @removedBy varchar(50)
        // Devuelve: http_status, code, message [, id_num, username, access]
        return await conn.QueryAsync(
            _sp.AdminRoleRemove,
            new { id_num = idNum, removedBy },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task<IEnumerable<dynamic>> UpdateRoleAsync(
        int idNum, string access, string updatedBy,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SP espera: @id_num int, @access varchar(50), @updatedBy varchar(50)
        // Devuelve: http_status, code, message [, id_num, username, access_anterior, access_nuevo]
        return await conn.QueryAsync(
            _sp.AdminRoleUpdate,
            new { id_num = idNum, access, updatedBy },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }
}