using System.Data;
using Dapper;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Cuadros de los torneos (eliminatorias, previas, liguillas y enfrentamientos de equipos).</summary>
public interface ICuadrosRepository
{
    /// <summary>Cuadros del torneo <paramref name="idTorneo"/>. Torneo <c>null</c> si no existe, está borrado o no es visible.</summary>
    Task<ConsultaCuadros<CuadrosTorneoDto>> ListarAsync(int idTorneo, CancellationToken cancellationToken);

    /// <summary>
    /// Un cuadro. Torneo <c>null</c> si el torneo no existe, está borrado o no es visible; datos <c>null</c> si el cuadro
    /// no tiene ninguna fila en <c>SFEP_Torneos_Cuadros</c>.
    /// </summary>
    Task<ConsultaCuadros<CuadroTorneoDto>> ObtenerAsync(ClaveCuadro clave, CancellationToken cancellationToken);

    /// <summary>
    /// Detalle del enfrentamiento <paramref name="posicion"/> de la ronda <paramref name="avance"/> de un cuadro por
    /// equipos. Datos <c>null</c> si el torneo no es por equipos, el cuadro no está publicado o no hay detalle.
    /// </summary>
    Task<ConsultaCuadros<EnfrentamientoEquiposDto>> ObtenerEnfrentamientoAsync(
        ClaveCuadro clave, int avance, int posicion, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Previo.asp</c>, <c>PrevioEquipos.asp</c> (reparto por número de filas), las páginas de cuadro
/// (<c>Cuadro4…128.asp</c>, <c>CuadroPrevia*.asp</c>, <c>CuadroPrePrevia*.asp</c>, <c>CuadroEquipos*.asp</c>,
/// <c>Liguilla3Equipos.asp</c>, <c>Liguilla4.asp</c>, <c>Liguilla4Equipos.asp</c>) y <c>EquiposEnfrentamientos.asp</c>.
/// El ASP concatenaba <c>IdTorneo</c>, <c>Genero</c>, <c>Cat</c>, <c>Nivel</c>, <c>Cuadro</c>, <c>Avance</c> y
/// <c>Posicion</c> de la petición en todas las consultas (sin limpiar) y hacía una consulta por ronda y otra por
/// pareja (N+1: un cuadro de 128 lanzaba ~400 consultas).
/// <para>
/// Aquí: parámetros siempre, columnas explícitas, una consulta para el torneo y un lote para el cuadro (más la
/// clasificación si es una liguilla); los nombres se resuelven en memoria.
/// </para>
/// </summary>
public sealed class CuadrosRepository(IDbConnectionFactory connectionFactory) : ICuadrosRepository
{
    // Borrado = 'N' y Visible <> 'N' son nuevos: las páginas de cuadro no miraban el torneo (ver análisis).
    private const string SqlTorneo = """
        SELECT t.IdPrueba, t.Torneo, t.TipoTorneo, t.FechaInicio, t.FechaFin, t.Ciudad, t.Visible
        FROM SFEP_Torneos AS t
        WHERE t.IdPrueba = @Id
          AND t.Borrado = 'N';
        """;

    private const string SqlCuadrosTorneo = """
        -- Cuadros del torneo (combinaciones distintas)
        SELECT DISTINCT c.Genero, c.Categoria, CAST(c.Nivel AS int) AS Nivel, c.Cuadro
        FROM SFEP_Torneos_Cuadros AS c
        INNER JOIN SFEP_Torneos AS t ON c.IdTorneo = t.IdTorneo AND c.IdFederacion = t.IdFederacion
        WHERE t.IdPrueba = @Id;
        """;

    // {0}: columnas Ver… (constantes de ReglasCuadros, nunca de la petición).
    private const string SqlDesgloseFormato = """
        -- Desglose por género y categoría (sql_ver, sql_torneo.textocuadros, sql_tipo.LiguillaVueltas)
        SELECT d.Genero, d.Tipo, CAST(d.TextoCuadros AS nvarchar(max)) AS TextoCuadros,
               TRY_CONVERT(int, d.LiguillaVueltas) AS LiguillaVueltas, {0}
        FROM SFEP_Torneos_Desglose AS d
        WHERE d.IdPrueba = @Id;
        """;

    private static readonly string SqlDesglose = string.Format(
        SqlDesgloseFormato,
        string.Join(", ", ReglasCuadros.ColumnasVer.Select(c => $"CAST(d.{c} AS varchar(5)) AS {c}")));

    private const string SqlFilasCuadro = """
        -- Huecos del cuadro (sql_cuadro, una por ronda en el ASP)
        SELECT CAST(c.Avance AS int) AS Avance, CAST(c.Posicion AS int) AS Posicion, TRY_CONVERT(int, c.Pareja) AS Pareja,
               CAST(c.CDS AS varchar(10)) AS Cds, CAST(c.Letra AS varchar(10)) AS Letra,
               CAST(c.Lucky AS varchar(5)) AS Lucky, CAST(c.Exento AS varchar(5)) AS Exento
        FROM SFEP_Torneos_Cuadros AS c
        INNER JOIN SFEP_Torneos AS t ON c.IdTorneo = t.IdTorneo AND c.IdFederacion = t.IdFederacion
        WHERE t.IdPrueba = @Id AND c.Genero = @Genero AND c.Categoria = @Categoria AND c.Nivel = @Nivel AND c.Cuadro = @Cuadro
        ORDER BY c.Avance DESC, c.Posicion;
        """;

    private const string SqlJugadores = """
        -- Jugadores de las parejas (sql_pareja, una por hueco en el ASP; sql_cs para puntos y wildcard)
        SELECT TRY_CONVERT(int, j.NumPareja) AS NumPareja, TRY_CONVERT(int, j.OrdenPareja) AS OrdenPareja,
               CAST(j.Jugador AS nvarchar(200)) AS Jugador, TRY_CONVERT(decimal(18, 2), j.Puntos) AS Puntos,
               CAST(j.Wildcard AS varchar(5)) AS Wildcard
        FROM SFEP_Torneos_Jugadores AS j
        INNER JOIN SFEP_Torneos AS t ON j.Torneo = t.IdTorneo AND j.IdFederacion = t.IdFederacion
        WHERE t.IdPrueba = @Id AND j.Genero = @Genero AND j.Categoria = @Categoria
        ORDER BY j.NumPareja, j.OrdenPareja;
        """;

    private const string SqlEquipos = """
        -- Equipos (sql_pareja de CuadroEquipos*.asp; sql_cs para los puntos)
        SELECT TRY_CONVERT(int, e.NumEquipo) AS NumEquipo, CAST(e.Equipo AS nvarchar(200)) AS Equipo,
               TRY_CONVERT(decimal(18, 2), e.Puntos) AS Puntos
        FROM SFEP_Torneos_Equipos AS e
        INNER JOIN SFEP_Torneos AS t ON e.Torneo = t.IdTorneo AND e.IdFederacion = t.IdFederacion
        WHERE t.IdPrueba = @Id AND e.Genero = @Genero
        ORDER BY e.NumEquipo;
        """;

    private const string SqlPartidos = """
        -- Resultados (sql_result)
        SELECT CAST(r.Avance AS int) AS Avance, CAST(r.Posicion AS int) AS Posicion, CAST(r.resultado AS nvarchar(200)) AS Resultado
        FROM SFEP_Torneos_Resultados AS r
        WHERE r.IdPrueba = @Id AND r.Categoria = @Categoria AND r.Genero = @Genero AND r.Cuadro = @Cuadro AND r.Nivel = @Nivel
        ORDER BY r.Avance, r.Posicion;

        -- Horarios (sql_horarios)
        SELECT CAST(h.Avance AS int) AS Avance, CAST(h.Posicion AS int) AS Posicion, TRY_CONVERT(datetime, h.Dia) AS Dia,
               CAST(h.Lugar AS nvarchar(200)) AS Lugar, CAST(h.Obs AS nvarchar(500)) AS Obs,
               CAST(h.VS AS varchar(5)) AS Vs, CAST(h.Visible AS varchar(5)) AS Visible
        FROM SFEP_Torneos_Horarios AS h
        WHERE h.IdPrueba = @Id AND h.Categoria = @Categoria AND h.Genero = @Genero AND h.Cuadro = @Cuadro AND h.Nivel = @Nivel
        ORDER BY h.Avance, h.Posicion;
        """;

    private const string SqlConEnfrentamiento = """
        -- Partidos con detalle de enfrentamiento (sql_parejas de CuadroEquipos*.asp)
        SELECT DISTINCT CAST(r.Avance AS int) AS Avance, CAST(r.Posicion AS int) AS Posicion
        FROM SFEP_Torneos_ResultadosEquipos AS r
        WHERE r.IdPrueba = @Id AND r.Categoria = @Categoria AND r.Genero = @Genero AND r.Cuadro = @Cuadro AND r.Nivel = @Nivel;
        """;

    // El ASP no filtraba la clasificación por nivel (SUPUESTO: la tabla no lo distingue). Categoría lleva tilde.
    private const string SqlLiguilla = """
        SELECT TRY_CONVERT(int, l.Posicion) AS Posicion, TRY_CONVERT(int, l.NumPareja) AS NumPareja,
               CAST(l.Pareja AS nvarchar(300)) AS Pareja, TRY_CONVERT(int, l.Puntos) AS Puntos,
               TRY_CONVERT(int, l.PG) AS Pg, TRY_CONVERT(int, l.PP) AS Pp, TRY_CONVERT(int, l.SetG) AS SetG,
               TRY_CONVERT(int, l.SetP) AS SetP, TRY_CONVERT(int, l.JG) AS Jg, TRY_CONVERT(int, l.JP) AS Jp,
               TRY_CONVERT(int, l.EG) AS Eg, TRY_CONVERT(int, l.EP) AS Ep
        FROM SFEP_Torneos_Liguilla AS l
        WHERE l.IdPrueba = @Id AND l.[Categoría] = @Categoria AND l.Cuadro = @Cuadro AND l.Genero = @Genero
        ORDER BY l.Posicion, l.NumPareja;
        """;

    // EquiposEnfrentamientos.asp: sql_partido + sql_Equipo1/2 + sql_Eq1jug1… (6 consultas por partido) en una.
    private const string SqlEnfrentamiento = """
        SELECT TRY_CONVERT(int, r.Partido) AS Partido,
               CAST(e1.Equipo AS nvarchar(200)) AS Equipo1, CAST(e2.Equipo AS nvarchar(200)) AS Equipo2,
               CAST(f11.NombreCompleto AS nvarchar(200)) AS Jugador11, CAST(f12.NombreCompleto AS nvarchar(200)) AS Jugador12,
               CAST(f21.NombreCompleto AS nvarchar(200)) AS Jugador21, CAST(f22.NombreCompleto AS nvarchar(200)) AS Jugador22,
               TRY_CONVERT(int, r.Set1_Equipo1) AS Set1E1, TRY_CONVERT(int, r.Set1_Equipo2) AS Set1E2,
               TRY_CONVERT(int, r.Set2_Equipo1) AS Set2E1, TRY_CONVERT(int, r.Set2_Equipo2) AS Set2E2,
               TRY_CONVERT(int, r.Set3_Equipo1) AS Set3E1, TRY_CONVERT(int, r.Set3_Equipo2) AS Set3E2,
               CAST(r.Obs_Equipo1 AS nvarchar(200)) AS Obs1, CAST(r.Obs_Equipo2 AS nvarchar(200)) AS Obs2
        FROM SFEP_Torneos_ResultadosEquipos AS r
        INNER JOIN SFEP_Torneos AS t ON t.IdPrueba = r.IdPrueba
        OUTER APPLY (SELECT TOP (1) e.Equipo FROM SFEP_Torneos_Equipos AS e
                     WHERE e.Torneo = t.IdTorneo AND e.IdFederacion = t.IdFederacion AND e.NumEquipo = r.Equipo1 AND e.Genero = @Genero) AS e1
        OUTER APPLY (SELECT TOP (1) e.Equipo FROM SFEP_Torneos_Equipos AS e
                     WHERE e.Torneo = t.IdTorneo AND e.IdFederacion = t.IdFederacion AND e.NumEquipo = r.Equipo2 AND e.Genero = @Genero) AS e2
        OUTER APPLY (SELECT TOP (1) f.NombreCompleto FROM SFEP_Fichas AS f WHERE f.LicenciaJugador = r.Equipo1_Lic1) AS f11
        OUTER APPLY (SELECT TOP (1) f.NombreCompleto FROM SFEP_Fichas AS f WHERE f.LicenciaJugador = r.Equipo1_Lic2) AS f12
        OUTER APPLY (SELECT TOP (1) f.NombreCompleto FROM SFEP_Fichas AS f WHERE f.LicenciaJugador = r.Equipo2_Lic1) AS f21
        OUTER APPLY (SELECT TOP (1) f.NombreCompleto FROM SFEP_Fichas AS f WHERE f.LicenciaJugador = r.Equipo2_Lic2) AS f22
        WHERE r.IdPrueba = @Id AND r.Categoria = @Categoria AND r.Nivel = @Nivel AND r.Cuadro = @Cuadro AND r.Genero = @Genero
          AND r.Avance = @Avance AND r.Posicion = @Posicion
        ORDER BY r.Partido;
        """;

    public async Task<ConsultaCuadros<CuadrosTorneoDto>> ListarAsync(int idTorneo, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var torneo = await Torneo(conexion, idTorneo, cancellationToken);
        if (torneo is null)
        {
            return new(null, null);
        }

        using var lector = await conexion.QueryMultipleAsync(
            new CommandDefinition(SqlCuadrosTorneo + SqlDesglose, new { Id = idTorneo }, cancellationToken: cancellationToken));
        var cuadros = (await lector.ReadAsync<FilaCuadroTorneo>()).ToArray();
        var desglose = (await lector.ReadAsync<FilaDesglose>()).ToArray();

        return new(torneo.Dto, new CuadrosTorneoDto(torneo.Dto, Disponibles(cuadros, desglose)));
    }

    public async Task<ConsultaCuadros<CuadroTorneoDto>> ObtenerAsync(ClaveCuadro clave, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var torneo = await Torneo(conexion, clave.IdTorneo, cancellationToken);
        if (torneo is null)
        {
            return new(null, null);
        }

        var parametros = Parametros(clave);
        var sql = SqlCuadrosTorneo + SqlDesglose + SqlFilasCuadro + (torneo.Equipos ? SqlEquipos : SqlJugadores) + SqlPartidos
                  + (torneo.Equipos ? SqlConEnfrentamiento : string.Empty);
        FilaCuadroTorneo[] cuadros;
        FilaDesglose[] desglose;
        FilaHueco[] filas;
        FilaJugador[] jugadores = [];
        FilaEquipo[] equipos = [];
        FilaResultado[] resultados;
        FilaHorario[] horarios;
        FilaClave[] conEnfrentamiento = [];
        using (var lector = await conexion.QueryMultipleAsync(new CommandDefinition(sql, parametros, cancellationToken: cancellationToken)))
        {
            cuadros = (await lector.ReadAsync<FilaCuadroTorneo>()).ToArray();
            desglose = (await lector.ReadAsync<FilaDesglose>()).ToArray();
            filas = (await lector.ReadAsync<FilaHueco>()).ToArray();
            if (torneo.Equipos)
            {
                equipos = (await lector.ReadAsync<FilaEquipo>()).ToArray();
            }
            else
            {
                jugadores = (await lector.ReadAsync<FilaJugador>()).ToArray();
            }

            resultados = (await lector.ReadAsync<FilaResultado>()).ToArray();
            horarios = (await lector.ReadAsync<FilaHorario>()).ToArray();
            if (torneo.Equipos)
            {
                conEnfrentamiento = (await lector.ReadAsync<FilaClave>()).ToArray();
            }
        }

        if (filas.Length == 0)
        {
            return new(torneo.Dto, null);
        }

        var fila = Desglose(desglose, clave.Genero, clave.Categoria);
        var publicado = ReglasCuadros.Publicado(clave.Nivel, fila is not null, c => fila?.Ver(c));
        var (formato, _) = ReglasCuadros.Formato(torneo.Equipos, filas.Select(f => f.Avance).ToArray(), fila?.LiguillaVueltas);

        FilaLiguilla[] liguilla = [];
        if (publicado && formato == "liguilla")
        {
            liguilla = (await conexion.QueryAsync<FilaLiguilla>(
                new CommandDefinition(SqlLiguilla, parametros, cancellationToken: cancellationToken))).ToArray();
        }

        return new(torneo.Dto, Componer(torneo.Dto, clave, torneo.Equipos, fila, Disponibles(cuadros, desglose),
            filas, jugadores, equipos, resultados, horarios, conEnfrentamiento, liguilla));
    }

    public async Task<ConsultaCuadros<EnfrentamientoEquiposDto>> ObtenerEnfrentamientoAsync(
        ClaveCuadro clave, int avance, int posicion, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var torneo = await Torneo(conexion, clave.IdTorneo, cancellationToken);
        if (torneo is null || !torneo.Equipos)
        {
            return new(torneo?.Dto, null);
        }

        var desglose = (await conexion.QueryAsync<FilaDesglose>(
            new CommandDefinition(SqlDesglose, new { Id = clave.IdTorneo }, cancellationToken: cancellationToken))).ToArray();
        var fila = Desglose(desglose, clave.Genero, clave.Categoria);
        if (!ReglasCuadros.Publicado(clave.Nivel, fila is not null, c => fila?.Ver(c)))
        {
            return new(torneo.Dto, null);
        }

        var parametros = Parametros(clave);
        parametros.Add("Avance", avance, DbType.Int32);
        parametros.Add("Posicion", posicion, DbType.Int32);
        var partidos = (await conexion.QueryAsync<FilaPartidoEquipos>(
            new CommandDefinition(SqlEnfrentamiento, parametros, cancellationToken: cancellationToken))).ToArray();

        return new(torneo.Dto, Enfrentamiento(partidos));
    }

    // ---------------------------------------------------------------------------------------------
    // Composición (sin BD: la prueban los tests).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Monta la respuesta de un cuadro a partir de las filas leídas.</summary>
    internal static CuadroTorneoDto Componer(
        TorneoCuadroDto torneo,
        ClaveCuadro clave,
        bool equipos,
        FilaDesglose? desglose,
        IReadOnlyList<CuadroDisponibleDto> otrosCuadros,
        IReadOnlyList<FilaHueco> filas,
        IReadOnlyList<FilaJugador> jugadores,
        IReadOnlyList<FilaEquipo> equiposTorneo,
        IReadOnlyList<FilaResultado> resultados,
        IReadOnlyList<FilaHorario> horarios,
        IReadOnlyList<FilaClave> conEnfrentamiento,
        IReadOnlyList<FilaLiguilla> liguilla)
    {
        var publicado = ReglasCuadros.Publicado(clave.Nivel, desglose is not null, c => desglose?.Ver(c));
        var (formato, plantilla) = ReglasCuadros.Formato(equipos, filas.Select(f => f.Avance).ToArray(), desglose?.LiguillaVueltas);
        var notas = desglose?.TextoCuadros?.Trim() is { Length: > 0 } texto ? texto : null;

        if (!publicado)
        {
            return new CuadroTorneoDto(torneo, clave.Genero, clave.Categoria, clave.Nivel, clave.Cuadro, false, notas, formato,
                plantilla, [], [], [], [], otrosCuadros);
        }

        // Parejas: jugadores por OrdenPareja (el ASP no reiniciaba la variable y una pareja sin jugadores heredaba el
        // nombre de la anterior). Equipos: nombre y puntos. Si un número se repite, gana la primera fila.
        var parejas = jugadores
            .Where(j => j.NumPareja is > 0)
            .GroupBy(j => j.NumPareja!.Value)
            .ToDictionary(g => g.Key, g => new Pareja(
                g.OrderBy(j => j.OrdenPareja ?? int.MaxValue)
                    .Select(j => CampeonatoEquiposRepository.Espacios(j.Jugador))
                    .OfType<string>()
                    .ToArray(),
                g.Any(j => j.Puntos is not null) ? g.Sum(j => j.Puntos ?? 0m) : null,
                g.Any(j => ReglasCuadros.EsSi(j.Wildcard))));
        var nombresEquipos = new Dictionary<int, FilaEquipo>();
        foreach (var e in equiposTorneo.Where(e => e.NumEquipo is > 0))
        {
            nombresEquipos.TryAdd(e.NumEquipo!.Value, e);
        }

        string NombreEquipo(int id) => CampeonatoEquiposRepository.Espacios(nombresEquipos.GetValueOrDefault(id)?.Equipo) ?? string.Empty;

        var eliminatoria = formato == "eliminatoria";
        var primeraRonda = filas.Count > 0 ? filas.Max(f => f.Avance) : 0;

        var participantes = new List<ParticipanteCuadroDto>();
        foreach (var f in filas.OrderByDescending(f => f.Avance).ThenBy(f => f.Posicion))
        {
            var lucky = ReglasCuadros.EsSi(f.Lucky);
            if (f.Pareja is > 0 and var id)
            {
                var cs = eliminatoria && f.Avance == primeraRonda ? ReglasCuadros.CabezaSerie(f.Cds) : null;
                if (equipos)
                {
                    participantes.Add(new(f.Avance, f.Posicion, "equipo", id, NombreEquipo(id), null, cs, null, lucky));
                }
                else
                {
                    var nombres = parejas.GetValueOrDefault(id)?.Jugadores ?? Array.Empty<string>();
                    participantes.Add(new(f.Avance, f.Posicion, "pareja", id, string.Join(" - ", nombres), nombres, cs, null, lucky));
                }

                continue;
            }

            if (ReglasCuadros.Hueco(clave.Nivel, f.Exento, f.Letra, f.Lucky) is { } hueco)
            {
                participantes.Add(new(f.Avance, f.Posicion, hueco.Tipo, null, hueco.Nombre, null, null, hueco.Letra, false));
            }
        }

        // Partidos: resultado (si no está vacío), horario visible y detalle de enfrentamiento, por (avance, posición).
        var porResultado = new Dictionary<(int, int), string>();
        foreach (var r in resultados)
        {
            if (Texto.Limpio(r.Resultado) is { } textoResultado)
            {
                porResultado.TryAdd((r.Avance, r.Posicion), textoResultado);
            }
        }

        var porHorario = new Dictionary<(int, int), HorarioPartidoCuadroDto>();
        foreach (var h in horarios)
        {
            if (string.Equals(h.Visible?.Trim(), "N", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lugar = CampeonatoEquiposRepository.Espacios(h.Lugar);
            var obs = CampeonatoEquiposRepository.Espacios(h.Obs);
            if (h.Dia is null && lugar is null && obs is null)
            {
                continue;
            }

            porHorario.TryAdd((h.Avance, h.Posicion), new HorarioPartidoCuadroDto(h.Dia, lugar, obs, ReglasCuadros.EsSi(h.Vs)));
        }

        var enfrentamientos = conEnfrentamiento.Select(c => (c.Avance, c.Posicion)).ToHashSet();
        var partidos = porResultado.Keys.Concat(porHorario.Keys).Concat(enfrentamientos)
            .Distinct()
            .OrderByDescending(k => k.Item1).ThenBy(k => k.Item2)
            .Select(k => new PartidoCuadroDto(k.Item1, k.Item2, porResultado.GetValueOrDefault(k), porHorario.GetValueOrDefault(k),
                equipos && enfrentamientos.Contains(k)))
            .ToArray();

        // Cabezas de serie: en eliminatoria, los CDS de la primera ronda (sql_cs agrupaba por CDS); en liguilla, los
        // participantes por número (Liguilla*.asp listaba NumPareja como "C.S.").
        IReadOnlyList<CabezaSerieCuadroDto> cabezas;
        if (eliminatoria)
        {
            cabezas = filas
                .Where(f => f.Avance == primeraRonda && f.Pareja is > 0)
                .Select(f => (Fila: f, Cs: ReglasCuadros.CabezaSerie(f.Cds)))
                .Where(x => x.Cs is not null)
                .GroupBy(x => x.Cs!.Value)
                .OrderBy(g => g.Key)
                .Select(g => CabezaSerie(g.Key, g.First().Fila.Pareja!.Value))
                .ToArray();
        }
        else
        {
            cabezas = filas
                .Where(f => f.Pareja is > 0)
                .Select(f => f.Pareja!.Value)
                .Distinct()
                .Order()
                .Select(id => CabezaSerie(id, id))
                .ToArray();
        }

        CabezaSerieCuadroDto CabezaSerie(int numero, int id)
        {
            if (equipos)
            {
                return new(numero, NombreEquipo(id), Redondeo(nombresEquipos.GetValueOrDefault(id)?.Puntos), false);
            }

            var p = parejas.GetValueOrDefault(id);
            return new(numero, string.Join("/", p?.Jugadores ?? Array.Empty<string>()), Redondeo(p?.Puntos), p?.Wildcard ?? false);
        }

        string NombreParticipante(int? id)
            => id is not > 0 ? string.Empty
                : equipos ? NombreEquipo(id.Value)
                : string.Join(" - ", parejas.GetValueOrDefault(id.Value)?.Jugadores ?? Array.Empty<string>());

        var clasificacion = formato == "liguilla"
            ? liguilla.Select(l => new ClasificacionLiguillaDto(
                    l.Posicion,
                    CampeonatoEquiposRepository.Espacios(l.Pareja) ?? NombreParticipante(l.NumPareja),
                    l.Puntos, l.Pg, l.Pp, l.SetG, l.SetP, l.Jg, l.Jp, l.Eg, l.Ep))
                .ToArray()
            : Array.Empty<ClasificacionLiguillaDto>();

        return new CuadroTorneoDto(torneo, clave.Genero, clave.Categoria, clave.Nivel, clave.Cuadro, true, notas, formato, plantilla,
            participantes, partidos, cabezas, clasificacion, otrosCuadros);
    }

    /// <summary>Monta el detalle de un enfrentamiento; <c>null</c> si no hay partidos.</summary>
    internal static EnfrentamientoEquiposDto? Enfrentamiento(IReadOnlyList<FilaPartidoEquipos> partidos)
    {
        if (partidos.Count == 0)
        {
            return null;
        }

        static IReadOnlyList<string> Jugadores(params string?[] nombres)
            => nombres.Select(CampeonatoEquiposRepository.Espacios).OfType<string>().ToArray();

        static IReadOnlyList<SetEnfrentamientoDto> Sets(FilaPartidoEquipos p)
        {
            var sets = new List<SetEnfrentamientoDto>();
            foreach (var (n, e1, e2) in new[] { (1, p.Set1E1, p.Set1E2), (2, p.Set2E1, p.Set2E2), (3, p.Set3E1, p.Set3E2) })
            {
                // El ASP vaciaba el tercer set si era 0-0; un set sin datos tampoco se devuelve.
                if ((e1 is null && e2 is null) || (n == 3 && (e1 ?? 0) == 0 && (e2 ?? 0) == 0))
                {
                    continue;
                }

                sets.Add(new SetEnfrentamientoDto(e1, e2));
            }

            return sets;
        }

        return new EnfrentamientoEquiposDto(
            partidos.Select(p => CampeonatoEquiposRepository.Espacios(p.Equipo1)).FirstOrDefault(e => e is not null),
            partidos.Select(p => CampeonatoEquiposRepository.Espacios(p.Equipo2)).FirstOrDefault(e => e is not null),
            partidos
                .Where(p => p.Partido is not null)
                .Select(p => new PartidoEnfrentamientoDto(p.Partido!.Value, Jugadores(p.Jugador11, p.Jugador12), Jugadores(p.Jugador21, p.Jugador22),
                    Sets(p), CampeonatoEquiposRepository.Espacios(p.Obs1), CampeonatoEquiposRepository.Espacios(p.Obs2)))
                .ToArray());
    }

    /// <summary>Cuadros distintos del torneo (recortados, sin repetir) con su publicación, en orden.</summary>
    internal static IReadOnlyList<CuadroDisponibleDto> Disponibles(IReadOnlyList<FilaCuadroTorneo> cuadros, IReadOnlyList<FilaDesglose> desglose)
    {
        var lista = new List<CuadroDisponibleDto>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in cuadros)
        {
            var genero = ReglasCuadros.Genero(c.Genero);
            var categoria = Texto.Limpio(c.Categoria);
            var cuadro = Texto.Limpio(c.Cuadro);
            if (genero is null || categoria is null || cuadro is null || c.Nivel is null
                || !vistos.Add($"{genero}|{categoria}|{c.Nivel}|{cuadro}"))
            {
                continue;
            }

            var fila = Desglose(desglose, genero, categoria);
            lista.Add(new CuadroDisponibleDto(genero, categoria, c.Nivel.Value, cuadro,
                ReglasCuadros.Publicado(c.Nivel.Value, fila is not null, col => fila?.Ver(col))));
        }

        return ReglasCuadros.Ordenar(lista);
    }

    /// <summary>Fila del desglose de un género y categoría (sin distinguir mayúsculas ni espacios de relleno).</summary>
    internal static FilaDesglose? Desglose(IReadOnlyList<FilaDesglose> desglose, string genero, string categoria)
        => desglose.FirstOrDefault(d => string.Equals(d.Genero?.Trim(), genero, StringComparison.OrdinalIgnoreCase)
                                        && string.Equals(d.Tipo?.Trim(), categoria, StringComparison.OrdinalIgnoreCase));

    private static int? Redondeo(decimal? valor) => valor is { } v ? (int)Math.Round(v, MidpointRounding.AwayFromZero) : null;

    private static async Task<TorneoLeido?> Torneo(IDbConnection conexion, int id, CancellationToken cancellationToken)
    {
        var t = await conexion.QuerySingleOrDefaultAsync<FilaTorneo>(
            new CommandDefinition(SqlTorneo, new { Id = id }, cancellationToken: cancellationToken));
        if (t is null || string.Equals(t.Visible?.Trim(), "N", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var equipos = string.Equals(t.TipoTorneo?.Trim(), "EQUIPOS", StringComparison.OrdinalIgnoreCase);
        return new TorneoLeido(
            new TorneoCuadroDto(
                t.IdPrueba,
                CampeonatoEquiposRepository.Espacios(t.Torneo) ?? string.Empty,
                equipos ? "equipos" : "parejas",
                t.FechaInicio is { } ini ? DateOnly.FromDateTime(ini) : null,
                t.FechaFin is { } fin ? DateOnly.FromDateTime(fin) : null,
                Texto.NombrePropio(t.Ciudad)),
            equipos);
    }

    /// <summary>Parámetros del cuadro. SUPUESTO: Genero, Categoria y Cuadro son char/varchar (parámetros no Unicode).</summary>
    private static DynamicParameters Parametros(ClaveCuadro clave)
    {
        var p = new DynamicParameters();
        p.Add("Id", clave.IdTorneo, DbType.Int32);
        p.Add("Genero", new DbString { Value = clave.Genero, IsAnsi = true, Length = 10 });
        p.Add("Categoria", new DbString { Value = clave.Categoria, IsAnsi = true, Length = 50 });
        p.Add("Nivel", clave.Nivel, DbType.Int32);
        p.Add("Cuadro", new DbString { Value = clave.Cuadro, IsAnsi = true, Length = 50 });
        return p;
    }

    private sealed record TorneoLeido(TorneoCuadroDto Dto, bool Equipos);

    private sealed record Pareja(IReadOnlyList<string> Jugadores, decimal? Puntos, bool Wildcard);

    // SUPUESTO: IdPrueba, Avance, Posicion y Nivel enteros (o convertibles); Pareja/NumPareja/NumEquipo enteros
    // (TRY_CONVERT); CDS texto numérico; Letra, Lucky, Exento, VS, Visible, Wildcard y los Ver… char/varchar; Dia datetime.
    private sealed class FilaTorneo
    {
        public int IdPrueba { get; set; }
        public string? Torneo { get; set; }
        public string? TipoTorneo { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? Ciudad { get; set; }
        public string? Visible { get; set; }
    }

    internal sealed class FilaCuadroTorneo
    {
        public string? Genero { get; set; }
        public string? Categoria { get; set; }
        public int? Nivel { get; set; }
        public string? Cuadro { get; set; }
    }

    internal sealed class FilaDesglose
    {
        public string? Genero { get; set; }
        public string? Tipo { get; set; }
        public string? TextoCuadros { get; set; }
        public int? LiguillaVueltas { get; set; }
        public string? VerPrimera { get; set; }
        public string? VerSegunda { get; set; }
        public string? VerTercera { get; set; }
        public string? VerCuarta { get; set; }
        public string? VerQuinta { get; set; }
        public string? VerSexta { get; set; }
        public string? VerSeptima { get; set; }
        public string? VerOctava { get; set; }
        public string? VerNovena { get; set; }
        public string? VerDecima { get; set; }
        public string? VerPrevia { get; set; }
        public string? VerPrePrevia { get; set; }

        /// <summary>Valor de una columna <c>Ver…</c> por nombre.</summary>
        public string? Ver(string columna) => columna.ToUpperInvariant() switch
        {
            "VERPRIMERA" => VerPrimera,
            "VERSEGUNDA" => VerSegunda,
            "VERTERCERA" => VerTercera,
            "VERCUARTA" => VerCuarta,
            "VERQUINTA" => VerQuinta,
            "VERSEXTA" => VerSexta,
            "VERSEPTIMA" => VerSeptima,
            "VEROCTAVA" => VerOctava,
            "VERNOVENA" => VerNovena,
            "VERDECIMA" => VerDecima,
            "VERPREVIA" => VerPrevia,
            "VERPREPREVIA" => VerPrePrevia,
            _ => null,
        };
    }

    internal sealed class FilaHueco
    {
        public int Avance { get; set; }
        public int Posicion { get; set; }
        public int? Pareja { get; set; }
        public string? Cds { get; set; }
        public string? Letra { get; set; }
        public string? Lucky { get; set; }
        public string? Exento { get; set; }
    }

    internal sealed class FilaJugador
    {
        public int? NumPareja { get; set; }
        public int? OrdenPareja { get; set; }
        public string? Jugador { get; set; }
        public decimal? Puntos { get; set; }
        public string? Wildcard { get; set; }
    }

    internal sealed class FilaEquipo
    {
        public int? NumEquipo { get; set; }
        public string? Equipo { get; set; }
        public decimal? Puntos { get; set; }
    }

    internal sealed class FilaResultado
    {
        public int Avance { get; set; }
        public int Posicion { get; set; }
        public string? Resultado { get; set; }
    }

    internal sealed class FilaHorario
    {
        public int Avance { get; set; }
        public int Posicion { get; set; }
        public DateTime? Dia { get; set; }
        public string? Lugar { get; set; }
        public string? Obs { get; set; }
        public string? Vs { get; set; }
        public string? Visible { get; set; }
    }

    internal sealed class FilaClave
    {
        public int Avance { get; set; }
        public int Posicion { get; set; }
    }

    internal sealed class FilaLiguilla
    {
        public int? Posicion { get; set; }
        public int? NumPareja { get; set; }
        public string? Pareja { get; set; }
        public int? Puntos { get; set; }
        public int? Pg { get; set; }
        public int? Pp { get; set; }
        public int? SetG { get; set; }
        public int? SetP { get; set; }
        public int? Jg { get; set; }
        public int? Jp { get; set; }
        public int? Eg { get; set; }
        public int? Ep { get; set; }
    }

    internal sealed class FilaPartidoEquipos
    {
        public int? Partido { get; set; }
        public string? Equipo1 { get; set; }
        public string? Equipo2 { get; set; }
        public string? Jugador11 { get; set; }
        public string? Jugador12 { get; set; }
        public string? Jugador21 { get; set; }
        public string? Jugador22 { get; set; }
        public int? Set1E1 { get; set; }
        public int? Set1E2 { get; set; }
        public int? Set2E1 { get; set; }
        public int? Set2E2 { get; set; }
        public int? Set3E1 { get; set; }
        public int? Set3E2 { get; set; }
        public string? Obs1 { get; set; }
        public string? Obs2 { get; set; }
    }
}
