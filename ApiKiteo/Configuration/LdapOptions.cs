namespace ApiKiteo.API.Configuration;

/// <summary>
/// Configuración LDAP — Novell.Directory.Ldap.NETStandard (cross-platform).
/// Dominio: STCLAIR.STCLAIRTECH.COM
/// </summary>
public sealed class LdapOptions
{
    public const string SectionName = "LdapOptions";

    /// <summary>
    /// Hostname del Domain Controller alcanzable desde este servidor.
    /// SSSD ya lo usa → la misma dirección funciona para LDAP directo.
    /// Ejemplo: "stclair.stclairtech.com" o IP del DC primario.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>Puerto LDAP. 389 = sin SSL (red interna). 636 = con LDAPS.</summary>
    public int Port { get; init; } = 389;

    /// <summary>
    /// NetBIOS domain name — para bind "NETBIOS\usuario".
    /// Verificar con: wbinfo --own-domain
    /// Probable: "STCLAIR"
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// FQDN del dominio — para bind UPN "usuario@fqdn" (preferido).
    /// Ejemplo: "stclair.stclairtech.com"
    /// </summary>
    public string DomainFqdn { get; init; } = string.Empty;

    /// <summary>LDAPS. Default false — red interna de planta.</summary>
    public bool UseSsl { get; init; } = false;
}