using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class DescaneoRepository : IDescaneoRepository
{
    private readonly IDbConnectionFactory    _db;
    private readonly StoredProceduresOptions _sp;

    public DescaneoRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    public async Task<IEnumerable<dynamic>> BuscarAsync(
        string?  wkname,
        string?  vin,
        string?  item,
        string?  operador,
        string?  cliente,
        DateOnly? fechaDesde,
        DateOnly? fechaHasta,
        byte     modo,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SP: Kit_vin_descan_buscar
        // Todos los parámetros son opcionales excepto @modo
        return await conn.QueryAsync(
            _sp.DescanBuscar,
            new
            {
                wkname,
                vin,
                item,
                operador,
                cliente,
                fecha_desde = fechaDesde.HasValue ? (DateTime?)fechaDesde.Value.ToDateTime(TimeOnly.MinValue) : null,
                fecha_hasta = fechaHasta.HasValue ? (DateTime?)fechaHasta.Value.ToDateTime(TimeOnly.MinValue) : null,
                modo
            },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }

    public async Task<dynamic?> AplicarAsync(
        int id, string username, string motivo,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // SP: Kit_vin_descaneo_aplicar
        // Siempre devuelve exactamente 1 fila con http_status, code, message
        return await conn.QueryFirstOrDefaultAsync(
            _sp.DescaneoAplicar,
            new { id, username, motivo },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);
    }
}
