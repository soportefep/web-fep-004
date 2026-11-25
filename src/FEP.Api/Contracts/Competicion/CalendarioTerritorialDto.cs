namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Calendario de competición de una federación territorial por temporada (página <c>CalendarioFT.asp</c>, URL
/// <c>/CalendarioFT</c>): pruebas de <c>SFEP_Calendario</c> con <c>Federacion = {id}</c> y <c>Visible = 'S'</c>,
/// por fecha de inicio (las que no tienen fecha, al final). Misma forma de temporada y de prueba que el
/// calendario FEP (<see cref="CalendarioCompeticionDto"/>), sin el aviso de la convocatoria (solo es de la FEP).
/// </summary>
/// <param name="Federacion">Federación consultada, con sus temporadas publicadas.</param>
/// <param name="Anio">
/// Temporada consultada (<c>SFEP_Calendario.Año</c>): la pedida o, sin parámetro <c>anio</c>, la del año en curso
/// si la federación la tiene publicada (si no, la más reciente anterior; si no hay, la más próxima). <c>null</c> si
/// la federación no tiene ninguna temporada publicada.
/// </param>
/// <param name="Temporadas">
/// Temporadas de la federación (con alguna prueba visible), de la más reciente a la más antigua, con el documento
/// de <c>SFEP_Calendario_Documentos</c> (<c>Fed = {id}</c>) si lo hay. El ASP ofrecía en el desplegable las
/// temporadas de la FEP, tuviera o no pruebas la federación.
/// </param>
/// <param name="Items">Pruebas de la temporada por fecha de inicio; las que no tienen fecha, al final.</param>
public sealed record CalendarioTerritorialDto(
    FederacionCalendarioTerritorialDto Federacion,
    int? Anio,
    IReadOnlyList<TemporadaCalendarioDto> Temporadas,
    IReadOnlyList<PruebaCalendarioCompeticionDto> Items);

/// <summary>Federación territorial del desplegable "Federación" de <c>CalendarioFT.asp</c>.</summary>
/// <param name="Id"><c>SFEP_Federaciones.IdFederacion</c> (1–22; parámetro <c>fed</c> del ASP).</param>
/// <param name="Territorio">
/// Comunidad o ciudad autónoma ("Andalucía", "Comunidad Valenciana"…), de <c>Contenidos/federaciones-autonomicas.json</c>;
/// si no está allí, la abreviatura de la BD.
/// </param>
/// <param name="Nombre">Nombre oficial ("Federación Andaluza de Pádel"), si se conoce.</param>
/// <param name="Abreviatura"><c>SFEP_Federaciones.Abreviatura</c>, tal cual (lo que mostraba el ASP: "ANDALUZA", "COM.VALENCIANA").</param>
/// <param name="Temporadas">Temporadas con alguna prueba visible, de la más reciente a la más antigua (vacía si no tiene calendario).</param>
public sealed record FederacionCalendarioTerritorialDto(
    int Id,
    string Territorio,
    string? Nombre,
    string? Abreviatura,
    IReadOnlyList<int> Temporadas);

/// <summary>Federación tal como sale de la BD (el controlador le añade territorio y nombre oficial).</summary>
/// <param name="Id"><c>SFEP_Federaciones.IdFederacion</c>.</param>
/// <param name="Abreviatura"><c>SFEP_Federaciones.Abreviatura</c>.</param>
/// <param name="Temporadas">Temporadas con alguna prueba visible, de la más reciente a la más antigua.</param>
public sealed record DatosFederacionCalendarioDto(int Id, string? Abreviatura, IReadOnlyList<int> Temporadas);

/// <summary>Parte del calendario territorial que sale de la BD.</summary>
/// <param name="Federacion">Federación consultada.</param>
/// <param name="Anio">Temporada consultada; <c>null</c> si la federación no tiene ninguna publicada.</param>
/// <param name="Temporadas">Temporadas de la federación con su documento.</param>
/// <param name="Items">Pruebas de la temporada.</param>
public sealed record DatosCalendarioTerritorialDto(
    DatosFederacionCalendarioDto Federacion,
    int? Anio,
    IReadOnlyList<TemporadaCalendarioDto> Temporadas,
    IReadOnlyList<PruebaCalendarioCompeticionDto> Items);
