using System.Globalization;

namespace FEP.Api.Contracts.Competicion;

/// <summary>Clave de búsqueda de un nombre personalizado en <c>SFEP_Torneos_Cuadros_Nombres</c>.</summary>
/// <param name="Nivel">Nivel.</param>
/// <param name="Categoria">Categoría (<c>Tipo</c> del desglose, o la fija que usaba el ASP).</param>
/// <param name="Genero"><c>M</c> | <c>F</c> | <c>X</c>.</param>
/// <param name="Cuadro"><c>Final</c>, <c>Consola</c>, <c>Liguilla</c>, <c>GrupoA</c>…</param>
public sealed record ClaveNombreCuadro(int Nivel, string Categoria, string Genero, string Cuadro);

/// <summary>
/// Un cuadro posible de un género: columna <c>Ver…</c> de <c>SFEP_Torneos_Desglose</c> que lo hace visible,
/// destino en <c>PrevioEquipos.asp</c> y nombre por defecto. <paramref name="NivelNombre"/>/<paramref name="CuadroNombre"/>
/// indican dónde buscaba el ASP el nombre personalizado (a veces con otro nivel que el del enlace: se respeta).
/// </summary>
/// <param name="Columna">Columna de visibilidad (<c>VerPrimera</c>, <c>VerGrupoA</c>…).</param>
/// <param name="Nivel">Nivel del enlace.</param>
/// <param name="Cuadro">Cuadro del enlace.</param>
/// <param name="Titulo">Nombre por defecto.</param>
/// <param name="NivelNombre">Nivel con el que el ASP buscaba el nombre personalizado (<c>null</c>: no lo buscaba).</param>
/// <param name="CuadroNombre">Cuadro con el que lo buscaba (por defecto, <paramref name="Cuadro"/>).</param>
/// <param name="CategoriaNombre">Categoría fija con la que lo buscaba (por defecto, la del desglose).</param>
/// <param name="TituloSinCat2">Nombre por defecto si el desglose no tiene segunda categoría (<c>Cat2 = 0</c>).</param>
public sealed record DefinicionCuadro(
    string Columna,
    int Nivel,
    string Cuadro,
    string Titulo,
    int? NivelNombre = null,
    string? CuadroNombre = null,
    string? CategoriaNombre = null,
    string? TituloSinCat2 = null);

/// <summary>Reglas heredadas de <c>Camp_Equipos.asp</c> (cuadros, géneros, plazo e inscripción de equipos).</summary>
public static class ReglasCampeonatoEquipos
{
    /// <summary>Id mínimo admitido (<c>IdPrueba</c>).</summary>
    public const int IdMinimo = 1;

    /// <summary>Géneros del desglose en el orden en que los pintaba el ASP.</summary>
    public static readonly IReadOnlyList<string> LetrasGenero = ["M", "F", "X"];

    /// <summary><c>M</c>/<c>F</c>/<c>X</c> → clave pública; otro valor → <c>null</c>.</summary>
    public static string? Genero(string? letra) => (letra?.Trim() ?? string.Empty).ToUpperInvariant() switch
    {
        "M" => "masculino",
        "F" => "femenino",
        "X" => "mixto",
        _ => null,
    };

    // ---------------------------------------------------------------------------------------------
    // Cuadros. Copia fiel del orden, niveles y búsquedas de nombre del ASP (tres bloques casi iguales
    // pero no idénticos: el femenino buscaba los grupos A y B con Nivel = 2, el mixto buscaba la 1.ª y la
    // 2.ª con Categoria = 'ABSOLUTO' fija…). Los textos por defecto se han unificado (ver análisis).
    // ---------------------------------------------------------------------------------------------
    private static DefinicionCuadro Grupo(char letra, int? nivelNombre = null)
        => new($"VerGrupo{letra}", 1, $"Grupo{letra}", $"Grupo {letra}", nivelNombre);

    private static IEnumerable<DefinicionCuadro> Grupos(char desde, char hasta)
    {
        for (var l = desde; l <= hasta; l++)
        {
            yield return Grupo(l);
        }
    }

    /// <summary>Cuadros masculinos (bloque <c>HayMasc</c> del ASP).</summary>
    public static readonly IReadOnlyList<DefinicionCuadro> CuadrosMasculinos =
    [
        new("VerPrimera", 1, "Final", "Primera categoría", NivelNombre: 1, TituloSinCat2: "Cuadro final"),
        new("VerPrimeraC", 1, "Consola", "Cuadro de consolación"),
        new("VerPrevia", 21, "Final", "Cuadro de previa"),
        new("VerSegunda", 2, "Final", "Segunda categoría", NivelNombre: 2),
        new("VerSegundaC", 2, "Consola", "Consolación de segunda categoría"),
        new("VerTercera", 3, "Final", "Tercera categoría"),
        new("VerLiguilla3", 3, "Liguilla", "Puestos 15.º a 17.º"),
        new("VerTerceraC", 3, "Consola", "Consolación de tercera categoría"),
        new("VerPrePrevia", 31, "Final", "Cuadro de pre-previa"),
        new("VerCuarta", 4, "Final", "Cuarta categoría"),
        new("VerCuartaC", 4, "Consola", "Consolación de cuarta categoría"),
        new("VerQuinta", 5, "Final", "Quinta categoría"),
        new("VerQuintaC", 5, "Consola", "Consolación de quinta categoría"),
        new("VerSexta", 6, "Final", "Sexta categoría"),
        new("VerSeptima", 7, "Final", "Séptima categoría"),
        new("VerOctava", 8, "Final", "Octava categoría"),
        new("VerNovena", 9, "Final", "Novena categoría"),
        new("VerDecima", 10, "Final", "Décima categoría"),
        new("VerLiguilla", 1, "Liguilla", "Liguilla"),
        new("VerLiguilla2", 2, "Liguilla", "Puestos 15.º a 17.º"),
        Grupo('A', nivelNombre: 1),
        Grupo('B', nivelNombre: 1),
        .. Grupos('C', 'Z'),
    ];

    /// <summary>Cuadros femeninos (bloque <c>HayFem</c> del ASP).</summary>
    public static readonly IReadOnlyList<DefinicionCuadro> CuadrosFemeninos =
    [
        new("VerPrimera", 1, "Final", "Primera categoría", NivelNombre: 1, TituloSinCat2: "Cuadro final"),
        new("VerPrimeraC", 1, "Consola", "Cuadro de consolación"),
        new("VerLiguilla", 1, "Liguilla", "Liguilla"),
        new("VerLiguilla2", 2, "Liguilla", "Liguilla de segunda categoría", NivelNombre: 2),
        Grupo('A', nivelNombre: 2),
        Grupo('B', nivelNombre: 2),
        Grupo('C'),
        Grupo('D', nivelNombre: 1),
        .. Grupos('E', 'K'),
        new("VerSegunda", 2, "Final", "Segunda categoría", NivelNombre: 2),
        new("VerSegundaC", 2, "Consola", "Consolación de segunda categoría"),
        new("VerTercera", 3, "Final", "Tercera categoría", NivelNombre: 3),
        new("VerLiguilla3", 3, "Liguilla", "Tercera categoría", NivelNombre: 3),
        new("VerTerceraC", 3, "Consola", "Consolación de tercera categoría"),
        new("VerPrevia", 21, "Final", "Cuadro de previa"),
        new("VerPrePrevia", 31, "Final", "Cuadro de pre-previa"),
        new("VerCuarta", 4, "Final", "Cuarta categoría", NivelNombre: 4),
        new("VerCuartaC", 4, "Consola", "Consolación de cuarta categoría"),
        new("VerQuinta", 5, "Final", "Quinta categoría", NivelNombre: 5),
        new("VerQuintaC", 5, "Consola", "Consolación de quinta categoría"),
        new("VerSexta", 6, "Final", "Sexta categoría"),
    ];

    /// <summary>Cuadros mixtos (bloque <c>HayMix</c> del ASP).</summary>
    public static readonly IReadOnlyList<DefinicionCuadro> CuadrosMixtos =
    [
        new("VerPrimera", 1, "Final", "Primera categoría", NivelNombre: 1, CategoriaNombre: "ABSOLUTO"),
        new("VerPrimeraC", 1, "Consola", "Cuadro de descenso"),
        new("VerLiguilla", 1, "Liguilla", "Liguilla"),
        new("VerSegunda", 2, "Final", "Segunda categoría", NivelNombre: 2, CategoriaNombre: "ABSOLUTO"),
        new("VerSegundaC", 2, "Consola", "Cuadro de descenso de segunda categoría"),
        new("VerTercera", 3, "Final", "Tercera categoría", NivelNombre: 3),
        new("VerTerceraC", 3, "Consola", "Consolación de tercera categoría, grupo A"),
        new("VerPrevia", 21, "Final", "Cuadro de previa"),
        new("VerPrePrevia", 31, "Final", "Cuadro de pre-previa"),
        new("VerCuarta", 4, "Final", "Cuarta categoría", NivelNombre: 4),
        new("VerCuartaC", 4, "Consola", "Consolación de tercera categoría, grupo B"),
        new("VerQuinta", 5, "Final", "Quinta categoría", NivelNombre: 5),
        new("VerSexta", 6, "Final", "Sexta categoría"),
        Grupo('A'),
        Grupo('B'),
    ];

    /// <summary>Definiciones de un género (<c>M</c>, <c>F</c>, <c>X</c>); vacío si no es ninguno.</summary>
    public static IReadOnlyList<DefinicionCuadro> Definiciones(string genero) => genero switch
    {
        "M" => CuadrosMasculinos,
        "F" => CuadrosFemeninos,
        "X" => CuadrosMixtos,
        _ => [],
    };

    /// <summary>Todas las columnas <c>Ver…</c> que se leen de <c>SFEP_Torneos_Desglose</c> (sin repetir).</summary>
    public static readonly IReadOnlyList<string> ColumnasVisibilidad = CuadrosMasculinos
        .Concat(CuadrosFemeninos).Concat(CuadrosMixtos)
        .Select(d => d.Columna)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Modalidades con cuadros en el ASP (<c>select case ModalidadTorneo</c>; con otro valor no se pintaba ninguno).
    /// </summary>
    public static readonly IReadOnlySet<string> ModalidadesConCuadros =
        new HashSet<string>(new[] { "ABSOLUTO", "SEL_ABS", "MENORES", "SEL_MEN", "VETERANOS", "SEL_VET" }, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cuadros visibles de una fila del desglose, en el orden del ASP.
    /// </summary>
    /// <param name="genero"><c>M</c> | <c>F</c> | <c>X</c>.</param>
    /// <param name="categoria"><c>Tipo</c> del desglose, recortado.</param>
    /// <param name="fila">Columnas de la fila (nombre → valor; sin distinguir mayúsculas).</param>
    /// <param name="nombre">Nombre personalizado para una clave (o <c>null</c>).</param>
    /// <param name="url">URL del cuadro a partir de nivel y cuadro.</param>
    public static IReadOnlyList<CuadroEquiposDto> Cuadros(
        string genero,
        string categoria,
        IReadOnlyDictionary<string, object?> fila,
        Func<ClaveNombreCuadro, string?> nombre,
        Func<int, string, string> url)
    {
        var sinCat2 = EsCero(fila.GetValueOrDefault("Cat2"));
        var cuadros = new List<CuadroEquiposDto>();
        foreach (var d in Definiciones(genero))
        {
            if (!EsSi(fila.GetValueOrDefault(d.Columna)))
            {
                continue;
            }

            var titulo = sinCat2 && d.TituloSinCat2 is not null ? d.TituloSinCat2 : d.Titulo;
            if (d.NivelNombre is { } nivelNombre)
            {
                var clave = new ClaveNombreCuadro(nivelNombre, d.CategoriaNombre ?? categoria, genero, d.CuadroNombre ?? d.Cuadro);
                titulo = nombre(clave) ?? titulo;
            }

            cuadros.Add(new CuadroEquiposDto(titulo, d.Nivel, d.Cuadro, TipoCuadro(d.Nivel, d.Cuadro), url(d.Nivel, d.Cuadro)));
        }

        return cuadros;
    }

    /// <summary>Tipo público de un cuadro: <c>previa</c> (niveles 21 y 31), <c>final</c>, <c>consolacion</c>, <c>liguilla</c> o <c>grupo</c>.</summary>
    public static string TipoCuadro(int nivel, string cuadro) => cuadro switch
    {
        "Consola" => "consolacion",
        "Liguilla" => "liguilla",
        _ when cuadro.StartsWith("Grupo", StringComparison.Ordinal) => "grupo",
        _ => nivel is 21 or 31 ? "previa" : "final",
    };

    // ---------------------------------------------------------------------------------------------
    // Inscripción de equipos
    // ---------------------------------------------------------------------------------------------

    /// <summary>Plazo abierto: el ASP lo daba por cerrado si <c>FechaInscripcion</c> estaba vacía o ya había pasado.</summary>
    public static bool InscripcionAbierta(DateTime? fechaInscripcion, DateTime ahora)
        => fechaInscripcion is { } fecha && fecha > ahora;

    /// <summary>
    /// Pasos de la inscripción (bloque "¿QUÉ DESEAS HACER?" del ASP) con sus excepciones por torneo, ya en
    /// <c>Contenidos/campeonatos-equipos.json</c>. <paramref name="url"/> convierte una ruta de la web actual
    /// (p. ej. <c>Camp_Equipos_Nuevo.asp?idtorneo=1</c>) en absoluta.
    /// </summary>
    public static IReadOnlyList<AccionInscripcionDto> Acciones(
        int id,
        bool preinscripcion,
        DateTime? fechaPreinscripcion,
        bool porClubes,
        string? circuito,
        DateTime ahora,
        ContenidoCampeonatosEquipos contenido,
        Func<string, string> url)
    {
        var acciones = new List<AccionInscripcionDto>();
        var quien = porClubes ? "del club" : "de la federación";
        string Ruta(string pagina) => url($"{pagina}?idtorneo={id}");

        if (preinscripcion)
        {
            if (fechaPreinscripcion is { } pre && pre > ahora && porClubes)
            {
                acciones.Add(new("preinscripcion-club",
                    "Realizar la preinscripción del club para obtener el código de inscripción",
                    "Es el primer paso: el pago de la inscripción, para obtener el código con el que se inscriben los equipos.",
                    Ruta("Camp_Equipos_InscPrevia.asp"), pre));
            }

            if (porClubes && !contenido.SinListadoClubes.Contains(id))
            {
                acciones.Add(new("clubes-preinscritos", "Ver el listado de clubes preinscritos", null,
                    Ruta("Camp_Equipos_Clubes.asp"), null));
            }
        }
        else
        {
            if (!contenido.SinPagoEquipos.Contains(id))
            {
                acciones.Add(new("pago-equipos",
                    "Realizar el pago de los equipos del club para obtener el código de inscripción",
                    "Es el primer paso para obtener el código con el que se inscriben los equipos.",
                    Ruta("Camp_Equipos_InscPrevia.asp"), null));
            }

            acciones.Add(new("clubes-pagados", "Ver el listado de clubes que han realizado el pago", null,
                Ruta("Camp_Equipos_Clubes.asp"), null));
        }

        if (string.Equals(circuito?.Trim(), "COPA1", StringComparison.OrdinalIgnoreCase))
        {
            acciones.Add(new("excel-clasificados",
                "Aún no tengo los equipos clasificados y quiero enviar un Excel con los posibles clasificados",
                $"Podrás enviar el Excel cuando recibas el código {quien} para este campeonato.",
                Ruta("Camp_Equipos_Copa_Excell.asp"), null));
            acciones.Add(new("nuevo-equipo",
                "Ya tengo los equipos clasificados y quiero crear un equipo nuevo",
                $"Podrás crear equipos nuevos cuando recibas el código {quien} para este campeonato.",
                Ruta("Camp_Equipos_Nuevo.asp"), null));
        }
        else
        {
            acciones.Add(new("nuevo-equipo", "Crear un equipo nuevo",
                $"Podrás crear equipos nuevos cuando recibas el código {quien} para este campeonato.",
                Ruta("Camp_Equipos_Nuevo.asp"), null));
        }

        acciones.Add(new("modificar-equipo", "Modificar, eliminar o pagar un equipo ya creado",
            $"Necesitas el código de creación del equipo, que se envió al responsable {quien} y al capitán. " +
            "Desde aquí puedes modificar o eliminar el equipo y añadir o quitar jugadores.",
            Ruta("Camp_Equipos_Modificar.asp"), null));

        return acciones;
    }

    // ---------------------------------------------------------------------------------------------
    // Normalización de valores heredados
    // ---------------------------------------------------------------------------------------------

    /// <summary><c>'S'</c> (con o sin espacios de relleno) → <c>true</c>.</summary>
    public static bool EsSi(object? valor)
        => string.Equals(Convert.ToString(valor, CultureInfo.InvariantCulture)?.Trim(), "S", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>Cat2 = 0</c> del ASP: vacío o cero → <c>true</c> (en VBScript <c>Null &lt;&gt; 0</c> no es verdadero, así que
    /// sin valor también se pintaba "Cuadro final").
    /// </summary>
    public static bool EsCero(object? valor)
    {
        var texto = Convert.ToString(valor, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            return true;
        }

        return decimal.TryParse(texto.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) && n == 0;
    }

    /// <summary>Coordenada guardada como texto ("40,4168" o "40.4168") dentro de <paramref name="limite"/>; si no, <c>null</c>.</summary>
    public static double? Coordenada(string? valor, double limite)
    {
        var texto = valor?.Trim().Replace(',', '.');
        return !string.IsNullOrEmpty(texto)
               && double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
               && double.IsFinite(n) && Math.Abs(n) <= limite
            ? n
            : null;
    }

    /// <summary>Tipo de adjunto (<c>Icono</c>: "pdf", "xls", "pic"…) en minúsculas; vacío o sin letras ("-- ") → <c>null</c>.</summary>
    public static string? TipoAdjunto(string? icono)
    {
        var t = icono?.Trim().ToLowerInvariant();
        return !string.IsNullOrEmpty(t) && t.Length <= 5 && t.All(char.IsAsciiLetterOrDigit) && t.Any(char.IsAsciiLetter) ? t : null;
    }
}
