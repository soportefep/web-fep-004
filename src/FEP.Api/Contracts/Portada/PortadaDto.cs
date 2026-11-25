using FEP.Api.Contracts.Avisos;
using FEP.Api.Contracts.Banners;
using FEP.Api.Contracts.Formacion;
using FEP.Api.Contracts.Multimedia;
using FEP.Api.Contracts.Navegacion;
using FEP.Api.Contracts.Noticias;
using FEP.Api.Contracts.Patrocinadores;

namespace FEP.Api.Contracts.Portada;

/// <summary>Todo el contenido dinámico de la home en una sola respuesta (equivale a lo que pintaba index.asp).</summary>
/// <param name="Menu">Menú principal anidado (= <c>GET /api/v1/menu</c>).</param>
/// <param name="RedesSociales">Redes sociales (= <c>GET /api/v1/redes-sociales</c>).</param>
/// <param name="NoticiasDestacadas">Hasta 6 noticias destacadas (= <c>GET /api/v1/noticias/destacadas</c>).</param>
/// <param name="EventosDestacados">Banners tipo evento (= <c>GET /api/v1/eventos/destacados</c>).</param>
/// <param name="Convocatorias">Formación con inscripción abierta de los tres grupos (= <c>GET /api/v1/formacion/convocatorias</c>).</param>
/// <param name="Avisos">Avisos (= <c>GET /api/v1/avisos</c>).</param>
/// <param name="BannersDestacados">Bloques de la barra lateral (= <c>GET /api/v1/banners?tipo=destacado</c>).</param>
/// <param name="BannersInteres">Enlaces de interés del pie (= <c>GET /api/v1/banners?tipo=interes</c>).</param>
/// <param name="Patrocinadores">Patrocinadores (= <c>GET /api/v1/patrocinadores</c>).</param>
/// <param name="UltimasFotos">Últimas 3 fotos (= <c>GET /api/v1/multimedia/fotos/ultimas?limite=3</c>).</param>
public sealed record PortadaDto(
    IReadOnlyList<MenuItemDto> Menu,
    IReadOnlyList<RedSocialDto> RedesSociales,
    IReadOnlyList<NoticiaDestacadaDto> NoticiasDestacadas,
    IReadOnlyList<BannerDto> EventosDestacados,
    IReadOnlyList<ConvocatoriaDto> Convocatorias,
    IReadOnlyList<AvisoDto> Avisos,
    IReadOnlyList<BannerDto> BannersDestacados,
    IReadOnlyList<BannerDto> BannersInteres,
    IReadOnlyList<PatrocinadorDto> Patrocinadores,
    IReadOnlyList<FotoDto> UltimasFotos);
