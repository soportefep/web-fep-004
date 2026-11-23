using System.Globalization;
using Dapper;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Competicion;

/// <summary>Ficha pública de un torneo de pádel en silla de ruedas por parejas.</summary>
public interface ICampeonatoSillaRepository
{
    /// <summary>
    /// Ficha del torneo <paramref name="id"/>. <paramref name="ahora"/> (hora local del servidor) decide si la
    /// inscripción sigue abierta. Si el torneo no existe, está borrado o no es visible, la consulta vuelve vacía; si
    /// existe pero tiene otra ficha (no es de silla de ruedas por parejas), trae su URL en la web actual.
    /// </summary>
    Task<ConsultaCampeonatoSilla> ObtenerAsync(int id, DateTime ahora, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Camp_Silla.asp</c> (sql_torneo, sql_cat, sql_noti, sql_inscritos, sql_notirel, sql_catM/F/X, sql_cuota,
/// sql_cuadrosM/F/X, sql_texto —una consulta por cuadro mixto—, sql_coach, sql_Numcoach, sql_arbitraje, sql_honor y
/// sql_club). El ASP concatenaba <c>IdTorneo</c> en ~25 consultas (las de nombres de cuadro con
/// <c>Request("idtorneo")</c> sin pasar por <c>limpiar()</c>), hacía <c>SELECT *</c> de <c>SFEP_Torneos</c> dos veces y
/// lanzaba otras consultas que no usaba (<c>sql_noticias</c>, <c>sql_adjuntos</c>, <c>sql_cuadros</c>, <c>sql_edad</c>
/// solo para el desplegable de años del formulario).
/// <para>
/// Aquí: <c>@Id</c> siempre como parámetro, columnas explícitas, una consulta para el torneo y un solo lote para el
/// resto (los nombres de cuadro se leen todos de una vez y se resuelven en memoria: sin N+1). La lista de columnas
/// <c>Ver…</c> sale de las constantes de <see cref="ReglasCampeonatoSilla"/>, nunca de la petición.
/// </para>
/// </summary>
public sealed class CampeonatoSillaRepository(
    IDbConnectionFactory connectionFactory,
    IMediaUrlBuilder media,
    IOptions<MediaOptions> opcionesMedia) : ICampeonatoSillaRepository
{
    // sql_torneo + sql_club (OUTER APPLY: TOP (1) por si IdClub se repitiera). Borrado = 'N' es nuevo (ver análisis).
    private const string SqlTorneo = """
        SELECT t.IdPrueba, t.[Año] AS Anio, t.Torneo, t.TipoTorneo, t.ModalidadTorneo, t.Circuito,
               t.Visible, t.FechaInicio, t.FechaFin, t.Ciudad, t.Club2,
               t.FechaInscripcion, t.InscripcionSolo, t.InfoInscripcion, t.InfFormulario,
               t.Notificaciones, t.NotaPrensa, t.VisibleNotaPrensa,
               t.InfoAdicional, t.VisibleCartel, t.FotoCartel, t.VerCuadros,
               t.TorneoPadre, t.Liga, t.Empresas,
               c.Club, c.Web AS ClubWeb,
               CAST(c.Latitud AS varchar(40)) AS ClubLatitud, CAST(c.Longitud AS varchar(40)) AS ClubLongitud
        FROM SFEP_Torneos AS t
        OUTER APPLY (SELECT TOP (1) cl.Club, cl.Web, cl.Latitud, cl.Longitud
                     FROM SFEP_Clubes AS cl
                     WHERE CAST(cl.IdClub AS varchar(20)) = CAST(t.Club AS varchar(20))) AS c
        WHERE t.IdPrueba = @Id
          AND t.Borrado = 'N';
        """;

    // {0}: columnas Ver… de SFEP_Torneos_Desglose (constantes de ReglasCampeonatoSilla).
    private const string SqlDetalle = """
        -- Géneros con categorías visibles (sql_cat)
        SELECT DISTINCT d.Genero
        FROM SFEP_Torneos_Desglose AS d
        WHERE d.IdPrueba = @Id AND d.Visible = 'S';

        -- Categorías de edad por género (sql_catM, sql_catF, sql_catX)
        SELECT d.Genero, d.Tipo
        FROM SFEP_CategoriasMenores AS cm
        INNER JOIN SFEP_Torneos_Desglose AS d ON cm.Texto = d.Tipo
        WHERE d.IdPrueba = @Id AND d.Genero IN ('M', 'F', 'X') AND d.Visible = 'S' AND d.Borrado = 'N'
        ORDER BY d.Genero, cm.AnoInicial DESC;

        -- Cuotas (sql_cuota; el ASP solo pintaba la primera, la mayor, y daba error 500 si no había ninguna)
        SELECT DISTINCT TRY_CONVERT(decimal(10, 2), d.Cuota) AS Importe
        FROM SFEP_Torneos_Desglose AS d
        WHERE d.IdPrueba = @Id AND d.Visible = 'S' AND d.Borrado = 'N'
        ORDER BY Importe DESC;

        -- Parejas inscritas por género (sql_inscritos)
        SELECT i.Genero, COUNT(*) AS Parejas
        FROM SFEP_Inscripciones AS i
        WHERE i.IdPrueba = @Id AND i.Borrado = 'N'
        GROUP BY i.Genero;

        -- Entrenadores con licencia de técnico tramitada en la temporada (sql_Numcoach: el GROUP BY del ASP
        -- dejaba una fila por entrenador distinto). El año se compara como texto, igual que el ASP ('2026').
        SELECT COUNT(DISTINCT i.Coach)
        FROM SFEP_Inscripciones AS i
        INNER JOIN SFEP_Licencia AS l ON i.Coach = l.IdLicencia
        WHERE i.IdPrueba = @Id AND i.Borrado = 'N' AND i.Coach <> 0
          AND l.[Año] = @AnioLicencia AND l.Tipo = 'T' AND l.Tramitado = 'S';

        -- Adjuntos de las notificaciones (sql_noti)
        SELECT a.IdArchivo, a.Archivo, a.Link, a.Icono
        FROM SFEP_Torneos_Adjuntos AS a
        WHERE a.IdTorneo = @Id
        ORDER BY a.IdArchivo DESC;

        -- Noticias relacionadas (sql_notirel)
        SELECT n.IdNoticia, n.Titulo, n.Foto, n.Link, CAST(n.TipoNoticia AS varchar(10)) AS TipoNoticia,
               n.Fecha, n.DescriCorta
        FROM SFEP_Torneos_Noticias AS tn
        INNER JOIN SFEP_Noticias AS n ON tn.IdNoticia = n.IdNoticia
        WHERE tn.IdTorneo = @Id AND n.Site = 'FEP' AND n.Visible = 'S' AND n.Borrado = 'N'
        ORDER BY n.Fecha DESC;

        -- Marcadores en directo (sql_arbitraje)
        SELECT a.Aleatorio
        FROM SFEP_Torneos_Arbitraje AS a
        WHERE a.IdPrueba = @Id AND a.Borrado = 'N' AND a.Visible = 'S'
        ORDER BY a.IdPartido;

        -- Cuadros por género (sql_cuadrosM/F/X: el ASP usaba la primera fila de cada género, sin ORDER BY)
        SELECT d.Genero, d.Tipo, d.VerCuadros, CAST(d.Cat2 AS varchar(20)) AS Cat2, {0}
        FROM SFEP_Torneos_Desglose AS d
        WHERE d.IdPrueba = @Id AND d.Genero IN ('M', 'F', 'X')
        ORDER BY CASE d.Genero WHEN 'M' THEN 1 WHEN 'F' THEN 2 ELSE 3 END, d.Tipo;

        -- Nombres personalizados de los cuadros (sql_texto, una consulta por cuadro mixto en el ASP)
        SELECT n.Nivel, n.Categoria, n.Genero, n.Cuadro, n.Nombre
        FROM SFEP_Torneos_Cuadros_Nombres AS n
        WHERE n.IdPrueba = @Id;

        -- Cuadro de honor (sql_honor: ranking de silla de ruedas; licencia de silla de SFEP_Fichas)
        SELECT CAST(rt.Ronda AS nvarchar(100)) AS Puesto, TRY_CONVERT(int, rt.IdJugador) AS Licencia,
               f.NOMBRECOMPLETO AS Nombre, TRY_CONVERT(decimal(10, 2), rt.Puntos) AS Puntos
        FROM SFEP_Fichas AS f
        INNER JOIN SFEP_RankingTorneos AS rt ON f.LicenciaSilla = rt.IdJugador
        WHERE rt.IdPrueba = @Id AND rt.Ranking = @Ranking
        ORDER BY rt.Puntos DESC;
        """;

    private static readonly string SqlDetalleConColumnas = string.Format(
        CultureInfo.InvariantCulture,
        SqlDetalle,
        string.Join(", ", ReglasCampeonatoSilla.ColumnasVisibilidad.Select(c => $"d.{c}")));

    public async Task<ConsultaCampeonatoSilla> ObtenerAsync(int id, DateTime ahora, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var t = await conexion.QuerySingleOrDefaultAsync<FilaTorneo>(
            new CommandDefinition(SqlTorneo, new { Id = id }, cancellationToken: cancellationToken));

        // El ASP daba error 500 si el torneo no existía y no comprobaba Visible (ver análisis).
        if (t is null || string.Equals(t.Visible?.Trim(), "N", StringComparison.OrdinalIgnoreCase))
        {
            return new ConsultaCampeonatoSilla(null, null);
        }

        // Redirecciones del ASP y reparto de Torneos_Previo.asp: aquí solo los de silla de ruedas por parejas.
        var otra = ReglasCampeonatoSilla.RutaOtraFicha(t.IdPrueba, t.Circuito, t.ModalidadTorneo, t.TipoTorneo, t.TorneoPadre, t.Liga, t.Empresas);
        if (otra is not null)
        {
            return new ConsultaCampeonatoSilla(null, AUrl(otra));
        }

        var parametros = new
        {
            Id = id,
            AnioLicencia = t.Anio.ToString(CultureInfo.InvariantCulture),
            Ranking = ReglasCampeonatoSilla.RankingSilla,
        };
        using var lector = await conexion.QueryMultipleAsync(
            new CommandDefinition(SqlDetalleConColumnas, parametros, cancellationToken: cancellationToken));
        var generosVisibles = (await lector.ReadAsync<string?>()).ToArray();
        var categorias = (await lector.ReadAsync<FilaCategoria>()).ToArray();
        var cuotas = (await lector.ReadAsync<decimal?>()).OfType<decimal>().ToArray();
        var inscritos = (await lector.ReadAsync<FilaInscritos>()).ToArray();
        var entrenadores = await lector.ReadSingleAsync<int>();
        var adjuntos = (await lector.ReadAsync<FilaAdjunto>()).ToArray();
        var noticias = (await lector.ReadAsync<FilaNoticia>()).ToArray();
        var marcadores = (await lector.ReadAsync<string?>()).ToArray();
        IEnumerable<object> filasDesglose = await lector.ReadAsync();
        var desglose = filasDesglose.Select(Fila).ToArray();
        var nombres = (await lector.ReadAsync<FilaNombre>()).ToArray();
        var honor = (await lector.ReadAsync<FilaHonor>()).ToArray();

        return new ConsultaCampeonatoSilla(
            Componer(t, ahora, generosVisibles, categorias, cuotas, inscritos, entrenadores, adjuntos, noticias, marcadores,
                desglose, nombres, honor),
            null);
    }

    private CampeonatoSillaDto Componer(
        FilaTorneo t,
        DateTime ahora,
        IReadOnlyList<string?> generosVisibles,
        IReadOnlyList<FilaCategoria> categorias,
        IReadOnlyList<decimal> cuotas,
        IReadOnlyList<FilaInscritos> inscritos,
        int entrenadores,
        IReadOnlyList<FilaAdjunto> adjuntos,
        IReadOnlyList<FilaNoticia> noticias,
        IReadOnlyList<string?> marcadores,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> desglose,
        IReadOnlyList<FilaNombre> nombres,
        IReadOnlyList<FilaHonor> honor)
    {
        var id = t.IdPrueba;
        var (categoria, seleccion) = ReglasCampeonatos.CategoriaDesdeBd(t.ModalidadTorneo);

        // Géneros visibles (masc/fem/mix del ASP) en el orden M, F, X, con sus categorías de edad (CatM/F/X).
        var letras = generosVisibles.Select(g => g?.Trim().ToUpperInvariant()).ToHashSet();
        var generos = ReglasCampeonatoEquipos.LetrasGenero
            .Where(letras.Contains)
            .Select(g => new GeneroCampeonatoDto(
                ReglasCampeonatoEquipos.Genero(g)!,
                categorias.Where(c => string.Equals(c.Genero?.Trim(), g, StringComparison.OrdinalIgnoreCase))
                    .Select(c => Texto.Limpio(c.Tipo))
                    .OfType<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

        // Listado de inscritos: el ASP lo pintaba si el total era distinto de 0, con una columna por género visible
        // y otra de entrenadores (ahora solo si hay alguno: el ASP pintaba "0 Coach").
        var porGenero = inscritos
            .GroupBy(i => i.Genero?.Trim().ToUpperInvariant() ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Parejas));
        var hayInscritos = porGenero.Values.Sum() != 0;
        IReadOnlyList<InscritosGeneroDto> inscritosParejas = hayInscritos
            ? ReglasCampeonatoEquipos.LetrasGenero
                .Where(letras.Contains)
                .Select(g => new InscritosGeneroDto(ReglasCampeonatoEquipos.Genero(g)!, porGenero.GetValueOrDefault(g),
                    WebActual.Url(opcionesMedia.Value, $"Camp_Parejas_Inscritos.asp?tipo={g}&id={id}")))
                .ToArray()
            : [];
        var coaches = hayInscritos && entrenadores > 0
            ? new EntrenadoresInscritosDto(entrenadores, WebActual.Url(opcionesMedia.Value, $"Camp_Parejas_Inscritos.asp?tipo=C&id={id}"))
            : null;

        var cuadrosPublicados = ReglasCampeonatoSilla.CuadrosPublicados(t.VerCuadros, t.ModalidadTorneo);
        IReadOnlyList<GrupoCuadrosDto> cuadros = cuadrosPublicados ? Cuadros(id, desglose, nombres) : [];

        var inscripcion = new InscripcionSillaDto(
            ReglasCampeonatoEquipos.InscripcionAbierta(t.FechaInscripcion, ahora),
            t.FechaInscripcion,
            ReglasCampeonatoSilla.InscripcionIndividual(t.InscripcionSolo),
            Html(t.InfoInscripcion),
            Html(t.InfFormulario),
            WebActual.Url(opcionesMedia.Value, $"Camp_Silla.asp?IdTorneo={id}"));

        // Información adicional y cartel, solo con VisibleCartel = 'S' (como el ASP).
        var visibleCartel = ReglasCampeonatoEquipos.EsSi(t.VisibleCartel);
        var cartel = visibleCartel ? Texto.Limpio(t.FotoCartel) : null;

        return new CampeonatoSillaDto(
            id,
            t.Anio,
            CampeonatoEquiposRepository.Espacios(t.Torneo) ?? string.Empty,
            categoria,
            seleccion,
            t.FechaInicio is { } ini ? DateOnly.FromDateTime(ini) : null,
            t.FechaFin is { } fin ? DateOnly.FromDateTime(fin) : null,
            Texto.Limpio(t.Ciudad),
            Sedes(t),
            cuotas,
            generos,
            ReglasCampeonatoEquipos.EsSi(t.VisibleNotaPrensa) ? Html(t.NotaPrensa) : null,
            marcadores.Select(Texto.Limpio).OfType<string>()
                .Select(m => new MarcadorDirectoDto(m, WebActual.Url(opcionesMedia.Value, $"ArbitrajeCtoFep.asp?Id={Uri.EscapeDataString(m)}")))
                .ToArray(),
            cuadrosPublicados,
            cuadros,
            honor.Select(Honor).OfType<PuestoHonorDto>().ToArray(),
            Html(t.Notificaciones),
            adjuntos.Select(a => Adjunto(t.Anio, a)).OfType<AdjuntoCampeonatoDto>().ToArray(),
            inscritosParejas,
            coaches,
            inscripcion,
            visibleCartel ? Html(t.InfoAdicional) : null,
            cartel,
            cartel is null ? null : UrlTorneos(t.Anio, cartel),
            noticias.Select(Noticia).ToArray(),
            WebActual.Url(opcionesMedia.Value, $"Camp_Silla.asp?IdTorneo={id}"),
            ahora);
    }

    /// <summary>Cuadros por género con los nombres personalizados de la BD (solo los buscaba el bloque mixto).</summary>
    private IReadOnlyList<GrupoCuadrosDto> Cuadros(
        int id,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> desglose,
        IReadOnlyList<FilaNombre> nombres)
    {
        // Clave: nivel|categoría|género|cuadro, sin distinguir mayúsculas (como la intercalación de SQL Server).
        // Si se repite, gana la primera fila (como .eof del ASP).
        var porClave = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in nombres)
        {
            var nombre = CampeonatoEquiposRepository.Espacios(n.Nombre);
            if (nombre is not null)
            {
                porClave.TryAdd(Clave(n.Nivel, n.Categoria, n.Genero, n.Cuadro), nombre);
            }
        }

        var grupos = new List<GrupoCuadrosDto>();
        foreach (var fila in desglose)
        {
            var genero = Convert.ToString(fila.GetValueOrDefault("Genero"), CultureInfo.InvariantCulture)?.Trim().ToUpperInvariant() ?? string.Empty;
            var clave = ReglasCampeonatoEquipos.Genero(genero);
            if (clave is null || !ReglasCampeonatoEquipos.EsSi(fila.GetValueOrDefault("VerCuadros")))
            {
                continue;
            }

            var categoria = Texto.Limpio(Convert.ToString(fila.GetValueOrDefault("Tipo"), CultureInfo.InvariantCulture)) ?? string.Empty;
            var cuadros = ReglasCampeonatoSilla.Cuadros(
                genero,
                categoria,
                fila,
                k => porClave.GetValueOrDefault(Clave(k.Nivel, k.Categoria, k.Genero, k.Cuadro)),
                (nivel, cuadro, cat) => WebActual.Url(opcionesMedia.Value,
                    $"Previo.asp?IdTorneo={id}&Genero={genero}&Nivel={nivel}&Cat={Uri.EscapeDataString(cat)}&Cuadro={cuadro}"));

            if (cuadros.Count > 0)
            {
                grupos.Add(new GrupoCuadrosDto(clave, categoria, cuadros));
            }
        }

        return grupos;
    }

    private static string Clave(int nivel, string? categoria, string? genero, string? cuadro)
        => $"{nivel}|{categoria?.Trim()}|{genero?.Trim()}|{cuadro?.Trim()}";

    private PuestoHonorDto? Honor(FilaHonor h)
    {
        var nombre = CampeonatoEquiposRepository.Espacios(h.Nombre);
        if (h.Licencia is not { } licencia || nombre is null)
        {
            return null;
        }

        return new PuestoHonorDto(
            CampeonatoEquiposRepository.Espacios(h.Puesto),
            licencia,
            nombre,
            h.Puntos ?? 0m,
            WebActual.Url(opcionesMedia.Value, $"Silla-Ruedas-Est?id={ReglasCampeonatoSilla.PrefijoLicencia}{licencia}"));
    }

    private IReadOnlyList<SedeCampeonatoDto> Sedes(FilaTorneo t)
    {
        var sedes = new List<SedeCampeonatoDto>();
        // Club sin fila en SFEP_Clubes: el ASP pintaba "--" (y "-- y Club2"); aquí no se muestra.
        if (CampeonatoEquiposRepository.Espacios(t.Club) is { } club && club != "--")
        {
            var latitud = ReglasCampeonatoEquipos.Coordenada(t.ClubLatitud, 90);
            var longitud = ReglasCampeonatoEquipos.Coordenada(t.ClubLongitud, 180);
            var conMapa = latitud is not null && longitud is not null && !(latitud == 0 && longitud == 0);
            sedes.Add(new SedeCampeonatoDto(club, CampeonatoEquiposRepository.WebClub(t.ClubWeb), conMapa ? latitud : null, conMapa ? longitud : null));
        }

        // Club2: texto libre ("X y Club2" en el ASP).
        if (CampeonatoEquiposRepository.Espacios(t.Club2) is { } club2 && !sedes.Any(s => string.Equals(s.Nombre, club2, StringComparison.OrdinalIgnoreCase)))
        {
            sedes.Add(new SedeCampeonatoDto(club2, null, null, null));
        }

        return sedes;
    }

    private AdjuntoCampeonatoDto? Adjunto(int anio, FilaAdjunto a)
    {
        var archivo = Texto.Limpio(a.Archivo);
        var titulo = CampeonatoEquiposRepository.Espacios(a.Link) ?? archivo;
        if (titulo is null)
        {
            return null;
        }

        return new AdjuntoCampeonatoDto(titulo, ReglasCampeonatoEquipos.TipoAdjunto(a.Icono), archivo,
            archivo is null ? null : UrlTorneos(anio, archivo));
    }

    private NoticiaRelacionadaDto Noticia(FilaNoticia n)
    {
        var foto = Texto.Limpio(n.Foto);
        var externa = string.Equals(n.TipoNoticia?.Trim(), "1", StringComparison.Ordinal);
        return new NoticiaRelacionadaDto(
            n.IdNoticia,
            Texto.TituloNoticia(n.Titulo),
            n.Fecha,
            Html(n.DescriCorta),
            foto,
            foto is null ? null : media.Construir("Noticias", foto),
            Texto.Limpio(n.Link),
            externa ? "_blank" : null);
    }

    /// <summary>
    /// Ficheros del torneo (<c>http://padelfederacion.es/Paginas/Torneos/{año}/{archivo}</c> en el ASP): URL absoluta en
    /// la web actual con cada segmento codificado (los nombres llevan espacios, tildes y paréntesis).
    /// </summary>
    private string UrlTorneos(int anio, string archivo)
    {
        if (Uri.TryCreate(archivo, UriKind.Absolute, out var absoluta)
            && (absoluta.Scheme == Uri.UriSchemeHttp || absoluta.Scheme == Uri.UriSchemeHttps))
        {
            return absoluta.AbsoluteUri;
        }

        var partes = archivo.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p is not "." and not "..")
            .Select(p => Uri.EscapeDataString(p));
        return WebActual.Url(opcionesMedia.Value, $"Paginas/Torneos/{anio}/{string.Join('/', partes)}");
    }

    /// <summary>Rutas relativas → web actual; las absolutas (circuito de menores) se respetan.</summary>
    private string AUrl(string ruta)
        => Uri.TryCreate(ruta, UriKind.Absolute, out var absoluta) ? absoluta.AbsoluteUri : WebActual.Url(opcionesMedia.Value, ruta);

    /// <summary>HTML heredado: se devuelve tal cual (lo sanea el cliente) salvo que esté vacío.</summary>
    private static string? Html(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Fila dinámica de Dapper → diccionario sin distinguir mayúsculas (como <c>rs("Vercuadros")</c> en ASP).</summary>
    private static IReadOnlyDictionary<string, object?> Fila(object fila)
        => new Dictionary<string, object?>((IDictionary<string, object?>)fila, StringComparer.OrdinalIgnoreCase);

    // SUPUESTO (mismos tipos que en Camp_Equipos): IdPrueba, [Año], TorneoPadre, IdArchivo, IdNoticia, Nivel enteros;
    // fechas datetime; Ver…, Visible…, InscripcionSolo, Circuito, Genero, Tipo char/varchar rellenos de espacios; Cat2
    // numérico (se lee como texto); Latitud/Longitud texto o número; Cuota y Puntos numéricos o texto numérico
    // (TRY_CONVERT); SFEP_RankingTorneos.IdJugador = SFEP_Fichas.LicenciaSilla numérico; Ronda texto;
    // SFEP_Licencia.[Año] texto o número (el ASP lo comparaba con '2026').
    private sealed class FilaTorneo
    {
        public int IdPrueba { get; set; }
        public int Anio { get; set; }
        public string? Torneo { get; set; }
        public string? TipoTorneo { get; set; }
        public string? ModalidadTorneo { get; set; }
        public string? Circuito { get; set; }
        public string? Visible { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? Ciudad { get; set; }
        public string? Club2 { get; set; }
        public DateTime? FechaInscripcion { get; set; }
        public string? InscripcionSolo { get; set; }
        public string? InfoInscripcion { get; set; }
        public string? InfFormulario { get; set; }
        public string? Notificaciones { get; set; }
        public string? NotaPrensa { get; set; }
        public string? VisibleNotaPrensa { get; set; }
        public string? InfoAdicional { get; set; }
        public string? VisibleCartel { get; set; }
        public string? FotoCartel { get; set; }
        public string? VerCuadros { get; set; }
        public int? TorneoPadre { get; set; }
        public string? Liga { get; set; }
        public string? Empresas { get; set; }
        public string? Club { get; set; }
        public string? ClubWeb { get; set; }
        public string? ClubLatitud { get; set; }
        public string? ClubLongitud { get; set; }
    }

    private sealed class FilaCategoria
    {
        public string? Genero { get; set; }
        public string? Tipo { get; set; }
    }

    private sealed class FilaInscritos
    {
        public string? Genero { get; set; }
        public int Parejas { get; set; }
    }

    private sealed class FilaAdjunto
    {
        public int IdArchivo { get; set; }
        public string? Archivo { get; set; }
        public string? Link { get; set; }
        public string? Icono { get; set; }
    }

    private sealed class FilaNoticia
    {
        public int IdNoticia { get; set; }
        public string? Titulo { get; set; }
        public string? Foto { get; set; }
        public string? Link { get; set; }
        public string? TipoNoticia { get; set; }
        public DateTime? Fecha { get; set; }
        public string? DescriCorta { get; set; }
    }

    private sealed class FilaNombre
    {
        public int Nivel { get; set; }
        public string? Categoria { get; set; }
        public string? Genero { get; set; }
        public string? Cuadro { get; set; }
        public string? Nombre { get; set; }
    }

    private sealed class FilaHonor
    {
        public string? Puesto { get; set; }
        public int? Licencia { get; set; }
        public string? Nombre { get; set; }
        public decimal? Puntos { get; set; }
    }
}
