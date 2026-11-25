using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Competicion;

/// <summary>
/// Estadísticas de un jugador o jugadora con licencia (sustituye a <c>Estadisticas.asp?id={licencia}</c>, regla
/// <c>^Estadisticas</c> de <c>web.config</c>, y a <c>CargarPruebas.asp?Rnk=&amp;id=</c>, que cargaba por AJAX las pruebas
/// de cada ranking). La búsqueda por DNI del ASP no se publica en la API (ver docs/api/analisis/Estadisticas.md).
/// </summary>
[ApiController]
[Route("api/v1/competicion/jugadores")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class EstadisticasJugadorController(
    IEstadisticasJugadorRepository repositorio,
    TimeProvider reloj,
    IOptions<MediaOptions> media) : ControllerBase
{
    /// <summary>
    /// Ficha (nombre, foto, categoría por edad y clubes de la última licencia), balance de partidos, sets y juegos del
    /// último año en el Ranking Único, última pareja, mejor clasificación, rankings nacionales con las pruebas que
    /// puntúan en cada uno, evolución en el Ranking Único y campeonatos de España por equipos.
    /// </summary>
    /// <param name="licencia">Número de licencia de jugador/a (entero positivo).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">La licencia no es un número entero positivo.</response>
    /// <response code="404">
    /// No existe la licencia (<c>motivo</c> = <c>licencia-no-encontrada</c>) o no figura en ningún ranking nacional
    /// (<c>motivo</c> = <c>sin-ranking</c>: el ASP no mostraba nada). Incluye <c>urlWebActual</c>.
    /// </response>
    [HttpGet("{licencia}/estadisticas")]
    [ProducesResponseType(typeof(EstadisticasJugadorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EstadisticasJugadorDto>> Get([FromRoute] int licencia, CancellationToken cancellationToken)
    {
        if (licencia < 1)
        {
            ModelState.AddModelError(nameof(licencia), "Debe ser un número de licencia positivo.");
            return ValidationProblem(ModelState);
        }

        // Hora de Madrid: año de la categoría por edad (year(now()) en el ASP) y fecha de la consulta.
        var ahora = ReglasOrganizacionTorneos.AhoraEnMadrid(reloj);
        var consulta = await repositorio.ObtenerAsync(licencia, ahora, cancellationToken);
        return consulta.Estado switch
        {
            EstadoEstadisticas.Encontradas when consulta.Estadisticas is not null => Ok(consulta.Estadisticas),
            EstadoEstadisticas.SinRanking => NoEncontrada(licencia, "sin-ranking", "Sin ranking nacional",
                $"La licencia {licencia} no figura en ningún ranking nacional de la FEP."),
            _ => NoEncontrada(licencia, "licencia-no-encontrada", "Licencia no encontrada",
                $"No existe ninguna licencia de jugador/a con el número {licencia} en la base de datos de la FEP."),
        };
    }

    private NotFoundObjectResult NoEncontrada(int licencia, string motivo, string titulo, string detalle)
    {
        var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, titulo, detail: detalle);
        problema.Extensions["motivo"] = motivo;
        problema.Extensions["urlWebActual"] = WebActual.Url(media.Value, ReglasClasificaciones.RutaEstadisticas(licencia));
        return NotFound(problema);
    }
}
