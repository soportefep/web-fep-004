using FEP.Api.Data;

namespace FEP.Api.Infrastructure;

/// <summary>Nombres de políticas CORS.</summary>
public static class CorsPolicies
{
    /// <summary>Política para el front FEP.Web (orígenes en <c>Cors:OrigenesPermitidos</c>).</summary>
    public const string Front = "FrontFep";
}

/// <summary>Políticas de caché para contenido público.</summary>
public static class CachePolicies
{
    /// <summary>Política de output cache para contenido público de la web.</summary>
    public const string ContenidoPublico = "ContenidoPublico";

    /// <summary>Duración (s) de la caché de servidor y de la cabecera Cache-Control.</summary>
    public const int SegundosContenidoPublico = 300;
}

/// <summary>Registro de la infraestructura común de la API.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFepInfraestructura(this IServiceCollection services, IConfiguration configuration)
    {
        // Hora del servidor inyectable (sustituye a now() del ASP; facilita los tests).
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

        services.AddOptions<MediaOptions>()
            .Bind(configuration.GetSection(MediaOptions.Seccion))
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "Media:BaseUrl debe ser una URL absoluta.")
            .ValidateOnStart();
        services.AddSingleton<IMediaUrlBuilder, MediaUrlBuilder>();

        var origenes = configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];
        services.AddCors(opciones => opciones.AddPolicy(CorsPolicies.Front, politica => politica
            .WithOrigins(origenes)
            // POST solo lo usa el chat del asistente (POST /api/v1/asistente/chat).
            .WithMethods("GET", "HEAD", "POST")
            .AllowAnyHeader()));

        services.AddOutputCache(opciones => opciones.AddPolicy(CachePolicies.ContenidoPublico, politica => politica
            .Expire(TimeSpan.FromSeconds(CachePolicies.SegundosContenidoPublico))
            // Parámetros de query de los endpoints públicos (los del buscador de clubes: federacion…pagina;
            // anio: listado de campeonatos; genero y licencia: clasificaciones de los rankings).
            .SetVaryByQuery("categoria", "tipo", "limite", "federacion", "provincia", "busqueda", "pagina", "anio",
                "genero", "licencia")));

        return services;
    }
}
