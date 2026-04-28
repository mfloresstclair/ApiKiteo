using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class EscaneoRepository : IEscaneoRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public EscaneoRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    public async Task<IEnumerable<dynamic>> GetVinToAdjustAsync(
        string wkname, string item, string empleado, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(
            _sp.GetVinToAdjust,
            new { wkname, item, empleado },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task<IEnumerable<dynamic>> EscanearAjusteAsync(
        string wkname, string item, string jsonVines, string empleado,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SP espera: @wkname, @item, @jsonVines, @empleado
        // Devuelve un result-set mixto: filas EvtData + filas de VINs
        return await conn.QueryAsync(
            _sp.EscanearAjuste,
            new { wkname, item, jsonVines, empleado },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 60);
    }

    public async Task<IEnumerable<dynamic>> EscanearAsync(
        string wkname, string item, int cantidad, string empleado,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SP espera: @wkname, @item, @cantidad, @empleado
        return await conn.QueryAsync(
            _sp.Escanear,
            new { wkname, item, cantidad, empleado },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 60);
    }

    public async Task<IEnumerable<dynamic>> EntregarVinesAsync(
        string wkname, string jsonVines, string empleado,
        string comentario, string supervisor, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SP espera: @wkname, @jsonVines, @empleado, @comentario, @supervisor
        return await conn.QueryAsync(
            _sp.EntregarVines,
            new { wkname, jsonVines, empleado, comentario, supervisor },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 60);
    }
}
