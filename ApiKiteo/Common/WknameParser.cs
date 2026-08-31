using System.Text.RegularExpressions;

namespace ApiKiteo.API.Common;

/// <summary>
/// Contraparte C# de <c>dbo.fn_kit_wkname_base</c> (T1, 8/2026).
///
/// El wkname tiene formato <c>wkNN_conteo_tipo</c> —
/// <c>wk36_30_Body</c>, <c>wk20_111_ZC/ZD</c>, <c>wk35_1_EPT3servicio</c> — y
/// desde el bloque de reordenados de <c>Loader_vines_wkname</c> puede traer un
/// marcador al final: <c>wk36_1_Body_RE1</c>, <c>wk29_3_Body_EXP1</c>.
///
/// MF 31/8/2026 — por qué existe esta clase.
/// La regla vivía SEIS veces en SQL con tres variantes distintas; T1 las unificó
/// en <c>fn_kit_wkname_base</c>. En C# pasaba lo mismo: cuatro lugares parsean
/// wknames a mano. Los tres de la UI (SemanasForm, FrmCorteValidacion,
/// FormLiberacion) sobreviven de milagro porque solo miran <c>parts[0]</c> y el
/// marcador va al final. El cuarto NO sobrevivía:
/// <c>AdminRepository.RefreshStatusCacheAsync</c> hacía
/// <c>string.Join("_", partes[2..])</c>, que para <c>wk36_1_Body_RE1</c> daba
/// el tipo <c>"Body_RE1"</c> — un tipo que no existe — y lo escribía en
/// <c>Kit_vin_wks_status_cache</c>. Con los compuestos era peor:
/// <c>wk20_111_ZC/ZD_RE1</c> se partía en <c>["ZC", "ZD_RE1"]</c> y el filtro
/// <c>vinDesc LIKE 'BodyCVZD_RE1\_%'</c> no empataba con nada: porcentaje y
/// kits en cero, en silencio.
///
/// LISTA BLANCA, no PATINDEX — igual que la función SQL. Se acepta exactamente
/// <c>RE</c>, <c>RE&lt;n&gt;</c>, <c>RE&lt;nn&gt;</c>, <c>EXP</c>,
/// <c>EXP&lt;n&gt;</c>, <c>EXP&lt;nn&gt;</c> como último segmento. Cualquier
/// otra cosa se deja intacta: un tipo que casualmente terminara en algo
/// parecido no se debe recortar.
///
/// <c>servicio</c> NO se toca aquí, igual que en SQL: vive en su propia capa y
/// tiene que empatar con <c>Vines.Descripcion</c>.
/// </summary>
public static class WknameParser
{
    // ^(RE|EXP)\d{0,2}$ — ancla en los dos extremos: el segmento COMPLETO debe
    // ser el marcador. Compilada porque RefreshStatusCacheAsync corre en cada
    // escaneo, entrega y generación.
    private static readonly Regex Marcador = new(
        @"^(RE|EXP)\d{0,2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Devuelve el wkname sin el marcador final. Si no trae marcador, lo
    /// devuelve tal cual. Espeja <c>dbo.fn_kit_wkname_base</c>.
    /// </summary>
    /// <example>
    /// wk36_1_Body_RE1   → wk36_1_Body
    /// wk29_3_Body_EXP   → wk29_3_Body
    /// wk36_30_Body      → wk36_30_Body   (sin cambios)
    /// wk20_111_ZC/ZD    → wk20_111_ZC/ZD (sin cambios)
    /// wk35_1_EPT3servicio → wk35_1_EPT3servicio (sin cambios: 'servicio' es otra capa)
    /// </example>
    public static string Base(string? wkname)
    {
        var t = (wkname ?? string.Empty).Trim();
        if (t.Length == 0) return t;

        int i = t.LastIndexOf('_');
        if (i <= 0 || i == t.Length - 1) return t;   // sin '_' o termina en '_'

        string ultimo = t.Substring(i + 1);
        return Marcador.IsMatch(ultimo) ? t.Substring(0, i) : t;
    }

    /// <summary>
    /// El marcador final (<c>RE1</c>, <c>EXP</c>, …) o <c>null</c> si no trae.
    /// Útil para etiquetar en pantalla sin volver a partir la cadena.
    /// </summary>
    public static string? Marca(string? wkname)
    {
        var t = (wkname ?? string.Empty).Trim();
        if (t.Length == 0) return null;

        int i = t.LastIndexOf('_');
        if (i <= 0 || i == t.Length - 1) return null;

        string ultimo = t.Substring(i + 1);
        return Marcador.IsMatch(ultimo) ? ultimo : null;
    }

    /// <summary>
    /// <c>true</c> si el wkname trae marcador, o sea si es una semana de
    /// reordenados/expeditados y no una semana normal.
    /// </summary>
    public static bool TieneMarcador(string? wkname) => Marca(wkname) is not null;
}
