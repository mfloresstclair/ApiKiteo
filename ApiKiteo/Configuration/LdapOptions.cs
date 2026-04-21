namespace KiteoAdmin.API.Configuration;

/// <summary>
/// Configuración de Active Directory.
/// 
/// Con PrincipalContext solo se necesita el nombre del dominio.
/// No requiere host, puerto ni configuración SSL.
/// </summary>
public sealed class LdapOptions
{
    public const string SectionName = "LdapOptions";

    /// <summary>
    /// Nombre NetBIOS del dominio — ej: "STCLAIRTECH".
    /// Si está vacío se usa Environment.UserDomainName (dominio de la máquina).
    /// </summary>
    public string Domain { get; init; } = string.Empty;
}
