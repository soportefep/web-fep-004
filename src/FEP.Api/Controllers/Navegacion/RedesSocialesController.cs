using FEP.Api.Contracts.Navegacion;
using FEP.Api.Data.Repositories.Navegacion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Navegacion;

/// <summary>Redes sociales de la FEP.</summary>
[ApiController]
[Route("api/v1/redes-sociales")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class RedesSocialesController(IRedesSocialesRepository repositorio) : ControllerBase
{
    /// <summary>Perfiles en redes sociales, en orden.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RedSocialDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<RedSocialDto>>> Get(CancellationToken cancellationToken)
        => Ok(await repositorio.ObtenerAsync(cancellationToken));
}
