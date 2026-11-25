using FEP.Api.Contracts.Formacion;
using FEP.Api.Data.Repositories.Formacion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Formacion;

/// <summary>Convocatorias de formación (técnicos, FOCO y jueces árbitros).</summary>
[ApiController]
[Route("api/v1/formacion/convocatorias")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class ConvocatoriasController(IConvocatoriasRepository repositorio, TimeProvider reloj) : ControllerBase
{
    /// <summary>Convocatorias con inscripción abierta, ordenadas por fecha tope de inscripción.</summary>
    /// <param name="categoria">
    /// <c>monitor</c> (ParcialN1, Monitor, EntrenadorN2, EntrenadorN3, FC), <c>foco</c> o <c>juez</c>.
    /// Si se omite, devuelve las de los tres grupos.
    /// </param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConvocatoriaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ConvocatoriaDto>>> Get(
        [FromQuery] string? categoria,
        CancellationToken cancellationToken)
    {
        CategoriaFormacion? filtro = null;
        if (!string.IsNullOrWhiteSpace(categoria))
        {
            if (!CategoriasFormacion.TryParse(categoria, out var valor))
            {
                ModelState.AddModelError(nameof(categoria), $"Valores admitidos: {CategoriasFormacion.ValoresAdmitidos}.");
                return ValidationProblem(ModelState);
            }

            filtro = valor;
        }

        // Hora local del servidor, como now() en el ASP (el servidor debe estar en hora de España).
        var ahora = reloj.GetLocalNow().DateTime;
        return Ok(await repositorio.ObtenerAbiertasAsync(filtro, ahora, cancellationToken));
    }
}
