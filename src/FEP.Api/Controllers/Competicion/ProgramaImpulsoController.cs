using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Competicion;

/// <summary>Programa Impulso: presentación, documentos y torneos del programa por temporada (sustituye a <c>Impulso.asp</c>).</summary>
[ApiController]
[Route("api/v1/competicion/programa-impulso")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class ProgramaImpulsoController(
    IContenidoProgramaImpulsoRepository contenido,
    ITorneosProgramaImpulsoRepository torneos,
    TimeProvider reloj) : ControllerBase
{
    /// <summary>
    /// Presentación y documentos del programa, con los torneos FEP del programa de una temporada (por defecto,
    /// la más reciente) y la lista de temporadas disponibles. Sin paginar (2–5 torneos por temporada).
    /// </summary>
    /// <param name="anio">Temporada (<c>Año</c>, 1901–2100; parámetro <c>ano</c> del ASP). Omitido: la más reciente con torneos.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Año fuera de rango o no numérico.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ProgramaImpulsoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProgramaImpulsoDto>> Get([FromQuery] int? anio, CancellationToken cancellationToken)
    {
        if (anio is < ReglasCampeonatos.AnioMinimo or > ReglasCampeonatos.AnioMaximo)
        {
            ModelState.AddModelError(nameof(anio),
                $"Debe estar entre {ReglasCampeonatos.AnioMinimo} y {ReglasCampeonatos.AnioMaximo} (omítelo para la temporada más reciente).");
            return ValidationProblem(ModelState);
        }

        // Hora local del servidor (España), como now() en el ASP: decide qué plazos de inscripción siguen abiertos.
        var ahora = reloj.GetLocalNow().DateTime;

        var textos = await contenido.ObtenerAsync(cancellationToken);
        var lista = await torneos.BuscarAsync(anio, ahora, cancellationToken);

        return Ok(new ProgramaImpulsoDto(
            textos.Actualizado,
            textos.Titulo,
            textos.Introduccion,
            textos.Imagen,
            textos.ImagenUrl,
            textos.ImagenAlt,
            textos.Documentos,
            lista.Anio,
            lista.Anios,
            lista.Items));
    }
}
