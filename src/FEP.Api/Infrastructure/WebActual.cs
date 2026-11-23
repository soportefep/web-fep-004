namespace FEP.Api.Infrastructure;

/// <summary>
/// Web actual (ASP clásico) a la que siguen apuntando los ficheros y páginas aún no migrados.
/// SUPUESTO: está en la raíz del mismo dominio que <c>Media:BaseUrl</c>
/// (<c>https://www.padelfederacion.es/refs/</c> → <c>https://www.padelfederacion.es/</c>).
/// </summary>
internal static class WebActual
{
    /// <summary>Raíz de la web actual, con barra final.</summary>
    public static Uri Raiz(MediaOptions opciones)
        => new(new Uri(opciones.BaseUrl, UriKind.Absolute).GetLeftPart(UriPartial.Authority) + "/");

    /// <summary>URL absoluta de una ruta de la web actual (p. ej. <c>Microsite.asp?Id=7</c>).</summary>
    public static string Url(MediaOptions opciones, string ruta) => new Uri(Raiz(opciones), ruta).AbsoluteUri;
}
