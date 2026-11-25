using System.Text.RegularExpressions;
using Dapper;
using FEP.Api.Contracts.Federacion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Federacion;

/// <summary>Ficha de una federación (contacto y estadísticas de licencias).</summary>
public interface IFichaFederacionRepository
{
    /// <summary>
    /// Ficha de la federación <paramref name="id"/> con las licencias del año de <paramref name="ahora"/>,
    /// o <c>null</c> si no existe en <c>SFEP_Federaciones</c>.
    /// </summary>
    /// <param name="id">Id de la federación (0 = FEP, total nacional).</param>
    /// <param name="ahora">Fecha y hora local del servidor (el año de las licencias es su año, como <c>year(now())</c>).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    Task<FichaFederacionDto?> ObtenerAsync(int id, DateTime ahora, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Datos_Federacion.asp</c> (sql_fed, sql_numLicJug, sql_puesto, sql_numLicTec, sql_numLicArb,
/// sql_evolucion, sql_genero, sql_edades y sql_clubes). El ASP concatenaba <c>id</c> en nueve consultas
/// (inyección: el valor iba sin comillas), hacía <c>SELECT *</c> de <c>SFEP_Federaciones</c> (que guarda
/// <c>Org_id</c>/<c>Secret_Key</c> de la API antigua), recorría todas las licencias del año para calcular el
/// puesto y traía una fila por licencia (≈120.000 en la FEP) para contar las edades en VBScript.
/// <para>
/// Aquí: <c>@Id</c>/<c>@Anio</c> siempre como parámetros, columnas explícitas, una consulta para la ficha y un
/// solo lote para las estadísticas (agregadas en SQL Server). El SQL solo se compone con fragmentos fijos de
/// esta clase según la federación sea la FEP (total nacional) o una autonómica, como hacían las dos ramas
/// <c>if id=0</c> del ASP.
/// </para>
/// </summary>
public sealed partial class FichaFederacionRepository(IDbConnectionFactory connectionFactory, IMediaUrlBuilder media)
    : IFichaFederacionRepository
{
    // Columnas pintadas por el ASP (nunca SELECT *: la tabla guarda credenciales de la API antigua).
    // SUPUESTO: CP y teléfonos son char/varchar rellenos de espacios; el CAST evita fallos si alguno fuera numérico.
    private const string SqlFicha = """
        SELECT f.IdFederacion, f.Federacion, f.Presidente, f.Direccion,
               CAST(f.CP AS nvarchar(20)) AS CP, f.Localidad,
               CAST(f.Telefono1 AS nvarchar(30)) AS Telefono1, CAST(f.Telefono2 AS nvarchar(30)) AS Telefono2,
               f.Email, f.Web
        FROM SFEP_Federaciones AS f
        WHERE f.IdFederacion = @Id;
        """;

    // {0}: filtro de federación de las licencias de jugador ("" en la FEP).
    // {1}: filtro de técnicos y árbitros. {2}: filtro de la federación de los clubes ("" en la FEP).
    private const string SqlEstadisticas = """
        -- Licencias de jugador/a, género y tramos de edad (sql_numLicJug + sql_genero + sql_edades)
        SELECT COUNT(l.Federacion) AS Jugadores,
               COUNT(CASE WHEN fi.Genero = 'M' THEN 1 END) AS Masculino,
               COUNT(CASE WHEN fi.Genero = 'F' THEN 1 END) AS Femenino,
               COUNT(CASE WHEN fi.Genero IS NULL OR fi.Genero NOT IN ('M', 'F') THEN 1 END) AS GeneroSinDato,
               COUNT(CASE WHEN @Anio - YEAR(fi.FechaNac) <= 18 THEN 1 END) AS Menores,
               COUNT(CASE WHEN @Anio - YEAR(fi.FechaNac) BETWEEN 19 AND 23 THEN 1 END) AS Sub23,
               COUNT(CASE WHEN @Anio - YEAR(fi.FechaNac) BETWEEN 24 AND 39 THEN 1 END) AS Senior,
               COUNT(CASE WHEN @Anio - YEAR(fi.FechaNac) >= 40 THEN 1 END) AS Veteranos,
               COUNT(CASE WHEN fi.FechaNac IS NULL THEN 1 END) AS EdadSinDato
        FROM SFEP_Licencia AS l
        INNER JOIN SFEP_Fichas AS fi ON fi.LicenciaJugador = l.IdLicencia
        WHERE l.[Año] = @Anio AND l.Tipo = 'J' AND l.Tramitado = 'S'{0};

        -- Puesto entre las federaciones autonómicas por licencias de jugador/a (sql_puesto)
        SELECT COUNT(*) AS FederacionesEnRanking,
               MAX(CASE WHEN r.Federacion = @Id THEN r.Puesto END) AS Puesto
        FROM (SELECT l.Federacion, RANK() OVER (ORDER BY COUNT(*) DESC) AS Puesto
              FROM SFEP_Licencia AS l
              INNER JOIN SFEP_Fichas AS fi ON fi.LicenciaJugador = l.IdLicencia
              WHERE l.[Año] = @Anio AND l.Tipo = 'J' AND l.Tramitado = 'S'
                AND l.Federacion BETWEEN @FederacionDesde AND @FederacionHasta
              GROUP BY l.Federacion) AS r;

        -- Técnicos nacionales (sql_numLicTec)
        SELECT COUNT(*)
        FROM SFEP_Licencia AS l
        INNER JOIN SFEP_Fichas AS fi ON fi.IdFicha = l.IdFicha
        WHERE l.[Año] = @Anio AND l.Tipo = 'T' AND l.Tramitado = 'S'{1};

        -- Jueces-árbitros nacionales habilitados por la FEP (sql_numLicArb)
        SELECT COUNT(*)
        FROM SFEP_Licencia AS l
        INNER JOIN SFEP_Fichas AS fi ON fi.IdFicha = l.IdFicha
        WHERE l.[Año] = @Anio AND l.Tipo = 'A' AND l.Tramitado = 'S' AND fi.ArbitroHabilitadoFEP = 'S'{1};

        -- Clubes federados (sql_clubes): EXISTS para no contar dos veces un club (ver análisis)
        SELECT COUNT(*)
        FROM SFEP_Clubes AS c
        WHERE EXISTS (SELECT 1 FROM SFEP_Clubes_Licencia AS cl
                      WHERE cl.IdClub = c.IdClub AND cl.[Año] = @Anio AND cl.Renovado = 'S')
          AND EXISTS (SELECT 1 FROM SFEP_Federaciones AS f WHERE f.Comunidad = c.Comunidad{2});

        -- Evolución: años cerrados (sql_evolucion)
        SELECT h.[Año] AS Anio, h.Licencia AS Licencias
        FROM SFEP_Web_Historial AS h
        WHERE h.Federacion = @Id AND h.[Año] >= @PrimerAnio AND h.[Año] < @Anio
        ORDER BY h.[Año];
        """;

    private const string FiltroJugadoresFederacion = "\n  AND l.Federacion = @Id";

    // Autonómica: licencias expedidas por la federación (SFEP_Licencia.Federacion), como el ASP.
    private const string FiltroTecnicosFederacion = "\n  AND l.Federacion = @Id";

    // FEP: el ASP contaba las licencias cuya ficha es de una comunidad con federación (INNER JOIN por
    // Comunidad). Con EXISTS se mantiene el criterio sin multiplicar filas si una comunidad tiene varias
    // filas en SFEP_Federaciones.
    private const string FiltroTecnicosNacional =
        "\n  AND EXISTS (SELECT 1 FROM SFEP_Federaciones AS f WHERE f.Comunidad = fi.Comunidad)";

    private const string FiltroClubesFederacion = " AND f.IdFederacion = @Id";

    public async Task<FichaFederacionDto?> ObtenerAsync(int id, DateTime ahora, CancellationToken cancellationToken)
    {
        var anio = ahora.Year;
        var parametros = new
        {
            Id = id,
            Anio = anio,
            PrimerAnio = ReglasFichaFederacion.PrimerAnioEvolucion,
            // Puesto entre las 22 federaciones autonómicas (DECISIÓN FEP).
            FederacionDesde = ReglasFederaciones.IdMinimo,
            FederacionHasta = ReglasFederaciones.IdMaximo,
        };

        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var ficha = await conexion.QuerySingleOrDefaultAsync<FilaFederacion>(
            new CommandDefinition(SqlFicha, parametros, cancellationToken: cancellationToken));
        if (ficha is null)
        {
            return null;
        }

        var nacional = id == ReglasFichaFederacion.IdNacional;
        var sql = string.Format(
            SqlEstadisticas,
            nacional ? string.Empty : FiltroJugadoresFederacion,
            nacional ? FiltroTecnicosNacional : FiltroTecnicosFederacion,
            nacional ? string.Empty : FiltroClubesFederacion);

        using var lector = await conexion.QueryMultipleAsync(
            new CommandDefinition(sql, parametros, cancellationToken: cancellationToken));
        var jugadores = await lector.ReadSingleAsync<FilaJugadores>();
        var ranking = await lector.ReadSingleAsync<FilaRanking>();
        var tecnicos = await lector.ReadSingleAsync<int>();
        var arbitros = await lector.ReadSingleAsync<int>();
        var clubes = await lector.ReadSingleAsync<int>();
        var historico = (await lector.ReadAsync<FilaHistorico>()).ToArray();

        var imagen = $"images/{id}.jpg";
        var licencias = new LicenciasFederacionDto(
            anio,
            jugadores.Jugadores,
            ranking.Puesto is { } puesto ? (int)puesto : null,
            ranking.FederacionesEnRanking,
            tecnicos,
            arbitros,
            clubes,
            Evolucion(historico.Select(h => (h.Anio, h.Licencias)), anio, jugadores.Jugadores),
            new GeneroLicenciasDto(jugadores.Masculino, jugadores.Femenino, jugadores.GeneroSinDato),
            new EdadesLicenciasDto(jugadores.Menores, jugadores.Sub23, jugadores.Senior, jugadores.Veteranos, jugadores.EdadSinDato));

        return new FichaFederacionDto(
            id,
            Espacios(ficha.Federacion) ?? string.Empty,
            nacional,
            imagen,
            media.Construir(imagen),
            Espacios(ficha.Presidente),
            Espacios(ficha.Direccion),
            Espacios(ficha.CP),
            Espacios(ficha.Localidad),
            Telefonos(ficha.Telefono1, ficha.Telefono2),
            Texto.Limpio(ficha.Email),
            Texto.EnlaceExterno(ficha.Web),
            licencias,
            ahora);
    }

    /// <summary>
    /// Serie del gráfico: un elemento por año desde <see cref="ReglasFichaFederacion.PrimerAnioEvolucion"/>
    /// hasta el anterior al en curso (<c>null</c> si el histórico no tiene el año; si lo repite, gana la
    /// última fila, como el <c>select case</c> del ASP) y, al final, el año en curso con la cifra en directo.
    /// </summary>
    internal static IReadOnlyList<EvolucionLicenciasDto> Evolucion(
        IEnumerable<(int Anio, int? Licencias)> historico, int anio, int jugadores)
    {
        var porAnio = new Dictionary<int, int?>();
        foreach (var (a, licencias) in historico)
        {
            porAnio[a] = licencias;
        }

        var serie = new List<EvolucionLicenciasDto>();
        for (var a = ReglasFichaFederacion.PrimerAnioEvolucion; a < anio; a++)
        {
            serie.Add(new EvolucionLicenciasDto(a, porAnio.GetValueOrDefault(a), false));
        }

        serie.Add(new EvolucionLicenciasDto(anio, jugadores, true));
        return serie;
    }

    /// <summary>Teléfonos no vacíos, sin repetir (se comparan solo los dígitos: "981 231 537" = "981231537").</summary>
    internal static IReadOnlyList<string> Telefonos(params string?[] telefonos)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var resultado = new List<string>();
        foreach (var telefono in telefonos)
        {
            var limpio = Espacios(telefono);
            if (limpio is null || limpio.Trim('-', '.', ' ').Length == 0)
            {
                continue;
            }

            var digitos = new string(limpio.Where(char.IsAsciiDigit).ToArray());
            if (vistos.Add(digitos.Length > 0 ? digitos : limpio))
            {
                resultado.Add(limpio);
            }
        }

        return resultado;
    }

    /// <summary>Recorta y colapsa espacios repetidos ("FEDERACION DE PADEL  CASTILLA-LA MANCHA"); vacío → <c>null</c>.</summary>
    internal static string? Espacios(string? valor)
    {
        var limpio = Texto.Limpio(valor);
        return limpio is null ? null : EspaciosRepetidos().Replace(limpio, " ");
    }

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex EspaciosRepetidos();

    // SUPUESTO: IdFederacion, [Año], Federacion (de SFEP_Licencia y SFEP_Web_Historial) y Licencia son
    // enteros (el ASP los comparaba sin comillas); Tipo, Tramitado, Genero, Renovado y ArbitroHabilitadoFEP
    // son char(1); FechaNac es fecha (el ASP hacía Year([FechaNac])).
    private sealed class FilaFederacion
    {
        public int IdFederacion { get; set; }
        public string? Federacion { get; set; }
        public string? Presidente { get; set; }
        public string? Direccion { get; set; }
        public string? CP { get; set; }
        public string? Localidad { get; set; }
        public string? Telefono1 { get; set; }
        public string? Telefono2 { get; set; }
        public string? Email { get; set; }
        public string? Web { get; set; }
    }

    private sealed class FilaJugadores
    {
        public int Jugadores { get; set; }
        public int Masculino { get; set; }
        public int Femenino { get; set; }
        public int GeneroSinDato { get; set; }
        public int Menores { get; set; }
        public int Sub23 { get; set; }
        public int Senior { get; set; }
        public int Veteranos { get; set; }
        public int EdadSinDato { get; set; }
    }

    private sealed class FilaRanking
    {
        public int FederacionesEnRanking { get; set; }

        // RANK() devuelve bigint.
        public long? Puesto { get; set; }
    }

    private sealed class FilaHistorico
    {
        public int Anio { get; set; }
        public int? Licencias { get; set; }
    }
}
