using System.Text.Json;
using FEP.Api.Contracts.Federacion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Federacion;

/// <summary>Órganos de gobierno de la FEP.</summary>
public interface IOrganosGobiernoRepository
{
    /// <summary>Composición actual de los órganos de gobierno.</summary>
    Task<OrganosGobiernoDto> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Organos_Gobierno.asp</c>. En el ASP todo el contenido estaba escrito a mano en el HTML
/// (no había tablas en BD); aquí pasa a <c>Contenidos/organos-gobierno.json</c>, separado de la
/// presentación. Las rutas relativas de imágenes y PDF (<c>images/…</c>, <c>docs/…</c>, bajo
/// <c>refs/</c>) se convierten en URLs absolutas con <c>Media:BaseUrl</c>.
/// Cuando exista modelo en SQL Server (ver docs/api/analisis/Organos_Gobierno.md), basta con
/// sustituir esta implementación por una con Dapper: el contrato no cambia.
/// </summary>
public sealed class OrganosGobiernoRepository(IWebHostEnvironment entorno, IMediaUrlBuilder media) : IOrganosGobiernoRepository
{
    /// <summary>Ruta del fichero de contenido, relativa a la raíz del proyecto.</summary>
    public const string RutaContenido = "Contenidos/organos-gobierno.json";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<OrganosGobiernoDto> ObtenerAsync(CancellationToken cancellationToken)
    {
        var fichero = entorno.ContentRootFileProvider.GetFileInfo(RutaContenido);
        if (!fichero.Exists)
        {
            throw new FileNotFoundException($"No se encuentra el contenido de órganos de gobierno ({RutaContenido}).");
        }

        await using var flujo = fichero.CreateReadStream();
        var contenido = await JsonSerializer.DeserializeAsync<OrganosGobiernoDto>(flujo, OpcionesJson, cancellationToken)
            ?? throw new InvalidDataException($"{RutaContenido} está vacío o no es válido.");

        return contenido with
        {
            Organos = contenido.Organos
                .Select(o => o with
                {
                    ImagenUrl = media.Construir(o.ImagenUrl),
                    Actas = o.Actas
                        .Select(a => a with { Url = media.Construir(a.Url) })
                        .OrderByDescending(a => a.Fecha)
                        .ToArray(),
                })
                .ToArray(),
        };
    }
}
