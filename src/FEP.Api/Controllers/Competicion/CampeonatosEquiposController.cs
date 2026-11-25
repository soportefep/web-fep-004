using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Competicion;

/// <summary>Ficha de un campeonato por equipos (sustituye a <c>Camp_Equipos.asp?IdTorneo={id}</c>).</summary>
[ApiController]
[Route("api/v1/competicion/campeonatos-equipos")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class CampeonatosEquiposController(
    ICampeonatoEquiposRepository repositorio,
    TimeProvider reloj,
    IOptions<MediaOptions> media) : ControllerBase
{
    /// <summary>
    /// Datos generales, cuadros, notificaciones y adjuntos, inscripción de equipos, marcadores en directo,
    /// vídeos, cartel y noticias relacionadas de un campeonato por equipos. Los cuadros, el listado de equipos
    /// y los pasos de la inscripción siguen en la web actual (se devuelven sus URL absolutas).
    /// </summary>
    /// <param name="id"><c>SFEP_Torneos.IdPrueba</c> (parámetro <c>IdTorneo</c> del ASP).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Id no numérico o menor que 1.</response>
    /// <response code="404">
    /// No existe, está borrado o no es visible (<c>urlWebActual</c>: listado de campeonatos), o no es por equipos
    /// (<c>urlWebActual</c>: su ficha en la web actual).
    /// </response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CampeonatoEquiposDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CampeonatoEquiposDto>> Get([FromRoute] int id, CancellationToken cancellationToken)
    {
        if (id < ReglasCampeonatoEquipos.IdMinimo)
        {
            ModelState.AddModelError(nameof(id), $"Debe ser un número entero mayor o igual que {ReglasCampeonatoEquipos.IdMinimo} (IdPrueba del torneo).");
            return ValidationProblem(ModelState);
        }

        // Hora local del servidor (España), como now() en el ASP: decide si la inscripción sigue abierta.
        var ahora = reloj.GetLocalNow().DateTime;
        var consulta = await repositorio.ObtenerAsync(id, ahora, cancellationToken);
        if (consulta.Ficha is { } ficha)
        {
            return Ok(ficha);
        }

        var otroTipo = consulta.FichaWebActual is not null;
        var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound,
            otroTipo ? "No es un campeonato por equipos" : "Campeonato no encontrado",
            detail: otroTipo
                ? $"El torneo {id} no es por equipos: su ficha está en la web actual."
                : $"No hay ningún campeonato publicado con id {id}.");
        problema.Extensions["urlWebActual"] = consulta.FichaWebActual ?? WebActual.Url(media.Value, "Campeonatos");
        return NotFound(problema);
    }
}
