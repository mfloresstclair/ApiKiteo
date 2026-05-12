using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class MandarFinalRepository : IMandarFinalRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public MandarFinalRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

   
    public async Task<IEnumerable<dynamic>> GetParentsAsync(
        string sitio, string search, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // El SP devuelve TOP 20. @search = '' desactiva el filtro de búsqueda.
        return await conn.QueryAsync(
            _sp.MandarFinalParents,
            new { sitio, search },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> GetPorParentAsync(
        string sitio, string parentItem, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // El SP recibe @sitio y @parentItem, calcula @monday internamente.
        return await conn.QueryAsync(
            _sp.MandarFinalPorParent,
            new { sitio, parentItem },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> GetListAsync(
        bool includeInactive, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // @includeInactive tinyint: 0 = solo activos, 1 = todos.
        return await conn.QueryAsync(
            _sp.MandarFinalList,
            new { includeInactive = includeInactive ? 1 : 0 },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> AddItemsAsync(
        string jsonItems, string usuario, string sitio,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // @sitio vacío desactiva la validación contra CNDetalle.
        return await conn.QueryAsync(
            _sp.MandarFinalAdd,
            new { jsonItems, usuario, sitio },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 60);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<dynamic>> RemoveItemsAsync(
        string jsonItems, string usuario, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        return await conn.QueryAsync(
            _sp.MandarFinalRemove,
            new { jsonItems, usuario },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 60);
    }
}