using Microsoft.Data.SqlClient;

namespace KiteoAdmin.API.Infrastructure.Database;

/// <summary>
/// Fábrica de conexiones SQL Server.
///
/// Resolución de cadena de conexión por prioridad:
///   Dev  → appsettings.Development.json  (ConnectionStrings:KiteoDB)
///   Prod → variable de entorno            (ConnectionStrings__KiteoDB)
///
/// Singleton — la cadena se resuelve una sola vez al arrancar.
/// CreateConnection() crea una nueva SqlConnection por llamada (Dapper la abre/cierra).
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly ILogger<DbConnectionFactory> _logger;

    public DbConnectionFactory(IConfiguration config, ILogger<DbConnectionFactory> logger)
    {
        _logger = logger;
        _connectionString = ResolveConnectionString(config);
    }

    /// <inheritdoc/>
    public SqlConnection CreateConnection() => new(_connectionString);

    // ─────────────────────────────────────────────────────────────────────────

    private string ResolveConnectionString(IConfiguration config)
    {
        var cs = config.GetConnectionString("KiteoDB");

        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException(
                "No se encontró ConnectionStrings:KiteoDB. " +
                "Dev  → verificar appsettings.Development.json. " +
                "Prod → verificar variable de entorno ConnectionStrings__KiteoDB.");

        // Microsoft.Data.SqlClient v4+ cifra por defecto y valida el certificado SSL.
        // En redes internas donde SQL Server no tiene cert de CA de confianza,
        // se fuerza Encrypt=False si no viene explícito en la cadena.
        cs = EnsureEncryptionHandled(cs);

        var safe = System.Text.RegularExpressions.Regex
            .Replace(cs, @"(?i)Password=[^;]*", "Password=***");
        _logger.LogDebug("Connection string activa: {Cs}", safe);

        return cs;
    }

    /// <summary>
    /// Si la cadena no trae Encrypt= explícito agrega Encrypt=False.
    /// Evita el error de certificado SSL con Microsoft.Data.SqlClient v4+.
    /// </summary>
    private static string EnsureEncryptionHandled(string cs)
    {
        if (cs.Contains("Encrypt=", StringComparison.OrdinalIgnoreCase))
            return cs;

        if (!cs.TrimEnd().EndsWith(";"))
            cs += ";";

        return cs + "Encrypt=False;TrustServerCertificate=True;";
    }
}