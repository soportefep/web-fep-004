using FEP.Api.Contracts.Navegacion;
using FEP.Api.Data.Repositories.Navegacion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Navegacion;

/// <summary>Menú principal de la web pública.</summary>
[ApiController]
[Route("api/v1/menu")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class MenuController(IMenuRepository repositorio) : ControllerBase
{
    /// <summary>Opciones de nivel 1 con sus opciones de nivel 2 anidadas en <c>hijos</c>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<MenuItemDto>>> Get(CancellationToken cancellationToken)
        => Ok(await repositorio.ObtenerAsync(cancellationToken));
}
