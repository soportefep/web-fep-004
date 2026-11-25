using FEP.Api.Contracts.Federacion;
using FEP.Api.Data.Repositories.Federacion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Federacion;

/// <summary>
/// Ficha de una federación autonómica o de la FEP: contacto y licencias del año en curso (sustituye a
/// <c>Datos_Federacion.asp?id={id}</c> y a su URL limpia <c>/Federacion/{id}/{nombre}</c>).
/// </summary>
[ApiController]
[Route("api/v1/federacion/federaciones-autonomicas")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class FichaFederacionController(
    IFichaFederacionRepository repositorio,
    TimeProvider reloj,
    IOptions<MediaOptions> media) : ControllerBase
{
    /// <summary>
    /// Datos de contacto de la federación y sus licencias del año en curso (jugadores, técnicos, árbitros,
    /// clubes, puesto entre las autonómicas, evolución desde 2012, género y tramos de edad).
    /// Con <c>id = 0</c> (FEP) las cifras son el total nacional.
    /// </summary>
    /// <param name="id"><c>SFEP_Federaciones.IdFederacion</c> (0–22: 0 = FEP y las 22 federaciones autonómicas).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Id no numérico o fuera de 0–22.</response>
    /// <response code="404">No existe ninguna federación con ese id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FichaFederacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FichaFederacionDto>> Get([FromRoute] int id, CancellationToken cancellationToken)
    {
        if (id is < ReglasFichaFederacion.IdMinimo or > ReglasFichaFederacion.IdMaximo)
        {
            ModelState.AddModelError(nameof(id),
                $"Debe estar entre {ReglasFichaFederacion.IdMinimo} y {ReglasFichaFederacion.IdMaximo} (0 = Federación Española de Pádel).");
            return ValidationProblem(ModelState);
        }

        // Año de las licencias según la hora local del servidor, como year(now()) en el ASP.
        var ahora = reloj.GetLocalNow().DateTime;
        var ficha = await repositorio.ObtenerAsync(id, ahora, cancellationToken);
        if (ficha is null)
        {
            var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound,
                "Federación no encontrada", detail: $"No existe ninguna federación con id {id}.");
            problema.Extensions["urlWebActual"] = WebActual.Url(media.Value, "Federaciones");
            return NotFound(problema);
        }

        return Ok(ficha);
    }
}
