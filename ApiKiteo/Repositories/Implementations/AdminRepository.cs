using Dapper;
using Microsoft.Extensions.Options;
using ApiKiteo.API.Configuration;
using ApiKiteo.API.Infrastructure.Database;
using ApiKiteo.API.Repositories.Interfaces;

namespace ApiKiteo.API.Repositories.Implementations;

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

    public async Task<(IEnumerable<dynamic> Resumen, IEnumerable<dynamic> Detalle)>
        PreviewSemanaAsync(string wkname, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // QueryMultipleAsync es el único método de Dapper que maneja múltiples result sets.
        // El GridReader debe consumirse en orden — primero resumen, luego detalle.
        using var multi = await conn.QueryMultipleAsync(
            _sp.PreviewSemana,
            new { wkname },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 30);

        // Result set 1: resumen (1 fila) — o fila de error (400/404)
        var resumen = (await multi.ReadAsync<dynamic>()).ToList();

        // Solo leer el result set 2 si el SP no devolvió error en el primero.
        // Si hay http_status en la primera fila, el SP hizo RETURN temprano
        // y no existe un segundo result set — intentar leerlo lanzaría excepción.
        var primeraFila = resumen.Count > 0
            ? (IDictionary<string, object?>)resumen[0]
            : null;

        var esError = primeraFila?.ContainsKey("http_status") == true;

        var detalle = esError
            ? Enumerable.Empty<dynamic>()
            : (await multi.ReadAsync<dynamic>()).ToList();

        return (resumen, detalle);
    }

    public async Task<bool> WkNameExistsInMacroAsync(
        string wkname, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // Consulta directa sin SP — simple COUNT para la guarda previa.
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.VinBusiness_DB_macro WITH (NOLOCK)
            WHERE WkName = @wkname
            """;

        var count = await conn.ExecuteScalarAsync<int>(sql, new { wkname });
        return count > 0;
    }

    public async Task<(IEnumerable<dynamic> Metadata, IEnumerable<dynamic> Registros)>
        CrearDbAsync(string wkname, string? wknamerename, string? usuario,
            CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // Timeout generoso: 300s — el SP hace CTEs pesadas + INSERT masivo.
        // Result set 1: metadata (wkname, wknamedata, descripcion, cliente, tipo) — 1 fila.
        // Result set 2: SELECT final desde VinBusiness_DB_macro — se cuentan en el service,
        //               no se devuelven completas al cliente para evitar timeout de red.
        using var multi = await conn.QueryMultipleAsync(
            _sp.CrearDb,
            new
            {
                wkname,
                wknamerename = string.IsNullOrWhiteSpace(wknamerename) ? null : wknamerename,
                usuario
            },
            commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 300);

        var metadata = (await multi.ReadAsync<dynamic>()).ToList();
        var registros = (await multi.ReadAsync<dynamic>()).ToList();

        return (metadata, registros);
    }
}