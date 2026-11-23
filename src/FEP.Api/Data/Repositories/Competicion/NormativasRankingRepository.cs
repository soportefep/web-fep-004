using System.Text.Json;
using FEP.Api.Contracts.Competicion;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Normativa de cálculo de cada ranking (texto informativo de las páginas <c>Ranking_*.asp</c>).</summary>
public interface INormativasRankingRepository
{
    /// <summary>
    /// Normativa del ranking (clave de <see cref="ReglasClasificaciones.Rankings"/>), o <c>null</c> si todavía no se
    /// ha publicado su contenido.
    /// </summary>
    Task<NormativaRankingDto?> ObtenerAsync(DefinicionClasificacion ranking, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Ranking_Absoluto.asp</c>, desplegable "Información Cálculo del Ranking" (introducción, sistema de
/// puntuación, tablas de puntos…), escrito a mano en el HTML. Aquí pasa a <c>Contenidos/rankings/{clave}.json</c>.
/// Cuando exista modelo en SQL Server (ver docs/api/analisis/Ranking_Absoluto.md), basta con sustituir esta
/// implementación por una con Dapper: el contrato no cambia.
/// </summary>
public sealed class NormativasRankingRepository(IWebHostEnvironment entorno) : INormativasRankingRepository
{
    /// <summary>Carpeta de los ficheros de contenido, relativa a la raíz del proyecto.</summary>
    public const string CarpetaContenido = "Contenidos/rankings";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<NormativaRankingDto?> ObtenerAsync(DefinicionClasificacion ranking, CancellationToken cancellationToken)
    {
        // La clave sale de la lista blanca de ReglasClasificaciones: el nombre del fichero nunca es texto de la petición.
        var clave = ReglasClasificaciones.Buscar(ranking.Clave)?.Clave
            ?? throw new ArgumentOutOfRangeException(nameof(ranking), ranking.Clave, "Ranking no admitido.");

        var ruta = $"{CarpetaContenido}/{clave}.json";
        var fichero = entorno.ContentRootFileProvider.GetFileInfo(ruta);
        if (!fichero.Exists)
        {
            return null;
        }

        await using var flujo = fichero.CreateReadStream();
        var contenido = await JsonSerializer.DeserializeAsync<NormativaRankingDto>(flujo, OpcionesJson, cancellationToken)
            ?? throw new InvalidDataException($"{ruta} está vacío o no es válido.");

        // La clave es siempre la pedida; las listas que el fichero omite llegan vacías, nunca null.
        return contenido with
        {
            Clave = clave,
            Secciones = (contenido.Secciones ?? [])
                .Select(s => s with
                {
                    Bloques = (s.Bloques ?? [])
                        .Select(b => b with
                        {
                            Elementos = b.Elementos ?? [],
                            Tabla = b.Tabla is { } tabla
                                ? tabla with { Columnas = tabla.Columnas ?? [], Filas = tabla.Filas ?? [] }
                                : null,
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }
}
