using System.Text.Json;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Presentación y documentos del Programa Impulso (parte escrita a mano de la página).</summary>
public interface IContenidoProgramaImpulsoRepository
{
    /// <summary>Título, introducción, imagen y documentos vigentes del programa.</summary>
    Task<ContenidoProgramaImpulsoDto> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Impulso.asp</c>. La imagen de cabecera (<c>refs/Noticias/Impulso2017.jpg</c>) y la tabla con los
/// dos PDF del año (<c>refs/docs/Programa-Impulso-2026 15-01-26.pdf</c> y <c>refs/docs/CALENDARIO-IMPULSO-2026.pdf</c>)
/// estaban escritas a mano en el HTML; aquí pasan a <c>Contenidos/programa-impulso.json</c>. Las rutas
/// relativas (bajo <c>refs/</c>) se completan con su URL absoluta y codificada (<c>Media:BaseUrl</c>).
/// Cuando exista modelo en SQL Server (ver docs/api/analisis/Impulso.md), basta con sustituir esta
/// implementación por una con Dapper: el contrato no cambia.
/// </summary>
public sealed class ContenidoProgramaImpulsoRepository(IWebHostEnvironment entorno, IMediaUrlBuilder media)
    : IContenidoProgramaImpulsoRepository
{
    /// <summary>Ruta del fichero de contenido, relativa a la raíz del proyecto.</summary>
    public const string RutaContenido = "Contenidos/programa-impulso.json";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<ContenidoProgramaImpulsoDto> ObtenerAsync(CancellationToken cancellationToken)
    {
        var fichero = entorno.ContentRootFileProvider.GetFileInfo(RutaContenido);
        if (!fichero.Exists)
        {
            throw new FileNotFoundException($"No se encuentra el contenido del Programa Impulso ({RutaContenido}).");
        }

        await using var flujo = fichero.CreateReadStream();
        var contenido = await JsonSerializer.DeserializeAsync<ContenidoProgramaImpulsoDto>(flujo, OpcionesJson, cancellationToken)
            ?? throw new InvalidDataException($"{RutaContenido} está vacío o no es válido.");

        return contenido with
        {
            Introduccion = contenido.Introduccion ?? [],
            ImagenUrl = media.Construir(contenido.Imagen),
            Documentos = (contenido.Documentos ?? [])
                .Select(d => d with { Url = media.Construir(d.Archivo) })
                .ToArray(),
        };
    }
}
