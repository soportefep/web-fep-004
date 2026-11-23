using System.Text;
using Dapper;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Datos del ranking de equipos absolutos tal como salen de la BD.</summary>
/// <param name="Anios">Temporadas con clasificación (desde <see cref="ReglasRankingEquipos.AnioMinimo"/>), de la más reciente a la más antigua.</param>
/// <param name="Filas">Equipos de la temporada pedida (o de todas).</param>
public sealed record DatosRankingEquipos(IReadOnlyList<int> Anios, IReadOnlyList<FilaRankingEquipo> Filas);

/// <summary>Resultados de los Campeonatos Absolutos por equipos (<c>SFEP_Ranking_Equipos</c>).</summary>
public interface IRankingEquiposRepository
{
    /// <summary>Temporadas publicadas y equipos de <paramref name="anio"/> (o de todas si es <c>null</c>).</summary>
    Task<DatosRankingEquipos> ObtenerAsync(int? anio, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Ranking_Equipos_Absolutos.asp</c>. El ASP lanzaba 32 consultas casi idénticas (una por temporada
/// 2017–2024 × categoría 1/2 × género M/F) con los valores escritos en el SQL y <c>INNER JOIN</c> con
/// <c>SFEP_Clubes</c>, <c>SFEP_Comunidades</c>, <c>SFEP_Federaciones</c> y <c>SFEP_Provincias</c>: un equipo cuyo club no
/// tuviera provincia o federación desaparecía, y en 2017 (sin <c>IdFederacion &lt; 100</c>) los clubes de comunidades
/// con dos federaciones salían repetidos. Además consultaba <c>SFEP_Noticias</c> sin usarlo.
/// Aquí: una sola consulta parametrizada para todas las temporadas, <c>OUTER APPLY TOP (1)</c> para club, provincia y
/// federación (sin duplicados ni filas perdidas) y la lista de temporadas sacada de los datos (ya no hay que tocar
/// código cada año).
/// <para>El SQL se compone solo con fragmentos fijos de esta clase.</para>
/// </summary>
public sealed class RankingEquiposRepository(IDbConnectionFactory connectionFactory) : IRankingEquiposRepository
{
    // Filtros fijos del ASP (Modalidad = 'EQU', Tipo = 'ABS', Categoria 1/2, Genero M/F) como parámetros.
    // SUPUESTO: ano, Categoria y Puesto son numéricos (el ASP los comparaba sin comillas); Idclub se lee como texto
    // porque tiene ceros a la izquierda (0908009), igual que SFEP_Clubes.IdClub en el resto de la API.
    private const string Filtros = """
        WHERE r.Modalidad = @Modalidad
          AND r.Tipo = @Tipo
          AND r.Genero IN @Generos
          AND r.Categoria IN @Categorias
          AND r.ano >= @AnioMinimo
        """;

    private const string SqlAnios = """
        SELECT DISTINCT CAST(r.ano AS int) AS Anio
        FROM SFEP_Ranking_Equipos AS r
        """;

    private const string SqlAniosFin = """

        ORDER BY Anio DESC;
        """;

    // Columnas realmente pintadas: Puesto, bandera (IdFederacion), Provincia, escudo (Idclub) y Equipo. Modalidad, Tipo,
    // Categoria y SFEP_Clubes.Web se seleccionaban pero no se usaban. La federación es la de la comunidad del club
    // (el ASP unía SFEP_Clubes.Comunidad → SFEP_Comunidades → SFEP_Federaciones.Comunidad; SFEP_Comunidades solo hacía
    // de puente y no aporta columnas).
    private const string SqlEquipos = """
        SELECT CAST(r.ano AS int) AS Anio,
               CAST(r.Categoria AS int) AS Categoria,
               r.Genero,
               CAST(r.Puesto AS int) AS Puesto,
               r.Equipo,
               CAST(r.Idclub AS varchar(20)) AS Club,
               pr.Provincia,
               fe.IdFederacion,
               fe.Abreviatura
        FROM SFEP_Ranking_Equipos AS r
        OUTER APPLY (SELECT TOP (1) c.Provincia, c.Comunidad
                     FROM SFEP_Clubes AS c
                     WHERE c.IdClub = r.Idclub) AS cl
        OUTER APPLY (SELECT TOP (1) p.Provincia
                     FROM SFEP_Provincias AS p
                     WHERE p.IdProvincia = cl.Provincia) AS pr
        OUTER APPLY (SELECT TOP (1) CAST(f.IdFederacion AS int) AS IdFederacion, f.Abreviatura
                     FROM SFEP_Federaciones AS f
                     WHERE f.Comunidad = cl.Comunidad
                       AND f.IdFederacion BETWEEN @IdFederacionMinimo AND @IdFederacionMaximo
                     ORDER BY f.IdFederacion) AS fe
        """;

    private const string FiltroAnio = """

          AND r.ano = @Anio
        """;

    private const string SqlEquiposFin = """

        ORDER BY r.ano DESC, r.Categoria, r.Puesto, r.Equipo;
        """;

    public async Task<DatosRankingEquipos> ObtenerAsync(int? anio, CancellationToken cancellationToken)
    {
        var parametros = new DynamicParameters();
        parametros.Add("Modalidad", ReglasRankingEquipos.Modalidad);
        parametros.Add("Tipo", ReglasRankingEquipos.Tipo);
        parametros.Add("Generos", ReglasRankingEquipos.Generos.ToArray());
        parametros.Add("Categorias", ReglasRankingEquipos.Categorias.ToArray());
        parametros.Add("AnioMinimo", ReglasRankingEquipos.AnioMinimo);
        parametros.Add("IdFederacionMinimo", ReglasRankingEquipos.IdFederacionMinimo);
        parametros.Add("IdFederacionMaximo", ReglasRankingEquipos.IdFederacionMaximo);

        var sql = new StringBuilder()
            .Append(SqlAnios).Append('\n').Append(Filtros).Append(SqlAniosFin)
            .Append("\n\n")
            .Append(SqlEquipos).Append('\n').Append(Filtros);
        if (anio is { } a)
        {
            sql.Append(FiltroAnio);
            parametros.Add("Anio", a);
        }

        sql.Append(SqlEquiposFin);

        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var lector = await conexion.QueryMultipleAsync(
            new CommandDefinition(sql.ToString(), parametros, cancellationToken: cancellationToken));

        var anios = (await lector.ReadAsync<int>()).ToArray();
        var filas = (await lector.ReadAsync<Fila>())
            .Select(f => new FilaRankingEquipo(
                f.Anio,
                f.Categoria,
                f.Genero,
                f.Puesto,
                f.Equipo,
                Texto.Limpio(f.Club),
                Texto.Limpio(f.Provincia),
                f.IdFederacion,
                Texto.Limpio(f.Abreviatura)))
            .ToArray();

        return new DatosRankingEquipos(anios, filas);
    }

    private sealed class Fila
    {
        public int Anio { get; set; }
        public int Categoria { get; set; }
        public string? Genero { get; set; }
        public int Puesto { get; set; }
        public string? Equipo { get; set; }
        public string? Club { get; set; }
        public string? Provincia { get; set; }
        public int? IdFederacion { get; set; }
        public string? Abreviatura { get; set; }
    }
}
