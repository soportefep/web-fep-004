using FEP.Api.Contracts.Federacion;
using FEP.Api.Data.Repositories.Federacion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Federacion;

/// <summary>Federaciones autonómicas de la FEP (sustituye a <c>Federaciones.asp</c>).</summary>
[ApiController]
[Route("api/v1/federacion/federaciones-autonomicas")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class FederacionesAutonomicasController(IFederacionesAutonomicasRepository repositorio) : ControllerBase
{
    /// <summary>Las 19 federaciones autonómicas (y la FEP), y la reserva de Zoom con su calendario.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(FederacionesAutonomicasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FederacionesAutonomicasDto>> Get(CancellationToken cancellationToken)
        => Ok(await repositorio.ObtenerAsync(cancellationToken));
}
