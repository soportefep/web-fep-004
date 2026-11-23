using Dapper;
using FEP.Api.Contracts.Noticias;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Noticias;

/// <summary>Noticias de la web pública.</summary>
public interface INoticiasRepository
{
    /// <summary>Noticias destacadas de la portada por orden, como máximo <paramref name="limite"/>.</summary>
    Task<IReadOnlyList<NoticiaDestacadaDto>> ObtenerDestacadasAsync(int limite, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>index.asp</c> (sql_noticias). El ASP pintaba 6 con <c>do while not i&gt;6</c> sin
/// comprobar EOF (fallaba con menos de 6 destacadas); aquí se usa <c>TOP (@Limite)</c>.
/// </summary>
public sealed class NoticiasRepository(IDbConnectionFactory connectionFactory, IMediaUrlBuilder media) : INoticiasRepository
{
    /// <summary>Número de destacadas que mostraba la portada antigua.</summary>
    public const int LimitePortada = 6;

    private const string Sql = """
        SELECT TOP (@Limite) n.IdNoticia, n.Titulo, n.Foto, n.Link, n.Ventana, n.Orden
        FROM SFEP_Noticias AS n
        WHERE n.Visible = 'S'
          AND n.Borrado = 'N'
          AND n.Destacada = 'S'
          AND n.Site = 'FEP'
          AND n.Orden <> 0
        ORDER BY n.Orden;
        """;

    public async Task<IReadOnlyList<NoticiaDestacadaDto>> ObtenerDestacadasAsync(int limite, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var filas = await conexion.QueryAsync<Fila>(
            new CommandDefinition(Sql, new { Limite = limite }, cancellationToken: cancellationToken));

        return filas
            .Select(f =>
            {
                var foto = Texto.Limpio(f.Foto);
                return new NoticiaDestacadaDto(
                    f.IdNoticia,
                    Texto.TituloNoticia(f.Titulo),
                    foto,
                    media.Construir("Noticias", foto),
                    Texto.Limpio(f.Link),
                    Texto.Limpio(f.Ventana),
                    f.Orden ?? 0);
            })
            .ToArray();
    }

    // SUPUESTO: IdNoticia entero (es el parámetro Idnoticia de noticia.asp); Titulo/Foto/Link/Ventana texto; Orden entero.
    private sealed class Fila
    {
        public int IdNoticia { get; set; }
        public string? Titulo { get; set; }
        public string? Foto { get; set; }
        public string? Link { get; set; }
        public string? Ventana { get; set; }
        public int? Orden { get; set; }
    }
}
