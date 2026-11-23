using FEP.Api.Contracts.Banners;
using FEP.Api.Data.Repositories.Banners;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Banners;

/// <summary>Banners de la web pública.</summary>
[ApiController]
[Route("api/v1/banners")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class BannersController(IBannersRepository repositorio) : ControllerBase
{
    /// <summary>Banners de un tipo, en orden.</summary>
    /// <param name="tipo">
    /// Obligatorio: <c>interes</c> (enlaces de interés del pie), <c>destacado</c> (barra lateral)
    /// o <c>evento</c> (eventos destacados de portada).
    /// </param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BannerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<BannerDto>>> Get(
        [FromQuery] string? tipo,
        CancellationToken cancellationToken)
    {
        if (!TiposBanner.TryParse(tipo, out var tipoBanner))
        {
            ModelState.AddModelError(nameof(tipo), $"Parámetro obligatorio. Valores admitidos: {TiposBanner.ValoresAdmitidos}.");
            return ValidationProblem(ModelState);
        }

        return Ok(await repositorio.ObtenerPorTipoAsync(tipoBanner, cancellationToken));
    }
}
