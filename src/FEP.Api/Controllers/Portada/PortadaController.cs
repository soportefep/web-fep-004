using FEP.Api.Contracts.Banners;
using FEP.Api.Contracts.Portada;
using FEP.Api.Controllers.Multimedia;
using FEP.Api.Data.Repositories.Avisos;
using FEP.Api.Data.Repositories.Banners;
using FEP.Api.Data.Repositories.Formacion;
using FEP.Api.Data.Repositories.Multimedia;
using FEP.Api.Data.Repositories.Navegacion;
using FEP.Api.Data.Repositories.Noticias;
using FEP.Api.Data.Repositories.Patrocinadores;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FEP.Api.Controllers.Portada;

/// <summary>Agregado de la home: todo lo dinámico de la portada en una llamada.</summary>
[ApiController]
[Route("api/v1/portada")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class PortadaController(
    IMenuRepository menu,
    IRedesSocialesRepository redesSociales,
    INoticiasRepository noticias,
    IBannersRepository banners,
    IConvocatoriasRepository convocatorias,
    IAvisosRepository avisos,
    IPatrocinadoresRepository patrocinadores,
    IFotosRepository fotos,
    TimeProvider reloj) : ControllerBase
{
    /// <summary>
    /// Menú, redes sociales, noticias destacadas, eventos, convocatorias de formación abiertas,
    /// avisos, banners (destacados e interés), patrocinadores y últimas fotos.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PortadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PortadaDto>> Get(CancellationToken cancellationToken)
    {
        var ahora = reloj.GetLocalNow().DateTime;

        // Cada repositorio abre su propia conexión del pool, así que las consultas pueden ir en paralelo.
        var tMenu = menu.ObtenerAsync(cancellationToken);
        var tRedes = redesSociales.ObtenerAsync(cancellationToken);
        var tNoticias = noticias.ObtenerDestacadasAsync(NoticiasRepository.LimitePortada, cancellationToken);
        var tEventos = banners.ObtenerPorTipoAsync(TipoBanner.Evento, cancellationToken);
        var tConvocatorias = convocatorias.ObtenerAbiertasAsync(null, ahora, cancellationToken);
        var tAvisos = avisos.ObtenerAsync(cancellationToken);
        var tDestacados = banners.ObtenerPorTipoAsync(TipoBanner.Destacado, cancellationToken);
        var tInteres = banners.ObtenerPorTipoAsync(TipoBanner.Interes, cancellationToken);
        var tPatrocinadores = patrocinadores.ObtenerAsync(cancellationToken);
        var tFotos = fotos.ObtenerUltimasAsync(FotosController.LimitePorDefecto, cancellationToken);

        await Task.WhenAll(tMenu, tRedes, tNoticias, tEventos, tConvocatorias, tAvisos, tDestacados, tInteres, tPatrocinadores, tFotos);

        return Ok(new PortadaDto(
            await tMenu,
            await tRedes,
            await tNoticias,
            await tEventos,
            await tConvocatorias,
            await tAvisos,
            await tDestacados,
            await tInteres,
            await tPatrocinadores,
            await tFotos));
    }
}
