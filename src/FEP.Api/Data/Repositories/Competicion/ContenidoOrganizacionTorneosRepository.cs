using System.Text.Json;
using FEP.Api.Contracts.Competicion;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Parte escrita a mano de la convocatoria para organizar torneos.</summary>
public interface IContenidoOrganizacionTorneosRepository
{
    /// <summary>Temporada, plazo, textos, documentos (sin fichero), pasos y cláusula de protección de datos.</summary>
    Task<ContenidoOrganizacionTorneosDto> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Organizacion_Torneos.asp</c> y <c>Organizacion_FueraPlazo.asp</c>. En el ASP la temporada
/// (<c>Año = '2027'</c> en seis consultas y en <c>BarraLateralLicitacion.inc</c>), la fecha límite
/// (<c>"20/11/26 14:00:00"</c>), los títulos de los documentos, el aviso de convocatoria cerrada y la cláusula de
/// protección de datos estaban escritos a mano en el código y había que editar el ASP cada año; aquí pasan a
/// <c>Contenidos/organizacion-torneos.json</c>. Los ficheros de los documentos siguen saliendo de la BD
/// (<c>SFEP_Calendario_Archivo</c>). Cuando exista modelo en SQL Server (ver
/// docs/api/analisis/Organizacion_Torneos.md), basta con sustituir esta implementación.
/// </summary>
public sealed class ContenidoOrganizacionTorneosRepository(IWebHostEnvironment entorno) : IContenidoOrganizacionTorneosRepository
{
    /// <summary>Ruta del fichero de contenido, relativa a la raíz del proyecto.</summary>
    public const string RutaContenido = "Contenidos/organizacion-torneos.json";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<ContenidoOrganizacionTorneosDto> ObtenerAsync(CancellationToken cancellationToken)
    {
        var fichero = entorno.ContentRootFileProvider.GetFileInfo(RutaContenido);
        if (!fichero.Exists)
        {
            throw new FileNotFoundException($"No se encuentra el contenido de la organización de torneos ({RutaContenido}).");
        }

        await using var flujo = fichero.CreateReadStream();
        var contenido = await JsonSerializer.DeserializeAsync<ContenidoOrganizacionTorneosDto>(flujo, OpcionesJson, cancellationToken)
            ?? throw new InvalidDataException($"{RutaContenido} está vacío o no es válido.");

        if (contenido.Temporada is < ReglasCampeonatos.AnioMinimo or > ReglasCampeonatos.AnioMaximo)
        {
            throw new InvalidDataException($"{RutaContenido}: la temporada debe estar entre {ReglasCampeonatos.AnioMinimo} y {ReglasCampeonatos.AnioMaximo}.");
        }

        // El JSON puede venir sin título aunque el contrato lo declare obligatorio.
        if (string.IsNullOrWhiteSpace(contenido.Titulo))
        {
            throw new InvalidDataException($"{RutaContenido}: falta el título de la convocatoria.");
        }

        return contenido;
    }
}
