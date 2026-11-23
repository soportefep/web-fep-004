using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Competicion;

/// <summary>
/// Clasificación y normativa de cada ranking (sustituye a <c>Ranking_Absoluto.asp</c> con <c>CargarRnkAbs.asp</c> y
/// <c>CargarRnkAbsNombre.asp</c>, a <c>Ranking_Menores.asp</c> con <c>CargarRnkMen.asp</c> y a
/// <c>Ranking_Silla_Ruedas.asp</c> y a <c>Ranking_Veteranos.asp</c> con <c>CargarRnkVet.asp</c>; pensado para el resto de
/// <c>Ranking_*.asp</c>). El índice de rankings está en
/// <see cref="RankingsController"/> (<c>GET /api/v1/competicion/rankings</c>).
/// </summary>
[ApiController]
[Route("api/v1/competicion/rankings")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class ClasificacionesRankingController(
    IClasificacionesRankingRepository clasificaciones,
    INormativasRankingRepository normativas,
    IOptions<MediaOptions> media) : ControllerBase
{
    /// <summary>
    /// Clasificación del ranking: una lista por género (y categoría, si el ranking la tiene) con el total de
    /// jugadores, las pruebas que suman, la fecha de actualización, el PDF completo y los primeros puestos.
    /// Sin filtros devuelve el top <c>limite</c> de cada lista.
    /// </summary>
    /// <param name="clave">Ranking (<c>absoluto</c>, <c>menores</c>, <c>silla-ruedas</c>, <c>veteranos</c>; sin distinguir mayúsculas).</param>
    /// <param name="genero">
    /// <c>M</c>/<c>masculino</c> o <c>F</c>/<c>femenino</c> (<c>silla-ruedas</c> tiene una sola lista mixta: <c>X</c>).
    /// Sin él, todos los géneros del ranking.
    /// </param>
    /// <param name="categoria">
    /// Categoría (solo en rankings con categorías: en <c>menores</c>, <c>benjamin</c>, <c>alevin</c>, <c>infantil</c>,
    /// <c>cadete</c> o <c>junior</c>, sin distinguir mayúsculas; en <c>veteranos</c>, la edad mínima: <c>35</c>, <c>40</c>,
    /// <c>45</c>, <c>50</c>, <c>55</c> o <c>60</c>; el absoluto no tiene). Sin ella, todas.
    /// </param>
    /// <param name="licencia">
    /// Número de licencia exacto (1–2147483647), sin prefijo (en silla de ruedas, <c>1008</c> para "PS1008"). Se combina
    /// con los demás filtros.
    /// </param>
    /// <param name="busqueda">
    /// Nombre y/o apellidos (máx. 100 caracteres y 6 palabras): el nombre completo debe contener todas las palabras,
    /// sin distinguir mayúsculas ni acentos.
    /// </param>
    /// <param name="limite">Puestos máximos por lista (1–500, por defecto 100, como el TOP 100 del ASP).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Algún filtro no es válido para este ranking.</response>
    /// <response code="404">Ranking sin clasificación migrada (<c>urlWebActual</c> si existe en la web actual).</response>
    [HttpGet("{clave}")]
    [ProducesResponseType(typeof(ClasificacionRankingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ClasificacionRankingDto>> Get(
        [FromRoute] string clave,
        [FromQuery] string? genero,
        [FromQuery] string? categoria,
        [FromQuery] int? licencia,
        [FromQuery] string? busqueda,
        [FromQuery] int? limite,
        CancellationToken cancellationToken)
    {
        var ranking = ReglasClasificaciones.Buscar(clave);
        if (ranking is null)
        {
            return RankingNoEncontrado();
        }

        if (!ReglasClasificaciones.TryNormalizarGenero(genero, out var generoNormalizado)
            || (generoNormalizado is not null && !ranking.Generos.Contains(generoNormalizado)))
        {
            ModelState.AddModelError(nameof(genero), $"Debe ser uno de: {string.Join(", ", ranking.Generos)} (omítelo para ver todos).");
        }

        var categoriaNormalizada = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim().ToLowerInvariant();
        if (categoriaNormalizada is not null && !ranking.Categorias.Contains(categoriaNormalizada))
        {
            ModelState.AddModelError(nameof(categoria), ranking.Categorias.Count == 0
                ? "Este ranking no tiene categorías."
                : $"Debe ser una de: {string.Join(", ", ranking.Categorias)}.");
        }

        if (licencia is < 1)
        {
            ModelState.AddModelError(nameof(licencia), "Debe ser un número de licencia positivo.");
        }

        var terminos = ReglasClasificaciones.Terminos(busqueda);
        if (busqueda?.Length > ReglasClasificaciones.LongitudMaximaBusqueda)
        {
            ModelState.AddModelError(nameof(busqueda), $"Máximo {ReglasClasificaciones.LongitudMaximaBusqueda} caracteres.");
        }
        else if (terminos.Count > ReglasClasificaciones.TerminosMaximos)
        {
            ModelState.AddModelError(nameof(busqueda), $"Máximo {ReglasClasificaciones.TerminosMaximos} palabras.");
        }

        if (limite is < 1 or > ReglasClasificaciones.LimiteMaximo)
        {
            ModelState.AddModelError(nameof(limite), $"Debe estar entre 1 y {ReglasClasificaciones.LimiteMaximo}.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var criterios = new CriteriosClasificacion(
            generoNormalizado,
            categoriaNormalizada,
            licencia,
            terminos,
            limite ?? ReglasClasificaciones.LimitePorDefecto);

        return Ok(await clasificaciones.ObtenerAsync(ranking, criterios, cancellationToken));
    }

    /// <summary>
    /// Normativa de cálculo del ranking (introducción, sistema de puntuación y tablas de puntos), en apartados.
    /// </summary>
    /// <param name="clave">Ranking (<c>absoluto</c>; sin distinguir mayúsculas).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="404">Ranking sin normativa publicada en la API (<c>urlWebActual</c>: página en la web actual).</response>
    [HttpGet("{clave}/normativa")]
    [ProducesResponseType(typeof(NormativaRankingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<NormativaRankingDto>> GetNormativa([FromRoute] string clave, CancellationToken cancellationToken)
    {
        var ranking = ReglasClasificaciones.Buscar(clave);
        if (ranking is null)
        {
            return RankingNoEncontrado();
        }

        var normativa = await normativas.ObtenerAsync(ranking, cancellationToken);
        if (normativa is null)
        {
            var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound,
                "Normativa no publicada", detail: $"La normativa del ranking {ranking.Clave} todavía no está disponible en la API.");
            problema.Extensions["urlWebActual"] = WebActual.Url(media.Value, ranking.PaginaWebActual);
            return NotFound(problema);
        }

        return Ok(normativa);
    }

    /// <summary>404 para rankings sin clasificación migrada (las claves del índice que aún siguen en la web actual).</summary>
    /// <remarks>No repite la clave pedida en la respuesta.</remarks>
    private NotFoundObjectResult RankingNoEncontrado()
    {
        var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound,
            "Ranking no disponible",
            detail: "El ranking no existe o su clasificación todavía no está disponible en la API. " +
                    $"Disponibles: {string.Join(", ", ReglasClasificaciones.Rankings.Keys)}.");
        return NotFound(problema);
    }
}
