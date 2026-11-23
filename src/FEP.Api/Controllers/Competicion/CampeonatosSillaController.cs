using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Competicion;

/// <summary>Ficha de un torneo de pádel en silla de ruedas por parejas (sustituye a <c>Camp_Silla.asp?IdTorneo={id}</c>).</summary>
[ApiController]
[Route("api/v1/competicion/campeonatos-silla")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class CampeonatosSillaController(
    ICampeonatoSillaRepository repositorio,
    TimeProvider reloj,
    IOptions<MediaOptions> media) : ControllerBase
{
    /// <summary>
    /// Datos generales, marcadores en directo, cuadros, cuadro de honor, notificaciones y adjuntos, parejas y
    /// entrenadores inscritos, inscripción, información adicional, cartel y noticias relacionadas de un torneo de
    /// pádel en silla de ruedas. Los cuadros, los listados de inscritos y el formulario de inscripción siguen en la
    /// web actual (se devuelven sus URL absolutas).
    /// </summary>
    /// <param name="id"><c>SFEP_Torneos.IdPrueba</c> (parámetro <c>IdTorneo</c> del ASP).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Id no numérico o menor que 1.</response>
    /// <response code="404">
    /// No existe, está borrado o no es visible (<c>urlWebActual</c>: listado de campeonatos), o no es de silla de
    /// ruedas por parejas (<c>urlWebActual</c>: su ficha en la web actual o en la web nueva si ya está migrada).
    /// </response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CampeonatoSillaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CampeonatoSillaDto>> Get([FromRoute] int id, CancellationToken cancellationToken)
    {
        if (id < ReglasCampeonatoSilla.IdMinimo)
        {
            ModelState.AddModelError(nameof(id), $"Debe ser un número entero mayor o igual que {ReglasCampeonatoSilla.IdMinimo} (IdPrueba del torneo).");
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
            otroTipo ? "No es un torneo de pádel en silla de ruedas" : "Torneo no encontrado",
            detail: otroTipo
                ? $"El torneo {id} no es de pádel en silla de ruedas por parejas: tiene otra ficha."
                : $"No hay ningún torneo publicado con id {id}.");
        problema.Extensions["urlWebActual"] = consulta.FichaWebActual ?? WebActual.Url(media.Value, "Campeonatos");
        return NotFound(problema);
    }
}
