using Dapper;
using FEP.Api.Contracts.Navegacion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Navegacion;

/// <summary>Menú principal de la web pública.</summary>
public interface IMenuRepository
{
    /// <summary>Opciones de nivel 1 ordenadas, cada una con sus opciones de nivel 2 anidadas.</summary>
    Task<IReadOnlyList<MenuItemDto>> ObtenerAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>include/Menus.inc</c> (sql_menu0, nivel 1) e <c>index.asp</c> (sql_menu1, nivel 2,
/// lanzado una vez por cada opción de nivel 1 y dos veces por página: N+1). Aquí se lee todo
/// en una sola consulta y se anida en memoria.
/// </summary>
public sealed class MenuRepository(IDbConnectionFactory connectionFactory) : IMenuRepository
{
    private const string Sql = """
        SELECT m.IdOpcion, m.Nivel, m.Origen, m.Opcion, m.Orden, m.Link, m.Ventana, m.Icono
        FROM SFEP_Web_Menu AS m
        WHERE m.Site = 'FEP'
          AND m.Nivel IN (1, 2)
        ORDER BY m.Nivel, m.Orden;
        """;

    public async Task<IReadOnlyList<MenuItemDto>> ObtenerAsync(CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var filas = (await conexion.QueryAsync<Fila>(new CommandDefinition(Sql, cancellationToken: cancellationToken))).AsList();

        // Las filas ya vienen ordenadas por Orden: ToLookup conserva ese orden dentro de cada grupo.
        var hijosPorOrigen = filas
            .Where(f => f.Nivel == 2 && f.Origen.HasValue)
            .ToLookup(f => f.Origen!.Value);

        return filas
            .Where(f => f.Nivel == 1)
            .Select(f => Mapear(f, hijosPorOrigen[f.IdOpcion].Select(h => Mapear(h, [])).ToArray()))
            .ToArray();
    }

    private static MenuItemDto Mapear(Fila fila, IReadOnlyList<MenuItemDto> hijos) => new(
        fila.IdOpcion,
        Texto.Requerido(fila.Opcion),
        Texto.Limpio(fila.Link),
        Texto.Limpio(fila.Ventana),
        Texto.Limpio(fila.Icono),
        fila.Orden ?? 0,
        hijos);

    // SUPUESTO: IdOpcion/Nivel/Origen/Orden numéricos enteros; Opcion/Ventana/Icono char (el ASP hacía rtrim).
    private sealed class Fila
    {
        public int IdOpcion { get; set; }
        public int Nivel { get; set; }
        public int? Origen { get; set; }
        public string? Opcion { get; set; }
        public int? Orden { get; set; }
        public string? Link { get; set; }
        public string? Ventana { get; set; }
        public string? Icono { get; set; }
    }
}
