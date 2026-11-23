using System.Globalization;
using System.Text.RegularExpressions;

namespace FEP.Api.Infrastructure;

/// <summary>Normalización de textos leídos de columnas <c>char</c>/<c>varchar</c> del modelo heredado.</summary>
internal static class Texto
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");
    private static readonly Regex Palabras = new(@"\p{L}+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Romano = new("^[IVX]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Preposiciones y conjunciones que van en minúscula (salvo al principio o tras un paréntesis o guion).</summary>
    private static readonly HashSet<string> Enlaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "del", "al", "y", "e", "o", "u", "en", "con", "por", "para",
    };

    /// <summary>
    /// Artículos: en minúscula solo tras una preposición ("Alhaurín de la Torre"); si no, son parte del topónimo
    /// ("La Nucia", "Las Palmas", "El Puerto").
    /// </summary>
    private static readonly HashSet<string> Articulos = new(StringComparer.OrdinalIgnoreCase) { "la", "las", "los", "el" };

    private static readonly HashSet<string> Preposiciones = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "en", "con", "por", "para", "a",
    };

    /// <summary>Siglas con vocales que se mantienen en mayúsculas.</summary>
    private static readonly HashSet<string> Siglas = new(StringComparer.OrdinalIgnoreCase)
    {
        "FEP", "FIP", "WPT", "UD", "SA", "SAD", "CDE",
    };

    /// <summary>
    /// Nombres propios (sedes) en formato "Nombre Propio": DECIDIDO por la FEP (13/11/2026) para la sede del
    /// calendario. Solo se transforma el texto escrito entero en mayúsculas (o en minúsculas), como
    /// "REGAL PADEL CLUB (ZARAGOZA)" → "Regal Padel Club (Zaragoza)"; si ya mezcla mayúsculas y minúsculas
    /// ("Club de Pádel Arruzafa - Córdoba") se respeta tal cual. Reglas: primera letra de cada palabra en mayúscula;
    /// preposiciones y conjunciones (<c>de</c>, <c>del</c>, <c>en</c>, <c>y</c>…) en minúscula si van tras un espacio
    /// y no son la primera; los artículos (<c>la</c>, <c>el</c>…) en minúscula solo tras una preposición
    /// ("Alhaurín de la Torre"; en cambio "La Nucia", "Las Palmas"); se quedan en mayúsculas las siglas sin vocales ("CT", "CDP", "RCT"), los números romanos
    /// ("II", "XXIII") y unas pocas siglas conocidas ("FEP", "UD"…). Vacío → <c>null</c>.
    /// </summary>
    public static string? NombrePropio(string? valor)
    {
        var limpio = Limpio(valor);
        if (limpio is null)
        {
            return null;
        }

        if (limpio.Any(char.IsUpper) && limpio.Any(char.IsLower))
        {
            return limpio;
        }

        string? anterior = null;
        return Palabras.Replace(limpio, m =>
        {
            var palabra = m.Value;
            var previa = anterior;
            anterior = palabra;
            if (Siglas.Contains(palabra) || Romano.IsMatch(palabra) || (palabra.Length > 1 && !TieneVocal(palabra)))
            {
                return palabra.ToUpper(Espanol);
            }

            var trasEspacio = m.Index > 0 && limpio[m.Index - 1] == ' ';
            if (trasEspacio && (Enlaces.Contains(palabra)
                                || (Articulos.Contains(palabra) && previa is not null && Preposiciones.Contains(previa))))
            {
                return palabra.ToLower(Espanol);
            }

            return char.ToUpper(palabra[0], Espanol) + palabra[1..].ToLower(Espanol);
        });
    }

    private static bool TieneVocal(string palabra)
        => palabra.ToLower(Espanol).IndexOfAny("aeiouyáéíóúàèìòùäëïöü".ToCharArray()) >= 0;

    /// <summary>Recorta espacios (columnas char rellenas, como hacía <c>rtrim()</c> en ASP); vacío → <c>null</c>.</summary>
    public static string? Limpio(string? valor)
    {
        var recortado = valor?.Trim();
        return string.IsNullOrEmpty(recortado) ? null : recortado;
    }

    /// <summary>Como <see cref="Limpio"/> pero nunca devuelve <c>null</c>.</summary>
    public static string Requerido(string? valor) => valor?.Trim() ?? string.Empty;

    /// <summary>
    /// Títulos de noticia: el backoffice guarda las comillas dobles como <c>``</c>
    /// (index.asp hacía <c>replace(Titulo, "``", Chr(34))</c>).
    /// </summary>
    public static string TituloNoticia(string? valor) => Requerido(valor).Replace("``", "\"");

    /// <summary>
    /// Enlace externo de patrocinador: en BD se guarda sin esquema y el ASP anteponía <c>https://</c>.
    /// Si ya trae esquema http(s) se respeta.
    /// </summary>
    public static string? EnlaceExterno(string? valor)
    {
        var limpio = Limpio(valor);
        if (limpio is null)
        {
            return null;
        }

        return limpio.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || limpio.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? limpio
            : "https://" + limpio;
    }
}
