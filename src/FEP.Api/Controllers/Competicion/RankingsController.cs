using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Competicion;

/// <summary>Índice de rankings de la FEP (sustituye a la rejilla de <c>Rankings.asp</c>).</summary>
[ApiController]
[Route("api/v1/competicion/rankings")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class RankingsController(IRankingsWebRepository repositorio) : ControllerBase
{
    /// <summary>
    /// Rankings publicados (absoluto, menores, equipos absolutos, silla de ruedas, veteranos…), en el orden de la
    /// web, con su enlace en la web actual y su imagen. Las clasificaciones de cada ranking no están aquí.
    /// </summary>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RankingWebDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<RankingWebDto>>> Get(CancellationToken cancellationToken)
        => Ok(await repositorio.ObtenerAsync(cancellationToken));
}
