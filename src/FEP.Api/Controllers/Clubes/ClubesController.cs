using FEP.Api.Contracts.Clubes;
using FEP.Api.Data.Repositories.Clubes;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Clubes;

/// <summary>Buscador de clubes federados (sustituye a <c>Clubes.asp</c>).</summary>
[ApiController]
[Route("api/v1/clubes")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class ClubesController(IClubesRepository repositorio, TimeProvider reloj) : ControllerBase
{
    /// <summary>
    /// Clubes con la licencia de club del año en curso renovada, ordenados por provincia, localidad y
    /// nombre. Sin filtros devuelve todos (paginados).
    /// </summary>
    /// <param name="federacion">Id de la federación autonómica (1–22, ver <c>/api/v1/clubes/filtros</c>).</param>
    /// <param name="provincia">Id de la provincia (1–999). Se combina con <paramref name="federacion"/>.</param>
    /// <param name="busqueda">
    /// Texto del nombre del club (máx. 100 caracteres). Se parte por espacios y guiones: el nombre debe
    /// contener todos los términos (máx. 8), sin distinguir mayúsculas ni acentos.
    /// </param>
    /// <param name="pagina">Página, desde 1 (por defecto 1; máx. 1000).</param>
    /// <param name="limite">Clubes por página (1–100, por defecto 50).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ClubesPaginaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ClubesPaginaDto>> Get(
        [FromQuery] int? federacion,
        [FromQuery] int? provincia,
        [FromQuery] string? busqueda,
        [FromQuery] int? pagina,
        [FromQuery] int? limite,
        CancellationToken cancellationToken)
    {
        if (federacion is < BusquedaClubes.FederacionMinima or > BusquedaClubes.FederacionMaxima)
        {
            ModelState.AddModelError(nameof(federacion),
                $"Debe estar entre {BusquedaClubes.FederacionMinima} y {BusquedaClubes.FederacionMaxima} (omítelo para no filtrar).");
        }

        if (provincia is < BusquedaClubes.ProvinciaMinima or > BusquedaClubes.ProvinciaMaxima)
        {
            ModelState.AddModelError(nameof(provincia),
                $"Debe estar entre {BusquedaClubes.ProvinciaMinima} y {BusquedaClubes.ProvinciaMaxima} (omítelo para no filtrar).");
        }

        var terminos = BusquedaClubes.DividirTerminos(busqueda);
        if (busqueda?.Length > BusquedaClubes.LongitudMaximaBusqueda)
        {
            ModelState.AddModelError(nameof(busqueda), $"Máximo {BusquedaClubes.LongitudMaximaBusqueda} caracteres.");
        }
        else if (terminos.Count > BusquedaClubes.TerminosMaximos)
        {
            ModelState.AddModelError(nameof(busqueda), $"Máximo {BusquedaClubes.TerminosMaximos} palabras.");
        }

        if (pagina is < 1 or > BusquedaClubes.PaginaMaxima)
        {
            ModelState.AddModelError(nameof(pagina), $"Debe estar entre 1 y {BusquedaClubes.PaginaMaxima}.");
        }

        if (limite is < 1 or > BusquedaClubes.LimiteMaximo)
        {
            ModelState.AddModelError(nameof(limite), $"Debe estar entre 1 y {BusquedaClubes.LimiteMaximo}.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var criterios = new BusquedaClubes(
            federacion,
            provincia,
            terminos,
            pagina ?? 1,
            limite ?? BusquedaClubes.LimitePorDefecto);

        // Año en curso según la hora local del servidor, como year(now()) en el ASP.
        var anio = reloj.GetLocalNow().Year;
        return Ok(await repositorio.BuscarAsync(criterios, anio, cancellationToken));
    }

    /// <summary>
    /// Ficha del club (sustituye a <c>Club.asp?id=</c>): contacto, licencias de jugador/a del año en curso
    /// (autonómicas y de competición nacional) y torneos realizados. Solo lectura.
    /// </summary>
    /// <param name="id"><c>IdClub</c>: solo dígitos, con sus ceros a la izquierda (p. ej. <c>0118003</c>).</param>
    /// <param name="fichas">Repositorio de fichas de club.</param>
    /// <param name="media">Opciones de medios (raíz de la web actual para <c>urlWebActual</c>).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">El id no son solo dígitos (máx. 10).</response>
    /// <response code="404">No existe el club (<c>motivo</c> = <c>club-no-encontrado</c>). Incluye <c>urlWebActual</c>.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ClubDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ClubDetalleDto>> GetFicha(
        [FromRoute] string id,
        [FromServices] IFichaClubRepository fichas,
        [FromServices] IOptions<MediaOptions> media,
        CancellationToken cancellationToken)
    {
        if (!ReglasFichaClub.IdValido(id))
        {
            ModelState.AddModelError(nameof(id), $"Debe ser un código de club numérico (máx. {ReglasFichaClub.LongitudMaximaId} cifras).");
            return ValidationProblem(ModelState);
        }

        // Año en curso según la hora local del servidor, como year(now()) en el ASP (igual que el buscador).
        var ficha = await fichas.ObtenerAsync(id, reloj.GetLocalNow().Year, cancellationToken);
        if (ficha is not null)
        {
            return Ok(ficha);
        }

        var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound,
            "Club no encontrado", detail: $"No existe ningún club con el código {id}.");
        problema.Extensions["motivo"] = "club-no-encontrado";
        problema.Extensions["urlWebActual"] = WebActual.Url(media.Value, ReglasFichaClub.RutaWebActual(id));
        return NotFound(problema);
    }

    /// <summary>Federaciones (con las provincias de su comunidad) y provincias para los desplegables.</summary>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    [HttpGet("filtros")]
    [ProducesResponseType(typeof(ClubesFiltrosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ClubesFiltrosDto>> GetFiltros(CancellationToken cancellationToken)
        => Ok(await repositorio.ObtenerFiltrosAsync(cancellationToken));
}
