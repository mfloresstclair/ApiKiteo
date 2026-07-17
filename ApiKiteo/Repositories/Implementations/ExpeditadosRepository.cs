
using System.Data;
using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class ExpeditadosRepository : IExpeditadosRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public ExpeditadosRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    public async Task<(IEnumerable<dynamic>, IEnumerable<dynamic>)> DetectarAsync(
        bool soloReportar, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
            _sp.ExpeditadosDetectar,
            new { solo_reportar = soloReportar ? 1 : 0 },
            commandType: CommandType.StoredProcedure,
            commandTimeout: 60, cancellationToken: ct));

        var resumen = await grid.ReadAsync();
        var pendientes = await grid.ReadAsync();
        return (resumen, pendientes);
    }

    public async Task<(IEnumerable<dynamic>, IEnumerable<dynamic>)> MoverAsync(
        string ids, string username, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
            _sp.ExpeditadosMover,
            new { ids, username },
            commandType: CommandType.StoredProcedure,
            commandTimeout: 60, cancellationToken: ct));

        var resultado = await grid.ReadAsync();

        // RS2 (vins) solo existe si http_status = 200 — el SP hace RETURN en los errores
        IEnumerable<dynamic> vins = Array.Empty<dynamic>();
        if (!grid.IsConsumed)
        {
            try { vins = await grid.ReadAsync(); }
            catch (ObjectDisposedException) { /* sin RS2: fue error */ }
        }

        return (resultado, vins);
    }

    public async Task<IEnumerable<dynamic>> IgnorarAsync(
        string ids, string username, string? motivo, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(new CommandDefinition(
            _sp.ExpeditadosIgnorar,
            new { ids, username, motivo },
            commandType: CommandType.StoredProcedure,
            commandTimeout: 30, cancellationToken: ct));
    }

    public async Task<IEnumerable<dynamic>> ValidarComunizacionAsync(
        string semana, DateOnly fechacorte, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync(new CommandDefinition(
            _sp.ValidarComunizacion,
            new { semana, fechacorte = fechacorte.ToDateTime(TimeOnly.MinValue) },
            commandType: CommandType.StoredProcedure,
            commandTimeout: 120, cancellationToken: ct));
    }
}
