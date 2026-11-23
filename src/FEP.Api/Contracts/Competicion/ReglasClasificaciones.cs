using System.Globalization;
using System.Text;

namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Un <c>TipoRanking</c> de <c>SFEP_RankingFinal</c> / <c>SFEP_Ranking_Tipos</c> que forma parte de un ranking publicado
/// (p. ej. el absoluto masculino es el 10000).
/// </summary>
/// <param name="TipoRanking">Valor de <c>TipoRanking</c> en la BD.</param>
/// <param name="Genero"><c>M</c> (masculino), <c>F</c> (femenino) o <c>X</c> (mixto).</param>
/// <param name="Categoria">Clave de la categoría (menores, veteranos…); <c>null</c> si el ranking no tiene categorías.</param>
/// <param name="Titulo">
/// Nombre de la lista cuando no basta con la categoría y el género (p. ej. la lista única, mixta, de silla de ruedas:
/// "General"); <c>null</c> = "{Categoría} {género}".
/// </param>
public sealed record TipoClasificacion(int TipoRanking, string Genero, string? Categoria, string? Titulo = null);

/// <summary>Numeración de las licencias que guarda <c>SFEP_RankingFinal.NumLicencia</c> en un ranking.</summary>
public enum LicenciasRanking
{
    /// <summary>Licencia de jugador (<c>SFEP_Fichas.LicenciaJugador</c>): el nombre sale de la ficha.</summary>
    Jugador,

    /// <summary>
    /// Licencia de pádel en silla de ruedas (<c>SFEP_Fichas.LicenciaSilla</c>, mostrada con el prefijo <c>PS</c>): otra
    /// numeración, así que no se cruza con la ficha por <c>LicenciaJugador</c>; el nombre sale de la propia
    /// <c>SFEP_RankingFinal</c>, como en <c>Ranking_Silla_Ruedas.asp</c>.
    /// </summary>
    Silla,
}

/// <summary>Ranking con clasificación consultable en <c>/api/v1/competicion/rankings/{clave}</c>.</summary>
/// <param name="Clave">Clave del ranking (la misma que <see cref="RankingWebDto.Clave"/> del índice).</param>
/// <param name="Titulo">Título de la página (<c>h5</c> del ASP).</param>
/// <param name="PaginaWebActual">Ruta de la página en la web actual (destino si la API no puede servirla).</param>
/// <param name="Tipos">Listas (género × categoría) que componen el ranking, en el orden en que se muestran.</param>
/// <param name="Licencias">Numeración de las licencias del ranking (por defecto, de jugador).</param>
public sealed record DefinicionClasificacion(
    string Clave,
    string Titulo,
    string PaginaWebActual,
    IReadOnlyList<TipoClasificacion> Tipos,
    LicenciasRanking Licencias = LicenciasRanking.Jugador)
{
    /// <summary>Prefijo con el que se muestran las licencias (<c>PS</c> en silla de ruedas) o <c>null</c>.</summary>
    public string? PrefijoLicencia => ReglasClasificaciones.PrefijoLicencia(Licencias);

    /// <summary>Géneros del ranking, en orden de aparición.</summary>
    public IReadOnlyList<string> Generos => Tipos.Select(t => t.Genero).Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>Categorías del ranking, en orden de aparición (vacía si no tiene).</summary>
    public IReadOnlyList<string> Categorias => Tipos
        .Where(t => t.Categoria is not null)
        .Select(t => t.Categoria!)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}

/// <summary>Criterios ya validados de una consulta de clasificación.</summary>
/// <param name="Genero"><c>M</c>/<c>F</c>/<c>X</c> o <c>null</c> (todos los géneros del ranking).</param>
/// <param name="Categoria">Clave de categoría o <c>null</c> (todas).</param>
/// <param name="Licencia">Número de licencia exacto o <c>null</c>.</param>
/// <param name="Terminos">Términos del nombre buscado: el nombre completo debe contenerlos todos.</param>
/// <param name="Limite">Puestos máximos por lista.</param>
public sealed record CriteriosClasificacion(string? Genero, string? Categoria, int? Licencia, IReadOnlyList<string> Terminos, int Limite);

/// <summary>
/// Reglas de las clasificaciones de los rankings (origen: <c>Ranking_Absoluto.asp</c> con <c>CargarRnkAbs.asp</c> y
/// <c>CargarRnkAbsNombre.asp</c>, <c>Ranking_Menores.asp</c> con <c>CargarRnkMen.asp</c> y
/// <c>Ranking_Silla_Ruedas.asp</c>, que consultaba la tabla directamente, y <c>Ranking_Veteranos.asp</c> con
/// <c>CargarRnkVet.asp</c>; las demás <c>Ranking_*.asp</c> usan las mismas tablas con otros <c>TipoRanking</c>).
/// </summary>
public static class ReglasClasificaciones
{
    /// <summary>Rankings nacionales: <c>SFEP_RankingFinal.IdFederacion = 0</c> (filtro fijo de los ASP).</summary>
    public const int IdFederacionFep = 0;

    /// <summary>Puestos por lista sin <c>limite</c>: el ASP pintaba el <c>TOP 100</c>.</summary>
    public const int LimitePorDefecto = 100;

    /// <summary>Puestos máximos por lista (el ranking completo está en el PDF de cada lista).</summary>
    public const int LimiteMaximo = 500;

    /// <summary>Longitud máxima del nombre buscado.</summary>
    public const int LongitudMaximaBusqueda = 100;

    /// <summary>Número máximo de palabras del nombre buscado.</summary>
    public const int TerminosMaximos = 6;

    /// <summary>Carpeta (bajo <c>refs/</c>) de los PDF del ranking completo: el ASP enlazaba <c>refs/docs/{ArchivoPDF}</c>.</summary>
    public const string CarpetaDocumentos = "docs";

    /// <summary>
    /// Rankings con clasificación migrada. Para añadir otro <c>Ranking_*.asp</c> que use <c>SFEP_RankingFinal</c> basta
    /// con declarar aquí sus <c>TipoRanking</c>, sin cambiar el contrato.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, DefinicionClasificacion> Rankings =
        new Dictionary<string, DefinicionClasificacion>(StringComparer.OrdinalIgnoreCase)
        {
            // CargarRnkAbs.asp: genero = "M" → 10000; cualquier otro valor → 10001 (femenino).
            ["absoluto"] = new("absoluto", "Ranking Único Absoluto", "Ranking_Absoluto",
            [
                new(10000, "M", null),
                new(10001, "F", null),
            ]),

            // Ranking_Menores.asp + CargarRnkMen.asp (?cat=BENJAMIN|ALEVIN|INFANTIL|CADETE|JUNIOR&genero=M|F): un
            // TipoRanking por categoría y género, en el orden de los desplegables del ASP. Los mixtos (8002, 7002, 6002,
            // 5002, 4002) no se publican: el ASP solo los servía por accidente (género oculto "X" al elegir antes la
            // categoría; sin datos en SFEP_Ranking_Tipos, sin evolución y error 500 en benjamín y alevín) y su búsqueda
            // por licencia los excluía expresamente. DECISIÓN FEP: basta con añadir aquí new(8002, "X", "benjamin")…
            ["menores"] = new("menores", "Ranking de Menores", "Ranking_Menores",
            [
                new(8000, "M", "benjamin"),
                new(8001, "F", "benjamin"),
                new(7000, "M", "alevin"),
                new(7001, "F", "alevin"),
                new(6000, "M", "infantil"),
                new(6001, "F", "infantil"),
                new(5000, "M", "cadete"),
                new(5001, "F", "cadete"),
                new(4000, "M", "junior"),
                new(4001, "F", "junior"),
            ]),

            // Ranking_Silla_Ruedas.asp: una sola lista, SFEP_RankingFinal con IdFederacion = 0 y TipoRanking = 15, sin
            // género ni categoría (en la clasificación hay jugadores y jugadoras: se declara mixta, "X") y con
            // licencias de silla ("PS" + NumLicencia; nombre de SFEP_RankingFinal, sin JOIN con SFEP_Fichas).
            ["silla-ruedas"] = new("silla-ruedas", "Ranking de Pádel en Silla de Ruedas", "Ranking_Silla_Ruedas",
            [
                new(15, "X", null, "General"),
            ], LicenciasRanking.Silla),

            // Ranking_Veteranos.asp + CargarRnkVet.asp (?cat=CAT35…CAT60&genero=M|F): masculino 10000 + edad, femenino
            // 11000 + edad, en el orden de los desplegables del ASP (CAT +35 … CAT +60). La clave de categoría es la edad
            // ("35"; se muestra "+35"). Los "X" de CargarRnkVet.asp (8002, 7002, 6002, 5002, 4002) son los mixtos de
            // MENORES copiados por error: no se publican. El 11060 (+60 femenino) no tiene filas hoy (CargarRnkVet.asp
            // daba 500): se declara para que aparezca en cuanto se calcule (DECISIÓN FEP).
            ["veteranos"] = new("veteranos", "Ranking de Veteranos", "Ranking_Veteranos",
            [
                new(10035, "M", "35"),
                new(11035, "F", "35"),
                new(10040, "M", "40"),
                new(11040, "F", "40"),
                new(10045, "M", "45"),
                new(11045, "F", "45"),
                new(10050, "M", "50"),
                new(11050, "F", "50"),
                new(10055, "M", "55"),
                new(11055, "F", "55"),
                new(10060, "M", "60"),
                new(11060, "F", "60"),
            ]),
        };

    /// <summary>Prefijo de las licencias de pádel en silla de ruedas ("PS1008"), como las pintaba el ASP.</summary>
    public const string PrefijoLicenciaSilla = "PS";

    /// <summary>Prefijo con el que se muestran las licencias de un ranking (<c>null</c> si no lleva).</summary>
    public static string? PrefijoLicencia(LicenciasRanking licencias)
        => licencias == LicenciasRanking.Silla ? PrefijoLicenciaSilla : null;

    /// <summary>
    /// Nombres de categoría que no salen de capitalizar la clave (tildes). Los valores del ASP iban en mayúsculas y sin
    /// tilde (<c>BENJAMIN</c>, <c>ALEVIN</c>, <c>JUNIOR</c>); "júnior" con tilde según la RAE, como en el calendario de
    /// menores ya migrado.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> NombresCategorias =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["benjamin"] = "Benjamín",
            ["alevin"] = "Alevín",
            ["junior"] = "Júnior",
        };

    /// <summary>Definición del ranking o <c>null</c> si no tiene clasificación migrada.</summary>
    public static DefinicionClasificacion? Buscar(string? clave)
        => string.IsNullOrWhiteSpace(clave) ? null : Rankings.GetValueOrDefault(clave.Trim());

    /// <summary>
    /// Normaliza el género pedido: <c>M</c>/<c>masculino</c> → <c>M</c>, <c>F</c>/<c>femenino</c> → <c>F</c>,
    /// <c>X</c>/<c>mixto</c> → <c>X</c>. Vacío → <c>null</c> (sin filtro). Devuelve <c>false</c> si no es ninguno.
    /// </summary>
    public static bool TryNormalizarGenero(string? genero, out string? normalizado)
    {
        normalizado = null;
        var limpio = genero?.Trim();
        if (string.IsNullOrEmpty(limpio))
        {
            return true;
        }

        normalizado = limpio.ToUpperInvariant() switch
        {
            "M" or "MASCULINO" => "M",
            "F" or "FEMENINO" => "F",
            "X" or "MIXTO" => "X",
            _ => null,
        };
        return normalizado is not null;
    }

    /// <summary>Nombre del género para mostrar.</summary>
    public static string NombreGenero(string genero) => genero switch
    {
        "M" => "Masculino",
        "F" => "Femenino",
        "X" => "Mixto",
        _ => genero,
    };

    /// <summary>
    /// Divide el nombre buscado en términos como el ASP (que cambiaba los espacios por "-" y partía por "-"): el
    /// nombre completo debe contenerlos todos. Sin términos → lista vacía.
    /// </summary>
    public static IReadOnlyList<string> Terminos(string? texto)
        => string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Split(Separadores, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static readonly char[] Separadores = [' ', '\t', '\r', '\n', '-'];

    /// <summary>Escapa los comodines de <c>LIKE</c> (<c>%</c>, <c>_</c>, <c>[</c>) para buscar el texto literalmente.</summary>
    public static string EscaparLike(string termino)
        => termino.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    /// <summary>
    /// Nombre del jugador como lo pintaba el ASP (<c>Nombre &amp; " " &amp; Apellido1 &amp; " " &amp; Apellido2</c>), sin
    /// los espacios de relleno ni dobles espacios cuando falta un apellido.
    /// </summary>
    public static string NombreJugador(string? nombre, string? apellido1, string? apellido2)
        => string.Join(' ', new[] { nombre, apellido1, apellido2 }
            .SelectMany(p => (p ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));

    /// <summary>
    /// Ruta de las estadísticas del jugador en la web actual (<c>Estadisticas.asp</c>, regla <c>^Estadisticas</c> de
    /// <c>web.config</c>). <c>CargarRnkAbs.asp</c> enlazaba por error a <c>Silla-Ruedas-Est?id=PS{licencia}</c>
    /// (estadísticas de pádel en silla de ruedas), que no muestra a los jugadores del ranking absoluto.
    /// Esa sí es la página de los rankings con licencias de silla (<c>Ranking_Silla_Ruedas.asp</c>:
    /// <c>Silla-Ruedas-Est?id=PS{licencia}</c>, regla de <c>web.config</c> → <c>Silla-Ruedas-Est.asp</c>).
    /// </summary>
    public static string RutaEstadisticas(int licencia, LicenciasRanking licencias = LicenciasRanking.Jugador)
        => licencias == LicenciasRanking.Silla
            ? string.Create(CultureInfo.InvariantCulture, $"Silla-Ruedas-Est?id={PrefijoLicenciaSilla}{licencia}")
            : string.Create(CultureInfo.InvariantCulture, $"Estadisticas?id={licencia}");

    /// <summary>
    /// Clave de categoría a nombre legible (<c>cat-35</c> → "Cat 35", <c>benjamin</c> → "Benjamín"; las de veteranos,
    /// que son la edad mínima, con "+": <c>35</c> → "+35", como "CAT +35" del ASP); sin categoría → <c>null</c>.
    /// </summary>
    public static string? NombreCategoria(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria))
        {
            return null;
        }

        if (NombresCategorias.TryGetValue(categoria.Trim(), out var nombre))
        {
            return nombre;
        }

        categoria = categoria.Trim();
        if (categoria.All(char.IsAsciiDigit))
        {
            return "+" + categoria;
        }

        var sb = new StringBuilder(categoria.Length);
        foreach (var parte in categoria.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(char.ToUpperInvariant(parte[0])).Append(parte.AsSpan(1));
        }

        return sb.ToString();
    }
}
