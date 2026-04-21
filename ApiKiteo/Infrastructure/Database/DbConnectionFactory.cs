using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using KiteoAdmin.API.Infrastructure.Cryptography;

namespace KiteoAdmin.API.Infrastructure.Database;

/// <summary>
/// Fábrica de conexiones SQL Server.
///
/// Estrategia de resolución de cadena de conexión:
///
///   1. Variables de entorno Thragg + DvT (disponibles en todas las PCs)
///      → descifra AES-CBC → normaliza ODBC → SqlClient
///      → si DatabaseOverride tiene valor, reemplaza el nombre de la DB
///        (útil en Dev para apuntar a DevTest en lugar de BOS)
///
///   2. ConnectionStrings:KiteoDB en appsettings (fallback / override local)
///
/// Singleton — la cadena se resuelve una sola vez al arrancar.
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

    public SqlConnection CreateConnection() => new(_connectionString);

    // ─────────────────────────────────────────────────────────────────────────

    private string ResolveConnectionString(IConfiguration config)
    {
        string cs;

        // ── Opción A: AES decrypt ─────────────────────────────────────────────
        var aesKey = Environment.GetEnvironmentVariable("Thragg");
        var encryptedCs = Environment.GetEnvironmentVariable("DvT");

        if (!string.IsNullOrWhiteSpace(aesKey) && !string.IsNullOrWhiteSpace(encryptedCs))
        {
            _logger.LogInformation("Resolviendo cadena de conexión vía AES decrypt (Thragg/DvT).");
            var raw = AesDecryptor.Decrypt(encryptedCs, aesKey);
            cs = NormalizeOdbcToSqlClient(raw);
        }
        else
        {
            // ── Opción B: cadena plana desde appsettings (fallback) ───────────
            var plain = config.GetConnectionString("KiteoDB");
            if (string.IsNullOrWhiteSpace(plain))
                throw new InvalidOperationException(
                    "No se encontró cadena de conexión. " +
                    "Verifica que las variables de entorno Thragg y DvT estén disponibles, " +
                    "o configura ConnectionStrings:KiteoDB en appsettings.Development.json.");

            _logger.LogInformation("Resolviendo cadena de conexión desde appsettings (fallback).");
            cs = plain;
        }

        // ── Override de base de datos (Dev = DevTest, Prod = BOS del DvT) ────
        // Si DatabaseOverride tiene valor en appsettings, reemplaza la DB
        // en la cadena descifrada. En Production este valor está vacío.
        var dbOverride = config["DatabaseOverride"];
        if (!string.IsNullOrWhiteSpace(dbOverride))
        {
            cs = OverrideDatabase(cs, dbOverride);
            _logger.LogInformation("Base de datos sobreescrita por DatabaseOverride: {Db}", dbOverride);
        }

        cs = EnsureEncryptionHandled(cs);
        LogSafe(cs);
        return cs;
    }

    /// <summary>
    /// Reemplaza el nombre de la base de datos en la cadena de conexión.
    /// Soporta Database= e Initial Catalog= (ambos formatos).
    /// </summary>
    private static string OverrideDatabase(string cs, string dbName)
    {
        // Reemplazar Database=valor
        if (Regex.IsMatch(cs, @"(?i)\bDatabase\s*="))
            return Regex.Replace(cs, @"(?i)\bDatabase\s*=[^;]*", $"Database={dbName}");

        // Reemplazar Initial Catalog=valor
        if (Regex.IsMatch(cs, @"(?i)\bInitial\s+Catalog\s*="))
            return Regex.Replace(cs, @"(?i)\bInitial\s+Catalog\s*=[^;]*", $"Initial Catalog={dbName}");

        // No tiene database — agregar al final
        if (!cs.TrimEnd().EndsWith(";"))
            cs += ";";
        return cs + $"Database={dbName};";
    }

    /// <summary>
    /// Convierte tokens ODBC heredados al formato de Microsoft.Data.SqlClient.
    /// Replica la normalización del Python original.
    /// </summary>
    private static string NormalizeOdbcToSqlClient(string raw)
    {
        var cs = Regex.Replace(raw.Trim(), @";\s+", ";");

        if (!cs.EndsWith(";"))
            cs += ";";

        cs = Regex.Replace(cs, @"(?i)\bData\s*Source\s*=", "Server=");
        cs = Regex.Replace(cs, @"(?i)\bUser\s*ID\s*=", "User Id=");
        cs = Regex.Replace(cs, @"(?i)\bPassword\s*=", "Password=");

        return cs;
    }

    /// <summary>
    /// Microsoft.Data.SqlClient v4+ cifra por defecto.
    /// En redes internas sin cert de CA de confianza, forzar Encrypt=False.
    /// </summary>
    private static string EnsureEncryptionHandled(string cs)
    {
        if (Regex.IsMatch(cs, @"(?i)\bEncrypt\s*="))
            return cs;

        if (!cs.TrimEnd().EndsWith(";"))
            cs += ";";

        if (!Regex.IsMatch(cs, @"(?i)\bTrustServerCertificate\s*="))
            cs += "TrustServerCertificate=True;";

        return cs + "Encrypt=False;";
    }

    private void LogSafe(string cs)
    {
        var safe = Regex.Replace(cs, @"(?i)Password\s*=[^;]*", "Password=***");
        _logger.LogDebug("Connection string activa: {Cs}", safe);
    }
}