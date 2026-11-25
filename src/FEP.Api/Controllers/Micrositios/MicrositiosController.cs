using FEP.Api.Contracts.Micrositios;
using FEP.Api.Data.Repositories.Micrositios;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Micrositios;

/// <summary>
/// Micrositios: mini-CMS de la web pública (sustituye a <c>Microsite.asp?Id=…&amp;IdOpcion=…</c>,
/// p. ej. Homologaciones o Elecciones 2024).
/// </summary>
[ApiController]
[Route("api/v1/micrositios")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class MicrositiosController(IMicrositiosRepository repositorio, IOptions<MediaOptions> media) : ControllerBase
{
    /// <summary>Cabecera del micrositio y su menú de opciones (una sola ida a la BD).</summary>
    /// <param name="id">Id del micrositio (<c>Id</c> de <c>Microsite.asp</c>), entero positivo.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Id no numérico o menor que 1.</response>
    /// <response code="403">Micrositio de acceso restringido (asambleas): solo en la web actual.</response>
    /// <response code="404">No existe, está borrado o no es visible.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MicrositioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MicrositioDto>> Get([FromRoute] int id, CancellationToken cancellationToken)
    {
        ValidarId(id, nameof(id));
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (ReglasMicrositios.EsPrivado(id))
        {
            return Privado(id, null);
        }

        var micrositio = await repositorio.ObtenerAsync(id, cancellationToken);
        if (micrositio is null)
        {
            return NoEncontrado($"No existe ningún micrositio publicado con id {id}.", id, null);
        }

        return Ok(micrositio);
    }

    /// <summary>Contenido de una opción del micrositio: texto (HTML heredado, sin sanear) y archivos adjuntos.</summary>
    /// <param name="id">Id del micrositio, entero positivo.</param>
    /// <param name="idOpcion">Id de la opción (<c>IdOpcion</c> de <c>Microsite.asp</c>), entero positivo.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Algún id no numérico o menor que 1.</response>
    /// <response code="403">Micrositio de acceso restringido: solo en la web actual.</response>
    /// <response code="404">El micrositio o la opción no existen, no son visibles o la opción es de otro micrositio.</response>
    [HttpGet("{id}/opciones/{idOpcion}")]
    [ProducesResponseType(typeof(OpcionMicrositioContenidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OpcionMicrositioContenidoDto>> GetOpcion(
        [FromRoute] int id,
        [FromRoute] int idOpcion,
        CancellationToken cancellationToken)
    {
        ValidarId(id, nameof(id));
        ValidarId(idOpcion, nameof(idOpcion));
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (ReglasMicrositios.EsPrivado(id))
        {
            return Privado(id, idOpcion);
        }

        var opcion = await repositorio.ObtenerOpcionAsync(id, idOpcion, cancellationToken);
        if (opcion is null)
        {
            return NoEncontrado($"El micrositio {id} no tiene ninguna opción publicada con id {idOpcion}.", id, idOpcion);
        }

        return Ok(opcion);
    }

    private void ValidarId(int valor, string campo)
    {
        if (valor < ReglasMicrositios.IdMinimo)
        {
            ModelState.AddModelError(campo, $"Debe ser un entero mayor o igual que {ReglasMicrositios.IdMinimo}.");
        }
    }

    private ObjectResult NoEncontrado(string detalle, int id, int? idOpcion)
        => Problema(StatusCodes.Status404NotFound, "Micrositio no encontrado", detalle, id, idOpcion);

    private ObjectResult Privado(int id, int? idOpcion)
        => Problema(StatusCodes.Status403Forbidden, "Micrositio de acceso restringido",
            "Este micrositio solo está disponible, con identificación, en la web actual de la FEP.", id, idOpcion);

    /// <summary>ProblemDetails con la URL equivalente de la web actual (extensión <c>urlWebActual</c>).</summary>
    private ObjectResult Problema(int estado, string titulo, string detalle, int id, int? idOpcion)
    {
        var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, estado, titulo, detail: detalle);
        problema.Extensions["urlWebActual"] = WebActual.Url(media.Value, ReglasMicrositios.RutaWebActual(id, idOpcion));
        return new ObjectResult(problema) { StatusCode = estado };
    }
}
