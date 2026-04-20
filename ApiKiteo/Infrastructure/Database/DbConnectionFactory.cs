using Microsoft.Data.SqlClient;
using KiteoAdmin.API.Infrastructure.Cryptography;

namespace KiteoAdmin.API.Infrastructure.Database;

/// <summary>
/// Fábrica de conexiones SQL Server.
/// 
/// Estrategia de resolución de cadena de conexión (por prioridad):
///   1. Variables de entorno KITEO_AES_KEY + KITEO_CONN_ENCRYPTED  → descifra AES
///   2. ConnectionStrings:DevTest en configuración (user-secrets / env var)
/// 
/// Registrada como Singleton — la cadena se resuelve una sola vez al arrancar.
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
        // Opción A: AES encriptado (equivalente al Python original)
        var aesKey      = Environment.GetEnvironmentVariable("Thragg");
        var encryptedCs = Environment.GetEnvironmentVariable("DvT");

        if (!string.IsNullOrWhiteSpace(aesKey) && !string.IsNullOrWhiteSpace(encryptedCs))
        {
            _logger.LogInformation("Resolviendo cadena de conexión vía AES decrypt.");
            var raw = AesDecryptor.Decrypt(encryptedCs, aesKey);
            return NormalizeOdbcToSqlClient(raw);
        }

        // Opción B: cadena plana desde user-secrets / env var
        var plain = config.GetConnectionString("KiteoDB");
        if (!string.IsNullOrWhiteSpace(plain))
        {
            _logger.LogInformation("Resolviendo cadena de conexión desde configuración.");
            return plain;
        }

        throw new InvalidOperationException(
            "No se encontró cadena de conexión. " +
            "Configura KITEO_AES_KEY + KITEO_CONN_ENCRYPTED, " +
            "o ConnectionStrings:DevTest en user-secrets.");
    }

    /// <summary>
    /// Convierte tokens ODBC heredados (Data Source=, User ID=, Password=)
    /// al formato que entiende Microsoft.Data.SqlClient (Server=, UID=, PWD=).
    /// Replica la normalización del Python original.
    /// </summary>
    private static string NormalizeOdbcToSqlClient(string raw)
    {
        // Si ya tiene 'Server=' no hace falta normalizar
        if (raw.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            return raw;

        return raw
            .Replace("Data Source=",  "Server=",   StringComparison.OrdinalIgnoreCase)
            .Replace("User ID=",      "User Id=",   StringComparison.OrdinalIgnoreCase)
            .Replace("Password=",     "Password=",  StringComparison.OrdinalIgnoreCase);
    }
}
