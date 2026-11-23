using FEP.Api.Contracts.Federacion;

namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Fila de <c>SFEP_Ranking_Equipos</c> con los datos del club que pintaba <c>Ranking_Equipos_Absolutos.asp</c>
/// (provincia y federación). Es el resultado del repositorio antes de componer el <see cref="RankingEquiposDto"/>.
/// </summary>
/// <param name="Anio">Temporada (<c>ano</c>).</param>
/// <param name="Categoria">Categoría (<c>Categoria</c>: 1 o 2).</param>
/// <param name="Genero"><c>Genero</c> (<c>M</c>/<c>F</c>, puede venir con espacios de relleno).</param>
/// <param name="Puesto">Puesto (<c>Puesto</c>).</param>
/// <param name="Equipo">Nombre del equipo (<c>Equipo</c>).</param>
/// <param name="Club">Código del club (<c>Idclub</c>) como texto.</param>
/// <param name="Provincia">Provincia del club.</param>
/// <param name="IdFederacion">Federación de la comunidad del club (1–22) o <c>null</c>.</param>
/// <param name="Abreviatura">Abreviatura de esa federación.</param>
public sealed record FilaRankingEquipo(
    int Anio,
    int Categoria,
    string? Genero,
    int Puesto,
    string? Equipo,
    string? Club,
    string? Provincia,
    int? IdFederacion,
    string? Abreviatura);

/// <summary>Reglas heredadas de <c>Ranking_Equipos_Absolutos.asp</c>.</summary>
public static class ReglasRankingEquipos
{
    /// <summary>Clave del ranking (la de <see cref="RankingWebDto.Clave"/> en el índice: <c>Ranking_Equipos_Absolutos</c>).</summary>
    public const string Clave = "equipos-absolutos";

    /// <summary>Título del ranking (<c>h5</c> "RANKING EQUIPOS ABSOLUTOS" del ASP).</summary>
    public const string Titulo = "Ranking de Equipos Absolutos";

    /// <summary>Página en la web actual.</summary>
    public const string PaginaWebActual = "Ranking_Equipos_Absolutos";

    /// <summary>Filtros fijos de las 32 consultas del ASP: <c>Modalidad = 'EQU'</c> y <c>Tipo = 'ABS'</c>.</summary>
    public const string Modalidad = "EQU", Tipo = "ABS";

    /// <summary>
    /// Primera temporada publicada: el ASP pintaba de 2017 a 2024, una a una. Las anteriores (si las hay en la BD) no
    /// se mostraban. Las posteriores a 2024 sí se publican ya sin tocar código (DECISIÓN FEP, ver análisis).
    /// </summary>
    public const int AnioMinimo = 2017;

    /// <summary>Categorías que pintaba el ASP (1.ª y 2.ª), en orden.</summary>
    public static readonly IReadOnlyList<int> Categorias = [1, 2];

    /// <summary>Géneros que pintaba el ASP, en el orden de cada categoría (masculina y femenina).</summary>
    public static readonly IReadOnlyList<string> Generos = ["M", "F"];

    /// <summary>
    /// Federaciones autonómicas válidas: desde 2018 el ASP filtraba <c>IdFederacion &lt; 100</c> porque algunas
    /// comunidades tienen además federaciones con id ≥ 100 (p. ej. 1001, 1002) y el <c>JOIN</c> duplicaba equipos; en
    /// 2017 no lo hacía y la web actual repite cinco equipos (federaciones 1001 y 1002). Ahora se aplica a todas las
    /// temporadas, con el rango de las 22 federaciones autonómicas (<see cref="ReglasFederaciones"/>, DECISIÓN FEP).
    /// </summary>
    public const int IdFederacionMinimo = ReglasFederaciones.IdMinimo,
        IdFederacionMaximo = ReglasFederaciones.IdMaximo;

    /// <summary>Carpeta (bajo <c>refs/</c>) de banderas y escudos: el ASP pintaba <c>refs/images/{id}.jpg</c>.</summary>
    public const string CarpetaImagenes = "images";

    /// <summary>"1.ª categoría masculina"… (el ASP: "1ª CATEGORÍA MASCULINA").</summary>
    public static string TituloLista(int categoria, string genero)
        => $"{categoria}.ª categoría {(genero == "F" ? "femenina" : "masculina")}";

    /// <summary>Nombre del equipo sin espacios de relleno, tabuladores ni espacios repetidos (hay uno que empieza por tabulador).</summary>
    public static string NombreEquipo(string? equipo)
        => string.Join(' ', (equipo ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Ruta de la ficha del club en la web actual (<c>Club.asp?id=</c>, como el ASP; aún no migrada).</summary>
    public static string RutaClub(string club) => $"Club.asp?id={Uri.EscapeDataString(club)}";

    /// <summary>Nombre del fichero de imagen (<c>{id}.jpg</c>) de un escudo o bandera.</summary>
    public static string Imagen(string id) => $"{id}.jpg";

    /// <summary>
    /// Agrupa las filas por temporada (de la más reciente a la más antigua) y, dentro de cada una, en listas por
    /// categoría y género en el orden del ASP (1.ª masculina, 1.ª femenina, 2.ª masculina, 2.ª femenina). Descarta filas
    /// de categorías o géneros que el ASP no pintaba y listas vacías; ordena por puesto y nombre del equipo.
    /// </summary>
    /// <param name="filas">Filas leídas de la BD.</param>
    /// <param name="puesto">Convierte una fila en el puesto publicado (URLs, federación…).</param>
    public static IReadOnlyList<TemporadaRankingEquiposDto> Temporadas(
        IEnumerable<FilaRankingEquipo> filas, Func<FilaRankingEquipo, PuestoEquipoDto> puesto)
    {
        var validas = filas
            .Select(f => f with { Genero = f.Genero?.Trim().ToUpperInvariant() })
            .Where(f => Categorias.Contains(f.Categoria) && f.Genero is not null && Generos.Contains(f.Genero))
            .ToArray();

        return validas
            .GroupBy(f => f.Anio)
            .OrderByDescending(g => g.Key)
            .Select(temporada => new TemporadaRankingEquiposDto(
                temporada.Key,
                Categorias
                    .SelectMany(c => Generos.Select(g => (Categoria: c, Genero: g)))
                    .Select(l => new ListaRankingEquiposDto(
                        l.Categoria,
                        l.Genero,
                        TituloLista(l.Categoria, l.Genero),
                        temporada
                            .Where(f => f.Categoria == l.Categoria && f.Genero == l.Genero)
                            .Select(puesto)
                            .OrderBy(p => p.Puesto)
                            .ThenBy(p => p.Equipo, StringComparer.Ordinal)
                            .ToArray()))
                    .Where(l => l.Puestos.Count > 0)
                    .ToArray()))
            .ToArray();
    }
}
