using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Data.Repositories.Federacion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Competicion;

/// <summary>
/// Calendarios de competición de las federaciones territoriales por temporada (sustituye a <c>CalendarioFT.asp</c>,
/// URL <c>/CalendarioFT</c>). Mismas consultas y forma que el calendario FEP (<c>/api/v1/competicion/calendario</c>)
/// con otra federación.
/// </summary>
[ApiController]
[Route("api/v1/competicion/calendario/federaciones")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class CalendarioTerritorialController(
    ICalendarioCompeticionRepository calendario,
    IFederacionesAutonomicasRepository federaciones,
    TimeProvider reloj,
    IOptions<MediaOptions> media) : ControllerBase
{
    /// <summary>
    /// Federaciones territoriales (desplegable "Federación" del ASP) con las temporadas en las que tienen alguna
    /// prueba publicada; las que no tienen calendario llegan con <c>temporadas</c> vacío. Orden de la BD
    /// (<c>Abreviatura</c>, como el ASP).
    /// </summary>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FederacionCalendarioTerritorialDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<FederacionCalendarioTerritorialDto>>> Get(CancellationToken cancellationToken)
    {
        var datos = await calendario.ListarFederacionesAsync(cancellationToken);
        var nombres = await NombresAsync(cancellationToken);
        return Ok(datos.Select(f => AFederacion(f, nombres)).ToArray());
    }

    /// <summary>
    /// Pruebas del calendario de una federación territorial en una temporada (por defecto, la del año en curso si la
    /// tiene publicada; si no, la más reciente anterior), por fecha de inicio, con las temporadas de la federación.
    /// Sin paginar (hasta ~260 pruebas por temporada en Andalucía).
    /// </summary>
    /// <param name="id"><c>SFEP_Federaciones.IdFederacion</c> (1–22; parámetro <c>fed</c> del ASP). La FEP (0) tiene su propio endpoint.</param>
    /// <param name="anio">Temporada (<c>Año</c>, 1901–2100; parámetro <c>ano</c> del ASP). Opcional.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Id o año fuera de rango o no numéricos.</response>
    /// <response code="404">No existe ninguna federación con ese id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CalendarioTerritorialDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CalendarioTerritorialDto>> Get(
        [FromRoute] int id, [FromQuery] int? anio, CancellationToken cancellationToken)
    {
        if (id is < ReglasCalendario.IdFederacionTerritorialMinimo or > ReglasCalendario.IdFederacionTerritorialMaximo)
        {
            ModelState.AddModelError(nameof(id),
                $"Debe estar entre {ReglasCalendario.IdFederacionTerritorialMinimo} y {ReglasCalendario.IdFederacionTerritorialMaximo} " +
                "(el calendario de la FEP está en /api/v1/competicion/calendario).");
        }

        if (anio is < ReglasCampeonatos.AnioMinimo or > ReglasCampeonatos.AnioMaximo)
        {
            ModelState.AddModelError(nameof(anio),
                $"Debe estar entre {ReglasCampeonatos.AnioMinimo} y {ReglasCampeonatos.AnioMaximo} (omítelo para la temporada en curso).");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Año en curso en hora de Madrid: la temporada por defecto cambia sola el 1 de enero (DECISIÓN FEP).
        var anioActual = ReglasCalendario.AnioEnCurso(reloj);

        var datos = await calendario.BuscarFederacionAsync(id, anio, anioActual, cancellationToken);
        if (datos is null)
        {
            var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound,
                "Federación no encontrada", detail: $"No existe ninguna federación con id {id}.");
            problema.Extensions["urlWebActual"] = WebActual.Url(media.Value, "CalendarioFT");
            return NotFound(problema);
        }

        var nombres = await NombresAsync(cancellationToken);
        return Ok(new CalendarioTerritorialDto(AFederacion(datos.Federacion, nombres), datos.Anio, datos.Temporadas, datos.Items));
    }

    /// <summary>Territorio y nombre oficial por id (los mismos de federaciones.html: <c>Contenidos/federaciones-autonomicas.json</c>).</summary>
    private async Task<IReadOnlyDictionary<int, (string Territorio, string Nombre)>> NombresAsync(CancellationToken cancellationToken)
    {
        var contenido = await federaciones.ObtenerAsync(cancellationToken);
        return contenido.Federaciones
            .Where(f => !f.Nacional)
            .GroupBy(f => f.Id)
            .ToDictionary(g => g.Key, g => (g.First().Territorio, g.First().Nombre));
    }

    private static FederacionCalendarioTerritorialDto AFederacion(
        DatosFederacionCalendarioDto f, IReadOnlyDictionary<int, (string Territorio, string Nombre)> nombres)
    {
        var conocido = nombres.TryGetValue(f.Id, out var n);
        var territorio = conocido ? n.Territorio : f.Abreviatura ?? $"Federación {f.Id}";
        return new FederacionCalendarioTerritorialDto(f.Id, territorio, conocido ? n.Nombre : null, f.Abreviatura, f.Temporadas);
    }
}
