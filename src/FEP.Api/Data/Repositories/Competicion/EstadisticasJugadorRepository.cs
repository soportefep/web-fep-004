using System.Data;
using Dapper;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Contracts.Federacion;
using FEP.Api.Data.Repositories.Federacion;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Resultado de la consulta de estadísticas de una licencia.</summary>
public enum EstadoEstadisticas
{
    /// <summary>Hay estadísticas.</summary>
    Encontradas,

    /// <summary>No existe ninguna ficha con esa licencia de jugador/a (o la ficha está marcada como fallecido).</summary>
    LicenciaNoEncontrada,

    /// <summary>
    /// La licencia existe pero no figura en ningún ranking nacional visible: el ASP no mostraba nada (todo el bloque
    /// dependía de <c>if not rs_rnks.eof</c>).
    /// </summary>
    SinRanking,
}

/// <summary>Estado y, si las hay, estadísticas de una licencia.</summary>
public sealed record ConsultaEstadisticas(EstadoEstadisticas Estado, EstadisticasJugadorDto? Estadisticas);

/// <summary>Estadísticas de un jugador o jugadora (<c>Estadisticas.asp</c> + <c>CargarPruebas.asp</c>).</summary>
public interface IEstadisticasJugadorRepository
{
    /// <summary>
    /// Estadísticas de la licencia <paramref name="licencia"/>. <paramref name="ahora"/> (hora de Madrid) da el año de la
    /// categoría por edad y la fecha de la consulta.
    /// </summary>
    Task<ConsultaEstadisticas> ObtenerAsync(int licencia, DateTime ahora, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Estadisticas.asp</c> y <c>CargarPruebas.asp</c>. El ASP concatenaba la licencia (y el DNI) en ~15
/// consultas (<c>limpiar()</c> es una lista negra), hacía una consulta por prueba para la pareja y otra para sus
/// partidos (N+1), <b>escribía en la BD en cada visita</b> (<c>DELETE</c> + 12 <c>INSERT</c> en
/// <c>SFEP_estadisticasWeb</c> para ordenar el gráfico, y <c>CargarPruebas.asp</c> varios <c>UPDATE</c> de
/// <c>SFEP_RankingTorneos.EstaSumando/Topavergae</c> que luego no pintaba) y comparaba fechas como texto
/// (<c>FechaFin &gt; 'd/m/aaaa'</c>). Consultaba además <c>SFEP_Noticias</c> y las pruebas del Ranking Único sin usarlas.
/// Aquí: dos idas a la BD de solo lectura con todos los valores como parámetros y las fechas como <c>DateTime</c>.
/// <para>El SQL es fijo; los valores van como parámetros.</para>
/// </summary>
public sealed class EstadisticasJugadorRepository(
    IDbConnectionFactory connectionFactory,
    IMediaUrlBuilder mediaUrls,
    IFederacionesAutonomicasRepository federaciones,
    IOptions<MediaOptions> media) : IEstadisticasJugadorRepository
{
    // --- Primera ida: ficha, rankings y fecha del periodo -----------------------------------------------------------

    // Comprobación de la licencia (sql_lic: LicenciaJugador y Fallecido = 'N') + datos de la ficha. El ASP sacaba nombre,
    // foto y fecha de nacimiento de la fila de licencia más reciente (sin licencia: sin nombre); aquí, de la ficha.
    // SUPUESTO: FechaNac es fecha (o texto dd/mm/aaaa: TRY_CONVERT con estilo 103 admite ambas).
    private const string SqlFicha = """
        SELECT TOP (1) f.Nombre, f.Apellido1, f.Apellido2, f.Foto,
               YEAR(TRY_CONVERT(date, f.FechaNac, 103)) AS AnioNacimiento
        FROM SFEP_Fichas AS f
        WHERE f.LicenciaJugador = @Licencia
          AND f.Fallecido = 'N';
        """;

    // sql_elejigo: última licencia de jugador/a (Tipo = 'J', por Año) con sus clubes autonómico y nacional.
    private const string SqlLicencia = """
        SELECT TOP (1) TRY_CONVERT(int, l.[Año]) AS AnioLicencia, ca.Club AS ClubAutonomico, cn.Club AS ClubNacional
        FROM SFEP_Licencia AS l
        INNER JOIN SFEP_Fichas AS f ON f.LicenciaJugador = l.IdLicencia
        LEFT JOIN SFEP_Clubes AS ca ON ca.IdClub = l.ClubLicencia
        LEFT JOIN SFEP_Clubes AS cn ON cn.IdClub = l.ClubNacional
        WHERE f.LicenciaJugador = @Licencia
          AND l.Tipo = 'J'
        ORDER BY l.[Año] DESC;
        """;

    // sql_rnks: rankings nacionales visibles que no son suma de otros. Mismos JOIN y filtros que el ASP (también el de
    // SFEP_Fichas, aunque solo filtra). FechaAct y Modalidad (de CargarPruebas.asp) para el periodo de cada ranking.
    // SUPUESTO: mismos tipos que en las clasificaciones (enteros; puntos redondeados; FechaAct fecha o dd/mm/aaaa).
    private const string SqlRankings = """
        SELECT CAST(r.TipoRanking AS int) AS TipoRanking,
               t.Categoria, t.Modalidad,
               TRY_CONVERT(date, NULLIF(t.FechaAct, ''), 103) AS FechaAct,
               CAST(r.Posicion AS int) AS Posicion,
               CAST(r.TorneosJugador AS int) AS TorneosTotales,
               CAST(ROUND(r.PuntosTotal, 0) AS int) AS PuntosTotales,
               CAST(r.Torneos AS int) AS TorneosRanking,
               CAST(ROUND(r.Puntos, 0) AS int) AS PuntosRanking
        FROM SFEP_Fichas AS f
        INNER JOIN SFEP_RankingFinal AS r ON r.NumLicencia = f.LicenciaJugador
        INNER JOIN SFEP_Ranking_Tipos AS t ON t.TipoRanking = r.TipoRanking AND t.IdFederacion = r.IdFederacion
        WHERE r.NumLicencia = @Licencia
          AND r.IdFederacion = @IdFederacion
          AND t.Visible = 'S'
          AND t.SumaDeRankings = 'N'
        ORDER BY r.TipoRanking;
        """;

    // sql_fecharnk: fecha de actualización del Ranking Único (10000), que marca el periodo del balance.
    private const string SqlFechaPeriodo = """
        SELECT TOP (1) TRY_CONVERT(date, NULLIF(t.FechaAct, ''), 103)
        FROM SFEP_Ranking_Tipos AS t
        WHERE t.IdFederacion = @IdFederacion
          AND t.TipoRanking = @TipoPeriodo;
        """;

    // --- Segunda ida: balance, pareja, mejor clasificación, evolución, pruebas y equipos -----------------------------

    // Pruebas del Ranking Único del periodo (sql_cuentan). DISTINCT: el ASP recorría una fila por RankingTorneos y, si una
    // prueba aparecía dos veces, contaba sus partidos dos veces.
    private const string CtePruebasPeriodo = """
        WITH Pruebas AS (
            SELECT DISTINCT t.IdPrueba, t.FechaFin
            FROM SFEP_Torneos AS t
            INNER JOIN SFEP_RankingTorneos AS rt ON rt.IdPrueba = t.IdPrueba
            WHERE rt.Ranking IN @TiposUnico
              AND rt.Federacion = @IdFederacion
              AND rt.IdJugador = @Licencia
              AND (@Desde IS NULL OR t.FechaFin > @Desde)
        ),
        Parejas AS (
            -- sql_numpareja (una consulta por prueba en el ASP): número de pareja del jugador en la prueba (la primera fila).
            SELECT p.IdPrueba, p.FechaFin, j.NumPareja, j.Genero, j.Categoria
            FROM Pruebas AS p
            CROSS APPLY (SELECT TOP (1) tj.NumPareja, tj.Genero, tj.Categoria
                         FROM SFEP_Torneos_Jugadores AS tj
                         INNER JOIN SFEP_Torneos AS t2 ON t2.IdTorneo = tj.Torneo AND t2.IdFederacion = tj.IdFederacion
                         WHERE t2.IdPrueba = p.IdPrueba
                           AND tj.IdFicha = @Licencia) AS j
        )
        """;

    // sql_part (otra consulta por prueba): partidos de esa pareja en la prueba. Como el ASP, sin filtrar por género ni
    // categoría (SUPUESTO: NumPareja no se repite dentro de una prueba).
    private const string SqlResultados = CtePruebasPeriodo + """

        SELECT TRY_CONVERT(int, pa.NumPareja) AS NumPareja,
               TRY_CONVERT(int, r.IdPareja1) AS IdPareja1, TRY_CONVERT(int, r.IdPareja2) AS IdPareja2,
               r.GanaPareja1, r.GanaPareja2,
               TRY_CONVERT(int, r.Set1Jug1) AS Set1Jug1, TRY_CONVERT(int, r.Set1Jug2) AS Set1Jug2,
               TRY_CONVERT(int, r.Set2Jug1) AS Set2Jug1, TRY_CONVERT(int, r.Set2Jug2) AS Set2Jug2,
               TRY_CONVERT(int, r.Set3Jug1) AS Set3Jug1, TRY_CONVERT(int, r.Set3Jug2) AS Set3Jug2
        FROM Parejas AS pa
        INNER JOIN SFEP_Torneos_Resultados AS r
                ON r.IdPrueba = pa.IdPrueba
               AND (r.IdPareja1 = pa.NumPareja OR r.IdPareja2 = pa.NumPareja);
        """;

    // sql_ultima: el ASP la recalculaba en cada prueba (por año y mes, de la más antigua a la más reciente) y se quedaba
    // con la última asignada: la pareja de la prueba más reciente en la que consta el jugador.
    private const string SqlUltimaPareja = CtePruebasPeriodo + """
        ,
        Ultima AS (
            SELECT TOP (1) pa.IdPrueba, pa.NumPareja, pa.Genero, pa.Categoria
            FROM Parejas AS pa
            ORDER BY YEAR(pa.FechaFin) DESC, MONTH(pa.FechaFin) DESC, pa.FechaFin DESC, pa.IdPrueba DESC
        )
        SELECT TOP (1) CAST(tj.IdFicha AS int) AS Licencia, f.Nombre, f.Apellido1, f.Apellido2
        FROM Ultima AS u
        INNER JOIN SFEP_Torneos AS t ON t.IdPrueba = u.IdPrueba
        INNER JOIN SFEP_Torneos_Jugadores AS tj ON tj.Torneo = t.IdTorneo AND tj.IdFederacion = t.IdFederacion
        INNER JOIN SFEP_Fichas AS f ON f.LicenciaJugador = tj.IdFicha
        WHERE tj.NumPareja = u.NumPareja
          AND tj.IdFicha <> @Licencia
          AND tj.Genero = u.Genero
          AND tj.Categoria = u.Categoria;
        """;

    // sql_mejor + sql_nummejor: nivel más alto (Nivel menor) y ronda más avanzada (Avance menor) del periodo, y cuántas
    // veces. Una consulta agrupada en lugar de dos. SUPUESTO: Nivel y Avance enteros.
    private const string SqlMejorClasificacion = """
        SELECT TOP (1) CAST(c.Nivel AS int) AS Nivel, CAST(c.Avance AS int) AS Avance, COUNT(*) AS Veces
        FROM SFEP_Torneos AS t
        INNER JOIN SFEP_RankingTorneos AS rt ON rt.IdPrueba = t.IdPrueba
        INNER JOIN SFEP_Torneos_Cuadros AS c ON c.IdFederacion = t.IdFederacion AND c.IdTorneo = t.IdTorneo
        INNER JOIN SFEP_Torneos_Jugadores AS tj
                ON tj.IdFederacion = c.IdFederacion
               AND tj.Torneo = c.IdTorneo
               AND tj.Genero = c.Genero
               AND tj.Categoria = c.Categoria
               AND tj.NumPareja = c.Pareja
               AND tj.IdFicha = rt.IdJugador
        WHERE rt.Ranking IN @TiposUnico
          AND rt.Federacion = @IdFederacion
          AND rt.IdJugador = @Licencia
          AND (@Desde IS NULL OR t.FechaFin > @Desde)
        GROUP BY c.Nivel, c.Avance
        ORDER BY c.Nivel, c.Avance;
        """;

    // sql_EvRU: últimas 12 publicaciones del Ranking Único, sin pasar por SFEP_estadisticasWeb (el ASP borraba e insertaba
    // ahí para ordenarlas por fecha). Corrección: solo los tipos del Ranking Único en los que figura el jugador
    // (@TiposHistorico) y publicaciones con puesto; con 10000 y 10001 a la vez, el histórico mezclaba filas de posición 0
    // y 2 puntos de la otra lista (SUPUESTO).
    private const string SqlEvolucion = """
        SELECT TOP (@Publicaciones) TRY_CONVERT(date, h.Fecha, 103) AS Fecha,
               TRY_CONVERT(int, h.Posicion) AS Posicion,
               CAST(ROUND(TRY_CONVERT(float, h.Puntos), 0) AS int) AS Puntos
        FROM SFEP_Ranking_Historico AS h
        WHERE h.Licencia = @Licencia
          AND h.Ranking IN @TiposHistorico
          AND h.Federacion = @IdFederacion
          AND TRY_CONVERT(int, h.Posicion) > 0
        ORDER BY h.Fecha DESC;
        """;

    // CargarPruebas.asp (sql_pruebas y sql_pruebasant): pruebas de cada ranking del jugador y de la categoría de menores
    // anterior. El periodo se aplica después (cada ranking tiene el suyo).
    private const string SqlPruebas = """
        SELECT CAST(rt.Ranking AS int) AS TipoRanking, CAST(t.IdPrueba AS int) AS IdPrueba,
               TRY_CONVERT(int, t.IdFederacion) AS IdFederacion, t.Torneo, t.FechaFin,
               rt.Ronda, TRY_CONVERT(float, rt.Puntos) AS Puntos, rt.Obs
        FROM SFEP_Torneos AS t
        INNER JOIN SFEP_RankingTorneos AS rt ON rt.IdPrueba = t.IdPrueba
        WHERE rt.Ranking IN @TiposPruebas
          AND rt.Federacion = @IdFederacion
          AND rt.IdJugador = @Licencia
        ORDER BY YEAR(t.FechaFin) DESC, MONTH(t.FechaFin) DESC, t.FechaFin DESC, rt.Puntos DESC;
        """;

    // sql_es: primer y último año de nacimiento de las categorías de menores implicadas.
    private const string SqlCategoriasMenores = """
        SELECT RTRIM(c.Abreviatura) AS Abreviatura,
               TRY_CONVERT(int, c.AnoInicial) AS AnoInicial,
               TRY_CONVERT(int, c.AnoFinal) AS AnoFinal
        FROM SFEP_CategoriasMenores AS c
        WHERE c.Abreviatura IN @Abreviaturas;
        """;

    // sql_equipos: campeonatos nacionales por equipos desde 2013 en los que juega o puntúa. Sin el JOIN con SFEP_Fichas
    // (solo filtraba: la licencia ya está comprobada). Columnas de Torneos_Previo.asp para el enlace a la ficha.
    private const string SqlEquipos = """
        SELECT CAST(t.IdPrueba AS int) AS IdPrueba, TRY_CONVERT(int, t.[Año]) AS Anio, t.FechaFin, t.Torneo,
               j.Puntua, j.Juega, t.ModalidadTorneo, t.TipoTorneo, TRY_CONVERT(int, t.TorneoPadre) AS TorneoPadre,
               t.Liga, t.Empresas, t.LinkCuadros
        FROM SFEP_Inscripciones_Equipos AS e
        INNER JOIN SFEP_Inscripciones_Equipos_Jugadores AS j ON j.IdEquipo = e.IdEquipo
        INNER JOIN SFEP_Torneos AS t ON t.IdPrueba = e.IdPrueba
        WHERE t.IdFederacion = @IdFederacion
          AND t.[Año] > @AnioEquiposDesde
          AND j.NumLicencia = @Licencia
          AND (j.Juega = 'S' OR j.Puntua = 'S')
        ORDER BY t.[Año] DESC, t.FechaFin DESC, t.IdPrueba DESC;
        """;

    public async Task<ConsultaEstadisticas> ObtenerAsync(int licencia, DateTime ahora, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var parametros = new DynamicParameters();
        parametros.Add("Licencia", licencia);
        parametros.Add("IdFederacion", ReglasEstadisticas.IdFederacionFep);
        parametros.Add("TipoPeriodo", ReglasEstadisticas.TipoRankingPeriodo);

        FilaFicha? ficha;
        FilaLicencia? licenciaJugador;
        FilaRanking[] rankings;
        DateTime? fechaPeriodo;
        using (var lector = await conexion.QueryMultipleAsync(new CommandDefinition(
                   string.Join("\n\n", SqlFicha, SqlLicencia, SqlRankings, SqlFechaPeriodo), parametros,
                   cancellationToken: cancellationToken)))
        {
            ficha = await lector.ReadFirstOrDefaultAsync<FilaFicha>();
            licenciaJugador = await lector.ReadFirstOrDefaultAsync<FilaLicencia>();
            rankings = (await lector.ReadAsync<FilaRanking>()).ToArray();
            fechaPeriodo = await lector.ReadFirstOrDefaultAsync<DateTime?>();
        }

        if (ficha is null)
        {
            return new ConsultaEstadisticas(EstadoEstadisticas.LicenciaNoEncontrada, null);
        }

        if (rankings.Length == 0)
        {
            return new ConsultaEstadisticas(EstadoEstadisticas.SinRanking, null);
        }

        // Periodo del balance (fechainicio del ASP): un año antes de la actualización del Ranking Único.
        var desde = ReglasEstadisticas.InicioPeriodo(fechaPeriodo is { } f ? DateOnly.FromDateTime(f) : null, null);

        // Rankings de menores con categoría anterior y sus abreviaturas (CargarPruebas.asp).
        var anteriores = rankings
            .Where(r => ReglasEstadisticas.CategoriasAnteriores.ContainsKey(r.TipoRanking))
            .DistinctBy(r => r.TipoRanking)
            .ToDictionary(r => r.TipoRanking, r => ReglasEstadisticas.CategoriasAnteriores[r.TipoRanking]);
        var tiposPruebas = rankings.Select(r => r.TipoRanking)
            .Concat(anteriores.Values.Select(a => a.TipoAnterior))
            .Distinct()
            .ToArray();

        // Histórico: los tipos del Ranking Único en los que figura; si no figura en ninguno, los dos (como el ASP).
        var tiposHistorico = rankings.Select(r => r.TipoRanking).Where(t => ReglasEstadisticas.TiposRankingUnico.Contains(t)).ToArray();
        if (tiposHistorico.Length == 0)
        {
            tiposHistorico = [.. ReglasEstadisticas.TiposRankingUnico];
        }

        parametros.Add("TiposUnico", ReglasEstadisticas.TiposRankingUnico.ToArray());
        parametros.Add("TiposHistorico", tiposHistorico);
        parametros.Add("TiposPruebas", tiposPruebas);
        parametros.Add("Abreviaturas", anteriores.Values.Select(a => a.Abreviatura).Distinct().ToArray(), DbType.AnsiString);
        parametros.Add("Desde", desde?.ToDateTime(TimeOnly.MinValue), DbType.DateTime);
        parametros.Add("Publicaciones", ReglasEstadisticas.PublicacionesEvolucion);
        parametros.Add("AnioEquiposDesde", ReglasEstadisticas.AnioEquiposDesde);

        ResultadoPartido[] partidos;
        FilaPareja? pareja;
        FilaMejor? mejor;
        FilaEvolucion[] evolucion;
        FilaPrueba[] pruebas;
        FilaCategoriaMenores[] categoriasMenores;
        FilaEquipo[] equipos;
        using (var lector = await conexion.QueryMultipleAsync(new CommandDefinition(
                   string.Join("\n\n", SqlResultados, SqlUltimaPareja, SqlMejorClasificacion, SqlEvolucion, SqlPruebas,
                       SqlCategoriasMenores, SqlEquipos),
                   parametros, cancellationToken: cancellationToken)))
        {
            partidos = (await lector.ReadAsync<FilaResultado>())
                .Where(r => r.NumPareja is not null)
                .Select(r => new ResultadoPartido(r.NumPareja!.Value, r.IdPareja1, r.IdPareja2, r.GanaPareja1, r.GanaPareja2,
                    r.Set1Jug1, r.Set1Jug2, r.Set2Jug1, r.Set2Jug2, r.Set3Jug1, r.Set3Jug2))
                .ToArray();
            pareja = await lector.ReadFirstOrDefaultAsync<FilaPareja>();
            mejor = await lector.ReadFirstOrDefaultAsync<FilaMejor>();
            evolucion = (await lector.ReadAsync<FilaEvolucion>()).ToArray();
            pruebas = (await lector.ReadAsync<FilaPrueba>()).ToArray();
            categoriasMenores = (await lector.ReadAsync<FilaCategoriaMenores>()).ToArray();
            equipos = (await lector.ReadAsync<FilaEquipo>()).ToArray();
        }

        var listadoFederaciones = (await federaciones.ObtenerAsync(cancellationToken)).Federaciones
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var nombre = ReglasClasificaciones.NombreJugador(ficha.Nombre, ficha.Apellido1, ficha.Apellido2);
        var conFoto = string.Equals(ficha.Foto?.Trim(), "S", StringComparison.OrdinalIgnoreCase);

        var dto = new EstadisticasJugadorDto(
            licencia,
            nombre,
            conFoto ? ReglasEstadisticas.RutaFoto(licencia) : null,
            conFoto ? WebActual.Url(media.Value, ReglasEstadisticas.RutaFoto(licencia)) : null,
            ReglasEstadisticas.CategoriaPorEdad(ficha.AnioNacimiento, ahora.Year),
            licenciaJugador?.AnioLicencia,
            Texto.Limpio(licenciaJugador?.ClubAutonomico),
            Texto.Limpio(licenciaJugador?.ClubNacional),
            desde,
            ReglasEstadisticas.Balance(partidos),
            pareja is null
                ? null
                : new ParejaJugadorDto(pareja.Licencia,
                    ReglasClasificaciones.NombreJugador(pareja.Nombre, pareja.Apellido1, pareja.Apellido2),
                    WebActual.Url(media.Value, ReglasClasificaciones.RutaEstadisticas(pareja.Licencia))),
            mejor is null
                ? null
                : new MejorClasificacionDto(mejor.Nivel, mejor.Avance, ReglasEstadisticas.Ronda(mejor.Avance),
                    ReglasEstadisticas.CategoriaCuadro(mejor.Nivel), mejor.Veces),
            rankings.Select(r => ARanking(r, ficha.AnioNacimiento, anteriores, pruebas, categoriasMenores, listadoFederaciones)).ToArray(),
            AEvolucion(tiposHistorico, evolucion),
            equipos.Select(AEquipo).ToArray(),
            WebActual.Url(media.Value, ReglasClasificaciones.RutaEstadisticas(licencia)),
            ahora);

        return new ConsultaEstadisticas(EstadoEstadisticas.Encontradas, dto);
    }

    private RankingJugadorDto ARanking(
        FilaRanking r,
        int? anioNacimiento,
        IReadOnlyDictionary<int, (string Abreviatura, int TipoAnterior)> anteriores,
        IReadOnlyList<FilaPrueba> pruebas,
        IReadOnlyList<FilaCategoriaMenores> categoriasMenores,
        IReadOnlyDictionary<int, FederacionTerritorialDto> listadoFederaciones)
    {
        var actualizado = r.FechaAct is { } fecha ? DateOnly.FromDateTime(fecha) : (DateOnly?)null;
        var desde = ReglasEstadisticas.InicioPeriodo(actualizado, r.Modalidad);
        bool EnPeriodo(FilaPrueba p) => desde is not { } d || (p.FechaFin is { } fin && fin > d.ToDateTime(TimeOnly.MinValue));

        var propias = pruebas
            .Where(p => p.TipoRanking == r.TipoRanking && EnPeriodo(p))
            .Select(p => APrueba(p, 100, false, listadoFederaciones));

        // Categoría anterior al 40 % solo si el año de nacimiento es el primero o el último de la categoría actual.
        var deAnterior = Enumerable.Empty<PruebaRankingJugadorDto>();
        if (anteriores.TryGetValue(r.TipoRanking, out var anterior))
        {
            var categoria = categoriasMenores.FirstOrDefault(c =>
                string.Equals(c.Abreviatura?.Trim(), anterior.Abreviatura, StringComparison.OrdinalIgnoreCase));
            if (ReglasEstadisticas.SumaCategoriaAnterior(anioNacimiento, categoria?.AnoInicial, categoria?.AnoFinal))
            {
                deAnterior = pruebas
                    .Where(p => p.TipoRanking == anterior.TipoAnterior && EnPeriodo(p))
                    .Select(p => APrueba(p, ReglasEstadisticas.PorcentajeCategoriaAnterior, true, listadoFederaciones));
            }
        }

        var declarado = ReglasEstadisticas.BuscarTipo(r.TipoRanking);
        return new RankingJugadorDto(
            r.TipoRanking,
            declarado?.Ranking.Clave,
            declarado?.Ranking.Titulo ?? ReglasEstadisticas.NombreRankingPorCategoria(r.Categoria),
            declarado is { } dec ? ReglasEstadisticas.NombreLista(dec.Tipo) : null,
            declarado?.Tipo.Genero,
            Texto.Limpio(r.Categoria),
            r.Posicion is > 0 ? r.Posicion : null,
            r.TorneosTotales,
            r.PuntosTotales,
            r.TorneosRanking,
            r.PuntosRanking,
            actualizado,
            desde,
            [.. propias, .. deAnterior]);
    }

    private PruebaRankingJugadorDto APrueba(
        FilaPrueba p, int porcentaje, bool categoriaAnterior,
        IReadOnlyDictionary<int, FederacionTerritorialDto> listadoFederaciones)
    {
        FederacionPruebaDto? federacion = null;
        if (p.IdFederacion is { } id && id >= 0)
        {
            listadoFederaciones.TryGetValue(id, out var info);
            var bandera = ReglasEstadisticas.RutaBandera(id);
            federacion = new FederacionPruebaDto(id, info?.Territorio, info?.Nombre, bandera, mediaUrls.Construir(bandera));
        }

        return new PruebaRankingJugadorDto(
            p.IdPrueba,
            Texto.Requerido(p.Torneo),
            p.FechaFin is { } fin ? DateOnly.FromDateTime(fin) : null,
            federacion,
            Texto.Limpio(p.Ronda),
            Texto.Limpio(p.Obs),
            ReglasEstadisticas.PuntosComputados(p.Puntos, 100),
            porcentaje,
            ReglasEstadisticas.PuntosComputados(p.Puntos, porcentaje),
            categoriaAnterior);
    }

    private static EvolucionRankingDto? AEvolucion(IReadOnlyList<int> tipos, IReadOnlyList<FilaEvolucion> filas)
    {
        var puntos = filas
            .Where(f => f.Fecha is not null)
            .OrderBy(f => f.Fecha)
            .Select(f => new PuntoEvolucionDto(DateOnly.FromDateTime(f.Fecha!.Value), f.Posicion is > 0 ? f.Posicion : null, f.Puntos))
            .ToArray();
        return puntos.Length == 0 ? null : new EvolucionRankingDto(tipos, puntos);
    }

    private TorneoEquiposJugadorDto AEquipo(FilaEquipo e)
    {
        // Como Campeonatos.asp: LinkCuadros en ventana nueva y, si no hay, la ficha de Torneos_Previo.asp.
        var cuadros = ReglasCampeonatos.EnlaceCuadros(e.LinkCuadros);
        var enlace = cuadros ?? WebActual.Url(media.Value,
            ReglasCampeonatos.RutaFicha(e.IdPrueba, e.ModalidadTorneo, e.TipoTorneo, e.TorneoPadre, e.Liga, e.Empresas));

        return new TorneoEquiposJugadorDto(
            e.IdPrueba,
            e.Anio ?? e.FechaFin?.Year ?? 0,
            e.FechaFin is { } fin ? DateOnly.FromDateTime(fin) : null,
            Texto.Requerido(e.Torneo),
            EsSi(e.Puntua),
            EsSi(e.Juega),
            enlace,
            cuadros is not null);
    }

    private static bool EsSi(string? valor) => string.Equals(valor?.Trim(), "S", StringComparison.OrdinalIgnoreCase);

    // --- Filas ------------------------------------------------------------------------------------------------------

    private sealed class FilaFicha
    {
        public string? Nombre { get; set; }
        public string? Apellido1 { get; set; }
        public string? Apellido2 { get; set; }
        public string? Foto { get; set; }
        public int? AnioNacimiento { get; set; }
    }

    private sealed class FilaLicencia
    {
        public int? AnioLicencia { get; set; }
        public string? ClubAutonomico { get; set; }
        public string? ClubNacional { get; set; }
    }

    private sealed class FilaRanking
    {
        public int TipoRanking { get; set; }
        public string? Categoria { get; set; }
        public string? Modalidad { get; set; }
        public DateTime? FechaAct { get; set; }
        public int? Posicion { get; set; }
        public int? TorneosTotales { get; set; }
        public int? PuntosTotales { get; set; }
        public int? TorneosRanking { get; set; }
        public int? PuntosRanking { get; set; }
    }

    private sealed class FilaResultado
    {
        public int? NumPareja { get; set; }
        public int? IdPareja1 { get; set; }
        public int? IdPareja2 { get; set; }
        public string? GanaPareja1 { get; set; }
        public string? GanaPareja2 { get; set; }
        public int? Set1Jug1 { get; set; }
        public int? Set1Jug2 { get; set; }
        public int? Set2Jug1 { get; set; }
        public int? Set2Jug2 { get; set; }
        public int? Set3Jug1 { get; set; }
        public int? Set3Jug2 { get; set; }
    }

    private sealed class FilaPareja
    {
        public int Licencia { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido1 { get; set; }
        public string? Apellido2 { get; set; }
    }

    private sealed class FilaMejor
    {
        public int Nivel { get; set; }
        public int Avance { get; set; }
        public int Veces { get; set; }
    }

    private sealed class FilaEvolucion
    {
        public DateTime? Fecha { get; set; }
        public int? Posicion { get; set; }
        public int? Puntos { get; set; }
    }

    private sealed class FilaPrueba
    {
        public int TipoRanking { get; set; }
        public int IdPrueba { get; set; }
        public int? IdFederacion { get; set; }
        public string? Torneo { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? Ronda { get; set; }
        public double? Puntos { get; set; }
        public string? Obs { get; set; }
    }

    private sealed class FilaCategoriaMenores
    {
        public string? Abreviatura { get; set; }
        public int? AnoInicial { get; set; }
        public int? AnoFinal { get; set; }
    }

    private sealed class FilaEquipo
    {
        public int IdPrueba { get; set; }
        public int? Anio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? Torneo { get; set; }
        public string? Puntua { get; set; }
        public string? Juega { get; set; }
        public string? ModalidadTorneo { get; set; }
        public string? TipoTorneo { get; set; }
        public int? TorneoPadre { get; set; }
        public string? Liga { get; set; }
        public string? Empresas { get; set; }
        public string? LinkCuadros { get; set; }
    }
}
