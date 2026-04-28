using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

public sealed class EmpleadosRepository : IEmpleadosRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly StoredProceduresOptions _sp;

    public EmpleadosRepository(
        IDbConnectionFactory db,
        IOptions<StoredProceduresOptions> sp)
    {
        _db = db;
        _sp = sp.Value;
    }

    public async Task<string?> GetNombreEmpleadoAsync(
        string empleado, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // El SP devuelve una fila con columna "nombre" si existe, vacío si no.
        var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
            _sp.CheckEmpleado,
            new { empleado },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);

        if (row is null) return null;

        var dict = (IDictionary<string, object?>)row;

        return dict.TryGetValue("nombre", out var val)
            ? val?.ToString()
            : null;
    }
}
