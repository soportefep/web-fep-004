namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Ranking publicado en el índice de rankings de la web (tabla <c>SFEP_Rankings_Web</c>, <c>Visible = 'S'</c>,
/// por <c>Orden</c>). Sustituye a la rejilla de <c>Rankings.asp</c>.
/// </summary>
/// <param name="Clave">
/// Identificador estable derivado del enlace (<c>Ranking_Equipos_Absolutos</c> → <c>equipos-absolutos</c>; ver
/// <see cref="ReglasRankings.Clave"/>). Sirve de ancla en la página y para elegir la página nueva del ranking.
/// </param>
/// <param name="Titulo">Texto del ranking (<c>Texto</c>), con las erratas conocidas corregidas.</param>
/// <param name="Enlace">Enlace tal cual en la BD (<c>Link</c>, p. ej. <c>Ranking_Absoluto</c>); <c>null</c> si está vacío o es <c>#</c>.</param>
/// <param name="EnlaceUrl">URL absoluta del ranking en la web actual (o la URL si <c>Link</c> ya es absoluta).</param>
/// <param name="NuevaVentana">
/// <c>true</c> solo si el enlace es de otro dominio (el ASP abría todos con <c>target="_parent"</c>, en la misma ventana).
/// </param>
/// <param name="Imagen">Ruta original de la imagen bajo <c>refs/</c> (<c>images/{Foto}</c>, p. ej. <c>images/RnkAbs.jpg</c>).</param>
/// <param name="ImagenUrl">URL absoluta de la imagen (<c>Media:BaseUrl</c>).</param>
/// <param name="Orden">Orden de aparición (<c>Orden</c>).</param>
public sealed record RankingWebDto(
    string Clave,
    string Titulo,
    string? Enlace,
    string? EnlaceUrl,
    bool NuevaVentana,
    string? Imagen,
    string? ImagenUrl,
    int Orden);
