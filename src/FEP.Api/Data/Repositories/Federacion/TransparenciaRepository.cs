using System.Text.Json;
using FEP.Api.Contracts.Federacion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Federacion;

/// <summary>Información de transparencia de la FEP.</summary>
public interface ITransparenciaRepository
{
    /// <summary>Contenido actual del portal de transparencia.</summary>
    Task<TransparenciaDto> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Ley_De_Transparencia.asp</c>. En el ASP todo el contenido estaba escrito a mano en el
/// HTML (22 desplegables con textos, tablas y 39 enlaces a PDF de <c>refs/docs/</c>); las cuatro
/// consultas del ASP (noticias y convocatorias de formación) no se usaban. Aquí pasa a
/// <c>Contenidos/transparencia.json</c>. La ruta original de cada documento (<c>archivo</c>) se
/// completa con su URL absoluta (<c>url</c>) usando <c>Media:BaseUrl</c>, y las listas que un
/// apartado no usa se devuelven vacías (nunca <c>null</c>).
/// Cuando exista modelo en SQL Server (ver docs/api/analisis/Ley_De_Transparencia.md), basta con
/// sustituir esta implementación por una con Dapper: el contrato no cambia.
/// </summary>
public sealed class TransparenciaRepository(IWebHostEnvironment entorno, IMediaUrlBuilder media) : ITransparenciaRepository
{
    /// <summary>Ruta del fichero de contenido, relativa a la raíz del proyecto.</summary>
    public const string RutaContenido = "Contenidos/transparencia.json";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<TransparenciaDto> ObtenerAsync(CancellationToken cancellationToken)
    {
        var fichero = entorno.ContentRootFileProvider.GetFileInfo(RutaContenido);
        if (!fichero.Exists)
        {
            throw new FileNotFoundException($"No se encuentra el contenido de transparencia ({RutaContenido}).");
        }

        await using var flujo = fichero.CreateReadStream();
        var contenido = await JsonSerializer.DeserializeAsync<TransparenciaDto>(flujo, OpcionesJson, cancellationToken)
            ?? throw new InvalidDataException($"{RutaContenido} está vacío o no es válido.");

        return contenido with
        {
            Categorias = (contenido.Categorias ?? [])
                .Select(c => c with { Apartados = (c.Apartados ?? []).Select(Normalizar).ToArray() })
                .ToArray(),
        };
    }

    /// <summary>Listas ausentes en el JSON → vacías; documentos con URL absoluta.</summary>
    private ApartadoTransparenciaDto Normalizar(ApartadoTransparenciaDto a) => a with
    {
        Parrafos = a.Parrafos ?? [],
        Lista = a.Lista ?? [],
        Enlaces = a.Enlaces ?? [],
        Personas = (a.Personas ?? [])
            .Select(p => p with { Funciones = p.Funciones ?? [] })
            .ToArray(),
        Importes = a.Importes ?? [],
        Documentos = (a.Documentos ?? [])
            .Select(d => d with { Url = media.Construir(d.Archivo) })
            .ToArray(),
    };
}
