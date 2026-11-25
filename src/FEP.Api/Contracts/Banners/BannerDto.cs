namespace FEP.Api.Contracts.Banners;

/// <summary>
/// Banner de la web (tabla <c>SFEP_Banners</c>, Portal='FEP'). Se usa para los enlaces de interés
/// del pie (<c>interes</c>), los bloques de la barra lateral (<c>destacado</c>) y los eventos
/// destacados de la portada (<c>evento</c>).
/// </summary>
/// <param name="Tipo">Tipo en minúsculas: <c>interes</c>, <c>destacado</c> o <c>evento</c>.</param>
/// <param name="Titulo">Título del banner (<c>Banner</c>).</param>
/// <param name="Texto">Texto descriptivo (<c>Texto</c>); el ASP solo lo pintaba en los eventos. Puede contener HTML heredado.</param>
/// <param name="Imagen">Nombre de fichero original (<c>Imagen</c>).</param>
/// <param name="ImagenUrl">URL absoluta; la carpeta depende del tipo (evento: <c>images/Banners/</c>, destacado: <c>images/</c>, interés: <c>images/banners/</c>).</param>
/// <param name="Enlace">Destino (<c>Link</c>).</param>
/// <param name="Ventana">Destino del enlace (<c>Ventana</c>).</param>
/// <param name="Orden">Orden de aparición (<c>orden</c>).</param>
public sealed record BannerDto(
    string Tipo,
    string Titulo,
    string? Texto,
    string? Imagen,
    string? ImagenUrl,
    string? Enlace,
    string? Ventana,
    int Orden);
