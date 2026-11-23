namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Listado de campeonatos y torneos FEP de una temporada (página <c>Campeonatos.asp</c>): torneos de la
/// FEP (<c>SFEP_Torneos.IdFederacion = 0</c>) visibles, no borrados, que no son de empresas ni del
/// Programa Impulso, del más reciente al más antiguo por fecha de inicio.
/// </summary>
/// <param name="Anio">
/// Temporada consultada (<c>SFEP_Torneos.Año</c>). Sin parámetro <c>anio</c> es la más reciente de
/// <paramref name="Anios"/>; <c>null</c> solo si no hay ningún torneo publicado.
/// </param>
/// <param name="Anios">Temporadas con torneos publicados, de la más reciente a la más antigua (desplegable "Año").</param>
/// <param name="Tipo">Filtro de tipo aplicado (<c>parejas</c> | <c>equipos</c>), si lo hay.</param>
/// <param name="Categoria">Filtro de categoría aplicado (<c>absoluto</c> | <c>menores</c> | <c>veteranos</c>), si lo hay.</param>
/// <param name="Items">Torneos de la temporada que cumplen los filtros, por fecha de inicio descendente.</param>
public sealed record CampeonatosDto(
    int? Anio,
    IReadOnlyList<int> Anios,
    string? Tipo,
    string? Categoria,
    IReadOnlyList<CampeonatoDto> Items);

/// <summary>Campeonato o torneo del listado.</summary>
/// <param name="Id"><c>SFEP_Torneos.IdPrueba</c> (el <c>IdTorneo</c> de las fichas de la web actual).</param>
/// <param name="Anio">Temporada del torneo (<c>Año</c>); puede no coincidir con el año de <paramref name="FechaInicio"/>.</param>
/// <param name="Titulo">Nombre del torneo (<c>Torneo</c>), tal cual (puede traer emojis).</param>
/// <param name="Tipo">Formato (<c>TipoTorneo</c>): <c>parejas</c> o <c>equipos</c>.</param>
/// <param name="Categoria">
/// Categoría de juego según <c>ModalidadTorneo</c>: <c>absoluto</c> (<c>ABSOLUTO</c>, <c>SEL_ABS</c>),
/// <c>menores</c> (<c>MENORES</c>, <c>SEL_MEN</c>) o <c>veteranos</c> (<c>VETERANOS</c>, <c>SEL_VET</c>);
/// <c>null</c> si el valor es otro.
/// </param>
/// <param name="Seleccion"><c>true</c> en los campeonatos de selecciones autonómicas (<c>SEL_ABS</c>, <c>SEL_MEN</c>, <c>SEL_VET</c>).</param>
/// <param name="SillaRuedas"><c>true</c> si es de pádel en silla de ruedas (<c>ABSOLUTO</c> con <c>Circuito = 'SILLA'</c>).</param>
/// <param name="FechaInicio">Primer día (<c>FechaInicio</c>).</param>
/// <param name="FechaFin">Último día (<c>FechaFin</c>).</param>
/// <param name="Ciudad">Localidad (<c>Ciudad</c>).</param>
/// <param name="Clubes">
/// Sede: nombre del club (<c>SFEP_Clubes.Club</c> de <c>SFEP_Torneos.Club</c>) y, si lo hay, el segundo
/// club escrito a mano (<c>Club2</c>). Vacío si no está definida (el ASP pintaba "Sin definir").
/// </param>
/// <param name="Enlace">
/// URL absoluta de la ficha del torneo: el enlace a los cuadros (<c>LinkCuadros</c>) si lo tiene o, si no,
/// la página de la web actual a la que redirigía <c>Torneos_Previo.asp</c> (<c>Camp_Parejas.asp</c>,
/// <c>Camp_Equipos.asp</c>, <c>Camp_Liga.asp</c>, <c>Camp_Clasificatorio.asp</c> o el circuito de menores).
/// </param>
/// <param name="NuevaVentana"><c>true</c> si el enlace es externo (<c>LinkCuadros</c>): el ASP lo abría en una ventana nueva.</param>
/// <param name="Inscripcion">Plazo de inscripción abierto, o <c>null</c> si está cerrado.</param>
public sealed record CampeonatoDto(
    int Id,
    int Anio,
    string Titulo,
    string Tipo,
    string? Categoria,
    bool Seleccion,
    bool SillaRuedas,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string? Ciudad,
    IReadOnlyList<string> Clubes,
    string? Enlace,
    bool NuevaVentana,
    PlazoInscripcionDto? Inscripcion);

/// <summary>Plazo de inscripción abierto en el momento de la consulta.</summary>
/// <param name="Tipo">
/// <c>inscripcion</c> (parejas), <c>inscripcion-equipos</c> o <c>preinscripcion</c> (equipos con
/// preinscripción previa a la inscripción definitiva).
/// </param>
/// <param name="FechaLimite">Fecha y hora tope (<c>FechaInscripcion</c> o <c>FechaPreInscripcion</c>, hora local de España, sin zona).</param>
public sealed record PlazoInscripcionDto(string Tipo, DateTime FechaLimite);
