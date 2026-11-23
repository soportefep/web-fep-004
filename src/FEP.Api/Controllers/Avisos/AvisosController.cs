using FEP.Api.Contracts.Avisos;
using FEP.Api.Data.Repositories.Avisos;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Avisos;

/// <summary>Avisos de la web pública.</summary>
[ApiController]
[Route("api/v1/avisos")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class AvisosController(IAvisosRepository repositorio) : ControllerBase
{
    /// <summary>Avisos visibles (portal FEP y comunes), del más reciente al más antiguo.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AvisoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<AvisoDto>>> Get(CancellationToken cancellationToken)
        => Ok(await repositorio.ObtenerAsync(cancellationToken));
}
