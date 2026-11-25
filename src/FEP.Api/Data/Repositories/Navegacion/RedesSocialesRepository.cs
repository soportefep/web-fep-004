using Dapper;
using FEP.Api.Contracts.Navegacion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Navegacion;

/// <summary>Perfiles de la FEP en redes sociales.</summary>
public interface IRedesSocialesRepository
{
    Task<IReadOnlyList<RedSocialDto>> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>Origen: <c>include/Menus.inc</c> (sql_RS), pintado en la cabecera de <c>index.asp</c>.</summary>
public sealed class RedesSocialesRepository(IDbConnectionFactory connectionFactory) : IRedesSocialesRepository
{
    // El original no tiene filtros (ni Site ni Borrado): se mantiene igual.
    private const string Sql = """
        SELECT rs.Red, rs.URLRED, rs.Icono, rs.Orden
        FROM SFEP_Web_RS AS rs
        ORDER BY rs.Orden;
        """;

    public async Task<IReadOnlyList<RedSocialDto>> ObtenerAsync(CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var filas = await conexion.QueryAsync<Fila>(new CommandDefinition(Sql, cancellationToken: cancellationToken));

        return filas
            .Select(f => new RedSocialDto(
                Texto.Requerido(f.Red),
                Texto.Limpio(f.UrlRed),
                Texto.Limpio(f.Icono),
                f.Orden ?? 0))
            .ToArray();
    }

    // SUPUESTO: Red/URLRED/Icono texto; Orden entero.
    private sealed class Fila
    {
        public string? Red { get; set; }
        public string? UrlRed { get; set; }   // columna URLRED (Dapper mapea sin distinguir mayúsculas)
        public string? Icono { get; set; }
        public int? Orden { get; set; }
    }
}
