using Dapper;
using FEP.Api.Contracts.Micrositios;
using FEP.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace FEP.Api.Data.Repositories.Micrositios;

/// <summary>Micrositios (mini-CMS de la web pública).</summary>
public interface IMicrositiosRepository
{
    /// <summary>Cabecera y menú de un micrositio visible, o <c>null</c> si no existe, está borrado o no es visible.</summary>
    Task<MicrositioDto?> ObtenerAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Contenido de una opción visible de un micrositio visible, o <c>null</c> si el micrositio o la opción
    /// no existen, no son visibles o la opción no pertenece al micrositio.
    /// </summary>
    Task<OpcionMicrositioContenidoDto?> ObtenerOpcionAsync(int id, int idOpcion, CancellationToken cancellationToken);
}

/// <summary>
/// Origen: <c>Microsite.asp</c> (sql_microsite, sql_menus, sql_menu, sql_post, sql_noticiaarchivos) e
/// <c>include/BarraLateralMicrosite.inc</c> (menú). El ASP concatenaba <c>Request("id")</c> e
/// <c>IdOpcion</c> en cinco consultas (inyección SQL; error 500 con valores no numéricos o con un
/// micrositio sin opciones) y no filtraba <c>SFEP_Microsites.Borrado</c>. Aquí: SQL parametrizado,
/// columnas explícitas y una sola ida a la BD por petición (<c>QueryMultiple</c>, sin N+1).
/// <para>
/// Filtros fijos: micrositio <c>Visible = 'S'</c> (el ASP mostraba "El Microsite no está visible aún")
/// y <c>Borrado = 'N'</c> (añadido); opción <c>Borrado = 'N'</c> y <c>Visible = 'S'</c> (como el ASP).
/// Los posts y archivos no tienen filtro: el ASP no lo aplicaba y la gestión inserta los posts de menú
/// sin rellenar <c>Borrado</c>/<c>Visible</c>; los archivos se borran físicamente (<c>DELETE</c>).
/// </para>
/// </summary>
public sealed class MicrositiosRepository(IDbConnectionFactory connectionFactory, IOptions<MediaOptions> media)
    : IMicrositiosRepository
{
    /// <summary>Carpeta de la web actual donde la gestión sube los archivos de los micrositios.</summary>
    public const string CarpetaArchivos = "Paginas/Microsites/";

    private const string SqlMicrositio = """
        SELECT m.IdMicrosite, m.Microsite, m.FechaUlt
        FROM SFEP_Microsites AS m
        WHERE m.IdMicrosite = @Id
          AND m.Visible = 'S'
          AND m.Borrado = 'N';

        SELECT o.IdMenu, o.Menu, o.Tipo, o.Orden
        FROM SFEP_Microsites_Menus AS o
        WHERE o.IdMicrosite = @Id
          AND o.Borrado = 'N'
          AND o.Visible = 'S'
        ORDER BY o.Orden, o.IdMenu;
        """;

    // El ASP pintaba el primer post de la opción (sin ORDER BY) y los archivos de todos sus posts
    // (en la práctica hay un post por opción: la gestión lo crea junto con la opción).
    private const string SqlOpcion = """
        SELECT m.IdMicrosite, m.Microsite, o.IdMenu, o.Menu, o.Tipo
        FROM SFEP_Microsites AS m
        INNER JOIN SFEP_Microsites_Menus AS o ON o.IdMicrosite = m.IdMicrosite
        WHERE m.IdMicrosite = @Id
          AND m.Visible = 'S'
          AND m.Borrado = 'N'
          AND o.IdMenu = @IdOpcion
          AND o.Borrado = 'N'
          AND o.Visible = 'S';

        SELECT TOP (1) p.Subtitulo, p.Post
        FROM SFEP_Microsites_Posts AS p
        WHERE p.IdMicrosite = @Id
          AND p.IdMenu = @IdOpcion
        ORDER BY p.IdPost;

        SELECT a.Archivo, a.Link, a.Icono
        FROM SFEP_Microsites_Archivos AS a
        INNER JOIN SFEP_Microsites_Posts AS p ON p.IdPost = a.IdPost
        WHERE p.IdMicrosite = @Id
          AND p.IdMenu = @IdOpcion
        ORDER BY p.IdPost, a.IdArchivo;
        """;

    private readonly Uri _raiz = WebActual.Raiz(media.Value);

    public async Task<MicrositioDto?> ObtenerAsync(int id, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var lector = await conexion.QueryMultipleAsync(
            new CommandDefinition(SqlMicrositio, new { Id = id }, cancellationToken: cancellationToken));
        var micrositio = await lector.ReadSingleOrDefaultAsync<FilaMicrositio>();
        if (micrositio is null)
        {
            return null;
        }

        var opciones = (await lector.ReadAsync<FilaOpcion>())
            .Select(o => new OpcionMicrositioDto(o.IdMenu, Texto.Requerido(o.Menu), TipoOpcion(o.Tipo), o.Orden ?? 0))
            .ToArray();

        return new MicrositioDto(
            micrositio.IdMicrosite,
            Texto.Requerido(micrositio.Microsite),
            micrositio.FechaUlt,
            opciones.Length > 0 ? opciones[0].Id : (int?)null,
            opciones);
    }

    public async Task<OpcionMicrositioContenidoDto?> ObtenerOpcionAsync(int id, int idOpcion, CancellationToken cancellationToken)
    {
        await using var conexion = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var lector = await conexion.QueryMultipleAsync(
            new CommandDefinition(SqlOpcion, new { Id = id, IdOpcion = idOpcion }, cancellationToken: cancellationToken));
        var opcion = await lector.ReadSingleOrDefaultAsync<FilaCabeceraOpcion>();
        if (opcion is null)
        {
            return null;
        }

        var post = await lector.ReadSingleOrDefaultAsync<FilaPost>();
        var archivos = (await lector.ReadAsync<FilaArchivo>())
            .Select(a => new ArchivoMicrositioDto(
                Texto.Limpio(a.Archivo) ?? Texto.Requerido(a.Link),
                TipoArchivo(a.Icono),
                Texto.Limpio(a.Link),
                UrlArchivo(a.Link, _raiz)))
            .Where(a => a.Titulo.Length > 0)
            .ToArray();

        return new OpcionMicrositioContenidoDto(
            opcion.IdMenu,
            opcion.IdMicrosite,
            Texto.Requerido(opcion.Microsite),
            Texto.Requerido(opcion.Menu),
            Texto.Limpio(post?.Subtitulo),
            TipoOpcion(opcion.Tipo),
            post?.Post is { } html && !string.IsNullOrWhiteSpace(html) ? html : null,
            archivos);
    }

    /// <summary>
    /// <c>NORMAL</c> → <c>normal</c>; cualquier otro valor → <c>formulario</c> (el ASP hacía
    /// <c>if rtrim(Tipo)="NORMAL" … else</c> formulario de inscripción; la gestión solo ofrece NORMAL y FORMULARIO).
    /// </summary>
    internal static string TipoOpcion(string? tipo)
        => string.Equals(Texto.Limpio(tipo), "NORMAL", StringComparison.OrdinalIgnoreCase) ? "normal" : "formulario";

    /// <summary>Columna <c>Icono</c> normalizada; <c>--</c> o desconocido → <c>null</c>.</summary>
    internal static string? TipoArchivo(string? icono)
    {
        var tipo = Texto.Limpio(icono)?.ToLowerInvariant();
        return tipo is not null && ReglasMicrositios.TiposArchivo.Contains(tipo) ? tipo : null;
    }

    /// <summary>
    /// URL absoluta de un adjunto. El ASP anteponía siempre <c>Paginas/Microsites/</c>, también a los
    /// enlaces web (que la gestión guarda como URL completa), dejándolos rotos: aquí las URL http(s) se
    /// respetan. Las rutas relativas se codifican por segmento (espacios, tildes, "º") y se descartan
    /// <c>.</c>/<c>..</c>; otros esquemas (<c>javascript:</c>…) → <c>null</c>.
    /// </summary>
    internal static string? UrlArchivo(string? link, Uri raizWebActual)
    {
        var limpio = Texto.Limpio(link);
        if (limpio is null)
        {
            return null;
        }

        if (limpio.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            limpio = "https://" + limpio;
        }

        if (limpio.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || limpio.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(limpio, UriKind.Absolute, out var absoluta) ? absoluta.AbsoluteUri : null;
        }

        // Un ":" antes de la primera "/" indica otro esquema (javascript:, mailto:, file:…).
        var dosPuntos = limpio.IndexOf(':');
        var barra = limpio.IndexOfAny(['/', '\\']);
        if (dosPuntos >= 0 && (barra < 0 || dosPuntos < barra))
        {
            return null;
        }

        var segmentos = limpio
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s is not "." and not "..")
            .Select(s => Uri.EscapeDataString(s))
            .ToArray();

        return segmentos.Length == 0
            ? null
            : new Uri(raizWebActual, CarpetaArchivos + string.Join('/', segmentos)).AbsoluteUri;
    }

    // SUPUESTO: IdMicrosite, IdMenu, IdPost, IdArchivo y Orden enteros (el ASP los concatenaba sin
    // comillas y hacía cint); Tipo/Icono char/varchar (se recortan como hacía rtrim); FechaUlt datetime
    // (Microsites.asp la formatea con formatdatetime); Post texto largo (ntext/nvarchar(max)).
    private sealed class FilaMicrositio
    {
        public int IdMicrosite { get; set; }
        public string? Microsite { get; set; }
        public DateTime? FechaUlt { get; set; }
    }

    private sealed class FilaOpcion
    {
        public int IdMenu { get; set; }
        public string? Menu { get; set; }
        public string? Tipo { get; set; }
        public int? Orden { get; set; }
    }

    private sealed class FilaCabeceraOpcion
    {
        public int IdMicrosite { get; set; }
        public string? Microsite { get; set; }
        public int IdMenu { get; set; }
        public string? Menu { get; set; }
        public string? Tipo { get; set; }
    }

    private sealed class FilaPost
    {
        public string? Subtitulo { get; set; }
        public string? Post { get; set; }
    }

    private sealed class FilaArchivo
    {
        public string? Archivo { get; set; }
        public string? Link { get; set; }
        public string? Icono { get; set; }
    }
}
