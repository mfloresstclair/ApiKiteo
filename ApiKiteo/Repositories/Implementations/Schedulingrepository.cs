using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class SchedulingRepository : ISchedulingRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public SchedulingRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<dynamic> Semanas, IEnumerable<dynamic>? Detalle)> GetAsync(
        string? wkname, string cliente, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // El SP devuelve 1 o 2 result sets dependiendo de si @wkname tiene valor.
        // Con @wkname = NULL  → solo RS1 (selector de semanas)
        // Con @wkname = valor → RS1 + RS2 (selector + detalle de la semana)
        // Se usa QueryMultipleAsync siempre; cuando @wkname es null intentamos
        // leer RS2 solo si hay datos disponibles.

        using var grid = await conn.QueryMultipleAsync(
            _sp.Scheduling,
            new { wkname = wkname ?? (object)DBNull.Value, cliente },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 60);

        var semanas = await grid.ReadAsync<dynamic>();

        // RS2 solo existe cuando se pasó @wkname
        IEnumerable<dynamic>? detalle = null;
        if (!string.IsNullOrWhiteSpace(wkname))
            detalle = await grid.ReadAsync<dynamic>();

        return (semanas, detalle);
    }
}