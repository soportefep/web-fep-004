using System.Text.Json;
using FEP.Api.Contracts.Competicion;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Vídeos y excepciones por torneo de la ficha de campeonatos por equipos (parte escrita a mano).</summary>
public interface IContenidoCampeonatosEquiposRepository
{
    /// <summary>Contenido de <c>Contenidos/campeonatos-equipos.json</c> (listas vacías si alguna falta).</summary>
    Task<ContenidoCampeonatosEquipos> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Camp_Equipos.asp</c>. El ASP tenía escritos a mano, con <c>if IdTorneo = …</c>: tres retransmisiones
/// de YouTube del torneo 28133, los torneos sin enlace al listado de clubes preinscritos (25071, 28676, 28677,
/// 28854) o sin pago previo de equipos (23069) y el nombre "5.º y 6.º puesto" de la consolación femenina del
/// 10666. Aquí pasan a <c>Contenidos/campeonatos-equipos.json</c>. Cuando exista modelo en SQL Server (ver
/// docs/api/analisis/Camp_Equipos.md), basta con sustituir esta implementación por una con Dapper.
/// </summary>
public sealed class ContenidoCampeonatosEquiposRepository(IWebHostEnvironment entorno) : IContenidoCampeonatosEquiposRepository
{
    /// <summary>Ruta del fichero de contenido, relativa a la raíz del proyecto.</summary>
    public const string RutaContenido = "Contenidos/campeonatos-equipos.json";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<ContenidoCampeonatosEquipos> ObtenerAsync(CancellationToken cancellationToken)
    {
        var fichero = entorno.ContentRootFileProvider.GetFileInfo(RutaContenido);
        if (!fichero.Exists)
        {
            throw new FileNotFoundException($"No se encuentra el contenido de los campeonatos por equipos ({RutaContenido}).");
        }

        await using var flujo = fichero.CreateReadStream();
        var contenido = await JsonSerializer.DeserializeAsync<ContenidoCampeonatosEquipos>(flujo, OpcionesJson, cancellationToken)
            ?? throw new InvalidDataException($"{RutaContenido} está vacío o no es válido.");

        // Solo se aceptan vídeos insertables de YouTube (el front los pinta en un iframe).
        return contenido with
        {
            Videos = (contenido.Videos ?? []).Where(v => v is not null && EsVideoYoutube(v.Url) && !string.IsNullOrWhiteSpace(v.Titulo)).ToArray(),
            SinListadoClubes = contenido.SinListadoClubes ?? [],
            SinPagoEquipos = contenido.SinPagoEquipos ?? [],
            NombresCuadros = (contenido.NombresCuadros ?? []).Where(n => n is not null).ToArray(),
        };
    }

    /// <summary><c>https://www.youtube.com/embed/…</c> (o <c>youtube-nocookie.com</c>).</summary>
    internal static bool EsVideoYoutube(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u)
           && u.Scheme == Uri.UriSchemeHttps
           && (u.Host is "www.youtube.com" or "youtube.com" or "www.youtube-nocookie.com")
           && u.AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal);
}
