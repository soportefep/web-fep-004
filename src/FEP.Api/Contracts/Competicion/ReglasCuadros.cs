using System.Globalization;
using System.Text.RegularExpressions;

namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Reglas heredadas de <c>Previo.asp</c>, <c>PrevioEquipos.asp</c> y las páginas de cuadro (<c>Cuadro*.asp</c>,
/// <c>CuadroPrevia*.asp</c>, <c>CuadroPrePrevia*.asp</c>, <c>CuadroEquipos*.asp</c>, <c>Liguilla*.asp</c>).
/// </summary>
public static partial class ReglasCuadros
{
    /// <summary>Id mínimo admitido (<c>IdPrueba</c>).</summary>
    public const int IdMinimo = 1;

    /// <summary>Nivel máximo admitido (1–10 categorías, 21 previa, 31 pre-previa; se deja margen).</summary>
    public const int NivelMaximo = 99;

    /// <summary>Avance máximo admitido (cuadro de 128 = avance 7; las liguillas de 6 parejas tienen 15 partidos).</summary>
    public const int AvanceMaximo = 64;

    /// <summary>Posición máxima admitida en un enfrentamiento.</summary>
    public const int PosicionMaxima = 256;

    /// <summary>Longitud máxima de categoría y cuadro.</summary>
    public const int LongitudMaxima = 30;

    /// <summary>Géneros en el orden en que se listan.</summary>
    public static readonly IReadOnlyList<string> LetrasGenero = ["M", "F", "X"];

    /// <summary><c>M</c>/<c>F</c>/<c>X</c> (sin distinguir mayúsculas) → letra en mayúsculas; otro valor → <c>null</c>.</summary>
    public static string? Genero(string? valor)
    {
        var g = valor?.Trim().ToUpperInvariant();
        return g is "M" or "F" or "X" ? g : null;
    }

    /// <summary>
    /// Categoría o cuadro de la ruta: letras, cifras, espacio, punto, guion, <c>+</c> y <c>_</c>, empezando por letra o
    /// cifra (<c>ABSOLUTO</c>, <c>Cat50</c>, <c>SUB-23</c>, <c>Grupo A</c>, <c>3y4</c>). Recortado; si no cumple, <c>null</c>.
    /// </summary>
    public static string? TextoRuta(string? valor)
    {
        var t = valor?.Trim();
        return !string.IsNullOrEmpty(t) && t.Length <= LongitudMaxima && TextoValido().IsMatch(t) ? t : null;
    }

    [GeneratedRegex(@"^[\p{L}\p{Nd}][\p{L}\p{Nd} ._+\-]*$")]
    private static partial Regex TextoValido();

    // ---------------------------------------------------------------------------------------------
    // Visibilidad: select case request("Nivel") de las páginas de cuadro (ver = rs_ver("Ver…")).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Columna <c>Ver…</c> de <c>SFEP_Torneos_Desglose</c> que publica un nivel; <c>null</c> si el ASP no miraba ninguna.</summary>
    public static string? ColumnaVer(int nivel) => nivel switch
    {
        1 => "VerPrimera",
        2 => "VerSegunda",
        3 => "VerTercera",
        4 => "VerCuarta",
        5 => "VerQuinta",
        6 => "VerSexta",
        7 => "VerSeptima",
        8 => "VerOctava",
        9 => "VerNovena",
        10 => "VerDecima",
        21 => "VerPrevia",
        31 => "VerPrePrevia",
        _ => null,
    };

    /// <summary>Todas las columnas <c>Ver…</c> que se leen.</summary>
    public static readonly IReadOnlyList<string> ColumnasVer =
        [.. new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 21, 31 }.Select(n => ColumnaVer(n)!)];

    /// <summary>
    /// El ASP ocultaba el cuadro solo si la columna del nivel valía <c>'N'</c> (vacía o de otro nivel: se veía).
    /// Sin fila de desglose el ASP daba error 500: aquí, no publicado.
    /// </summary>
    /// <param name="nivel">Nivel del cuadro.</param>
    /// <param name="hayDesglose">Hay fila en <c>SFEP_Torneos_Desglose</c> para el género y la categoría.</param>
    /// <param name="valor">Valor de la columna <c>Ver…</c> por nombre (sin distinguir mayúsculas).</param>
    public static bool Publicado(int nivel, bool hayDesglose, Func<string, string?> valor)
    {
        if (!hayDesglose)
        {
            return false;
        }

        var columna = ColumnaVer(nivel);
        return columna is null || !string.Equals(valor(columna)?.Trim(), "N", StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------------------------
    // Formato: Previo.asp / PrevioEquipos.asp contaban las filas del cuadro y redirigían a una plantilla.
    // ---------------------------------------------------------------------------------------------

    private static readonly IReadOnlyDictionary<int, string> PlantillasParejas = new Dictionary<int, string>
    {
        [3] = "Cuadro2",
        [6] = "Liguilla3",
        [7] = "Cuadro4",
        [14] = "CuadroPrevia8-2",
        [15] = "Cuadro8",
        [20] = "Liguilla5",
        [28] = "CuadroPrevia16-4",
        [31] = "Cuadro16",
        [40] = "Liguilla5",
        [56] = "CuadroPrevia32-8",
        [60] = "CuadroPrevia32-4",
        [62] = "CuadroPrevia32-2",
        [63] = "Cuadro32",
        [112] = "CuadroPrevia64-16",
        [120] = "CuadroPrevia64-8",
        [124] = "CuadroPrevia64-4",
        [127] = "Cuadro64",
        [240] = "CuadroPrevia128-16",
        [248] = "CuadroPrevia128-8",
        [255] = "Cuadro128",
    };

    private static readonly IReadOnlyDictionary<int, string> PlantillasEquipos = new Dictionary<int, string>
    {
        [6] = "Liguilla3Equipos",
        [7] = "CuadroEquipos4",
        [15] = "CuadroEquipos8",
        [31] = "CuadroEquipos16",
        [63] = "CuadroEquipos32",
        [127] = "CuadroEquipos64",
    };

    /// <summary>
    /// Formato (<c>eliminatoria</c>/<c>liguilla</c>) y plantilla del ASP a partir de las filas del cuadro.
    /// Parejas: tabla de <c>Previo.asp</c>; con 12, 24 o 30 filas decide el avance más bajo distinto de 0 (1 = liguilla,
    /// y con 12 filas <c>LiguillaVueltas = 2</c> → <c>Liguilla3</c>). Equipos: <c>PrevioEquipos.asp</c> (6 →
    /// <c>Liguilla3Equipos</c>, 12 → <c>Liguilla4Equipos</c>, 7/15/31/63/127 → <c>CuadroEquipos4…64</c>). Con un número de
    /// filas que el ASP no contemplaba (redirigía a una URL rota), plantilla <c>null</c> y el formato se deduce de la
    /// forma: liguilla si no hay campeón (avance 0) y cada avance tiene como mucho 2 huecos.
    /// </summary>
    /// <param name="equipos">Torneo por equipos.</param>
    /// <param name="avances">Avance de cada fila del cuadro.</param>
    /// <param name="liguillaVueltas"><c>LiguillaVueltas</c> del desglose.</param>
    public static (string Formato, string? Plantilla) Formato(bool equipos, IReadOnlyList<int> avances, int? liguillaVueltas)
    {
        var filas = avances.Count;
        var distintosDeCero = avances.Where(a => a != 0).ToArray();
        var empiezaEnUno = distintosDeCero.Length > 0 && distintosDeCero.Min() == 1;

        string? plantilla;
        if (equipos)
        {
            plantilla = filas == 12
                ? (empiezaEnUno ? "Liguilla4Equipos" : null)
                : PlantillasEquipos.GetValueOrDefault(filas);
        }
        else
        {
            plantilla = filas switch
            {
                12 => empiezaEnUno ? (liguillaVueltas == 2 ? "Liguilla3" : "Liguilla4") : "CuadroPrevia8-4",
                24 => empiezaEnUno ? "Liguilla4" : "CuadroPrevia16-8",
                30 => empiezaEnUno ? "Liguilla6" : "CuadroPrevia16-2",
                _ => PlantillasParejas.GetValueOrDefault(filas),
            };
        }

        if (plantilla is not null)
        {
            return (plantilla.StartsWith("Liguilla", StringComparison.Ordinal) ? "liguilla" : "eliminatoria", plantilla);
        }

        var pareceLiguilla = filas >= 2
                             && !avances.Contains(0)
                             && avances.GroupBy(a => a).All(g => g.Count() <= 2)
                             && avances.Distinct().Count() >= 2;
        return (pareceLiguilla ? "liguilla" : "eliminatoria", null);
    }

    // ---------------------------------------------------------------------------------------------
    // Huecos sin pareja (Pareja = 0): el ASP comprobaba Exento, Letra y Lucky en ese orden y ganaba el último.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Tipo y texto de un hueco sin pareja; <c>null</c> si está vacío. Orden del ASP (gana el último que se cumple):
    /// <c>Exento = 'E'</c> → "EXENTO"; <c>Letra</c> → "PREVIA - X" ("PRE-PREVIA - X" en los cuadros de previa, nivel 21);
    /// <c>Lucky = 'S'</c> → "LUCKY LOSER".
    /// </summary>
    public static (string Tipo, string Nombre, string? Letra)? Hueco(int nivel, string? exento, string? letra, string? lucky)
    {
        (string, string, string?)? hueco = null;
        if (string.Equals(exento?.Trim(), "E", StringComparison.OrdinalIgnoreCase))
        {
            hueco = ("exento", "EXENTO", null);
        }

        var l = letra?.Trim();
        if (!string.IsNullOrEmpty(l))
        {
            hueco = ("previa", (nivel == 21 ? "PRE-PREVIA - " : "PREVIA - ") + l, l);
        }

        if (EsSi(lucky))
        {
            hueco = ("lucky", "LUCKY LOSER", null);
        }

        return hueco;
    }

    /// <summary><c>CDS</c> (texto en la BD: <c>HAVING CDS &lt;&gt; ''</c> y <c>CAST(CDS AS INT)</c>) → número &gt; 0, o <c>null</c>.</summary>
    public static int? CabezaSerie(string? cds)
        => int.TryParse(cds?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;

    /// <summary><c>'S'</c> (con o sin espacios de relleno) → <c>true</c>.</summary>
    public static bool EsSi(string? valor) => string.Equals(valor?.Trim(), "S", StringComparison.OrdinalIgnoreCase);

    // ---------------------------------------------------------------------------------------------
    // Orden de los cuadros del torneo.
    // ---------------------------------------------------------------------------------------------

    /// <summary>Ordena por género (M, F, X), categoría, nivel y cuadro (Final, Consola y el resto alfabéticamente).</summary>
    public static IReadOnlyList<CuadroDisponibleDto> Ordenar(IEnumerable<CuadroDisponibleDto> cuadros)
        => cuadros
            .OrderBy(c => OrdenGenero(c.Genero))
            .ThenBy(c => c.Categoria, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Nivel)
            .ThenBy(c => OrdenCuadro(c.Cuadro))
            .ThenBy(c => c.Cuadro, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int OrdenGenero(string genero) => genero switch { "M" => 0, "F" => 1, "X" => 2, _ => 3 };

    private static int OrdenCuadro(string cuadro)
        => string.Equals(cuadro, "Final", StringComparison.OrdinalIgnoreCase) ? 0
            : string.Equals(cuadro, "Consola", StringComparison.OrdinalIgnoreCase) ? 1
            : 2;

    /// <summary>Página del cuadro en la web actual (<c>Previo.asp</c> o <c>PrevioEquipos.asp</c>), relativa a la raíz.</summary>
    public static string RutaWebActual(ClaveCuadro clave, bool equipos)
        => $"{(equipos ? "PrevioEquipos" : "Previo")}.asp?IdTorneo={clave.IdTorneo}&Genero={clave.Genero}"
           + $"&Cat={Uri.EscapeDataString(clave.Categoria)}&Nivel={clave.Nivel}&Cuadro={Uri.EscapeDataString(clave.Cuadro)}";
}
