using System.Text.Json;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Parte escrita a mano del calendario FEP (aviso de la convocatoria para organizar torneos).</summary>
public interface IContenidoCalendarioRepository
{
    /// <summary>Aviso de la convocatoria con sus URL absolutas (imagen y página de la web actual).</summary>
    Task<ContenidoCalendarioDto> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>CalendarioFEP.asp</c>. El banner de la convocatoria (<c>refs/images/Torneos2027.jpg</c> con enlace a
/// <c>Organizacion_Torneos</c>, sin <c>alt</c>) estaba escrito a mano en el HTML y había que editar el ASP cada
/// año para cambiar la imagen; aquí pasa a <c>Contenidos/calendario-fep.json</c> con título, texto y alternativo.
/// Si se muestra o no lo sigue decidiendo la BD (<c>SFEP_Calendario_Archivo.HabitaSolicitud</c>). Cuando exista
/// modelo en SQL Server (ver docs/api/analisis/CalendarioFEP.md), basta con sustituir esta implementación.
/// </summary>
public sealed class ContenidoCalendarioRepository(
    IWebHostEnvironment entorno,
    IMediaUrlBuilder mediaUrl,
    IOptions<MediaOptions> media) : IContenidoCalendarioRepository
{
    /// <summary>Ruta del fichero de contenido, relativa a la raíz del proyecto.</summary>
    public const string RutaContenido = "Contenidos/calendario-fep.json";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<ContenidoCalendarioDto> ObtenerAsync(CancellationToken cancellationToken)
    {
        var fichero = entorno.ContentRootFileProvider.GetFileInfo(RutaContenido);
        if (!fichero.Exists)
        {
            throw new FileNotFoundException($"No se encuentra el contenido del calendario FEP ({RutaContenido}).");
        }

        await using var flujo = fichero.CreateReadStream();
        var contenido = await JsonSerializer.DeserializeAsync<ContenidoCalendarioDto>(flujo, OpcionesJson, cancellationToken)
            ?? throw new InvalidDataException($"{RutaContenido} está vacío o no es válido.");

        if (contenido.Licitacion is not { } licitacion)
        {
            return contenido;
        }

        // El JSON puede venir sin "enlace" aunque el contrato lo declare obligatorio.
        var enlace = licitacion.Enlace?.Trim();
        if (string.IsNullOrEmpty(enlace))
        {
            throw new InvalidDataException($"{RutaContenido}: la convocatoria (licitacion) necesita un enlace.");
        }

        return contenido with
        {
            Licitacion = licitacion with
            {
                Enlace = enlace,
                ImagenUrl = mediaUrl.Construir(licitacion.Imagen),
                EnlaceUrl = Uri.TryCreate(enlace, UriKind.Absolute, out var absoluta)
                    ? absoluta.AbsoluteUri
                    : WebActual.Url(media.Value, enlace),
            },
        };
    }
}
