namespace KiteoAdmin.API.Configuration;

/// <summary>
/// Configuración de Active Directory / LDAP.
/// BindPassword NO va en appsettings.json — viene de user-secrets o env var.
/// </summary>
public sealed class LdapOptions
{
    public const string SectionName = "LdapOptions";

    public string Host         { get; init; } = string.Empty;
    public int    Port         { get; init; } = 636;
    public bool   UseSsl       { get; init; } = true;
    public string Domain       { get; init; } = string.Empty;   // STCLAIRTECH
    public string BaseDn       { get; init; } = string.Empty;
    public string BindDn       { get; init; } = string.Empty;
    public string BindPassword { get; init; } = string.Empty;   // ← user-secret
}
