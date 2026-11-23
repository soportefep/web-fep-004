using FEP.Api.Contracts.Patrocinadores;
using FEP.Api.Data.Repositories.Patrocinadores;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Patrocinadores;

/// <summary>Patrocinadores de la FEP.</summary>
[ApiController]
[Route("api/v1/patrocinadores")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class PatrocinadoresController(IPatrocinadoresRepository repositorio) : ControllerBase
{
    /// <summary>Patrocinadores visibles, en orden.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PatrocinadorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<PatrocinadorDto>>> Get(CancellationToken cancellationToken)
        => Ok(await repositorio.ObtenerAsync(cancellationToken));
}
