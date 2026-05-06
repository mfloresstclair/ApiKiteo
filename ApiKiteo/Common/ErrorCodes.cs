namespace ApiKiteo.API.Common;

/// <summary>
/// Códigos de error estándar. Replicados desde la API Python original.
/// </summary>
public static class ErrorCodes
{
    public const string Auth400    = "AUTH_400";
    public const string Auth401    = "AUTH_401";
    public const string AuthNoAccess = "AUTH_NO_ACCESS";
    public const string Auth500    = "AUTH_500";

    public const string Kiteo400   = "KITEO_400";
    public const string Kiteo404   = "KITEO_404";
    public const string Kiteo500   = "KITEO_500";

    public const string Admin400   = "ADMIN_400";
    public const string Admin500   = "ADMIN_500";
    public const string Admin404 = "ADMIN_404";  
    public const string Admin409 = "ADMIN_409";
}
