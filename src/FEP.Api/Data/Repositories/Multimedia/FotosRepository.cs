using System.Globalization;
using Dapper;
using FEP.Api.Contracts.Multimedia;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Multimedia;

/// <summary>Fotos de la galería multimedia.</summary>
public interface IFotosRepository
{
    /// <summary>Las <paramref name="limite"/> fotos modificadas más recientemente (galerías FEP de tipo foto).</summary>
    Task<IReadOnlyList<FotoDto>> ObtenerUltimasAsync(int limite, CancellationToken cancellationToken);
}

/// <summary>Origen: <c>include/PiePagina.inc</c> (sql_multimedia, "últimas fotos").</summary>
public sealed class FotosRepository(IDbConnectionFactory connectionFactory, IMediaUrlBuilder media) : IFotosRepository
{
    private const string Sql = """
        SELECT TOP (@Limite) a.IdGaleria, a.Archivo, a.Subtitulo, a.FechaMod, g.FechaEvento
        FROM SFEP_Galeria_Multimedia_Archivos AS a
        INNER JOIN SFEP_Galeria_Multimedia AS g ON a.IdGaleria = g.IdGaleria
        WHERE g.Tipo = 'FOT'
          AND g.Fed = 0
        ORDER BY a.FechaMod DESC;
        """;

    public async Task<IReadOnlyList<FotoDto>> ObtenerUltimasAsync(int limite, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var filas = await conexion.QueryAsync<Fila>(
            new CommandDefinition(Sql, new { Limite = limite }, cancellationToken: cancellationToken));

        return filas
            .Select(f =>
            {
                var archivo = Texto.Limpio(f.Archivo);
                // El ASP componía refs/Multimedia/{year(FechaEvento)}/{Archivo}; sin fecha no hay ruta válida.
                var url = f.FechaEvento is { } evento
                    ? media.Construir("Multimedia", evento.Year.ToString(CultureInfo.InvariantCulture), archivo)
                    : null;

                return new FotoDto(
                    f.IdGaleria,
                    archivo,
                    url,
                    Texto.Limpio(f.Subtitulo),
                    f.FechaEvento is { } fe ? DateOnly.FromDateTime(fe) : null,
                    f.FechaMod);
            })
            .ToArray();
    }

    // SUPUESTO: IdGaleria entero; FechaMod y FechaEvento datetime; Archivo/Subtitulo texto.
    private sealed class Fila
    {
        public int IdGaleria { get; set; }
        public string? Archivo { get; set; }
        public string? Subtitulo { get; set; }
        public DateTime? FechaMod { get; set; }
        public DateTime? FechaEvento { get; set; }
    }
}
