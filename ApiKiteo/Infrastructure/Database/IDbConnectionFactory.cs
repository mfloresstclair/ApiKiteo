using Microsoft.Data.SqlClient;

namespace ApiKiteo.API.Infrastructure.Database;

/// <summary>
/// Abstracción de la fábrica de conexiones SQL.
/// Facilita el testing con mocks sin tocar la infraestructura real.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Crea una nueva <see cref="SqlConnection"/> (sin abrir).</summary>
    SqlConnection CreateConnection();
}
