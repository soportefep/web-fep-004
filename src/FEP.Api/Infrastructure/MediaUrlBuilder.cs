using Microsoft.Extensions.Options;

namespace FEP.Api.Infrastructure;

/// <summary>Opciones de la sección <c>Media</c>.</summary>
public sealed class MediaOptions
{
    public const string Seccion = "Media";

    /// <summary>URL absoluta de la carpeta <c>refs/</c> del sitio (p. ej. https://www.padelfederacion.es/refs/).</summary>
    public string BaseUrl { get; set; } = "https://www.padelfederacion.es/refs/";
}

/// <summary>Construye URLs absolutas de imágenes a partir de las rutas relativas guardadas en BD.</summary>
public interface IMediaUrlBuilder
{
    /// <summary>
    /// Une <c>Media:BaseUrl</c> con los segmentos indicados (cada parte se codifica para URL).
    /// Devuelve <c>null</c> si falta algún segmento. Si el último segmento ya es una URL http(s), se devuelve tal cual.
    /// </summary>
    string? Construir(params string?[] segmentos);
}

/// <inheritdoc />
public sealed class MediaUrlBuilder(IOptions<MediaOptions> opciones) : IMediaUrlBuilder
{
    private readonly string _base = opciones.Value.BaseUrl.TrimEnd('/') + "/";

    public string? Construir(params string?[] segmentos)
    {
        if (segmentos.Length == 0 || segmentos.Any(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        var ultimo = segmentos[^1]!.Trim();
        if (Uri.TryCreate(ultimo, UriKind.Absolute, out var absoluta)
            && (absoluta.Scheme == Uri.UriSchemeHttp || absoluta.Scheme == Uri.UriSchemeHttps))
        {
            return absoluta.ToString();
        }

        var partes = segmentos
            .SelectMany(s => s!.Trim().Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Where(p => p is not "." and not "..")
            .Select(p => Uri.EscapeDataString(p));

        return _base + string.Join('/', partes);
    }
}
