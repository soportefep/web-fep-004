using Dapper;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Convocatoria para organizar las pruebas del calendario FEP (datos de la BD).</summary>
public interface IOrganizacionTorneosRepository
{
    /// <summary>
    /// Pruebas licitables de <paramref name="temporada"/>, comunidades con sus provincias y ficheros de la
    /// convocatoria, en una sola ida a la BD.
    /// </summary>
    Task<DatosOrganizacionTorneosDto> ObtenerAsync(int temporada, CancellationToken cancellationToken);

    /// <summary>Clubes que pueden ser sede en la provincia <paramref name="provincia"/>, por nombre.</summary>
    Task<IReadOnlyList<ClubSedeDto>> ListarClubesAsync(int provincia, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Organizacion_Torneos.asp</c> (<c>sql_menores</c>, <c>sql_abs</c>, <c>sql_vet</c>, <c>sql_silla</c>,
/// <c>sql_com</c>, <c>sql_archivo</c>), <c>BarraLateralLicitacion.inc</c> (calendario lateral: <c>sql_ano</c> y una
/// consulta <c>sql_mes</c> por mes), <c>cargarfechastorneo.asp</c> (fechas y fianza del torneo elegido, una petición
/// por cambio del desplegable), <c>comboProvincias.asp</c> y <c>comboclubesLicitacion.asp</c>.
/// El ASP lanzaba 10 consultas propias por página más una por mes del calendario lateral (y las de <c>Menus.inc</c>),
/// con <c>id</c> concatenado sin comillas en los tres ficheros auxiliares (inyección SQL). Aquí: una ida a la BD
/// (<c>QueryMultiple</c>) con las pruebas (con fechas y fianza: ya no hace falta <c>cargarfechastorneo.asp</c>), las
/// comunidades, las provincias y los ficheros; y otra, parametrizada, para los clubes de una provincia.
/// <para>
/// No se usan (el ASP las consultaba sin pintarlas): <c>sql_noticias</c>, <c>sql_prov</c> (todas las provincias),
/// <c>sql_num</c> (contador con la variable <c>ano</c> sin asignar), la columna <c>Archivo</c> de
/// <c>SFEP_Calendario_Archivo</c> y <c>SFEP_Calendario.Tipo</c>.
/// </para>
/// </summary>
public sealed class OrganizacionTorneosRepository(IDbConnectionFactory connectionFactory) : IOrganizacionTorneosRepository
{
    /// <summary>Clave del documento "Calendario Provisional" (<c>archivo3</c> del ASP: columna <c>ArchivosLicitacion</c>).</summary>
    public const string ClaveCalendarioProvisional = "calendario-provisional";

    /// <summary>Clave del documento "Condiciones Organización de Torneos" (<c>archivo2</c>: columna <c>CalendarioLicitacion</c>).</summary>
    public const string ClaveCondiciones = "condiciones";

    // Las cuatro consultas del desplegable y la del calendario lateral filtraban lo mismo (Año, Federacion = 0,
    // Licitable = 'S') con distintas categorías: una sola consulta con todas las licitables (el lateral no filtraba
    // categoría) y el grupo se calcula con ReglasOrganizacionTorneos.Grupos. Fechas y fianza, de cargarfechastorneo.asp.
    // Mismo criterio que el calendario FEP para [Año] (entero o texto numérico, comparado con un parámetro entero).
    // sql_com: comunidades con IdComunidad < 20, por nombre. comboProvincias.asp: provincias de cada comunidad por
    // nombre (todas de una vez). sql_archivo: tabla de configuración de una fila (TOP (1), como en el calendario FEP).
    private const string SqlConvocatoria = """
        SELECT c.IdCalendario AS Id, c.Campeonato, c.Categoria, c.FechaIni, c.FechaFin,
               TRY_CAST(c.Fianza AS decimal(12, 2)) AS Fianza
        FROM SFEP_Calendario AS c
        WHERE c.[Año] = @Temporada
          AND c.Federacion = @Federacion
          AND c.Licitable = 'S'
        ORDER BY CASE WHEN c.FechaIni IS NULL THEN 1 ELSE 0 END, c.FechaIni, c.IdCalendario;

        SELECT co.IdComunidad AS Id, co.Comunidad AS Nombre
        FROM SFEP_Comunidades AS co
        WHERE co.IdComunidad < @ComunidadLimite
        ORDER BY co.Comunidad;

        SELECT p.IdProvincia AS Id, p.Provincia AS Nombre, p.IdComunidad
        FROM SFEP_Provincias AS p
        WHERE p.IdComunidad < @ComunidadLimite
          AND p.IdProvincia <> 0
        ORDER BY p.Provincia;

        SELECT TOP (1) a.ArchivosLicitacion, a.CalendarioLicitacion
        FROM SFEP_Calendario_Archivo AS a;
        """;

    // comboclubesLicitacion.asp: clubes que no están de baja ni son independientes, por nombre. IdClub como texto
    // (tiene ceros a la izquierda, como en el resto de la API).
    private const string SqlClubes = """
        SELECT CAST(cl.IdClub AS varchar(20)) AS Id, cl.Club AS Nombre, cl.Provincia AS ProvinciaId
        FROM SFEP_Clubes AS cl
        WHERE cl.Provincia = @Provincia
          AND cl.Baja = 'N'
          AND cl.Independiente = 'N'
        ORDER BY cl.Club;
        """;

    public async Task<DatosOrganizacionTorneosDto> ObtenerAsync(int temporada, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var lector = await conexion.QueryMultipleAsync(new CommandDefinition(
            SqlConvocatoria,
            new
            {
                Temporada = temporada,
                Federacion = ReglasOrganizacionTorneos.IdFederacionFep,
                ComunidadLimite = ReglasOrganizacionTorneos.IdComunidadLimite,
            },
            cancellationToken: cancellationToken));

        var torneos = (await lector.ReadAsync<FilaTorneo>())
            .Select(f => new TorneoLicitableDto(
                f.Id,
                ReglasOrganizacionTorneos.Titulo(f.Campeonato),
                ReglasOrganizacionTorneos.GrupoDe(f.Categoria),
                f.FechaIni is { } ini ? DateOnly.FromDateTime(ini) : null,
                f.FechaFin is { } fin ? DateOnly.FromDateTime(fin) : null,
                ReglasOrganizacionTorneos.Fianza(f.Fianza)))
            .Where(t => t.Titulo.Length > 0)
            .ToArray();

        var comunidades = (await lector.ReadAsync<FilaComunidad>()).ToArray();
        var provincias = (await lector.ReadAsync<FilaProvincia>())
            .GroupBy(p => p.IdComunidad)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ProvinciaSolicitudDto>)g
                    .Select(p => new ProvinciaSolicitudDto(p.Id, Texto.Requerido(p.Nombre)))
                    .ToArray());

        var fila = await lector.ReadFirstOrDefaultAsync<FilaArchivos>();
        var archivos = new Dictionary<string, string>();
        // El ASP enlazaba "refs/docs/" + columna aunque estuviera vacía (enlace a la carpeta: 403).
        if (Texto.Limpio(fila?.ArchivosLicitacion) is { } calendario)
        {
            archivos[ClaveCalendarioProvisional] = calendario;
        }

        if (Texto.Limpio(fila?.CalendarioLicitacion) is { } condiciones)
        {
            archivos[ClaveCondiciones] = condiciones;
        }

        return new DatosOrganizacionTorneosDto(
            torneos,
            comunidades
                .Select(c => new ComunidadSolicitudDto(c.Id, Texto.Requerido(c.Nombre), provincias.GetValueOrDefault(c.Id) ?? []))
                .ToArray(),
            archivos);
    }

    public async Task<IReadOnlyList<ClubSedeDto>> ListarClubesAsync(int provincia, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var filas = await conexion.QueryAsync<FilaClub>(new CommandDefinition(
            SqlClubes, new { Provincia = provincia }, cancellationToken: cancellationToken));

        return filas
            .Where(f => Texto.Limpio(f.Id) is not null && Texto.Limpio(f.Nombre) is not null)
            .Select(f => new ClubSedeDto(f.Id!.Trim(), f.Nombre!.Trim(), f.ProvinciaId ?? provincia))
            .ToArray();
    }

    // SUPUESTO (mismo que P0063): IdCalendario, Categoria y Federacion enteros; [Año] entero o texto numérico;
    // FechaIni/FechaFin datetime; Fianza numérica (se lee con TRY_CAST); Licitable char.
    private sealed class FilaTorneo
    {
        public int Id { get; set; }
        public string? Campeonato { get; set; }
        public int? Categoria { get; set; }
        public DateTime? FechaIni { get; set; }
        public DateTime? FechaFin { get; set; }
        public decimal? Fianza { get; set; }
    }

    private sealed class FilaComunidad
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
    }

    private sealed class FilaProvincia
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public int IdComunidad { get; set; }
    }

    private sealed class FilaArchivos
    {
        public string? ArchivosLicitacion { get; set; }
        public string? CalendarioLicitacion { get; set; }
    }

    // SUPUESTO (mismo que P0138): IdClub texto o número (se lee como texto), Provincia entera, Baja e Independiente char(1).
    private sealed class FilaClub
    {
        public string? Id { get; set; }
        public string? Nombre { get; set; }
        public int? ProvinciaId { get; set; }
    }
}
