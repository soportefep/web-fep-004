using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Data.Repositories.Federacion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Competicion;

/// <summary>
/// Ranking de equipos absolutos: resultado de los Campeonatos Absolutos por equipos de cada temporada (sustituye a
/// <c>Ranking_Equipos_Absolutos.asp</c>, URL <c>/Ranking_Equipos_Absolutos</c>). Va en una ruta literal bajo
/// <c>/api/v1/competicion/rankings</c>, que tiene prioridad sobre <c>{clave}</c> de
/// <see cref="ClasificacionesRankingController"/>: sus puestos son equipos por temporada, no jugadores con licencia.
/// </summary>
[ApiController]
[Route("api/v1/competicion/rankings/" + ReglasRankingEquipos.Clave)]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class RankingEquiposController(
    IRankingEquiposRepository ranking,
    IFederacionesAutonomicasRepository federaciones,
    IMediaUrlBuilder mediaUrls,
    IOptions<MediaOptions> media) : ControllerBase
{
    /// <summary>
    /// Clasificación de los Campeonatos Absolutos por equipos: por temporada (de la más reciente a la más antigua),
    /// una lista por categoría (1.ª y 2.ª) y género (masculina y femenina) con el puesto, el equipo, su club (escudo y
    /// ficha), la federación autonómica (bandera) y la provincia. Sin <c>anio</c>, todas las temporadas (desde 2017,
    /// como el ASP; son ~250 equipos).
    /// </summary>
    /// <param name="anio">Temporada (1901–2100). Si no tiene clasificación, <c>temporadas</c> llega vacío.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Año fuera de rango o no numérico.</response>
    [HttpGet]
    [ProducesResponseType(typeof(RankingEquiposDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RankingEquiposDto>> Get([FromQuery] int? anio, CancellationToken cancellationToken)
    {
        if (anio is < ReglasCampeonatos.AnioMinimo or > ReglasCampeonatos.AnioMaximo)
        {
            ModelState.AddModelError(nameof(anio),
                $"Debe estar entre {ReglasCampeonatos.AnioMinimo} y {ReglasCampeonatos.AnioMaximo} (omítelo para ver todas las temporadas).");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var datos = await ranking.ObtenerAsync(anio, cancellationToken);
        var nombres = await NombresAsync(cancellationToken);

        return Ok(new RankingEquiposDto(
            ReglasRankingEquipos.Clave,
            ReglasRankingEquipos.Titulo,
            datos.Anios,
            anio,
            ReglasRankingEquipos.Temporadas(datos.Filas, f => Puesto(f, nombres))));
    }

    private PuestoEquipoDto Puesto(FilaRankingEquipo f, IReadOnlyDictionary<int, (string Territorio, string Nombre)> nombres)
    {
        var club = Texto.Limpio(f.Club);
        var escudo = club is null ? null : ReglasRankingEquipos.Imagen(club);

        FederacionEquipoDto? federacion = null;
        if (f.IdFederacion is { } id)
        {
            var conocida = nombres.TryGetValue(id, out var n);
            var abreviatura = Texto.Limpio(f.Abreviatura);
            var bandera = ReglasRankingEquipos.Imagen(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            federacion = new FederacionEquipoDto(
                id,
                conocida ? n.Territorio : abreviatura ?? $"Federación {id}",
                conocida ? n.Nombre : null,
                abreviatura,
                $"{ReglasRankingEquipos.CarpetaImagenes}/{bandera}",
                mediaUrls.Construir(ReglasRankingEquipos.CarpetaImagenes, bandera));
        }

        return new PuestoEquipoDto(
            f.Puesto,
            ReglasRankingEquipos.NombreEquipo(f.Equipo),
            club,
            club is null ? null : WebActual.Url(media.Value, ReglasRankingEquipos.RutaClub(club)),
            escudo is null ? null : $"{ReglasRankingEquipos.CarpetaImagenes}/{escudo}",
            escudo is null ? null : mediaUrls.Construir(ReglasRankingEquipos.CarpetaImagenes, escudo),
            federacion,
            Texto.Limpio(f.Provincia));
    }

    /// <summary>Territorio y nombre oficial por id (los mismos de federaciones.html: <c>Contenidos/federaciones-autonomicas.json</c>).</summary>
    private async Task<IReadOnlyDictionary<int, (string Territorio, string Nombre)>> NombresAsync(CancellationToken cancellationToken)
    {
        var contenido = await federaciones.ObtenerAsync(cancellationToken);
        return contenido.Federaciones
            .Where(f => !f.Nacional)
            .GroupBy(f => f.Id)
            .ToDictionary(g => g.Key, g => (g.First().Territorio, g.First().Nombre));
    }
}
