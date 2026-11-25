using System.Data;
using Dapper;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Campeonatos y torneos de la FEP (listado público por temporada).</summary>
public interface ICampeonatosRepository
{
    /// <summary>
    /// Temporadas con torneos publicados y los torneos de <see cref="BusquedaCampeonatos.Anio"/> (o de la
    /// más reciente) que cumplen los filtros. <paramref name="ahora"/> decide qué plazos de inscripción
    /// siguen abiertos.
    /// </summary>
    Task<CampeonatosDto> BuscarAsync(BusquedaCampeonatos busqueda, DateTime ahora, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Campeonatos.asp</c> (sql_anotorneos, sql_torneos y, por cada torneo, sql_club) y
/// <c>Torneos_Previo.asp</c> (a qué ficha se va al pulsar un torneo). El ASP concatenaba <c>ano</c>,
/// <c>modalidad</c> y <c>tipo</c> en el SQL (inyección: <c>limpiar()</c> es una lista negra), recorría el
/// recordset dos veces (contar y pintar) y lanzaba una consulta a <c>SFEP_Clubes</c> por torneo (N+1).
/// Aquí: una sola ida a la BD (<c>QueryMultiple</c>) con parámetros y el club con <c>OUTER APPLY</c>.
/// <para>
/// El SQL se compone solo con fragmentos fijos; los valores del usuario van como parámetros
/// (<c>@Anio</c>, <c>@Tipo</c>, <c>@Modalidad</c>).
/// </para>
/// </summary>
public sealed class CampeonatosRepository(IDbConnectionFactory connectionFactory, IOptions<MediaOptions> media)
    : ICampeonatosRepository
{
    // sql_anotorneos: el desplegable de años sí filtraba PPT = 'N' (el listado no); se mantiene igual.
    private const string SqlAnios = """
        SELECT t.[Año]
        FROM SFEP_Torneos AS t
        WHERE t.IdFederacion = 0
          AND t.Borrado = 'N'
          AND t.Visible = 'S'
          AND t.PPT = 'N'
          AND t.Empresas = 'N'
          AND t.ProgramaImpulso = 'N'
        GROUP BY t.[Año]
        HAVING t.[Año] > 1900
        ORDER BY t.[Año] DESC;
        """;

    // sql_torneos. Mismos filtros fijos que el ASP. TorneoLigadoFEP <> 19721 se mantiene tal cual (también
    // deja fuera las filas con TorneoLigadoFEP NULL, como hoy). El CEM 2021 se añade con OR sin filtros de
    // visibilidad (como el ASP), pero ahora sí respeta los filtros de tipo y categoría (en el ASP los ignoraba).
    // sql_club (N+1) → OUTER APPLY; TOP (1) por si IdClub se repitiera.
    private const string SqlTorneos = """
        SELECT t.IdPrueba, t.[Año] AS Anio, t.Torneo, t.TipoTorneo, t.ModalidadTorneo, t.Circuito,
               t.FechaInicio, t.FechaFin, t.Ciudad, t.FechaInscripcion, t.FechaPreInscripcion,
               t.LinkCuadros, t.TorneoPadre, t.Liga, t.Empresas, c.Club, t.Club2
        FROM SFEP_Torneos AS t
        OUTER APPLY (SELECT TOP (1) cl.Club
                     FROM SFEP_Clubes AS cl
                     WHERE CAST(cl.IdClub AS varchar(20)) = CAST(t.Club AS varchar(20))) AS c
        WHERE t.[Año] = @Anio
          AND ((t.IdFederacion = 0
                AND t.Borrado = 'N'
                AND t.Visible = 'S'
                AND t.Empresas = 'N'
                AND t.ProgramaImpulso = 'N'
                AND t.TorneoLigadoFEP <> @IdCem2021)
               OR t.IdPrueba = @IdCem2021)
          AND (@Tipo IS NULL OR t.TipoTorneo = @Tipo)
          AND (@Modalidad IS NULL OR t.ModalidadTorneo = @Modalidad)
        ORDER BY t.FechaInicio DESC, t.IdPrueba DESC;
        """;

    public async Task<CampeonatosDto> BuscarAsync(BusquedaCampeonatos busqueda, DateTime ahora, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var anios = (await conexion.QueryAsync<int>(new CommandDefinition(SqlAnios, cancellationToken: cancellationToken))).ToArray();

        // Sin año pedido: la temporada más reciente (como rs_anotorneos("Año") en el ASP).
        int? anio = busqueda.Anio ?? (anios.Length > 0 ? anios[0] : (int?)null);
        if (anio is null)
        {
            return new CampeonatosDto(null, anios, busqueda.Tipo, busqueda.Categoria, []);
        }

        var parametros = new DynamicParameters();
        parametros.Add("Anio", anio.Value);
        parametros.Add("IdCem2021", ReglasCampeonatos.IdCampeonatoMenores2021);
        parametros.Add("Tipo", ValorBd(ReglasCampeonatos.Tipos, busqueda.Tipo), DbType.AnsiString, size: 20);
        parametros.Add("Modalidad", ValorBd(ReglasCampeonatos.Categorias, busqueda.Categoria), DbType.AnsiString, size: 20);

        var filas = await conexion.QueryAsync<FilaTorneo>(new CommandDefinition(SqlTorneos, parametros, cancellationToken: cancellationToken));

        var items = filas.Select(f => AItem(f, ahora)).ToArray();
        return new CampeonatosDto(anio, anios, busqueda.Tipo, busqueda.Categoria, items);
    }

    private CampeonatoDto AItem(FilaTorneo f, DateTime ahora)
    {
        var tipo = ReglasCampeonatos.TipoDesdeBd(f.TipoTorneo);
        var (categoria, seleccion) = ReglasCampeonatos.CategoriaDesdeBd(f.ModalidadTorneo);

        // El ASP enlazaba LinkCuadros en ventana nueva y, si no había, enviaba a Torneos_Previo.asp.
        var cuadros = ReglasCampeonatos.EnlaceCuadros(f.LinkCuadros);
        var ficha = cuadros ?? AUrl(ReglasCampeonatos.RutaFicha(f.IdPrueba, f.ModalidadTorneo, f.TipoTorneo, f.TorneoPadre, f.Liga, f.Empresas));

        var clubes = new[] { Texto.Limpio(f.Club), Texto.Limpio(f.Club2) }
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CampeonatoDto(
            f.IdPrueba,
            f.Anio,
            Texto.Requerido(f.Torneo),
            tipo,
            categoria,
            seleccion,
            ReglasCampeonatos.EsSillaRuedas(f.ModalidadTorneo, f.Circuito),
            f.FechaInicio is { } ini ? DateOnly.FromDateTime(ini) : null,
            f.FechaFin is { } fin ? DateOnly.FromDateTime(fin) : null,
            Texto.Limpio(f.Ciudad),
            clubes,
            ficha,
            cuadros is not null,
            ReglasCampeonatos.Plazo(tipo, f.FechaInscripcion, f.FechaPreInscripcion, ahora));
    }

    /// <summary>Rutas relativas → web actual; las absolutas (circuito de menores) se respetan.</summary>
    private string AUrl(string ruta)
        => Uri.TryCreate(ruta, UriKind.Absolute, out var absoluta) ? absoluta.AbsoluteUri : WebActual.Url(media.Value, ruta);

    private static string? ValorBd(IReadOnlyDictionary<string, string> valores, string? clave)
        => clave is not null && valores.TryGetValue(clave, out var valor) ? valor : null;

    // SUPUESTO: IdPrueba, [Año], TorneoPadre, TorneoLigadoFEP enteros (el ASP los comparaba sin comillas
    // salvo Año, que iba entre comillas pero con HAVING Año > 1900); fechas datetime (el ASP usaba
    // month()/day()/formatdatetime); TipoTorneo, ModalidadTorneo, Circuito, Liga, Empresas char/varchar
    // (rellenos de espacios: el ASP hacía rtrim); Club (de SFEP_Torneos) es el código de SFEP_Clubes.IdClub
    // y Club2 texto libre.
    private sealed class FilaTorneo
    {
        public int IdPrueba { get; set; }
        public int Anio { get; set; }
        public string? Torneo { get; set; }
        public string? TipoTorneo { get; set; }
        public string? ModalidadTorneo { get; set; }
        public string? Circuito { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? Ciudad { get; set; }
        public DateTime? FechaInscripcion { get; set; }
        public DateTime? FechaPreInscripcion { get; set; }
        public string? LinkCuadros { get; set; }
        public int? TorneoPadre { get; set; }
        public string? Liga { get; set; }
        public string? Empresas { get; set; }
        public string? Club { get; set; }
        public string? Club2 { get; set; }
    }
}
