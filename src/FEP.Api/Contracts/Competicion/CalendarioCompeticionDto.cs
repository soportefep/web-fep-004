namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Calendario oficial de competición de la FEP de una temporada (página <c>CalendarioFEP.asp</c>, URL
/// <c>/CalendarioFEP</c>): pruebas de <c>SFEP_Calendario</c> con <c>Federacion = 0</c> y <c>Visible = 'S'</c>,
/// por fecha de inicio (las que no tienen fecha, al final), con el PDF y la fecha de actualización de cada
/// temporada (<c>SFEP_Calendario_Documentos</c>) y el aviso de la convocatoria para organizar torneos.
/// </summary>
/// <param name="Anio">
/// Temporada consultada (<c>SFEP_Calendario.Año</c>): la pedida o, sin parámetro <c>anio</c>, la del año en curso
/// si está publicada (si no, la más reciente anterior; si no hay, la más próxima). <c>null</c> solo si no hay
/// ninguna temporada publicada.
/// </param>
/// <param name="Temporadas">
/// Temporadas publicadas (desplegable "Año del Torneo", <c>Año &gt; 2014</c>), de la más reciente a la más antigua,
/// cada una con su fecha de actualización y su PDF, si los hay.
/// </param>
/// <param name="Licitacion">
/// Convocatoria para organizar torneos (banner <c>Torneos2027.jpg</c> del ASP), o <c>null</c> si está cerrada
/// (<c>SFEP_Calendario_Archivo.HabitaSolicitud &lt;&gt; 'S'</c>).
/// </param>
/// <param name="Items">Pruebas de la temporada por fecha de inicio; las que no tienen fecha, al final.</param>
public sealed record CalendarioCompeticionDto(
    int? Anio,
    IReadOnlyList<TemporadaCalendarioDto> Temporadas,
    LicitacionTorneosDto? Licitacion,
    IReadOnlyList<PruebaCalendarioCompeticionDto> Items);

/// <summary>Temporada del calendario con su documento descargable.</summary>
/// <param name="Anio">Temporada (<c>SFEP_Calendario.Año</c>).</param>
/// <param name="Actualizado">
/// Fecha de actualización del calendario (<c>SFEP_Calendario_Documentos.FechaAct</c>, bloque "Fecha de
/// Actualización" del ASP); <c>null</c> si la temporada no tiene documento.
/// </param>
/// <param name="Archivo">
/// Ruta original del PDF bajo <c>refs/</c> (<c>docs/</c> + <c>SFEP_Calendario_Documentos.Archivo</c>, p. ej.
/// <c>docs/CALENDARIO NACIONAL 2026 A 13.10.26.pdf</c>); <c>null</c> si no hay fichero (el ASP enlazaba entonces a
/// la carpeta <c>refs/docs/</c>, que da 403).
/// </param>
/// <param name="Url">URL absoluta y codificada del PDF (<c>Media:BaseUrl</c>; los espacios como <c>%20</c>).</param>
public sealed record TemporadaCalendarioDto(int Anio, DateOnly? Actualizado, string? Archivo, string? Url);

/// <summary>Convocatoria abierta para organizar los torneos del calendario (página <c>Organizacion_Torneos.asp</c>).</summary>
/// <param name="Titulo">Título del aviso (en el ASP solo existía dentro de la imagen).</param>
/// <param name="Texto">Explicación breve de la convocatoria (texto plano).</param>
/// <param name="Imagen">Ruta original del banner bajo <c>refs/</c> (<c>images/Torneos2027.jpg</c>).</param>
/// <param name="ImagenUrl">URL absoluta del banner (<c>Media:BaseUrl</c>).</param>
/// <param name="ImagenAlt">Texto alternativo del banner (el ASP no tenía).</param>
/// <param name="Enlace">Ruta de la convocatoria en la web actual (<c>Organizacion_Torneos</c>).</param>
/// <param name="EnlaceUrl">URL absoluta de la convocatoria en la web actual.</param>
public sealed record LicitacionTorneosDto(
    string Titulo,
    string? Texto,
    string? Imagen,
    string? ImagenUrl,
    string? ImagenAlt,
    string Enlace,
    string? EnlaceUrl);

/// <summary>Prueba del calendario.</summary>
/// <param name="Anio">Temporada (<c>Año</c>); puede no coincidir con el año de <paramref name="FechaInicio"/>.</param>
/// <param name="FechaInicio">Primer día (<c>FechaIni</c>); <c>null</c> en "Sin fecha asignada".</param>
/// <param name="FechaFin">Último día (<c>FechaFin</c>).</param>
/// <param name="FechaPorConfirmar">
/// <c>true</c> si la prueba no tiene fecha o tiene la fecha comodín de todo el año (1 de enero – 31 de diciembre),
/// que la gestión usa para las pruebas aún sin fecha ni sede (p. ej. "En Licitación").
/// </param>
/// <param name="Titulo">Nombre de la prueba (<c>Campeonato</c>), tal cual.</param>
/// <param name="Categoria">Categoría visible (<c>SFEP_Calendario_Categorias.TextoCategoria</c>: "Absoluta", "Menores", "SILLA RUEDAS"…).</param>
/// <param name="Sede">Sede o estado de la adjudicación (<c>Sede</c>: club, "En Licitación", "Por Designar"…).</param>
/// <param name="IdTorneo">
/// <c>SFEP_Torneos.IdPrueba</c> del torneo enlazado (<c>SFEP_Calendario.IdPrueba</c>), si existe y no está borrado.
/// </param>
/// <param name="Enlace">
/// URL absoluta de la ficha del torneo: los cuadros (<c>LinkCuadros</c>) o la ficha a la que redirige
/// <c>Torneos_Previo.asp</c>. El ASP enviaba a <c>Campeonatos_Previo.asp</c>, que no existe (la reescritura
/// <c>^Campeonatos</c> lo convertía en el listado general de campeonatos).
/// </param>
/// <param name="NuevaVentana"><c>true</c> si el enlace es externo (<c>LinkCuadros</c>).</param>
public sealed record PruebaCalendarioCompeticionDto(
    int Anio,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    bool FechaPorConfirmar,
    string Titulo,
    string? Categoria,
    string? Sede,
    int? IdTorneo,
    string? Enlace,
    bool NuevaVentana);

/// <summary>Parte del calendario que sale de la BD (la combina el controlador con el contenido escrito a mano).</summary>
/// <param name="Anio">Temporada consultada; <c>null</c> si no hay ninguna publicada.</param>
/// <param name="Temporadas">Temporadas publicadas con su documento.</param>
/// <param name="LicitacionAbierta"><c>SFEP_Calendario_Archivo.HabitaSolicitud = 'S'</c>: se muestra el aviso de la convocatoria.</param>
/// <param name="Items">Pruebas de la temporada.</param>
public sealed record DatosCalendarioCompeticionDto(
    int? Anio,
    IReadOnlyList<TemporadaCalendarioDto> Temporadas,
    bool LicitacionAbierta,
    IReadOnlyList<PruebaCalendarioCompeticionDto> Items);

/// <summary>Parte escrita a mano de la página (lo que se lee de <c>Contenidos/calendario-fep.json</c>).</summary>
/// <param name="Actualizado">Fecha de la última revisión del contenido.</param>
/// <param name="Licitacion">Aviso de la convocatoria para organizar torneos (se muestra solo si está abierta en la BD).</param>
public sealed record ContenidoCalendarioDto(DateOnly? Actualizado, LicitacionTorneosDto? Licitacion);
