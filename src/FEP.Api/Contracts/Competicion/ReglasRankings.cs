using System.Globalization;
using System.Text;

namespace FEP.Api.Contracts.Competicion;

/// <summary>Reglas del índice de rankings (<c>Rankings.asp</c>, tabla <c>SFEP_Rankings_Web</c>).</summary>
public static class ReglasRankings
{
    /// <summary>Carpeta (bajo <c>refs/</c>) de las imágenes: el ASP pintaba <c>"refs/images/" &amp; Foto</c>.</summary>
    public const string CarpetaImagenes = "images";

    /// <summary>Dominio de la web actual: sus enlaces se abren en la misma ventana.</summary>
    private const string DominioFep = "padelfederacion.es";

    /// <summary>
    /// Erratas conocidas en <c>Texto</c> (en la web pública a 13/11/2026). Se corrigen al servirlas; si se
    /// corrigen en la BD, la regla deja de aplicarse sola.
    /// </summary>
    private static readonly (string Original, string Correcto)[] Erratas =
    [
        ("Asbolutos", "Absolutos"),
        ("Asboluto", "Absoluto"),
    ];

    /// <summary>Título a mostrar: <c>Texto</c> recortado, espacios repetidos unificados y erratas conocidas corregidas.</summary>
    public static string Titulo(string? texto)
    {
        var limpio = string.Join(' ', (texto ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        foreach (var (original, correcto) in Erratas)
        {
            limpio = limpio.Replace(original, correcto, StringComparison.Ordinal);
        }

        return limpio;
    }

    /// <summary>Enlace de la BD utilizable: recortado; vacío o <c>#</c> → <c>null</c>.</summary>
    public static string? Enlace(string? link)
    {
        var limpio = link?.Trim();
        return string.IsNullOrEmpty(limpio) || limpio == "#" ? null : limpio;
    }

    /// <summary>
    /// Clave estable del ranking a partir de su enlace: último segmento de la ruta sin <c>.asp</c> ni el prefijo
    /// <c>Ranking_</c>, en minúsculas, sin tildes y con guiones (<c>Ranking_Silla_Ruedas</c> → <c>silla-ruedas</c>).
    /// Si el enlace no da nada, se usa el título; si tampoco, <c>ranking-{orden}</c>.
    /// </summary>
    public static string Clave(string? link, string? titulo, int orden)
    {
        var segmento = UltimoSegmento(Enlace(link));
        if (segmento is not null)
        {
            if (segmento.EndsWith(".asp", StringComparison.OrdinalIgnoreCase))
            {
                segmento = segmento[..^4];
            }

            if (segmento.StartsWith("Ranking_", StringComparison.OrdinalIgnoreCase) && segmento.Length > "Ranking_".Length)
            {
                segmento = segmento["Ranking_".Length..];
            }
        }

        var clave = Slug(segmento);
        if (clave.Length == 0)
        {
            clave = Slug(titulo);
        }

        return clave.Length == 0 ? $"ranking-{orden}" : clave;
    }

    /// <summary>Un enlace absoluto http(s) de un dominio que no es el de la FEP (se abre en ventana nueva).</summary>
    public static bool EsExterno(string? link)
        => Uri.TryCreate(Enlace(link), UriKind.Absolute, out var url)
           && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
           && !(url.Host.Equals(DominioFep, StringComparison.OrdinalIgnoreCase)
                || url.Host.EndsWith("." + DominioFep, StringComparison.OrdinalIgnoreCase));

    private static string? UltimoSegmento(string? enlace)
    {
        if (enlace is null)
        {
            return null;
        }

        var ruta = Uri.TryCreate(enlace, UriKind.Absolute, out var absoluta) && absoluta.Scheme.StartsWith("http", StringComparison.Ordinal)
            ? absoluta.AbsolutePath
            : enlace.Split('?', '#')[0];

        return ruta.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    private static string Slug(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(c);
            }
            else if (c is >= 'A' and <= 'Z')
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }

        return sb.ToString().Trim('-');
    }
}
