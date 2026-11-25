using FEP.Api.Contracts.Federacion;
using FEP.Api.Data.Repositories.Federacion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Federacion;

/// <summary>Equipo técnico y administrativo de la FEP (sustituye a <c>Equipo_FEP.asp</c>).</summary>
[ApiController]
[Route("api/v1/federacion/equipo")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class EquipoFepController(IEquipoFepRepository repositorio) : ControllerBase
{
    /// <summary>Organigrama (presidencia, área deportiva, equipo administrativo) y equipo de menores.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(EquipoFepDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EquipoFepDto>> Get(CancellationToken cancellationToken)
        => Ok(await repositorio.ObtenerAsync(cancellationToken));
}
