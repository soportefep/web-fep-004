using System.Text.Json;
using FEP.Api.Contracts.Competicion;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Calendarios de pruebas autonómicas puntuables para los rankings FEP (menores, veteranos).</summary>
public interface ICalendariosAutonomicosRepository
{
    /// <summary>
    /// Calendario de la categoría indicada (ya normalizada con <see cref="ReglasCalendariosAutonomicos.Normalizar"/>),
    /// o <c>null</c> si todavía no se ha publicado su contenido.
    /// </summary>
    Task<CalendarioAutonomicoDto?> ObtenerAsync(string categoria, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Calendario_Menores_FFAA.asp</c> y <c>Calendario_Veteranos_FFAA.asp</c>. En el ASP
/// las pruebas estaban escritas a mano en el HTML, una tabla por federación, y las "verdes" (clase
/// <c>creativ-shortcode-alertbox-colour-theme</c>) eran las incluidas en el ranking. Las consultas a
/// <c>SFEP_Calendario</c>, <c>SFEP_Calendario_Archivo</c>, <c>SFEP_Federaciones</c> y <c>SFEP_Noticias</c> se
/// ejecutaban pero no se pintaban. Aquí el contenido pasa a <c>Contenidos/calendarios-autonomicos/{categoria}.json</c>.
/// Cuando exista modelo en SQL Server (ver docs/api/analisis/Calendario_Menores_FFAA.md), basta con sustituir
/// esta implementación por una con Dapper: el contrato no cambia.
/// </summary>
public sealed class CalendariosAutonomicosRepository(IWebHostEnvironment entorno) : ICalendariosAutonomicosRepository
{
    /// <summary>Carpeta de los ficheros de contenido, relativa a la raíz del proyecto.</summary>
    public const string CarpetaContenido = "Contenidos/calendarios-autonomicos";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<CalendarioAutonomicoDto?> ObtenerAsync(string categoria, CancellationToken cancellationToken)
    {
        // Solo categorías de la lista blanca: el nombre del fichero nunca sale tal cual de la petición.
        var clave = ReglasCalendariosAutonomicos.Normalizar(categoria)
            ?? throw new ArgumentOutOfRangeException(nameof(categoria), categoria, "Categoría de calendario no admitida.");

        var ruta = $"{CarpetaContenido}/{clave}.json";
        var fichero = entorno.ContentRootFileProvider.GetFileInfo(ruta);
        if (!fichero.Exists)
        {
            return null;
        }

        await using var flujo = fichero.CreateReadStream();
        var contenido = await JsonSerializer.DeserializeAsync<CalendarioAutonomicoDto>(flujo, OpcionesJson, cancellationToken)
            ?? throw new InvalidDataException($"{ruta} está vacío o no es válido.");

        // Las listas que el fichero omite llegan vacías, nunca null; la categoría es siempre la pedida.
        return contenido with
        {
            Categoria = clave,
            Introduccion = contenido.Introduccion ?? [],
            ActualizacionesRanking = contenido.ActualizacionesRanking is { } act
                ? act with { Momentos = (act.Momentos ?? []).OrderBy(m => m.Orden).ToArray() }
                : null,
            Federaciones = (contenido.Federaciones ?? [])
                .Select(f => f with
                {
                    Pruebas = (f.Pruebas ?? [])
                        .Select(p => p with
                        {
                            FechaFin = p.FechaFin ?? p.FechaInicio,
                            Fases = (p.Fases ?? [])
                                .Select(fase => fase with { FechaFin = fase.FechaFin ?? fase.FechaInicio })
                                .ToArray(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }
}
