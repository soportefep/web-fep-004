using System.Data;
using Dapper;
using FEP.Api.Contracts.Clubes;
using FEP.Api.Data.Repositories.Federacion;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Clubes;

/// <summary>Ficha pública de un club (solo lectura).</summary>
public interface IFichaClubRepository
{
    /// <summary>
    /// Ficha del club <paramref name="id"/> con las licencias de <paramref name="anio"/>, o <c>null</c> si no existe
    /// en <c>SFEP_Clubes</c>.
    /// </summary>
    Task<ClubDetalleDto?> ObtenerAsync(string id, int anio, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Club.asp</c> (sql_club, sql_licencias, sql_licenciasNAC y sql_torneos). El ASP concatenaba <c>id</c>
/// en las cuatro consultas, hacía <c>SELECT *</c> de <c>SFEP_Clubes</c>, recorría cada recordset dos veces (una para
/// contar) y unía <c>SFEP_Torneos_Desglose</c> con <c>GROUP BY</c> para quitar los duplicados.
/// <para>
/// Aquí: solo <c>SELECT</c> con columnas explícitas, <c>@Id</c>/<c>@Anio</c> siempre como parámetros, un único lote,
/// <c>EXISTS</c> en lugar del <c>JOIN</c> + <c>GROUP BY</c> con el desglose, y la fecha de nacimiento reducida al año
/// en SQL (solo sirve para la categoría; nunca sale de la API).
/// </para>
/// </summary>
public sealed class FichaClubRepository(IDbConnectionFactory connectionFactory, IOptions<MediaOptions> media)
    : IFichaClubRepository
{
    private const string Sql = """
        -- Club (sql_club; sin SELECT *) y su federación autonómica (la de su comunidad, como Clubes.asp)
        SELECT CAST(c.IdClub AS varchar(20)) AS IdClub, c.Club, c.Presidente, c.Direccion,
               CAST(c.CP AS nvarchar(20)) AS CP, c.Localidad,
               CAST(c.Telefono AS nvarchar(30)) AS Telefono, CAST(c.Telefono2 AS nvarchar(30)) AS Telefono2,
               c.Email, c.Web, p.IdProvincia, p.Provincia, fed.IdFederacion, fed.Abreviatura
        FROM SFEP_Clubes AS c
        LEFT JOIN SFEP_Provincias AS p ON p.IdProvincia = c.Provincia
        OUTER APPLY (SELECT TOP (1) f.IdFederacion, f.Abreviatura
                     FROM SFEP_Federaciones AS f
                     WHERE f.Comunidad = c.Comunidad AND f.IdFederacion BETWEEN @FederacionDesde AND @FederacionHasta
                     ORDER BY f.IdFederacion) AS fed
        WHERE c.IdClub = @Id;

        -- Licencias autonómicas (sql_licencias)
        SELECT CAST(fi.LicenciaJugador AS varchar(20)) AS Licencia, fi.Nombre, fi.Apellido1, fi.Apellido2, fi.Genero,
               YEAR(TRY_CONVERT(date, fi.FechaNac, 103)) AS AnioNacimiento
        FROM SFEP_Fichas AS fi
        INNER JOIN SFEP_Licencia AS l ON fi.LicenciaJugador = l.IdLicencia
        WHERE l.[Año] = @Anio AND l.Tipo = 'J' AND l.Tramitado = 'S' AND l.ClubLicencia = @Id
        ORDER BY fi.Apellido1, fi.Apellido2, fi.Nombre;

        -- Licencias de competición nacional (sql_licenciasNAC)
        SELECT CAST(fi.LicenciaJugador AS varchar(20)) AS Licencia, fi.Nombre, fi.Apellido1, fi.Apellido2, fi.Genero,
               YEAR(TRY_CONVERT(date, fi.FechaNac, 103)) AS AnioNacimiento
        FROM SFEP_Fichas AS fi
        INNER JOIN SFEP_Licencia AS l ON fi.LicenciaJugador = l.IdLicencia
        WHERE l.[Año] = @Anio AND l.Tipo = 'J' AND l.Tramitado = 'S' AND l.ClubNacional = @Id
        ORDER BY fi.Apellido1, fi.Apellido2, fi.Nombre;

        -- Torneos realizados (sql_torneos): visibles, no borrados y con desglose
        SELECT t.IdPrueba, t.IdFederacion, t.Torneo, t.FechaFin, f.Web, t.ModalidadTorneo
        FROM SFEP_Torneos AS t
        INNER JOIN SFEP_Federaciones AS f ON f.IdFederacion = t.IdFederacion
        WHERE t.Borrado = 'N' AND t.Visible = 'S' AND t.Club = @Id
          AND EXISTS (SELECT 1 FROM SFEP_Torneos_Desglose AS d WHERE d.IdPrueba = t.IdPrueba)
        ORDER BY t.FechaFin DESC, t.IdPrueba DESC;
        """;

    public async Task<ClubDetalleDto?> ObtenerAsync(string id, int anio, CancellationToken cancellationToken)
    {
        var parametros = new DynamicParameters();
        // varchar: el ASP comparaba IdClub con comillas ('0118003'); así se conservan los ceros a la izquierda.
        parametros.Add("Id", id, DbType.AnsiString, size: 20);
        parametros.Add("Anio", anio);
        parametros.Add("FederacionDesde", BusquedaClubes.FederacionMinima);
        parametros.Add("FederacionHasta", BusquedaClubes.FederacionMaxima);

        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var lector = await conexion.QueryMultipleAsync(new CommandDefinition(Sql, parametros, cancellationToken: cancellationToken));
        var club = await lector.ReadFirstOrDefaultAsync<FilaClub>();
        if (club is null)
        {
            return null;
        }

        var autonomicas = (await lector.ReadAsync<FilaLicencia>()).Select(l => Licencia(l, anio)).ToArray();
        var nacionales = (await lector.ReadAsync<FilaLicencia>()).Select(l => Licencia(l, anio)).ToArray();
        var torneos = (await lector.ReadAsync<FilaTorneo>())
            .Where(t => t.FechaFin is not null && !string.IsNullOrWhiteSpace(t.Torneo))
            .Select(Torneo)
            .ToArray();

        return new ClubDetalleDto(
            Texto.Requerido(club.IdClub),
            FichaFederacionRepository.Espacios(club.Club) ?? string.Empty,
            club.IdProvincia,
            Texto.Limpio(club.Provincia),
            club.IdFederacion,
            Texto.Limpio(club.Abreviatura),
            FichaFederacionRepository.Espacios(club.Presidente),
            FichaFederacionRepository.Espacios(club.Direccion),
            Texto.Limpio(club.CP),
            FichaFederacionRepository.Espacios(club.Localidad),
            FichaFederacionRepository.Telefonos(club.Telefono, club.Telefono2),
            Texto.Limpio(club.Email),
            Texto.EnlaceExterno(club.Web),
            anio,
            autonomicas,
            nacionales,
            torneos);
    }

    internal static LicenciaClubDto Licencia(FilaLicencia l, int anio) => new(
        ReglasFichaClub.NumeroLicencia(l.Licencia),
        ReglasFichaClub.Jugador(l.Apellido1, l.Apellido2, l.Nombre),
        ReglasFichaClub.Genero(l.Genero),
        ReglasFichaClub.CategoriaAbreviada(l.AnioNacimiento, anio));

    private TorneoClubDto Torneo(FilaTorneo t)
    {
        var fin = t.FechaFin!.Value;
        // Como el ASP: los de la FEP enlazan a su ficha; los autonómicos, a la web de su federación.
        var url = t.IdFederacion == 0
            ? WebActual.Url(media.Value, $"torneo.asp?Idtorneo={t.IdPrueba}")
            : Texto.EnlaceExterno(t.Web);
        return new TorneoClubDto(
            t.IdPrueba,
            t.IdFederacion,
            fin.Year,
            fin.Month,
            FichaFederacionRepository.Espacios(t.Torneo) ?? string.Empty,
            ReglasFichaClub.Modalidad(t.ModalidadTorneo),
            url);
    }

    // SUPUESTO (como en ClubesRepository y FichaFederacionRepository): IdProvincia, IdFederacion, IdPrueba y [Año]
    // enteros; IdClub, LicenciaJugador y los teléfonos se leen como texto (CAST); FechaFin datetime (el ASP usaba
    // month()/year()); FechaNac fecha o texto dd/mm/aaaa (TRY_CONVERT con estilo 103 admite ambas, como Estadisticas).
    // SUPUESTO: SFEP_Torneos.Club guarda el IdClub organizador (el ASP filtraba HAVING SFEP_Torneos.Club = 'id').
    internal sealed class FilaClub
    {
        public string? IdClub { get; set; }
        public string? Club { get; set; }
        public string? Presidente { get; set; }
        public string? Direccion { get; set; }
        public string? CP { get; set; }
        public string? Localidad { get; set; }
        public string? Telefono { get; set; }
        public string? Telefono2 { get; set; }
        public string? Email { get; set; }
        public string? Web { get; set; }
        public int? IdProvincia { get; set; }
        public string? Provincia { get; set; }
        public int? IdFederacion { get; set; }
        public string? Abreviatura { get; set; }
    }

    internal sealed class FilaLicencia
    {
        public string? Licencia { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido1 { get; set; }
        public string? Apellido2 { get; set; }
        public string? Genero { get; set; }
        public int? AnioNacimiento { get; set; }
    }

    internal sealed class FilaTorneo
    {
        public int IdPrueba { get; set; }
        public int IdFederacion { get; set; }
        public string? Torneo { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? Web { get; set; }
        public string? ModalidadTorneo { get; set; }
    }
}
