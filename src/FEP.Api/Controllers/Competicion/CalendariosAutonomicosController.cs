using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Competicion;

/// <summary>
/// Calendarios de pruebas de las federaciones autonómicas puntuables para los rankings FEP (sustituye a
/// <c>Calendario_Menores_FFAA.asp</c> y a <c>Calendario_Veteranos_FFAA.asp</c>).
/// </summary>
[ApiController]
[Route("api/v1/competicion/calendarios-autonomicos")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class CalendariosAutonomicosController(
    ICalendariosAutonomicosRepository repositorio,
    IOptions<MediaOptions> media) : ControllerBase
{
    /// <summary>
    /// Presentación (reglamento, momentos de incorporación al ranking, participación) y pruebas de cada
    /// federación autonómica, con fechas, sede y si están incluidas en el ranking o dan el título autonómico.
    /// </summary>
    /// <param name="categoria"><c>menores</c> o <c>veteranos</c> (sin distinguir mayúsculas).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Categoría no admitida.</response>
    /// <response code="404">Calendario aún no publicado en la API (<c>urlWebActual</c>: página en la web actual).</response>
    [HttpGet("{categoria}")]
    [ProducesResponseType(typeof(CalendarioAutonomicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CalendarioAutonomicoDto>> Get([FromRoute] string categoria, CancellationToken cancellationToken)
    {
        var clave = ReglasCalendariosAutonomicos.Normalizar(categoria);
        if (clave is null)
        {
            ModelState.AddModelError(nameof(categoria),
                $"Debe ser una de: {string.Join(", ", ReglasCalendariosAutonomicos.PaginasWebActual.Keys)}.");
            return ValidationProblem(ModelState);
        }

        var calendario = await repositorio.ObtenerAsync(clave, cancellationToken);
        if (calendario is null)
        {
            var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound,
                "Calendario no publicado", detail: $"El calendario autonómico de {clave} todavía no está disponible en la API.");
            problema.Extensions["urlWebActual"] = WebActual.Url(media.Value, ReglasCalendariosAutonomicos.PaginasWebActual[clave]);
            return NotFound(problema);
        }

        return Ok(calendario);
    }
}
