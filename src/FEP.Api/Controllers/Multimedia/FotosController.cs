using System.ComponentModel.DataAnnotations;
using FEP.Api.Contracts.Multimedia;
using FEP.Api.Data.Repositories.Multimedia;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Multimedia;

/// <summary>Galería multimedia: fotos.</summary>
[ApiController]
[Route("api/v1/multimedia/fotos")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class FotosController(IFotosRepository repositorio) : ControllerBase
{
    /// <summary>Número de fotos que mostraba el pie de la web antigua.</summary>
    public const int LimitePorDefecto = 3;

    /// <summary>Últimas fotos publicadas (por fecha de modificación).</summary>
    /// <param name="limite">Número de fotos (1–24). Por defecto 3.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    [HttpGet("ultimas")]
    [ProducesResponseType(typeof(IReadOnlyList<FotoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<FotoDto>>> GetUltimas(
        [FromQuery, Range(1, 24)] int limite = LimitePorDefecto,
        CancellationToken cancellationToken = default)
        => Ok(await repositorio.ObtenerUltimasAsync(limite, cancellationToken));
}
