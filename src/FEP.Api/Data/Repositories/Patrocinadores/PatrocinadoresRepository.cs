using Dapper;
using FEP.Api.Contracts.Patrocinadores;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Patrocinadores;

/// <summary>Patrocinadores de la FEP.</summary>
public interface IPatrocinadoresRepository
{
    Task<IReadOnlyList<PatrocinadorDto>> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>Origen: <c>include/Menus.inc</c> (sql_patro), pintado en <c>BarraLateral.inc</c>.</summary>
public sealed class PatrocinadoresRepository(IDbConnectionFactory connectionFactory, IMediaUrlBuilder media) : IPatrocinadoresRepository
{
    // El original repetía "Federacion=0" dos veces; se deja una.
    private const string Sql = """
        SELECT p.Logo, p.Link, p.Orden
        FROM SFEP_Patrocinadores AS p
        WHERE p.Borrado = 'N'
          AND p.Ver = 'S'
          AND p.Federacion = 0
        ORDER BY p.Orden;
        """;

    public async Task<IReadOnlyList<PatrocinadorDto>> ObtenerAsync(CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var filas = await conexion.QueryAsync<Fila>(new CommandDefinition(Sql, cancellationToken: cancellationToken));

        return filas
            .Select(f =>
            {
                var logo = Texto.Limpio(f.Logo);
                return new PatrocinadorDto(
                    logo,
                    media.Construir("images/banners", logo),
                    Texto.EnlaceExterno(f.Link),
                    f.Orden ?? 0);
            })
            .ToArray();
    }

    // SUPUESTO: Logo/Link texto (Link sin esquema); Orden entero. No se conoce ninguna columna
    // de nombre del patrocinador (el ASP no la usaba): pendiente de decisión.
    private sealed class Fila
    {
        public string? Logo { get; set; }
        public string? Link { get; set; }
        public int? Orden { get; set; }
    }
}
