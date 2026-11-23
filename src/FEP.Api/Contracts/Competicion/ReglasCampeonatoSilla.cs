namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Un cuadro posible de un género en <c>Camp_Silla.asp</c>: columna <c>Ver…</c> de <c>SFEP_Torneos_Desglose</c> que lo
/// hace visible, destino en <c>Previo.asp</c> y nombre por defecto.
/// </summary>
/// <param name="Columna">Columna de visibilidad (<c>VerPrimera</c>, <c>VerGrupoA</c>…).</param>
/// <param name="Nivel">Nivel del enlace.</param>
/// <param name="Cuadro">Cuadro del enlace (<c>Final</c>, <c>Consola</c>, <c>GrupoA</c>, <c>GrupoB</c>).</param>
/// <param name="Titulo">Nombre por defecto.</param>
/// <param name="BuscaNombre">El ASP buscaba un nombre personalizado en <c>SFEP_Torneos_Cuadros_Nombres</c> (solo en mixto).</param>
/// <param name="TituloSinCat2">Nombre por defecto si el desglose no tiene segunda categoría (<c>Cat2 = 0</c>).</param>
/// <param name="CategoriaDelDesglose">El enlace lleva <c>Cat={Tipo}</c> del desglose en lugar de <c>Cat=ABSOLUTO</c> fijo.</param>
public sealed record DefinicionCuadroSilla(
    string Columna,
    int Nivel,
    string Cuadro,
    string Titulo,
    bool BuscaNombre = false,
    string? TituloSinCat2 = null,
    bool CategoriaDelDesglose = false);

/// <summary>Reglas heredadas de <c>Camp_Silla.asp</c> (redirecciones, cuadros y cuadro de honor).</summary>
public static class ReglasCampeonatoSilla
{
    /// <summary>Id mínimo admitido (<c>IdPrueba</c>).</summary>
    public const int IdMinimo = 1;

    /// <summary>Torneo que el ASP mandaba siempre a <c>Camp_Parejas_Copa.asp</c> (escrito a mano: <c>if clng(id)=20255</c>).</summary>
    public const int IdCopaParejas = 20255;

    /// <summary><c>SFEP_RankingTorneos.Ranking</c> del cuadro de honor (ranking de pádel en silla de ruedas).</summary>
    public const int RankingSilla = 15;

    /// <summary>Categoría fija de los enlaces y de los nombres personalizados (<c>Cat=ABSOLUTO</c> en el ASP).</summary>
    public const string CategoriaCuadros = "ABSOLUTO";

    /// <summary>Prefijo de las licencias de pádel en silla de ruedas.</summary>
    public const string PrefijoLicencia = "PS";

    // ---------------------------------------------------------------------------------------------
    // Cuadros. Copia del orden y los niveles de los tres bloques del ASP (no son iguales entre sí). Solo el
    // mixto buscaba nombres personalizados (siempre con Categoria = 'ABSOLUTO'). Los textos por defecto se
    // han unificado con los de Camp_Equipos (ver análisis).
    // ---------------------------------------------------------------------------------------------
    private static readonly string[] Ordinales =
        ["Primera", "Segunda", "Tercera", "Cuarta", "Quinta", "Sexta", "Séptima", "Octava", "Novena", "Décima"];

    private static readonly string[] Columnas =
        ["VerPrimera", "VerSegunda", "VerTercera", "VerCuarta", "VerQuinta", "VerSexta", "VerSeptima", "VerOctava", "VerNovena", "VerDecima"];

    private static DefinicionCuadroSilla Categoria(int nivel, bool buscaNombre = false)
        => new(Columnas[nivel - 1], nivel, "Final", $"{Ordinales[nivel - 1]} categoría", buscaNombre);

    /// <summary>Cuadros masculinos (bloque <c>HayMasc</c> del ASP).</summary>
    public static readonly IReadOnlyList<DefinicionCuadroSilla> CuadrosMasculinos =
    [
        new("VerPrimera", 1, "Final", "Primera categoría", TituloSinCat2: "Cuadro final"),
        new("VerPrimeraC", 1, "Consola", "Cuadro de consolación", CategoriaDelDesglose: true),
        Categoria(2),
        Categoria(3),
        new("VerPrevia", 21, "Final", "Cuadro de previa"),
        new("VerPrePrevia", 31, "Final", "Cuadro de pre-previa"),
        Categoria(4),
        Categoria(5),
        Categoria(6),
        Categoria(7),
        Categoria(8),
        Categoria(9),
        Categoria(10),
    ];

    /// <summary>Cuadros femeninos (bloque <c>HayFem</c> del ASP).</summary>
    public static readonly IReadOnlyList<DefinicionCuadroSilla> CuadrosFemeninos =
    [
        new("VerPrimera", 1, "Final", "Primera categoría", TituloSinCat2: "Cuadro final"),
        new("VerPrimeraC", 1, "Consola", "Cuadro de consolación", CategoriaDelDesglose: true),
        Categoria(2),
        Categoria(3),
        new("VerPrevia", 21, "Final", "Cuadro de previa"),
        new("VerPrePrevia", 31, "Final", "Cuadro de pre-previa"),
        Categoria(4),
        Categoria(5),
        new("VerGrupoA", 1, "GrupoA", "Grupo A"),
        new("VerGrupoB", 1, "GrupoB", "Grupo B"),
        Categoria(6),
    ];

    /// <summary>
    /// Cuadros mixtos (bloque <c>HayMix</c> del ASP, titulado "Femeninos" por error). El nombre por defecto de la
    /// primera categoría es siempre "Cuadro final": el ASP lo sobrescribía con la búsqueda aunque hubiera <c>Cat2</c>.
    /// </summary>
    public static readonly IReadOnlyList<DefinicionCuadroSilla> CuadrosMixtos =
    [
        new("VerPrimera", 1, "Final", "Cuadro final", BuscaNombre: true),
        Categoria(2, buscaNombre: true),
        new("VerPrimeraC", 1, "Consola", "Consolación del cuadro final", BuscaNombre: true),
        new("VerSegundaC", 2, "Consola", "Puestos 7.º y 8.º", BuscaNombre: true),
        Categoria(3, buscaNombre: true),
        new("VerTerceraC", 3, "Consola", "Puestos 13.º a 16.º", BuscaNombre: true),
        new("VerPrevia", 21, "Final", "Cuadro de previa", BuscaNombre: true),
        new("VerPrePrevia", 31, "Final", "Cuadro de pre-previa"),
        Categoria(4, buscaNombre: true),
        Categoria(5, buscaNombre: true),
        Categoria(6),
    ];

    /// <summary>Definiciones de un género (<c>M</c>, <c>F</c>, <c>X</c>); vacío si no es ninguno.</summary>
    public static IReadOnlyList<DefinicionCuadroSilla> Definiciones(string genero) => genero switch
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
    /// Cuadros publicados: el ASP solo los pintaba con <c>VerCuadros = 'S'</c> en el torneo y en <c>ABSOLUTO</c>
    /// (<c>select case ModalidadTorneo</c>; menores y veteranos se redirigían, el resto no pintaba ninguno).
    /// </summary>
    public static bool CuadrosPublicados(string? verCuadros, string? modalidad)
        => ReglasCampeonatoEquipos.EsSi(verCuadros)
           && string.Equals(modalidad?.Trim(), CategoriaCuadros, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cuadros visibles de una fila del desglose, en el orden del ASP.
    /// </summary>
    /// <param name="genero"><c>M</c> | <c>F</c> | <c>X</c>.</param>
    /// <param name="categoria"><c>Tipo</c> del desglose, recortado (solo lo usaban las consolaciones masculina y femenina).</param>
    /// <param name="fila">Columnas de la fila (nombre → valor; sin distinguir mayúsculas).</param>
    /// <param name="nombre">Nombre personalizado para una clave (o <c>null</c>).</param>
    /// <param name="url">URL del cuadro a partir de nivel, cuadro y categoría.</param>
    public static IReadOnlyList<CuadroEquiposDto> Cuadros(
        string genero,
        string categoria,
        IReadOnlyDictionary<string, object?> fila,
        Func<ClaveNombreCuadro, string?> nombre,
        Func<int, string, string, string> url)
    {
        var sinCat2 = ReglasCampeonatoEquipos.EsCero(fila.GetValueOrDefault("Cat2"));
        var cuadros = new List<CuadroEquiposDto>();
        foreach (var d in Definiciones(genero))
        {
            if (!ReglasCampeonatoEquipos.EsSi(fila.GetValueOrDefault(d.Columna)))
            {
                continue;
            }

            var titulo = sinCat2 && d.TituloSinCat2 is not null ? d.TituloSinCat2 : d.Titulo;
            if (d.BuscaNombre)
            {
                titulo = nombre(new ClaveNombreCuadro(d.Nivel, CategoriaCuadros, genero, d.Cuadro)) ?? titulo;
            }

            var cat = d.CategoriaDelDesglose && !string.IsNullOrWhiteSpace(categoria) ? categoria : CategoriaCuadros;
            cuadros.Add(new CuadroEquiposDto(titulo, d.Nivel, d.Cuadro, ReglasCampeonatoEquipos.TipoCuadro(d.Nivel, d.Cuadro),
                url(d.Nivel, d.Cuadro, cat)));
        }

        return cuadros;
    }

    /// <summary>
    /// Ficha que corresponde a un torneo que no se muestra en esta página (ruta relativa a la web actual o URL
    /// absoluta), o <c>null</c> si es un torneo de silla de ruedas por parejas. Reúne las redirecciones de
    /// <c>Camp_Silla.asp</c> (20255 → copa, <c>Circuito = 'INCLUSIVO'</c>, menores y veteranos), la de
    /// <c>Camp_Parejas.asp</c> (solo <c>Circuito = 'SILLA'</c> llega aquí) y el reparto de <c>Torneos_Previo.asp</c>
    /// para los demás (p. ej. los de silla por equipos van a <c>Camp_Equipos.asp</c>).
    /// </summary>
    public static string? RutaOtraFicha(
        int id, string? circuito, string? modalidad, string? tipoTorneo, int? torneoPadre, string? liga, string? empresas)
    {
        if (id == IdCopaParejas)
        {
            return $"Camp_Parejas_Copa.asp?IdTorneo={id}";
        }

        var circ = circuito?.Trim().ToUpperInvariant();
        if (circ == "INCLUSIVO")
        {
            return $"Camp_Parejas_Inclusivo.asp?IdTorneo={id}";
        }

        if (circ != "SILLA")
        {
            return ReglasCampeonatos.RutaFicha(id, modalidad, tipoTorneo, torneoPadre, liga, empresas);
        }

        var equipos = string.Equals(tipoTorneo?.Trim(), "EQUIPOS", StringComparison.OrdinalIgnoreCase);
        switch (modalidad?.Trim().ToUpperInvariant())
        {
            case "MENORES":
                return equipos ? $"PruebaEquipos_Cuadros.asp?Idtorneo={id}" : $"Prueba_CuadrosMenores.asp?Idtorneo={id}";
            case "VETERANOS":
                return equipos ? $"PruebaEquipos_Cuadros.asp?Idtorneo={id}" : $"Camp_Parejas_Veteranos.asp?Idtorneo={id}";
        }

        // Silla por equipos (Campeonato de España de selecciones…): Torneos_Previo.asp la mandaba a Camp_Equipos.asp.
        return equipos ? ReglasCampeonatos.RutaFicha(id, modalidad, tipoTorneo, torneoPadre, liga, empresas) : null;
    }

    /// <summary><c>InscripcionSolo</c>: el ASP pedía la segunda persona solo con <c>'N'</c>; cualquier otro valor es individual.</summary>
    public static bool InscripcionIndividual(string? inscripcionSolo)
        => !string.Equals(inscripcionSolo?.Trim(), "N", StringComparison.OrdinalIgnoreCase);
}
