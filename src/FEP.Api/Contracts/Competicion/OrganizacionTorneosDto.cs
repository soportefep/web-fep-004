namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Convocatoria para organizar las pruebas del calendario oficial FEP (página <c>Organizacion_Torneos.asp</c>, URL
/// <c>/Organizacion_Torneos</c>): estado y plazo, documentos de la convocatoria, pruebas que se pueden solicitar
/// (<c>SFEP_Calendario</c> con <c>Licitable = 'S'</c>) y los datos de los desplegables del formulario de solicitud.
/// La solicitud en sí (y los pasos siguientes: autorización firmada, oferta y fianza) sigue en la web actual:
/// el formulario se envía a <see cref="EnvioUrl"/>.
/// </summary>
/// <param name="Temporada">Temporada cuyas pruebas se licitan (en el ASP, <c>Año = '2027'</c> escrito a mano).</param>
/// <param name="Abierta">
/// <c>true</c> si la convocatoria sigue abierta en hora de Madrid (antes de <paramref name="FechaLimite"/>). El ASP
/// redirigía a <c>Organizacion_FueraPlazo.asp</c> pasada la fecha.
/// </param>
/// <param name="FechaLimite">Fin del plazo (hora local de España, sin zona; en el ASP, <c>"20/11/26 14:00:00"</c>).</param>
/// <param name="Titulo">Título de la convocatoria.</param>
/// <param name="Introduccion">Párrafos de presentación (texto plano).</param>
/// <param name="AvisoCerrado">Texto que se muestra con la convocatoria cerrada (el de <c>Organizacion_FueraPlazo.asp</c>).</param>
/// <param name="Documentos">Documentos de la convocatoria con URL absoluta (solo los que tienen fichero).</param>
/// <param name="Pasos">Pasos del proceso de solicitud, en orden (el primero es el formulario de esta página).</param>
/// <param name="Grupos">Grupos del desplegable "Selecciona el torneo", en el orden del ASP.</param>
/// <param name="Torneos">
/// Pruebas licitables de la temporada por fecha de inicio (columna "Calendario licitación" y desplegable). Vacía con la
/// convocatoria cerrada.
/// </param>
/// <param name="Comunidades">
/// Comunidades autónomas con sus provincias (desplegables "Comunidad Autónoma" y "Provincia"). Vacía con la
/// convocatoria cerrada.
/// </param>
/// <param name="ProteccionDatos">Cláusula de protección de datos del formulario (párrafos, texto plano).</param>
/// <param name="EnvioUrl">URL absoluta a la que se envía el formulario (<c>Organizacion_P0.asp</c> de la web actual, <c>POST</c>).</param>
/// <param name="Actualizado">Fecha de la última revisión del contenido escrito a mano.</param>
public sealed record OrganizacionTorneosDto(
    int Temporada,
    bool Abierta,
    DateTime? FechaLimite,
    string Titulo,
    IReadOnlyList<string> Introduccion,
    string? AvisoCerrado,
    IReadOnlyList<DocumentoOrganizacionTorneosDto> Documentos,
    IReadOnlyList<PasoOrganizacionTorneosDto> Pasos,
    IReadOnlyList<GrupoTorneosLicitablesDto> Grupos,
    IReadOnlyList<TorneoLicitableDto> Torneos,
    IReadOnlyList<ComunidadSolicitudDto> Comunidades,
    IReadOnlyList<string> ProteccionDatos,
    string EnvioUrl,
    DateOnly? Actualizado);

/// <summary>Documento de la convocatoria (calendario provisional, condiciones de organización).</summary>
/// <param name="Clave">
/// Identificador estable: <c>calendario-provisional</c> (<c>SFEP_Calendario_Archivo.ArchivosLicitacion</c>) o
/// <c>condiciones</c> (<c>SFEP_Calendario_Archivo.CalendarioLicitacion</c>).
/// </param>
/// <param name="Titulo">Título visible.</param>
/// <param name="Descripcion">Explicación breve (texto plano).</param>
/// <param name="Archivo">Ruta original bajo <c>refs/</c> (<c>docs/Bases-de-Licitacion-2027.pdf</c>).</param>
/// <param name="Url">URL absoluta y codificada (<c>Media:BaseUrl</c>).</param>
public sealed record DocumentoOrganizacionTorneosDto(string Clave, string Titulo, string? Descripcion, string Archivo, string? Url);

/// <summary>Paso del proceso de solicitud.</summary>
/// <param name="Orden">Número del paso (1, 2…).</param>
/// <param name="Titulo">Título del paso.</param>
/// <param name="Texto">Explicación breve (texto plano).</param>
public sealed record PasoOrganizacionTorneosDto(int Orden, string Titulo, string? Texto);

/// <summary>Grupo de pruebas del desplegable (las cabeceras <c>optgroup</c> del ASP).</summary>
/// <param name="Clave"><c>menores</c>, <c>absolutos</c>, <c>veteranos</c> o <c>silla</c>.</param>
/// <param name="Titulo">Texto del grupo ("Torneos de menores"…).</param>
public sealed record GrupoTorneosLicitablesDto(string Clave, string Titulo);

/// <summary>Prueba del calendario que se puede solicitar organizar.</summary>
/// <param name="Id"><c>SFEP_Calendario.IdCalendario</c>: valor del campo <c>torneo</c> del formulario.</param>
/// <param name="Titulo">Nombre de la prueba (<c>Campeonato</c>, sin espacios ni tabuladores sobrantes).</param>
/// <param name="Grupo">
/// Clave del grupo del desplegable (<see cref="GrupoTorneosLicitablesDto.Clave"/>); <c>null</c> si su categoría no está
/// en ningún grupo (el ASP la mostraba en el calendario lateral, pero no se podía elegir).
/// </param>
/// <param name="FechaInicio">Primer día (<c>FechaIni</c>).</param>
/// <param name="FechaFin">Último día (<c>FechaFin</c>).</param>
/// <param name="Fianza">
/// Fianza en euros (<c>Fianza</c>); <c>null</c> si es 0 o está vacía: "a negociar directamente con la FEP".
/// </param>
public sealed record TorneoLicitableDto(
    int Id,
    string Titulo,
    string? Grupo,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    decimal? Fianza);

/// <summary>Comunidad autónoma del desplegable, con sus provincias.</summary>
/// <param name="Id"><c>SFEP_Comunidades.IdComunidad</c> (1–19).</param>
/// <param name="Nombre"><c>Comunidad</c>, tal cual (en mayúsculas).</param>
/// <param name="Provincias">Provincias de la comunidad por nombre (<c>SFEP_Provincias</c>).</param>
public sealed record ComunidadSolicitudDto(int Id, string Nombre, IReadOnlyList<ProvinciaSolicitudDto> Provincias);

/// <summary>Provincia del desplegable.</summary>
/// <param name="Id"><c>SFEP_Provincias.IdProvincia</c>: valor del campo <c>provincia</c> del formulario.</param>
/// <param name="Nombre"><c>Provincia</c>, tal cual (en mayúsculas).</param>
public sealed record ProvinciaSolicitudDto(int Id, string Nombre);

/// <summary>Club que puede ser sede (desplegable "Club Sede"; <c>comboclubesLicitacion.asp</c>).</summary>
/// <param name="Id"><c>SFEP_Clubes.IdClub</c> como texto (hay códigos con ceros a la izquierda): valor del campo <c>club</c>.</param>
/// <param name="Nombre"><c>Club</c>, tal cual (en mayúsculas), sin espacios sobrantes.</param>
/// <param name="ProvinciaId"><c>SFEP_Clubes.Provincia</c>.</param>
public sealed record ClubSedeDto(string Id, string Nombre, int ProvinciaId);

/// <summary>Parte de la convocatoria que sale de la BD (la combina el controlador con el contenido escrito a mano).</summary>
/// <param name="Torneos">Pruebas licitables de la temporada.</param>
/// <param name="Comunidades">Comunidades con sus provincias.</param>
/// <param name="Archivos">
/// Ficheros de la convocatoria por clave de documento (<c>calendario-provisional</c>, <c>condiciones</c>): nombre del
/// fichero bajo <c>refs/docs/</c>; solo los que no están vacíos.
/// </param>
public sealed record DatosOrganizacionTorneosDto(
    IReadOnlyList<TorneoLicitableDto> Torneos,
    IReadOnlyList<ComunidadSolicitudDto> Comunidades,
    IReadOnlyDictionary<string, string> Archivos);

/// <summary>Parte escrita a mano de la convocatoria (lo que se lee de <c>Contenidos/organizacion-torneos.json</c>).</summary>
/// <param name="Actualizado">Fecha de la última revisión del contenido.</param>
/// <param name="Temporada">Temporada que se licita.</param>
/// <param name="FechaLimite">Fin del plazo, hora local de España.</param>
/// <param name="Titulo">Título de la convocatoria.</param>
/// <param name="Introduccion">Párrafos de presentación.</param>
/// <param name="AvisoCerrado">Texto con la convocatoria cerrada.</param>
/// <param name="Documentos">Títulos y descripciones de los documentos, por clave (sin fichero: lo pone la BD).</param>
/// <param name="Pasos">Pasos del proceso de solicitud.</param>
/// <param name="ProteccionDatos">Cláusula de protección de datos.</param>
/// <param name="Envio">Ruta de la web actual a la que se envía el formulario (<c>Organizacion_P0.asp</c>).</param>
public sealed record ContenidoOrganizacionTorneosDto(
    DateOnly? Actualizado,
    int Temporada,
    DateTime? FechaLimite,
    string Titulo,
    IReadOnlyList<string>? Introduccion,
    string? AvisoCerrado,
    IReadOnlyList<DocumentoContenidoOrganizacionDto>? Documentos,
    IReadOnlyList<PasoOrganizacionTorneosDto>? Pasos,
    IReadOnlyList<string>? ProteccionDatos,
    string? Envio);

/// <summary>Documento de la convocatoria tal como está en el contenido escrito a mano (sin fichero).</summary>
/// <param name="Clave"><c>calendario-provisional</c> o <c>condiciones</c>.</param>
/// <param name="Titulo">Título visible.</param>
/// <param name="Descripcion">Explicación breve.</param>
public sealed record DocumentoContenidoOrganizacionDto(string Clave, string Titulo, string? Descripcion);
