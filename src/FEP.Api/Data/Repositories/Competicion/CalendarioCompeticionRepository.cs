using Dapper;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Calendario oficial de competición de la FEP (listado público por temporada).</summary>
public interface ICalendarioCompeticionRepository
{
    /// <summary>
    /// Temporadas publicadas (con su documento), estado de la convocatoria para organizar torneos y pruebas de
    /// <paramref name="anio"/> o, si es <c>null</c>, de la temporada por defecto según <paramref name="anioActual"/>
    /// (<see cref="ReglasCalendario.TemporadaPorDefecto"/>).
    /// </summary>
    Task<DatosCalendarioCompeticionDto> BuscarAsync(int? anio, int anioActual, CancellationToken cancellationToken);

    /// <summary>
    /// Federaciones territoriales (<c>SFEP_Federaciones</c>, ids 1–22, por <c>Abreviatura</c>) con las temporadas en
    /// las que tienen alguna prueba visible (<c>CalendarioFT.asp</c>).
    /// </summary>
    Task<IReadOnlyList<DatosFederacionCalendarioDto>> ListarFederacionesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Calendario de la federación territorial <paramref name="federacion"/>: sus temporadas (con documento) y las
    /// pruebas de <paramref name="anio"/> o, si es <c>null</c>, de su temporada por defecto según
    /// <paramref name="anioActual"/>. <c>null</c> si la federación no existe en <c>SFEP_Federaciones</c>.
    /// </summary>
    Task<DatosCalendarioTerritorialDto?> BuscarFederacionAsync(int federacion, int? anio, int anioActual, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>CalendarioFEP.asp</c> (sql_ano, sql_archivo, sql_calen y las 13 consultas sql_ene…sql_dic +
/// "sin fecha", una por mes) y <c>Torneos_Previo.asp</c> (ficha del torneo enlazado). El ASP concatenaba
/// <c>ano</c> y <c>fed</c> en el SQL (inyección: <c>limpiar()</c> es una lista negra; <c>fed</c> iba sin comillas),
/// lanzaba 19 consultas propias por página (noticias, años, configuración, contador sin filtrar <c>Visible</c>,
/// federaciones, documento y 13 de meses + "sin fecha"), además de las de <c>Menus.inc</c>, y fallaba con un
/// error 500 en las temporadas sin documento (leía <c>rs_calen("Archivo")</c> sin comprobar EOF).
/// Aquí: dos idas a la BD (<c>QueryMultiple</c> con temporadas, documentos y convocatoria; y las pruebas con su
/// torneo en <c>OUTER APPLY</c>), todo con parámetros.
/// <para>
/// No se usan (el ASP las consultaba sin pintarlas): <c>sql_noticias</c>, <c>sql_fed</c>, las columnas
/// <c>Archivo</c>, <c>ArchivosLicitacion</c> y <c>CalendarioLicitacion</c> de <c>SFEP_Calendario_Archivo</c> y
/// <c>SFEP_Calendario_Categorias.Categoria</c>.
/// </para>
/// <para>
/// <c>CalendarioFT.asp</c> (calendarios de las federaciones territoriales) es el mismo fichero con <c>fed</c> elegido
/// en un desplegable (<c>sql_fed</c>): reutiliza aquí las mismas consultas con otra <c>@Federacion</c>. Diferencias
/// del ASP que se corrigen: ofrecía las temporadas de la FEP (<c>sql_ano</c> con <c>Federacion = 0</c>) en lugar de las
/// de la federación, abría por defecto la más reciente de la FEP (2027) aunque la federación no tuviera pruebas, y
/// dejaba fuera del desplegable a Castilla-La Mancha (<c>IdFederacion BETWEEN 1 AND 21</c>; es la 22).
/// </para>
/// </summary>
public sealed class CalendarioCompeticionRepository(
    IDbConnectionFactory connectionFactory,
    IMediaUrlBuilder mediaUrl,
    IOptions<MediaOptions> media) : ICalendarioCompeticionRepository
{
    // sql_ano: temporadas con alguna prueba visible de la FEP posteriores a 2014. sql_calen: documento
    // "Calendario" no borrado de cada temporada (el ASP cogía la primera fila sin orden; aquí la más reciente).
    // sql_archivo: tabla de configuración de una fila; HabitaSolicitud = 'S' muestra el banner de la convocatoria.
    // SUPUESTO: [Año] y Ano pueden ser texto (el ASP los comparaba entre comillas): TRY_CAST a int.
    private const string SqlTemporadas = """
        SELECT DISTINCT TRY_CAST(c.[Año] AS int) AS Anio
        FROM SFEP_Calendario AS c
        WHERE c.Federacion = @Federacion
          AND c.Visible = 'S'
          AND TRY_CAST(c.[Año] AS int) > @AnioDesde
        ORDER BY Anio DESC;

        SELECT d.Anio, d.Archivo, d.FechaAct
        FROM (SELECT TRY_CAST(cd.Ano AS int) AS Anio, cd.Archivo, cd.FechaAct,
                     ROW_NUMBER() OVER (PARTITION BY cd.Ano ORDER BY cd.FechaAct DESC) AS Fila
              FROM SFEP_Calendario_Documentos AS cd
              WHERE cd.Fed = @Federacion
                AND cd.Tipodoc = 'Calendario'
                AND cd.Borrado = 'N') AS d
        WHERE d.Fila = 1 AND d.Anio IS NOT NULL;
        """;

    private const string SqlCabecera = SqlTemporadas + """

        SELECT TOP (1) a.HabitaSolicitud
        FROM SFEP_Calendario_Archivo AS a;
        """;

    // CalendarioFT.asp: la federación (sql_fed solo para el desplegable) y, después, sus temporadas y documentos
    // (el ASP no mostraba documentos con fed <> 0; SUPUESTO: si la gestión los sube con Fed = {id}, se publican).
    private const string SqlCabeceraTerritorial = """
        SELECT f.IdFederacion AS Id, f.Abreviatura
        FROM SFEP_Federaciones AS f
        WHERE f.IdFederacion = @Federacion;

        """ + SqlTemporadas;

    // sql_fed (ids 1–22: las 22 federaciones, DECISIÓN FEP; el ASP 1–21) y, en la misma ida, las temporadas con alguna prueba visible
    // de cada federación (el ASP no las consultaba: ofrecía las de la FEP).
    private const string SqlFederaciones = """
        SELECT f.IdFederacion AS Id, f.Abreviatura
        FROM SFEP_Federaciones AS f
        WHERE f.IdFederacion BETWEEN @Desde AND @Hasta
        ORDER BY f.Abreviatura, f.IdFederacion;

        SELECT DISTINCT c.Federacion AS Id, TRY_CAST(c.[Año] AS int) AS Anio
        FROM SFEP_Calendario AS c
        WHERE c.Federacion BETWEEN @Desde AND @Hasta
          AND c.Visible = 'S'
          AND TRY_CAST(c.[Año] AS int) > @AnioDesde;
        """;

    // sql_ene … sql_dic + FechaIni IS NULL: mismas condiciones (Año, Federacion, Visible = 'S', INNER JOIN con la
    // categoría) en una sola consulta, por fecha de inicio y las que no tienen fecha al final. Columnas: las que
    // pintaba el ASP (FechaIni, FechaFin, Campeonato, TextoCategoria, Sede) y las que decidían el enlace (IdPrueba,
    // Link). El torneo enlazado (OUTER APPLY, no borrado) aporta lo que Torneos_Previo.asp usaba para elegir la
    // ficha y LinkCuadros (como en el listado de campeonatos).
    private const string SqlPruebas = """
        SELECT TRY_CAST(c.[Año] AS int) AS Anio, c.FechaIni, c.FechaFin, c.Campeonato, cat.TextoCategoria, c.Sede,
               c.IdPrueba, c.Link,
               t.IdPrueba AS IdTorneo, t.ModalidadTorneo, t.TipoTorneo, t.TorneoPadre, t.Liga, t.Empresas, t.LinkCuadros
        FROM SFEP_Calendario AS c
        INNER JOIN SFEP_Calendario_Categorias AS cat ON cat.IdCategoria = c.Categoria
        OUTER APPLY (SELECT TOP (1) tr.IdPrueba, tr.ModalidadTorneo, tr.TipoTorneo, tr.TorneoPadre, tr.Liga,
                            tr.Empresas, tr.LinkCuadros
                     FROM SFEP_Torneos AS tr
                     WHERE c.IdPrueba <> 0
                       AND tr.IdPrueba = c.IdPrueba
                       AND tr.Borrado = 'N') AS t
        WHERE c.[Año] = @Anio
          AND c.Federacion = @Federacion
          AND c.Visible = 'S'
        ORDER BY CASE WHEN c.FechaIni IS NULL THEN 1 ELSE 0 END, c.FechaIni, c.FechaFin, c.Campeonato;
        """;

    public async Task<DatosCalendarioCompeticionDto> BuscarAsync(int? anio, int anioActual, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        int[] anios;
        Dictionary<int, FilaDocumento> documentos;
        bool licitacionAbierta;
        using (var lector = await conexion.QueryMultipleAsync(new CommandDefinition(
            SqlCabecera,
            new { Federacion = ReglasCalendario.IdFederacionFep, AnioDesde = ReglasCalendario.AnioPublicadoDesde },
            cancellationToken: cancellationToken)))
        {
            anios = (await lector.ReadAsync<int?>()).OfType<int>().ToArray();
            documentos = (await lector.ReadAsync<FilaDocumento>())
                .GroupBy(d => d.Anio)
                .ToDictionary(g => g.Key, g => g.First());
            licitacionAbierta = string.Equals((await lector.ReadFirstOrDefaultAsync<string?>())?.Trim(), "S", StringComparison.OrdinalIgnoreCase);
        }

        var temporadas = anios.Select(a => ATemporada(a, documentos.GetValueOrDefault(a))).ToArray();

        var temporada = anio ?? ReglasCalendario.TemporadaPorDefecto(anios, anioActual);
        var items = await PruebasAsync(conexion, ReglasCalendario.IdFederacionFep, temporada, cancellationToken);
        return new DatosCalendarioCompeticionDto(temporada, temporadas, licitacionAbierta, items);
    }

    public async Task<IReadOnlyList<DatosFederacionCalendarioDto>> ListarFederacionesAsync(CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var lector = await conexion.QueryMultipleAsync(new CommandDefinition(
            SqlFederaciones,
            new
            {
                Desde = ReglasCalendario.IdFederacionTerritorialMinimo,
                Hasta = ReglasCalendario.IdFederacionTerritorialMaximo,
                AnioDesde = ReglasCalendario.AnioPublicadoDesdeTerritorial,
            },
            cancellationToken: cancellationToken));

        var federaciones = (await lector.ReadAsync<FilaFederacion>()).ToArray();
        var temporadas = (await lector.ReadAsync<FilaTemporadaFederacion>())
            .Where(t => t.Anio is not null)
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Anio!.Value).Distinct().OrderDescending().ToArray());

        return federaciones
            .Select(f => new DatosFederacionCalendarioDto(f.Id, Texto.Limpio(f.Abreviatura), temporadas.GetValueOrDefault(f.Id) ?? []))
            .ToArray();
    }

    public async Task<DatosCalendarioTerritorialDto?> BuscarFederacionAsync(int federacion, int? anio, int anioActual, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        FilaFederacion? fila;
        int[] anios;
        Dictionary<int, FilaDocumento> documentos;
        using (var lector = await conexion.QueryMultipleAsync(new CommandDefinition(
            SqlCabeceraTerritorial,
            new { Federacion = federacion, AnioDesde = ReglasCalendario.AnioPublicadoDesdeTerritorial },
            cancellationToken: cancellationToken)))
        {
            fila = await lector.ReadFirstOrDefaultAsync<FilaFederacion>();
            anios = (await lector.ReadAsync<int?>()).OfType<int>().ToArray();
            documentos = (await lector.ReadAsync<FilaDocumento>())
                .GroupBy(d => d.Anio)
                .ToDictionary(g => g.Key, g => g.First());
        }

        if (fila is null)
        {
            return null;
        }

        var temporadas = anios.Select(a => ATemporada(a, documentos.GetValueOrDefault(a))).ToArray();
        var datosFederacion = new DatosFederacionCalendarioDto(fila.Id, Texto.Limpio(fila.Abreviatura), anios);

        var temporada = anio ?? ReglasCalendario.TemporadaPorDefecto(anios, anioActual);
        var items = await PruebasAsync(conexion, federacion, temporada, cancellationToken);
        return new DatosCalendarioTerritorialDto(datosFederacion, temporada, temporadas, items);
    }

    /// <summary>Pruebas visibles de la federación en la temporada (ninguna si no hay temporada).</summary>
    private async Task<PruebaCalendarioCompeticionDto[]> PruebasAsync(
        System.Data.Common.DbConnection conexion, int federacion, int? temporada, CancellationToken cancellationToken)
    {
        if (temporada is null)
        {
            return [];
        }

        var filas = await conexion.QueryAsync<FilaPrueba>(new CommandDefinition(
            SqlPruebas,
            new { Anio = temporada.Value, Federacion = federacion },
            cancellationToken: cancellationToken));

        return filas.Select(f => AItem(f, temporada.Value)).ToArray();
    }

    private TemporadaCalendarioDto ATemporada(int anio, FilaDocumento? documento)
    {
        if (documento is null)
        {
            return new TemporadaCalendarioDto(anio, null, null, null);
        }

        // El ASP enlazaba "refs/docs/" + Archivo aunque Archivo estuviera vacío (enlace a la carpeta: 403).
        var nombre = Texto.Limpio(documento.Archivo);
        var archivo = nombre is null ? null : $"docs/{nombre}";
        return new TemporadaCalendarioDto(
            anio,
            documento.FechaAct is { } f ? DateOnly.FromDateTime(f) : null,
            archivo,
            mediaUrl.Construir(archivo));
    }

    private PruebaCalendarioCompeticionDto AItem(FilaPrueba f, int temporada)
    {
        string? enlace = null;
        var nuevaVentana = false;
        if (ReglasCalendario.Enlazable(f.IdPrueba, f.Link) && f.IdTorneo is { } idTorneo)
        {
            // Como el listado de campeonatos: los cuadros externos (LinkCuadros) o la ficha de Torneos_Previo.asp.
            var cuadros = ReglasCampeonatos.EnlaceCuadros(f.LinkCuadros);
            enlace = cuadros ?? AUrl(ReglasCampeonatos.RutaFicha(idTorneo, f.ModalidadTorneo, f.TipoTorneo, f.TorneoPadre, f.Liga, f.Empresas));
            nuevaVentana = cuadros is not null;
        }

        return new PruebaCalendarioCompeticionDto(
            f.Anio ?? temporada,
            f.FechaIni is { } ini ? DateOnly.FromDateTime(ini) : null,
            f.FechaFin is { } fin ? DateOnly.FromDateTime(fin) : null,
            ReglasCalendario.FechaPorConfirmar(f.FechaIni, f.FechaFin),
            Texto.Requerido(f.Campeonato),
            Texto.Limpio(f.TextoCategoria),
            Texto.NombrePropio(f.Sede),
            f.IdTorneo,
            enlace,
            nuevaVentana);
    }

    /// <summary>Rutas relativas → web actual; las absolutas (circuito de menores) se respetan.</summary>
    private string AUrl(string ruta)
        => Uri.TryCreate(ruta, UriKind.Absolute, out var absoluta) ? absoluta.AbsoluteUri : WebActual.Url(media.Value, ruta);

    // SUPUESTO: IdFederacion entero (el ASP lo comparaba con cint) y Abreviatura texto; SFEP_Calendario.Federacion entero.
    private sealed class FilaFederacion
    {
        public int Id { get; set; }
        public string? Abreviatura { get; set; }
    }

    private sealed class FilaTemporadaFederacion
    {
        public int Id { get; set; }
        public int? Anio { get; set; }
    }

    // SUPUESTO: Archivo varchar; FechaAct datetime (el ASP hacía formatdatetime).
    private sealed class FilaDocumento
    {
        public int Anio { get; set; }
        public string? Archivo { get; set; }
        public DateTime? FechaAct { get; set; }
    }

    // SUPUESTO: FechaIni/FechaFin datetime (el ASP usaba day()/month()); IdPrueba entero (comparado con 0 sin
    // comillas); Campeonato, Sede, Link y TextoCategoria texto; columnas de SFEP_Torneos como en CampeonatosRepository.
    private sealed class FilaPrueba
    {
        public int? Anio { get; set; }
        public DateTime? FechaIni { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? Campeonato { get; set; }
        public string? TextoCategoria { get; set; }
        public string? Sede { get; set; }
        public int? IdPrueba { get; set; }
        public string? Link { get; set; }
        public int? IdTorneo { get; set; }
        public string? ModalidadTorneo { get; set; }
        public string? TipoTorneo { get; set; }
        public int? TorneoPadre { get; set; }
        public string? Liga { get; set; }
        public string? Empresas { get; set; }
        public string? LinkCuadros { get; set; }
    }
}
