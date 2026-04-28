namespace ApiKiteo.API.Common;

/// <summary>
/// Extensiones para acceso seguro a filas dinámicas de Dapper.
/// Maneja variaciones de casing en nombres de columnas (SQL Server es case-insensitive,
/// pero IDictionary en C# no lo es por defecto).
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// Busca una clave de forma case-insensitive y devuelve el valor como string.
    /// </summary>
    public static string? GetStr(this IDictionary<string, object?> d, string key)
    {
        var val = d.FindValue(key);
        return val?.ToString();
    }

    /// <summary>
    /// Busca una clave de forma case-insensitive y devuelve el valor como int?.
    /// </summary>
    public static int? GetInt(this IDictionary<string, object?> d, string key)
    {
        var val = d.FindValue(key);
        if (val is null) return null;
        return int.TryParse(val.ToString(), out var i) ? i : null;
    }

    /// <summary>
    /// Busca una clave de forma case-insensitive y devuelve el valor como decimal?.
    /// </summary>
    public static decimal? GetDecimal(this IDictionary<string, object?> d, string key)
    {
        var val = d.FindValue(key);
        if (val is null) return null;
        return decimal.TryParse(
            val.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var dec) ? dec : null;
    }

    /// <summary>
    /// Busca una clave de forma case-insensitive y devuelve el valor raw.
    /// Equivalente a dict.get() de Python.
    /// </summary>
    public static object? GetValueOrDefault(this IDictionary<string, object?> d, string key)
        => d.FindValue(key);

    // ── Búsqueda case-insensitive ────────────────────────────────────────────

    private static object? FindValue(this IDictionary<string, object?> d, string key)
    {
        // Búsqueda exacta primero (más rápida)
        if (d.TryGetValue(key, out var val)) return val;

        // Búsqueda case-insensitive como fallback
        foreach (var kv in d)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return null;
    }
}
