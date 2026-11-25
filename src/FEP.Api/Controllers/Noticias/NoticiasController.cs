using System.ComponentModel.DataAnnotations;
using FEP.Api.Contracts.Noticias;
using FEP.Api.Data.Repositories.Noticias;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Noticias;

/// <summary>Noticias de la web pública.</summary>
[ApiController]
[Route("api/v1/noticias")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class NoticiasController(INoticiasRepository repositorio) : ControllerBase
{
    /// <summary>Noticias destacadas de portada, por orden.</summary>
    /// <param name="limite">Máximo de noticias (1–20). Por defecto 6, como la portada antigua.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    [HttpGet("destacadas")]
    [ProducesResponseType(typeof(IReadOnlyList<NoticiaDestacadaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<NoticiaDestacadaDto>>> GetDestacadas(
        [FromQuery, Range(1, 20)] int limite = NoticiasRepository.LimitePortada,
        CancellationToken cancellationToken = default)
        => Ok(await repositorio.ObtenerDestacadasAsync(limite, cancellationToken));
}
