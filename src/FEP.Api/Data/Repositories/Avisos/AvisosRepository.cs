using Dapper;
using FEP.Api.Contracts.Avisos;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Avisos;

/// <summary>Avisos de la web pública.</summary>
public interface IAvisosRepository
{
    /// <summary>Avisos visibles del portal FEP (y los comunes 'AMB'), del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<AvisoDto>> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>Origen: <c>include/Menus.inc</c> (sql_avisos), pintado en <c>BarraLateral.inc</c>.</summary>
public sealed class AvisosRepository(IDbConnectionFactory connectionFactory) : IAvisosRepository
{
    private const string Sql = """
        SELECT a.Fecha, a.Aviso, a.Link, a.Ventana
        FROM SFEP_Avisos AS a
        WHERE a.Visible = 'S'
          AND a.Borrado = 'N'
          AND a.Portal IN ('FEP', 'AMB')
        ORDER BY a.Fecha DESC;
        """;

    public async Task<IReadOnlyList<AvisoDto>> ObtenerAsync(CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var filas = await conexion.QueryAsync<Fila>(new CommandDefinition(Sql, cancellationToken: cancellationToken));

        return filas
            .Select(f => new AvisoDto(
                f.Fecha,
                Texto.Requerido(f.Aviso),
                Texto.Limpio(f.Link),
                Texto.Limpio(f.Ventana)))
            .ToArray();
    }

    // SUPUESTO: Fecha datetime (el ASP pintaba fecha larga + hora); Aviso/Link/Ventana texto.
    private sealed class Fila
    {
        public DateTime? Fecha { get; set; }
        public string? Aviso { get; set; }
        public string? Link { get; set; }
        public string? Ventana { get; set; }
    }
}
