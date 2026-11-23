using System.Text.Json;
using FEP.Api.Contracts.Federacion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Federacion;

/// <summary>Federaciones autonómicas que forman la FEP.</summary>
public interface IFederacionesAutonomicasRepository
{
    /// <summary>Listado de federaciones y bloque de reserva de Zoom.</summary>
    Task<FederacionesAutonomicasDto> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Federaciones.asp</c>. En el ASP el listado (bandera, nombre y enlace a
/// <c>Datos_Federacion.asp?Id=</c>) y el bloque "Reserva Herramienta ZOOM" con su Google Calendar
/// estaban escritos a mano en el HTML: la página no consultaba <c>SFEP_Federaciones</c>. Aquí pasan a
/// <c>Contenidos/federaciones-autonomicas.json</c>; las rutas de imagen (<c>images/…</c>, bajo
/// <c>refs/</c>) se completan con su URL absoluta usando <c>Media:BaseUrl</c>.
/// <para>
/// Solo se admite un calendario de <c>https://calendar.google.com/</c>: cualquier otra URL se descarta
/// (el front la incrusta en un <c>iframe</c>).
/// </para>
/// Cuando se gestione desde la BD (ver docs/api/analisis/Federaciones.md: columnas nuevas en
/// <c>SFEP_Federaciones</c>), basta con sustituir esta implementación por una con Dapper: el contrato no cambia.
/// </summary>
public sealed class FederacionesAutonomicasRepository(IWebHostEnvironment entorno, IMediaUrlBuilder media)
    : IFederacionesAutonomicasRepository
{
    /// <summary>Ruta del fichero de contenido, relativa a la raíz del proyecto.</summary>
    public const string RutaContenido = "Contenidos/federaciones-autonomicas.json";

    /// <summary>Único origen admitido para el calendario incrustado.</summary>
    public const string OrigenCalendario = "https://calendar.google.com/";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<FederacionesAutonomicasDto> ObtenerAsync(CancellationToken cancellationToken)
    {
        var fichero = entorno.ContentRootFileProvider.GetFileInfo(RutaContenido);
        if (!fichero.Exists)
        {
            throw new FileNotFoundException($"No se encuentra el contenido de federaciones autonómicas ({RutaContenido}).");
        }

        await using var flujo = fichero.CreateReadStream();
        var contenido = await JsonSerializer.DeserializeAsync<FederacionesAutonomicasDto>(flujo, OpcionesJson, cancellationToken)
            ?? throw new InvalidDataException($"{RutaContenido} está vacío o no es válido.");

        var zoom = contenido.ReservaZoom;
        return contenido with
        {
            Federaciones = contenido.Federaciones
                .Select(f => f with
                {
                    ImagenUrl = media.Construir(f.Imagen),
                    Web = Texto.EnlaceExterno(f.Web),
                })
                .ToArray(),
            ReservaZoom = zoom is null ? null : zoom with
            {
                ImagenUrl = media.Construir(zoom.Imagen),
                Calendario = EsCalendarioValido(zoom.Calendario) ? zoom.Calendario : null,
            },
        };
    }

    private static bool EsCalendarioValido(CalendarioEmbebidoDto? calendario)
        => calendario is not null
            && !string.IsNullOrWhiteSpace(calendario.Titulo)
            && calendario.Url?.StartsWith(OrigenCalendario, StringComparison.OrdinalIgnoreCase) == true
            && Uri.IsWellFormedUriString(calendario.Url, UriKind.Absolute);
}
