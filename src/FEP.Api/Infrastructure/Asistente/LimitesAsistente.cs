using System.Threading.RateLimiting;
using FEP.Api.Data.Repositories.Asistente;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FEP.Api.Infrastructure.Asistente;

/// <summary>Registro del asistente y su límite de peticiones por dirección IP.</summary>
public static class LimitesAsistente
{
    /// <summary>Política de rate limiting del chat.</summary>
    public const string Politica = "Asistente";

    /// <summary>Servicios del asistente: corpus, herramientas, presupuesto, cliente de Claude y límite por IP.</summary>
    public static IServiceCollection AddFepAsistente(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AsistenteOptions>()
            .Bind(configuration.GetSection(AsistenteOptions.Seccion))
            .Validate(o => o.PreguntasPorMinuto > 0 && o.PreguntasPorHora > 0 && o.IteracionesMaximas > 0 && o.MaxTokens > 0,
                "Asistente: los límites deben ser positivos.")
            .Validate(o => Uri.TryCreate(o.UrlContacto, UriKind.Absolute, out _), "Asistente:UrlContacto debe ser una URL absoluta.")
            .ValidateOnStart();

        services.AddSingleton<ICorpusNormativa>(sp =>
            new CorpusNormativa(Path.Combine(sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "Contenidos")));
        services.AddSingleton<PresupuestoAsistente>();
        services.AddScoped<HerramientasAsistente>();
        services.AddScoped<IAsistente, AsistenteClaude>();

        services.AddRateLimiter(opciones =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opciones.AddPolicy(Politica, contexto =>
            {
                var o = contexto.RequestServices.GetRequiredService<IOptions<AsistenteOptions>>().Value;
                var ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
                // Dos ventanas: ráfagas (por minuto) y uso sostenido (por hora).
                return RateLimitPartition.Get(ip, _ => RateLimiter.CreateChained(
                    new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = o.PreguntasPorMinuto, Window = TimeSpan.FromMinutes(1) }),
                    new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = o.PreguntasPorHora, Window = TimeSpan.FromHours(1) })));
            });
            opciones.OnRejected = async (contexto, ct) =>
            {
                var http = contexto.HttpContext;
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                {
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString();
                }

                var o = http.RequestServices.GetRequiredService<IOptions<AsistenteOptions>>().Value;
                var problema = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Demasiadas preguntas",
                    Detail = "Has hecho muchas preguntas seguidas. Espera un poco o escribe a la FEP desde el formulario de contacto.",
                };
                problema.Extensions["urlContacto"] = o.UrlContacto;
                await http.Response.WriteAsJsonAsync(problema, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", ct);
            };
        });

        return services;
    }
}
