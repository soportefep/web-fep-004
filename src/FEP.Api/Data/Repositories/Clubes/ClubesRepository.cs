using System.Text;
using Dapper;
using FEP.Api.Contracts.Clubes;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Clubes;

/// <summary>Clubes federados (buscador público).</summary>
public interface IClubesRepository
{
    /// <summary>
    /// Clubes con la licencia de club de <paramref name="anio"/> renovada que cumplen los criterios,
    /// paginados y ordenados por provincia, localidad y nombre.
    /// </summary>
    Task<ClubesPaginaDto> BuscarAsync(BusquedaClubes busqueda, int anio, CancellationToken cancellationToken);

    /// <summary>Federaciones y provincias para los desplegables del buscador.</summary>
    Task<ClubesFiltrosDto> ObtenerFiltrosAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Clubes.asp</c> (sql_fed, sql_prov y las seis variantes de sql_clubes). El ASP concatenaba
/// <c>fed</c>, <c>provincia</c> y el texto en el SQL (inyección: los numéricos ni siquiera iban entre
/// comillas), contaba los resultados recorriendo todo el recordset y pintaba todos sin paginar.
/// Aquí: una sola consulta con los filtros como parámetros, <c>COUNT</c> + <c>OFFSET/FETCH</c>, y
/// <c>EXISTS</c> en lugar de los <c>INNER JOIN</c> con <c>SFEP_Clubes_Licencia</c> y
/// <c>SFEP_Federaciones</c> (que podían duplicar clubes).
/// <para>
/// El SQL se compone solo con fragmentos fijos de esta clase; los valores del usuario van siempre
/// como parámetros (<c>@Federacion</c>, <c>@Provincia</c>, <c>@T0…@T7</c>).
/// </para>
/// </summary>
public sealed class ClubesRepository(IDbConnectionFactory connectionFactory) : IClubesRepository
{
    // Mismas tablas y filtros fijos que el ASP: licencia de club del año en curso renovada,
    // comunidad del club con federación y provincia existente (INNER JOIN).
    private const string Desde = """
        FROM SFEP_Clubes AS c
        INNER JOIN SFEP_Provincias AS p ON p.IdProvincia = c.Provincia
        WHERE EXISTS (SELECT 1 FROM SFEP_Clubes_Licencia AS l
                      WHERE l.IdClub = c.IdClub AND l.[Año] = @Anio AND l.Renovado = 'S')
        """;

    private const string FiltroFederacion = """

          AND EXISTS (SELECT 1 FROM SFEP_Federaciones AS f
                      WHERE f.Comunidad = c.Comunidad AND f.IdFederacion = @Federacion)
        """;

    // Sin filtro de federación el ASP seguía exigiendo (INNER JOIN) que la comunidad del club tuviera federación.
    private const string FiltroConFederacion = """

          AND EXISTS (SELECT 1 FROM SFEP_Federaciones AS f WHERE f.Comunidad = c.Comunidad)
        """;

    private const string FiltroProvincia = """

          AND p.IdProvincia = @Provincia
        """;

    // SUPUESTO: la intercalación de la BD distingue acentos (Modern_Spanish_CI_AS). Se compara sin
    // distinguir mayúsculas ni acentos para que "padel" encuentre "Pádel".
    private const string FiltroTermino = "\n  AND c.Club COLLATE Latin1_General_CI_AI LIKE @T{0}";

    private const string Columnas = """
        SELECT CAST(c.IdClub AS varchar(20)) AS IdClub, c.Club, c.Localidad, p.IdProvincia, p.Provincia
        """;

    private const string OrdenYPagina = """

        ORDER BY p.Provincia, c.Localidad, c.Club, c.IdClub
        OFFSET @Saltar ROWS FETCH NEXT @Limite ROWS ONLY;
        """;

    private const string SqlFiltros = """
        SELECT f.IdFederacion, f.Abreviatura
        FROM SFEP_Federaciones AS f
        WHERE f.IdFederacion BETWEEN @FederacionDesde AND @FederacionHasta
        ORDER BY f.Abreviatura;

        SELECT p.IdProvincia, p.Provincia
        FROM SFEP_Provincias AS p
        WHERE p.IdProvincia <> 0
        ORDER BY p.Provincia;

        SELECT f.IdFederacion, p.IdProvincia
        FROM SFEP_Federaciones AS f
        INNER JOIN SFEP_Provincias AS p ON p.IdComunidad = f.Comunidad
        WHERE f.IdFederacion BETWEEN @FederacionDesde AND @FederacionHasta;
        """;

    public async Task<ClubesPaginaDto> BuscarAsync(BusquedaClubes busqueda, int anio, CancellationToken cancellationToken)
    {
        var parametros = new DynamicParameters();
        parametros.Add("Anio", anio);
        parametros.Add("Saltar", (busqueda.Pagina - 1) * busqueda.Limite);
        parametros.Add("Limite", busqueda.Limite);

        var where = new StringBuilder(Desde);
        if (busqueda.Federacion is { } federacion)
        {
            where.Append(FiltroFederacion);
            parametros.Add("Federacion", federacion);
        }
        else
        {
            where.Append(FiltroConFederacion);
        }

        if (busqueda.Provincia is { } provincia)
        {
            where.Append(FiltroProvincia);
            parametros.Add("Provincia", provincia);
        }

        for (var i = 0; i < busqueda.Terminos.Count; i++)
        {
            where.AppendFormat(FiltroTermino, i);
            parametros.Add($"T{i}", $"%{BusquedaClubes.EscaparLike(busqueda.Terminos[i])}%");
        }

        var sql = $"SELECT COUNT(*)\n{where};\n\n{Columnas}\n{where}{OrdenYPagina}";

        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var lector = await conexion.QueryMultipleAsync(new CommandDefinition(sql, parametros, cancellationToken: cancellationToken));
        var total = await lector.ReadSingleAsync<int>();
        var filas = await lector.ReadAsync<FilaClub>();

        var items = filas
            .Select(f => new ClubResumenDto(
                Texto.Requerido(f.IdClub),
                Texto.Requerido(f.Club),
                f.IdProvincia,
                Texto.Requerido(f.Provincia),
                Texto.Limpio(f.Localidad)))
            .ToArray();

        return new ClubesPaginaDto(
            anio,
            busqueda.Federacion,
            busqueda.Provincia,
            busqueda.Terminos.Count > 0 ? string.Join(' ', busqueda.Terminos) : null,
            busqueda.Pagina,
            busqueda.Limite,
            total,
            items);
    }

    public async Task<ClubesFiltrosDto> ObtenerFiltrosAsync(CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var lector = await conexion.QueryMultipleAsync(new CommandDefinition(
            SqlFiltros,
            // Las 22 federaciones autonómicas (DECISIÓN FEP; el ASP: IdFederacion <> 0 AND IdFederacion < 100).
            new { FederacionDesde = BusquedaClubes.FederacionMinima, FederacionHasta = BusquedaClubes.FederacionMaxima },
            cancellationToken: cancellationToken));
        var federaciones = (await lector.ReadAsync<FilaFederacion>()).ToArray();
        var provincias = (await lector.ReadAsync<FilaProvincia>()).ToArray();
        var relacion = (await lector.ReadAsync<FilaFederacionProvincia>()).ToArray();

        var provinciasPorFederacion = relacion
            .GroupBy(r => r.IdFederacion)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(r => r.IdProvincia).Distinct().ToArray());

        return new ClubesFiltrosDto(
            federaciones
                .Select(f => new FederacionFiltroDto(
                    f.IdFederacion,
                    Texto.Requerido(f.Abreviatura),
                    provinciasPorFederacion.TryGetValue(f.IdFederacion, out var ids) ? ids : Array.Empty<int>()))
                .ToArray(),
            provincias
                .Select(p => new ProvinciaFiltroDto(p.IdProvincia, Texto.Requerido(p.Provincia)))
                .ToArray());
    }

    // SUPUESTO: IdProvincia, IdFederacion, Comunidad, Provincia (de SFEP_Clubes) e IdComunidad son
    // enteros (el ASP los comparaba sin comillas y hacía clng). IdClub se lee como texto (CAST) porque
    // hay códigos con ceros a la izquierda; [Año] es entero y Renovado char(1).
    private sealed class FilaClub
    {
        public string? IdClub { get; set; }
        public string? Club { get; set; }
        public string? Localidad { get; set; }
        public int IdProvincia { get; set; }
        public string? Provincia { get; set; }
    }

    private sealed class FilaFederacion
    {
        public int IdFederacion { get; set; }
        public string? Abreviatura { get; set; }
    }

    private sealed class FilaProvincia
    {
        public int IdProvincia { get; set; }
        public string? Provincia { get; set; }
    }

    private sealed class FilaFederacionProvincia
    {
        public int IdFederacion { get; set; }
        public int IdProvincia { get; set; }
    }
}
