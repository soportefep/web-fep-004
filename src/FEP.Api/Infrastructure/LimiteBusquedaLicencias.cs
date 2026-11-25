using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FEP.Api.Infrastructure;

/// <summary>
/// Límite de búsquedas por dirección IP del buscador público de licencias (<c>GET /api/v1/licencias</c>). La búsqueda
/// devuelve nombres y números de licencia de personas: el límite evita que se recorra la base de datos con un programa
/// (el ASP no tenía ninguno). Política propia, con su respuesta <c>429</c>, independiente de la del asistente.
/// <para>DECISIÓN FEP: valores de partida (30 por minuto y 300 por hora por IP).</para>
/// </summary>
public sealed class LimiteBusquedaLicencias : IRateLimiterPolicy<string>
{
    /// <summary>Nombre de la política (<c>[EnableRateLimiting]</c>).</summary>
    public const string Politica = "BusquedaLicencias";

    /// <summary>Búsquedas por minuto y por IP.</summary>
    public const int BusquedasPorMinuto = 30;

    /// <summary>Búsquedas por hora y por IP.</summary>
    public const int BusquedasPorHora = 300;

    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = async (contexto, ct) =>
    {
        var http = contexto.HttpContext;
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
        {
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var problema = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Demasiadas búsquedas",
            Detail = "Has hecho muchas búsquedas de licencias seguidas. Espera unos minutos y vuelve a intentarlo.",
        };
        await http.Response.WriteAsJsonAsync(problema, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", ct);
    };

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        // Dos ventanas, como el asistente: ráfagas (por minuto) y uso sostenido (por hora).
        return RateLimitPartition.Get(ip, _ => RateLimiter.CreateChained(
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = BusquedasPorMinuto, Window = TimeSpan.FromMinutes(1) }),
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = BusquedasPorHora, Window = TimeSpan.FromHours(1) })));
    }
}

/// <summary>Registro del límite del buscador de licencias.</summary>
public static class LimiteBusquedaLicenciasExtensions
{
    /// <summary>Registra la política <see cref="LimiteBusquedaLicencias.Politica"/> (se suma a la de <c>AddFepAsistente</c>).</summary>
    public static IServiceCollection AddLimiteBusquedaLicencias(this IServiceCollection services)
        => services.AddRateLimiter(opciones =>
            opciones.AddPolicy<string, LimiteBusquedaLicencias>(LimiteBusquedaLicencias.Politica));
}
