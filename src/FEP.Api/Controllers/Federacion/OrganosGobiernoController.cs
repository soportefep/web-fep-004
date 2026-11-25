using FEP.Api.Contracts.Federacion;
using FEP.Api.Data.Repositories.Federacion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Federacion;

/// <summary>Órganos de gobierno de la FEP (sustituye a <c>Organos_Gobierno.asp</c>).</summary>
[ApiController]
[Route("api/v1/federacion/organos-gobierno")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class OrganosGobiernoController(IOrganosGobiernoRepository repositorio) : ControllerBase
{
    /// <summary>Junta Directiva, Asamblea General, Comisión Delegada y CTAP, con sus actas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(OrganosGobiernoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrganosGobiernoDto>> Get(CancellationToken cancellationToken)
        => Ok(await repositorio.ObtenerAsync(cancellationToken));
}
