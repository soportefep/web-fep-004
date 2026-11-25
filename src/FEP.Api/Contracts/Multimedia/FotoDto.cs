namespace FEP.Api.Contracts.Multimedia;

/// <summary>
/// Foto de la galería multimedia (<c>SFEP_Galeria_Multimedia_Archivos</c> unida a
/// <c>SFEP_Galeria_Multimedia</c>, Tipo='FOT', Fed=0).
/// </summary>
/// <param name="IdGaleria">Galería a la que pertenece (<c>IdGaleria</c>); la web antigua enlazaba a <c>Documentos_Multimedia_Previo.asp?id=</c>.</param>
/// <param name="Archivo">Nombre de fichero original (<c>Archivo</c>).</param>
/// <param name="ImagenUrl">URL absoluta: <c>Media:BaseUrl</c> + <c>Multimedia/{año de FechaEvento}/{Archivo}</c>; <c>null</c> si falta la fecha del evento.</param>
/// <param name="Subtitulo">Pie de foto (<c>Subtitulo</c>); úsese como texto alternativo.</param>
/// <param name="FechaEvento">Fecha del evento de la galería (<c>FechaEvento</c>).</param>
/// <param name="FechaModificacion">Última modificación del archivo (<c>FechaMod</c>); criterio de "últimas".</param>
public sealed record FotoDto(
    int IdGaleria,
    string? Archivo,
    string? ImagenUrl,
    string? Subtitulo,
    DateOnly? FechaEvento,
    DateTime? FechaModificacion);
