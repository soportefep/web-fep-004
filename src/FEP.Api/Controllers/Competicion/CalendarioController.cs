using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Competicion;

/// <summary>Calendario oficial de competición de la FEP por temporada (sustituye a <c>CalendarioFEP.asp</c>).</summary>
[ApiController]
[Route("api/v1/competicion/calendario")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class CalendarioController(
    ICalendarioCompeticionRepository calendario,
    IContenidoCalendarioRepository contenido,
    TimeProvider reloj) : ControllerBase
{
    /// <summary>
    /// Pruebas del calendario FEP de una temporada (por defecto, la del año en curso), por fecha de inicio, con
    /// las temporadas publicadas y su PDF, y el aviso de la convocatoria para organizar torneos si está abierta.
    /// Sin paginar (25–55 pruebas por temporada).
    /// </summary>
    /// <param name="anio">
    /// Temporada (<c>Año</c>, 1901–2100; parámetro <c>ano</c> del ASP). Omitido: la del año en curso si está
    /// publicada (si no, la más reciente anterior).
    /// </param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Año fuera de rango o no numérico.</response>
    [HttpGet]
    [ProducesResponseType(typeof(CalendarioCompeticionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CalendarioCompeticionDto>> Get([FromQuery] int? anio, CancellationToken cancellationToken)
    {
        if (anio is < ReglasCampeonatos.AnioMinimo or > ReglasCampeonatos.AnioMaximo)
        {
            ModelState.AddModelError(nameof(anio),
                $"Debe estar entre {ReglasCampeonatos.AnioMinimo} y {ReglasCampeonatos.AnioMaximo} (omítelo para la temporada en curso).");
            return ValidationProblem(ModelState);
        }

        // Año en curso en hora de Madrid: la temporada por defecto cambia sola el 1 de enero (DECISIÓN FEP).
        var anioActual = ReglasCalendario.AnioEnCurso(reloj);

        var datos = await calendario.BuscarAsync(anio, anioActual, cancellationToken);
        var licitacion = datos.LicitacionAbierta
            ? (await contenido.ObtenerAsync(cancellationToken)).Licitacion
            : null;

        return Ok(new CalendarioCompeticionDto(datos.Anio, datos.Temporadas, licitacion, datos.Items));
    }
}
