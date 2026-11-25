namespace FEP.Api.Contracts.Noticias;

/// <summary>Noticia destacada de la portada (tabla <c>SFEP_Noticias</c>, Destacada='S', Site='FEP').</summary>
/// <param name="IdNoticia">Identificador (<c>IdNoticia</c>, autonumérico): el más alto es la noticia más reciente.
/// El front lo usa para colocar las noticias nuevas en la sección superior.</param>
/// <param name="Titulo">Titular (<c>Titulo</c>) con las comillas <c>``</c> ya convertidas a <c>"</c>.</param>
/// <param name="Imagen">Nombre de fichero original (<c>foto</c>), relativo a <c>refs/Noticias/</c>.</param>
/// <param name="ImagenUrl">URL absoluta de la imagen (<c>Media:BaseUrl</c> + <c>Noticias/</c> + foto).</param>
/// <param name="Enlace">Destino de la noticia (<c>Link</c>).</param>
/// <param name="Ventana">Destino del enlace (<c>Ventana</c>).</param>
/// <param name="Orden">Posición en portada (<c>Orden</c>, distinto de 0).</param>
public sealed record NoticiaDestacadaDto(
    int IdNoticia,
    string Titulo,
    string? Imagen,
    string? ImagenUrl,
    string? Enlace,
    string? Ventana,
    int Orden);
