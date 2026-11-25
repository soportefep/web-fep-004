using System.Data;
using System.Text;
using Dapper;
using FEP.Api.Contracts.Licencias;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Licencias;

/// <summary>Buscador público de licencias ("Busca tu licencia").</summary>
public interface ILicenciasRepository
{
    /// <summary>
    /// Licencias que cumplen los criterios, ordenadas por apellidos y nombre: como mucho
    /// <see cref="ReglasLicencias.ResultadosMaximos"/>, con el total.
    /// </summary>
    Task<LicenciasBusquedaDto> BuscarAsync(BusquedaLicencias busqueda, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Licencias_Buscar.asp</c> (12 variantes de <c>sql_jugador</c>: 4 tipos × "solo nombre", "solo fecha" y
/// "nombre y fecha"). El ASP concatenaba en el SQL la federación sin comillas (<c>?fed=abc</c> da error 500), el texto
/// (con <c>Replace("'", "''")</c> después de que <c>limpiar()</c> ya hubiera cambiado el apóstrofo) y la fecha de
/// nacimiento como texto <c>'d/m/aaaa'</c> (depende del idioma del servidor; con un día o mes a 0 da error 500), contaba
/// los resultados recorriendo todo el recordset y no tenía límite.
/// Aquí: una sola consulta con los filtros como parámetros, <c>COUNT</c> + <c>TOP</c>, la fecha como
/// <see cref="DateTime"/> y solo las columnas que se pintaban (licencia, apellidos y nombre).
/// <para>
/// Correcciones de las variantes de silla de ruedas (ver docs/api/analisis/Licencias_Buscar.md): "solo nombre" unía
/// <c>SFEP_Fichas.LicenciaJugador = SillaUltimoAno.LicenciaSilla</c> (otra numeración: en la web actual no encuentra
/// nunca a nadie) y "nombre y fecha" usaba <c>SillaUltimoAno.LicenciaJugador</c> (error 500). Las tres usan ahora la
/// unión de "solo fecha", que era la correcta.
/// </para>
/// <para>El SQL se compone solo con fragmentos fijos de esta clase; los valores del usuario van siempre como parámetros.</para>
/// </summary>
public sealed class LicenciasRepository(IDbConnectionFactory connectionFactory, IOptions<MediaOptions> media) : ILicenciasRepository
{
    // Jugador/a: licencia de la última temporada (vista JugadorUltimoAno) expedida por la federación elegida. Mismos JOIN
    // que el ASP. SUPUESTO: JugadorUltimoAno y SillaUltimoAno son vistas o tablas con una fila por licencia (LicenciaJugador
    // o LicenciaSilla, Ano, Tipo) con la última temporada; SFEP_Licencia.Federacion y [Año] comparables con ellas.
    private const string DesdeJugador = """
        FROM SFEP_Fichas AS f
        INNER JOIN JugadorUltimoAno AS u ON u.LicenciaJugador = f.LicenciaJugador
        INNER JOIN SFEP_Licencia AS l ON l.IdLicencia = u.LicenciaJugador AND l.[Año] = u.Ano AND l.Tipo = u.Tipo
        WHERE l.Federacion = @Federacion
        """;

    // Silla de ruedas: la unión de la variante "solo fecha" del ASP (la única correcta). La federación es opcional.
    private const string DesdeSilla = """
        FROM SFEP_Fichas AS f
        INNER JOIN SillaUltimoAno AS u ON u.LicenciaSilla = f.LicenciaSilla
        INNER JOIN SFEP_Licencia AS l ON l.IdLicencia = u.LicenciaSilla AND l.[Año] = u.Ano AND l.Tipo = u.Tipo
        WHERE 1 = 1
        """;

    private const string FiltroFederacionSilla = "\n  AND l.Federacion = @Federacion";

    // Técnico/a y juez-árbitro: cualquier ficha con ese número de licencia (el ASP no miraba temporada ni federación).
    private const string DesdeTecnico = """
        FROM SFEP_Fichas AS f
        WHERE f.LicenciaTecnico IS NOT NULL
        """;

    private const string DesdeArbitro = """
        FROM SFEP_Fichas AS f
        WHERE f.LicenciaArbitro IS NOT NULL
        """;

    // SUPUESTO: como en el buscador de clubes, la intercalación de la BD distingue acentos (en la web actual "GARCÍA" no
    // encuentra "GARCIA"): se compara sin distinguir mayúsculas ni acentos. NombreCompleto existe y está relleno (mismo
    // supuesto que P0315).
    private const string FiltroTermino = "\n  AND f.NombreCompleto COLLATE Latin1_General_CI_AI LIKE @T{0}";

    // SUPUESTO: SFEP_Fichas.FechaNac es datetime (en la web actual una fecha imposible como '0/0/1975' da error 500 de
    // conversión); TRY_CONVERT con estilo 103 admite también texto dd/mm/aaaa (mismo supuesto que P0461).
    private const string FiltroFecha = "\n  AND TRY_CONVERT(date, f.FechaNac, 103) = @FechaNacimiento";

    private const string Orden = "\nORDER BY f.Apellido1, f.Apellido2, f.Nombre, Licencia;";

    public async Task<LicenciasBusquedaDto> BuscarAsync(BusquedaLicencias busqueda, CancellationToken cancellationToken)
    {
        var parametros = new DynamicParameters();
        parametros.Add("Limite", ReglasLicencias.ResultadosMaximos);

        var (desde, columnaLicencia) = busqueda.Tipo switch
        {
            TipoLicencia.Tecnico => (DesdeTecnico, "f.LicenciaTecnico"),
            TipoLicencia.Arbitro => (DesdeArbitro, "f.LicenciaArbitro"),
            TipoLicencia.SillaRuedas => (DesdeSilla, "f.LicenciaSilla"),
            _ => (DesdeJugador, "f.LicenciaJugador"),
        };

        var where = new StringBuilder(desde);
        if (busqueda.Tipo == TipoLicencia.Jugador)
        {
            parametros.Add("Federacion", busqueda.Federacion ?? 0);
        }
        else if (busqueda.Tipo == TipoLicencia.SillaRuedas && busqueda.Federacion is { } federacion)
        {
            where.Append(FiltroFederacionSilla);
            parametros.Add("Federacion", federacion);
        }

        for (var i = 0; i < busqueda.Terminos.Count; i++)
        {
            where.AppendFormat(FiltroTermino, i);
            parametros.Add($"T{i}", $"%{ReglasLicencias.EscaparLike(busqueda.Terminos[i])}%");
        }

        if (busqueda.FechaNacimiento is { } fecha)
        {
            where.Append(FiltroFecha);
            parametros.Add("FechaNacimiento", fecha.ToDateTime(TimeOnly.MinValue), DbType.Date);
        }

        // SUPUESTO: las columnas de licencia son numéricas o texto corto; se leen como texto (CAST) para no perder
        // ceros a la izquierda ni fallar si alguna fuera alfanumérica.
        var sql = $"""
            SELECT COUNT(*)
            {where};

            SELECT TOP (@Limite) CAST({columnaLicencia} AS varchar(20)) AS Licencia, f.Nombre, f.Apellido1, f.Apellido2
            {where}{Orden}
            """;

        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var lector = await conexion.QueryMultipleAsync(new CommandDefinition(sql, parametros, cancellationToken: cancellationToken));
        var total = await lector.ReadSingleAsync<int>();
        var filas = await lector.ReadAsync<FilaLicencia>();

        var items = filas
            .Select(f => (Licencia: Texto.Limpio(f.Licencia), Fila: f))
            .Where(x => x.Licencia is not null)
            .Select(x => new LicenciaEncontradaDto(
                ReglasLicencias.LicenciaVisible(busqueda.Tipo, x.Licencia!),
                Texto.Limpio(x.Fila.Apellido1),
                Texto.Limpio(x.Fila.Apellido2),
                Texto.Limpio(x.Fila.Nombre),
                ReglasLicencias.RutaEstadisticas(busqueda.Tipo, x.Licencia!) is { } ruta ? WebActual.Url(media.Value, ruta) : null))
            .ToArray();

        return new LicenciasBusquedaDto(
            ReglasLicencias.Clave(busqueda.Tipo),
            busqueda.Federacion,
            busqueda.Terminos.Count > 0 ? string.Join(' ', busqueda.Terminos) : null,
            busqueda.FechaNacimiento is not null,
            total,
            ReglasLicencias.ResultadosMaximos,
            items);
    }

    private sealed class FilaLicencia
    {
        public string? Licencia { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido1 { get; set; }
        public string? Apellido2 { get; set; }
    }
}
