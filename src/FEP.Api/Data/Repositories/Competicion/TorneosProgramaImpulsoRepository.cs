using Dapper;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Torneos FEP incluidos en el Programa Impulso (listado público por temporada).</summary>
public interface ITorneosProgramaImpulsoRepository
{
    /// <summary>
    /// Temporadas con torneos del programa y los torneos de <paramref name="anio"/> (o de la más reciente).
    /// <paramref name="ahora"/> decide qué plazos de inscripción siguen abiertos.
    /// </summary>
    Task<TorneosProgramaImpulsoDto> BuscarAsync(int? anio, DateTime ahora, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Impulso.asp</c> (sql_anotorneos, sql_torneos y, por cada torneo, sql_club) y
/// <c>Torneos_Previo.asp</c> (ficha a la que se iba al pulsar un torneo sin <c>LinkCuadros</c>). El ASP
/// concatenaba <c>ano</c> en el SQL (inyección: <c>limpiar()</c> es una lista negra), recorría el recordset dos
/// veces (contar y pintar) y lanzaba una consulta a <c>SFEP_Clubes</c> por torneo (N+1). Aquí: una sola
/// conexión, parámetros y el club con <c>OUTER APPLY</c>. Las reglas de enlace, plazo y categoría son las
/// de <see cref="ReglasCampeonatos"/> (mismo modelo de torneo que <c>Campeonatos.asp</c>).
/// </summary>
public sealed class TorneosProgramaImpulsoRepository(IDbConnectionFactory connectionFactory, IOptions<MediaOptions> media)
    : ITorneosProgramaImpulsoRepository
{
    // sql_anotorneos de Impulso.asp: a diferencia de Campeonatos.asp no filtra PPT ni Empresas.
    private const string SqlAnios = """
        SELECT t.[Año]
        FROM SFEP_Torneos AS t
        WHERE t.IdFederacion = 0
          AND t.Borrado = 'N'
          AND t.Visible = 'S'
          AND t.ProgramaImpulso = 'S'
        GROUP BY t.[Año]
        HAVING t.[Año] > 1900
        ORDER BY t.[Año] DESC;
        """;

    // sql_torneos con los mismos filtros fijos que el ASP. Columnas: las que pintaba el ASP (IdPrueba,
    // Torneo, FechaInicio, FechaFin, Ciudad, FechaInscripcion, FechaPreInscripcion, LinkCuadros, Club, Club2),
    // TipoTorneo/ModalidadTorneo (el ASP calculaba su color pero no lo pintaba) y las que usaba
    // Torneos_Previo.asp para elegir la ficha (TorneoPadre, Liga, Empresas). Circuito: silla de ruedas.
    // sql_club (N+1) → OUTER APPLY; TOP (1) por si IdClub se repitiera.
    private const string SqlTorneos = """
        SELECT t.IdPrueba, t.[Año] AS Anio, t.Torneo, t.TipoTorneo, t.ModalidadTorneo, t.Circuito,
               t.FechaInicio, t.FechaFin, t.Ciudad, t.FechaInscripcion, t.FechaPreInscripcion,
               t.LinkCuadros, t.TorneoPadre, t.Liga, t.Empresas, c.Club, t.Club2
        FROM SFEP_Torneos AS t
        OUTER APPLY (SELECT TOP (1) cl.Club
                     FROM SFEP_Clubes AS cl
                     WHERE CAST(cl.IdClub AS varchar(20)) = CAST(t.Club AS varchar(20))) AS c
        WHERE t.IdFederacion = 0
          AND t.[Año] = @Anio
          AND t.Borrado = 'N'
          AND t.Visible = 'S'
          AND t.ProgramaImpulso = 'S'
        ORDER BY t.FechaInicio DESC, t.IdPrueba DESC;
        """;

    public async Task<TorneosProgramaImpulsoDto> BuscarAsync(int? anio, DateTime ahora, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var anios = (await conexion.QueryAsync<int>(new CommandDefinition(SqlAnios, cancellationToken: cancellationToken))).ToArray();

        // Sin año pedido: la temporada más reciente (como rs_anotorneos("Año") en el ASP, que fallaba si no había ninguna).
        var temporada = anio ?? (anios.Length > 0 ? anios[0] : (int?)null);
        if (temporada is null)
        {
            return new TorneosProgramaImpulsoDto(null, anios, []);
        }

        var filas = await conexion.QueryAsync<FilaTorneo>(new CommandDefinition(
            SqlTorneos, new { Anio = temporada.Value }, cancellationToken: cancellationToken));

        return new TorneosProgramaImpulsoDto(temporada, anios, filas.Select(f => AItem(f, ahora)).ToArray());
    }

    private CampeonatoDto AItem(FilaTorneo f, DateTime ahora)
    {
        var tipo = ReglasCampeonatos.TipoDesdeBd(f.TipoTorneo);
        var (categoria, seleccion) = ReglasCampeonatos.CategoriaDesdeBd(f.ModalidadTorneo);

        // Como el ASP: LinkCuadros en ventana nueva; si no hay, la ficha de Torneos_Previo.asp.
        var cuadros = ReglasCampeonatos.EnlaceCuadros(f.LinkCuadros);
        var ficha = cuadros ?? AUrl(ReglasCampeonatos.RutaFicha(f.IdPrueba, f.ModalidadTorneo, f.TipoTorneo, f.TorneoPadre, f.Liga, f.Empresas));

        // El ASP pintaba "Sin definir" si no había club; aquí la lista va vacía.
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

    // SUPUESTO: mismos tipos de columna que en CampeonatosRepository (IdPrueba, [Año], TorneoPadre enteros;
    // fechas datetime; TipoTorneo, ModalidadTorneo, Circuito, Liga, Empresas, ProgramaImpulso char/varchar;
    // SFEP_Torneos.Club = código de SFEP_Clubes.IdClub; Club2 texto libre).
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
