using Dapper;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Rankings publicados en el índice de rankings de la web.</summary>
public interface IRankingsWebRepository
{
    /// <summary>Rankings visibles, en el orden de la web.</summary>
    Task<IReadOnlyList<RankingWebDto>> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Rankings.asp</c> (sql_galeria). El ASP hacía <c>SELECT SFEP_Rankings_Web.*</c>, recorría el
/// recordset una vez solo para contarlo (<c>numregistros</c>, que no se usaba) y otra para pintarlo. No recibía
/// parámetros del usuario. Aquí: columnas explícitas (las únicas que pintaba) y una sola pasada.
/// </summary>
public sealed class RankingsWebRepository(IDbConnectionFactory connectionFactory, IMediaUrlBuilder mediaUrls, IOptions<MediaOptions> media)
    : IRankingsWebRepository
{
    // Mismo filtro fijo que el ASP (Visible = 'S'; no filtraba Borrado). Texto como desempate de Orden para que
    // el orden sea estable.
    private const string Sql = """
        SELECT r.Texto, r.Link, r.Foto, r.Orden
        FROM SFEP_Rankings_Web AS r
        WHERE r.Visible = 'S'
        ORDER BY r.Orden, r.Texto;
        """;

    public async Task<IReadOnlyList<RankingWebDto>> ObtenerAsync(CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var filas = await conexion.QueryAsync<Fila>(new CommandDefinition(Sql, cancellationToken: cancellationToken));

        var claves = new HashSet<string>(StringComparer.Ordinal);
        var resultado = new List<RankingWebDto>();
        foreach (var f in filas)
        {
            var titulo = ReglasRankings.Titulo(f.Texto);
            var enlace = ReglasRankings.Enlace(f.Link);
            if (titulo.Length == 0 && enlace is null)
            {
                continue; // fila vacía: el ASP pintaba una tarjeta sin texto ni destino
            }

            var orden = f.Orden ?? 0;

            // La clave es un ancla: si dos filas dieran la misma, la segunda lleva sufijo.
            var baseClave = ReglasRankings.Clave(enlace, titulo, orden);
            var clave = baseClave;
            for (var n = 2; !claves.Add(clave); n++)
            {
                clave = $"{baseClave}-{n}";
            }

            var foto = Texto.Limpio(f.Foto);
            resultado.Add(new RankingWebDto(
                clave,
                titulo.Length > 0 ? titulo : clave,
                enlace,
                enlace is null ? null : UrlAbsoluta(enlace),
                ReglasRankings.EsExterno(enlace),
                foto is null ? null : $"{ReglasRankings.CarpetaImagenes}/{foto}",
                mediaUrls.Construir(ReglasRankings.CarpetaImagenes, foto),
                orden));
        }

        return resultado;
    }

    private string UrlAbsoluta(string ruta)
        => Uri.TryCreate(ruta, UriKind.Absolute, out var absoluta)
           && (absoluta.Scheme == Uri.UriSchemeHttp || absoluta.Scheme == Uri.UriSchemeHttps)
            ? absoluta.AbsoluteUri
            : WebActual.Url(media.Value, ruta.TrimStart('/'));

    // SUPUESTO: Texto/Link/Foto texto y Orden entero (el ASP solo los pintaba; no hay página de gestión que
    // escriba en SFEP_Rankings_Web, se edita directamente en la BD).
    private sealed class Fila
    {
        public string? Texto { get; set; }
        public string? Link { get; set; }
        public string? Foto { get; set; }
        public int? Orden { get; set; }
    }
}
