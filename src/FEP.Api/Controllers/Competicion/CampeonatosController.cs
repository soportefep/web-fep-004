using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Competicion;

/// <summary>Listado de campeonatos y torneos FEP por temporada (sustituye a <c>Campeonatos.asp</c>).</summary>
[ApiController]
[Route("api/v1/competicion/campeonatos")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class CampeonatosController(ICampeonatosRepository repositorio, TimeProvider reloj) : ControllerBase
{
    /// <summary>
    /// Torneos FEP de una temporada (por defecto, la más reciente), del más reciente al más antiguo, con
    /// la lista de temporadas disponibles. Sin paginar (unos 40–50 torneos por temporada).
    /// </summary>
    /// <param name="anio">Temporada (<c>Año</c>, 1901–2100). Omitido: la más reciente con torneos.</param>
    /// <param name="tipo">Tipo de torneo: <c>parejas</c> o <c>equipos</c> (parámetro <c>modalidad</c> del ASP).</param>
    /// <param name="categoria">
    /// Categoría de juego: <c>absoluto</c>, <c>menores</c> o <c>veteranos</c> (parámetro <c>tipo</c> del ASP).
    /// Como en la web actual, no incluye los campeonatos de selecciones (<c>SEL_*</c>).
    /// </param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Año fuera de rango o tipo/categoría desconocidos.</response>
    [HttpGet]
    [ProducesResponseType(typeof(CampeonatosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CampeonatosDto>> Get(
        [FromQuery] int? anio,
        [FromQuery] string? tipo,
        [FromQuery] string? categoria,
        CancellationToken cancellationToken)
    {
        if (anio is < ReglasCampeonatos.AnioMinimo or > ReglasCampeonatos.AnioMaximo)
        {
            ModelState.AddModelError(nameof(anio),
                $"Debe estar entre {ReglasCampeonatos.AnioMinimo} y {ReglasCampeonatos.AnioMaximo} (omítelo para la temporada más reciente).");
        }

        var claveTipo = ReglasCampeonatos.ClaveTipo(tipo);
        if (!string.IsNullOrWhiteSpace(tipo) && claveTipo is null)
        {
            ModelState.AddModelError(nameof(tipo), $"Valores admitidos: {string.Join(", ", ReglasCampeonatos.Tipos.Keys)} (omítelo para no filtrar).");
        }

        var claveCategoria = ReglasCampeonatos.ClaveCategoria(categoria);
        if (!string.IsNullOrWhiteSpace(categoria) && claveCategoria is null)
        {
            ModelState.AddModelError(nameof(categoria), $"Valores admitidos: {string.Join(", ", ReglasCampeonatos.Categorias.Keys)} (omítelo para no filtrar).");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Hora local del servidor (España), como now() en el ASP: decide qué plazos de inscripción siguen abiertos.
        var ahora = reloj.GetLocalNow().DateTime;
        return Ok(await repositorio.BuscarAsync(new BusquedaCampeonatos(anio, claveTipo, claveCategoria), ahora, cancellationToken));
    }
}
