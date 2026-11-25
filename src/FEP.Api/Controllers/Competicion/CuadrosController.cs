using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Competicion;

/// <summary>
/// Cuadros de los torneos (sustituye a <c>Previo.asp</c>, <c>PrevioEquipos.asp</c>, <c>Cuadro*.asp</c>,
/// <c>CuadroPrevia*.asp</c>, <c>CuadroPrePrevia*.asp</c>, <c>CuadroEquipos*.asp</c>, <c>Liguilla*.asp</c> y
/// <c>EquiposEnfrentamientos.asp</c>). Los parámetros van en la ruta (el modo prototipo del front no admite query).
/// </summary>
[ApiController]
[Route("api/v1/competicion/cuadros")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class CuadrosController(ICuadrosRepository repositorio, IOptions<MediaOptions> media) : ControllerBase
{
    /// <summary>Cuadros de un torneo (género, categoría, nivel y cuadro, con su publicación).</summary>
    /// <param name="idTorneo"><c>SFEP_Torneos.IdPrueba</c>.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Id no válido.</response>
    /// <response code="404">El torneo no existe, está borrado o no es visible (<c>urlWebActual</c>: listado de campeonatos).</response>
    [HttpGet("{idTorneo}")]
    [ProducesResponseType(typeof(CuadrosTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CuadrosTorneoDto>> Listar([FromRoute] int idTorneo, CancellationToken cancellationToken)
    {
        if (idTorneo < ReglasCuadros.IdMinimo)
        {
            ModelState.AddModelError(nameof(idTorneo), $"Debe ser un número entero mayor o igual que {ReglasCuadros.IdMinimo} (IdPrueba del torneo).");
            return ValidationProblem(ModelState);
        }

        var consulta = await repositorio.ListarAsync(idTorneo, cancellationToken);
        return consulta.Datos is { } datos ? Ok(datos) : TorneoNoEncontrado(idTorneo);
    }

    /// <summary>
    /// Un cuadro: participantes de cada ronda, resultados, horarios, cabezas de serie, clasificación (liguillas) y el
    /// resto de cuadros del torneo. Si la categoría no está publicada (<c>Ver… = 'N'</c>), 200 con <c>publicado: false</c>
    /// y las listas vacías.
    /// </summary>
    /// <param name="idTorneo"><c>SFEP_Torneos.IdPrueba</c> (<c>IdTorneo</c> del ASP).</param>
    /// <param name="genero"><c>M</c> | <c>F</c> | <c>X</c>.</param>
    /// <param name="categoria"><c>Tipo</c> del desglose (<c>Cat</c> del ASP): <c>ABSOLUTO</c>, <c>Cat50</c>…</param>
    /// <param name="nivel">1–10, 21 (previa), 31 (pre-previa).</param>
    /// <param name="cuadro"><c>Final</c>, <c>Consola</c>, <c>Grupo A</c> (codificado), <c>Descenso</c>…</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Parámetros no válidos.</response>
    /// <response code="404">
    /// El torneo no existe, está borrado o no es visible (<c>urlWebActual</c>: listado), o el cuadro no tiene filas
    /// (<c>urlWebActual</c>: el cuadro en la web actual).
    /// </response>
    [HttpGet("{idTorneo}/{genero}/{categoria}/{nivel}/{cuadro}")]
    [ProducesResponseType(typeof(CuadroTorneoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CuadroTorneoDto>> Get(
        [FromRoute] int idTorneo,
        [FromRoute] string genero,
        [FromRoute] string categoria,
        [FromRoute] int nivel,
        [FromRoute] string cuadro,
        CancellationToken cancellationToken)
    {
        if (Validar(idTorneo, genero, categoria, nivel, cuadro) is not { } clave)
        {
            return ValidationProblem(ModelState);
        }

        var consulta = await repositorio.ObtenerAsync(clave, cancellationToken);
        if (consulta.Datos is { } datos)
        {
            return Ok(datos);
        }

        return consulta.Torneo is { } torneo
            ? NoEncontrado("Cuadro no encontrado",
                $"El torneo {idTorneo} no tiene el cuadro {clave.Cuadro} de {clave.Categoria} ({clave.Genero}, nivel {clave.Nivel}).",
                ReglasCuadros.RutaWebActual(clave, torneo.Tipo == "equipos"))
            : TorneoNoEncontrado(idTorneo);
    }

    /// <summary>Partidos de un enfrentamiento entre equipos (jugadores, sets y observaciones).</summary>
    /// <param name="idTorneo"><c>SFEP_Torneos.IdPrueba</c>.</param>
    /// <param name="genero"><c>M</c> | <c>F</c> | <c>X</c>.</param>
    /// <param name="categoria">Categoría.</param>
    /// <param name="nivel">Nivel.</param>
    /// <param name="cuadro">Cuadro.</param>
    /// <param name="avance">Ronda del enfrentamiento (0–64).</param>
    /// <param name="posicion">Número de partido dentro de la ronda (1–256).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Parámetros no válidos.</response>
    /// <response code="404">El torneo no existe o no es por equipos, el cuadro no está publicado o el enfrentamiento no tiene detalle.</response>
    [HttpGet("{idTorneo}/{genero}/{categoria}/{nivel}/{cuadro}/enfrentamientos/{avance}/{posicion}")]
    [ProducesResponseType(typeof(EnfrentamientoEquiposDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EnfrentamientoEquiposDto>> GetEnfrentamiento(
        [FromRoute] int idTorneo,
        [FromRoute] string genero,
        [FromRoute] string categoria,
        [FromRoute] int nivel,
        [FromRoute] string cuadro,
        [FromRoute] int avance,
        [FromRoute] int posicion,
        CancellationToken cancellationToken)
    {
        var clave = Validar(idTorneo, genero, categoria, nivel, cuadro);
        if (avance is < 0 or > ReglasCuadros.AvanceMaximo)
        {
            ModelState.AddModelError(nameof(avance), $"Debe estar entre 0 y {ReglasCuadros.AvanceMaximo}.");
        }

        if (posicion is < 1 or > ReglasCuadros.PosicionMaxima)
        {
            ModelState.AddModelError(nameof(posicion), $"Debe estar entre 1 y {ReglasCuadros.PosicionMaxima}.");
        }

        if (clave is null || !ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var consulta = await repositorio.ObtenerEnfrentamientoAsync(clave, avance, posicion, cancellationToken);
        if (consulta.Datos is { } datos)
        {
            return Ok(datos);
        }

        return consulta.Torneo is { } torneo
            ? NoEncontrado("Enfrentamiento no encontrado",
                $"No hay detalle del enfrentamiento {posicion} de la ronda {avance} en ese cuadro.",
                ReglasCuadros.RutaWebActual(clave, torneo.Tipo == "equipos"))
            : TorneoNoEncontrado(idTorneo);
    }

    /// <summary>Valida y normaliza los parámetros de un cuadro; si alguno no vale, lo anota en <c>ModelState</c>.</summary>
    private ClaveCuadro? Validar(int idTorneo, string genero, string categoria, int nivel, string cuadro)
    {
        if (idTorneo < ReglasCuadros.IdMinimo)
        {
            ModelState.AddModelError(nameof(idTorneo), $"Debe ser un número entero mayor o igual que {ReglasCuadros.IdMinimo} (IdPrueba del torneo).");
        }

        var g = ReglasCuadros.Genero(genero);
        if (g is null)
        {
            ModelState.AddModelError(nameof(genero), "Debe ser M, F o X.");
        }

        var cat = ReglasCuadros.TextoRuta(categoria);
        if (cat is null)
        {
            ModelState.AddModelError(nameof(categoria),
                $"Máximo {ReglasCuadros.LongitudMaxima} caracteres: letras, cifras, espacios, '.', '-', '+' o '_'.");
        }

        if (nivel is < 1 or > ReglasCuadros.NivelMaximo)
        {
            ModelState.AddModelError(nameof(nivel), $"Debe estar entre 1 y {ReglasCuadros.NivelMaximo}.");
        }

        var cua = ReglasCuadros.TextoRuta(cuadro);
        if (cua is null)
        {
            ModelState.AddModelError(nameof(cuadro),
                $"Máximo {ReglasCuadros.LongitudMaxima} caracteres: letras, cifras, espacios, '.', '-', '+' o '_'.");
        }

        return ModelState.IsValid ? new ClaveCuadro(idTorneo, g!, cat!, nivel, cua!) : null;
    }

    private ObjectResult TorneoNoEncontrado(int idTorneo)
        => NoEncontrado("Torneo no encontrado", $"No hay ningún torneo publicado con id {idTorneo}.", "Campeonatos");

    private ObjectResult NoEncontrado(string titulo, string detalle, string rutaWebActual)
    {
        var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, titulo, detail: detalle);
        problema.Extensions["urlWebActual"] = WebActual.Url(media.Value, rutaWebActual);
        return NotFound(problema);
    }
}
