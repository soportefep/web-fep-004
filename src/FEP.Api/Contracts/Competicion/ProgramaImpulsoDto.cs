namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Página del Programa Impulso (<c>Impulso.asp</c>, URL <c>/Programa_Impulso</c>): presentación y documentos
/// del programa (escritos a mano en el ASP; ahora en <c>Contenidos/programa-impulso.json</c>) y torneos FEP
/// del programa de una temporada (<c>SFEP_Torneos.ProgramaImpulso = 'S'</c>), del más reciente al más antiguo.
/// </summary>
/// <param name="Actualizado">Fecha de la última revisión del contenido escrito a mano.</param>
/// <param name="Titulo">Nombre del programa.</param>
/// <param name="Introduccion">Párrafos de presentación (texto plano).</param>
/// <param name="Imagen">Ruta original de la imagen de cabecera bajo <c>refs/</c> (p. ej. <c>Noticias/Impulso2017.jpg</c>).</param>
/// <param name="ImagenUrl">URL absoluta de la imagen (<c>Media:BaseUrl</c>).</param>
/// <param name="ImagenAlt">Texto alternativo de la imagen.</param>
/// <param name="Documentos">Documentos del programa (bases y calendario), en el orden de la página.</param>
/// <param name="Anio">
/// Temporada de los torneos (<c>SFEP_Torneos.Año</c>): la pedida o, sin parámetro <c>anio</c>, la más reciente
/// de <paramref name="Anios"/>; <c>null</c> solo si no hay ningún torneo del programa publicado.
/// </param>
/// <param name="Anios">Temporadas con torneos del programa, de la más reciente a la más antigua (desplegable "Año del Torneo").</param>
/// <param name="Items">Torneos del programa de la temporada, por fecha de inicio descendente (mismo formato que <c>/competicion/campeonatos</c>).</param>
public sealed record ProgramaImpulsoDto(
    DateOnly? Actualizado,
    string Titulo,
    IReadOnlyList<string> Introduccion,
    string? Imagen,
    string? ImagenUrl,
    string? ImagenAlt,
    IReadOnlyList<DocumentoProgramaImpulsoDto> Documentos,
    int? Anio,
    IReadOnlyList<int> Anios,
    IReadOnlyList<CampeonatoDto> Items);

/// <summary>Parte escrita a mano de la página (lo que se lee de <c>Contenidos/programa-impulso.json</c>).</summary>
/// <param name="Actualizado">Fecha de la última revisión del contenido.</param>
/// <param name="Titulo">Nombre del programa.</param>
/// <param name="Introduccion">Párrafos de presentación.</param>
/// <param name="Imagen">Ruta original de la imagen bajo <c>refs/</c>.</param>
/// <param name="ImagenUrl">URL absoluta de la imagen (la calcula el repositorio).</param>
/// <param name="ImagenAlt">Texto alternativo de la imagen.</param>
/// <param name="Documentos">Documentos del programa.</param>
public sealed record ContenidoProgramaImpulsoDto(
    DateOnly? Actualizado,
    string Titulo,
    IReadOnlyList<string> Introduccion,
    string? Imagen,
    string? ImagenUrl,
    string? ImagenAlt,
    IReadOnlyList<DocumentoProgramaImpulsoDto> Documentos);

/// <summary>Documento descargable del programa.</summary>
/// <param name="Clave">Identificador estable (p. ej. <c>bases-2026</c>).</param>
/// <param name="Titulo">Título visible (en el ASP, el texto en negrita sobre el botón "PDF descargar").</param>
/// <param name="Descripcion">Resumen del contenido, si lo hay.</param>
/// <param name="Temporada">Temporada a la que se refiere.</param>
/// <param name="Fecha">Fecha del documento, si se conoce (p. ej. la del nombre del fichero).</param>
/// <param name="Tipo">Formato (<c>pdf</c>).</param>
/// <param name="Archivo">Ruta original bajo <c>refs/</c> (p. ej. <c>docs/CALENDARIO-IMPULSO-2026.pdf</c>).</param>
/// <param name="Url">URL absoluta y codificada (<c>Media:BaseUrl</c>; los espacios como <c>%20</c>).</param>
public sealed record DocumentoProgramaImpulsoDto(
    string Clave,
    string Titulo,
    string? Descripcion,
    int? Temporada,
    DateOnly? Fecha,
    string? Tipo,
    string Archivo,
    string? Url);

/// <summary>Torneos del programa de una temporada (parte que sale de la BD).</summary>
/// <param name="Anio">Temporada consultada (la pedida o la más reciente); <c>null</c> si no hay torneos.</param>
/// <param name="Anios">Temporadas con torneos del programa, de la más reciente a la más antigua.</param>
/// <param name="Items">Torneos de la temporada, por fecha de inicio descendente.</param>
public sealed record TorneosProgramaImpulsoDto(int? Anio, IReadOnlyList<int> Anios, IReadOnlyList<CampeonatoDto> Items);
