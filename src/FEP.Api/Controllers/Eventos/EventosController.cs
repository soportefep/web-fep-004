using FEP.Api.Contracts.Banners;
using FEP.Api.Data.Repositories.Banners;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Eventos;

/// <summary>Eventos destacados e inscripciones abiertas de la portada.</summary>
[ApiController]
[Route("api/v1/eventos")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class EventosController(IBannersRepository banners) : ControllerBase
{
    /// <summary>
    /// Eventos destacados. En el modelo actual son banners de tipo <c>Evento</c>
    /// (mismo resultado que <c>GET /api/v1/banners?tipo=evento</c>).
    /// </summary>
    [HttpGet("destacados")]
    [ProducesResponseType(typeof(IReadOnlyList<BannerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<BannerDto>>> GetDestacados(CancellationToken cancellationToken)
        => Ok(await banners.ObtenerPorTipoAsync(TipoBanner.Evento, cancellationToken));
}
