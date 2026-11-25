using Dapper;
using FEP.Api.Contracts.Banners;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Banners;

/// <summary>Banners de la web pública (interés, destacados y eventos).</summary>
public interface IBannersRepository
{
    Task<IReadOnlyList<BannerDto>> ObtenerPorTipoAsync(TipoBanner tipo, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>include/Menus.inc</c> (sql_banners, sql_destacados, sql_evento): tres consultas
/// iguales salvo el tipo, fusionadas en una parametrizada. Se pintaban en <c>PiePagina.inc</c>,
/// <c>BarraLateral.inc</c> e <c>index.asp</c> respectivamente.
/// </summary>
public sealed class BannersRepository(IDbConnectionFactory connectionFactory, IMediaUrlBuilder media) : IBannersRepository
{
    // El ASP usaba RTrim([Tipo]) = '...'. En SQL Server la igualdad ya ignora los espacios
    // finales, así que "b.Tipo = @Tipo" es equivalente y no impide usar índices.
    private const string Sql = """
        SELECT b.Banner, b.Texto, b.Imagen, b.Link, b.Ventana, b.Orden
        FROM SFEP_Banners AS b
        WHERE b.Borrado = 'N'
          AND b.Visible = 'S'
          AND b.Portal = 'FEP'
          AND b.Tipo = @Tipo
        ORDER BY b.Orden;
        """;

    public async Task<IReadOnlyList<BannerDto>> ObtenerPorTipoAsync(TipoBanner tipo, CancellationToken cancellationToken)
    {
        var parametros = new { Tipo = new DbString { Value = TiposBanner.ValorBd(tipo), IsAnsi = true, Length = 50 } };

        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var filas = await conexion.QueryAsync<Fila>(new CommandDefinition(Sql, parametros, cancellationToken: cancellationToken));

        var clave = TiposBanner.Clave(tipo);
        var carpeta = TiposBanner.CarpetaImagen(tipo);

        return filas
            .Select(f =>
            {
                var imagen = Texto.Limpio(f.Imagen);
                return new BannerDto(
                    clave,
                    Texto.Requerido(f.Banner),
                    Texto.Limpio(f.Texto),
                    imagen,
                    media.Construir(carpeta, imagen),
                    Texto.Limpio(f.Link),
                    Texto.Limpio(f.Ventana),
                    f.Orden ?? 0);
            })
            .ToArray();
    }

    // SUPUESTO: Banner/Texto/Imagen/Link/Ventana texto (Tipo varchar/char ≤ 50); Orden entero.
    // Imagen2 existe (BarraLateral.inc la leía) pero nunca se pintaba: no se expone.
    private sealed class Fila
    {
        public string? Banner { get; set; }
        public string? Texto { get; set; }
        public string? Imagen { get; set; }
        public string? Link { get; set; }
        public string? Ventana { get; set; }
        public int? Orden { get; set; }
    }
}
