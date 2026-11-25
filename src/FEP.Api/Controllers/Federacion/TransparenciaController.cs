using FEP.Api.Contracts.Federacion;
using FEP.Api.Data.Repositories.Federacion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Federacion;

/// <summary>Portal de transparencia de la FEP (sustituye a <c>Ley_De_Transparencia.asp</c>).</summary>
[ApiController]
[Route("api/v1/federacion/transparencia")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class TransparenciaController(ITransparenciaRepository repositorio) : ControllerBase
{
    /// <summary>
    /// Información institucional, contratos y convenios, información económica y buen gobierno,
    /// con los documentos publicados (PDF).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TransparenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TransparenciaDto>> Get(CancellationToken cancellationToken)
        => Ok(await repositorio.ObtenerAsync(cancellationToken));
}
