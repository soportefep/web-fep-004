using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Competicion;

/// <summary>
/// Convocatoria para organizar las pruebas del calendario oficial FEP (sustituye a <c>Organizacion_Torneos.asp</c>,
/// <c>Organizacion_FueraPlazo.asp</c>, <c>cargarfechastorneo.asp</c>, <c>comboProvincias.asp</c> y
/// <c>comboclubesLicitacion.asp</c>). Solo lectura: la solicitud se sigue enviando a la web actual.
/// </summary>
[ApiController]
[Route("api/v1/competicion/organizacion-torneos")]
[OutputCache(PolicyName = CachePolicies.ContenidoPublico)]
[ResponseCache(Duration = CachePolicies.SegundosContenidoPublico, Location = ResponseCacheLocation.Any)]
public sealed class OrganizacionTorneosController(
    IOrganizacionTorneosRepository repositorio,
    IContenidoOrganizacionTorneosRepository contenido,
    IMediaUrlBuilder mediaUrl,
    IOptions<MediaOptions> media,
    TimeProvider reloj) : ControllerBase
{
    /// <summary>Ruta de la web actual que recibe el formulario si el contenido no indica otra.</summary>
    public const string EnvioPorDefecto = "Organizacion_P0.asp";

    /// <summary>
    /// Estado y plazo de la convocatoria, documentos, pasos del proceso, pruebas que se pueden solicitar (con fechas
    /// y fianza) y comunidades con sus provincias para el formulario. Con la convocatoria cerrada (como
    /// <c>Organizacion_FueraPlazo.asp</c>) pruebas y comunidades llegan vacías; los documentos se siguen publicando.
    /// </summary>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    [HttpGet]
    [ProducesResponseType(typeof(OrganizacionTorneosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrganizacionTorneosDto>> Get(CancellationToken cancellationToken)
    {
        var texto = await contenido.ObtenerAsync(cancellationToken);
        var abierta = ReglasOrganizacionTorneos.Abierta(texto.FechaLimite, reloj);
        var datos = await repositorio.ObtenerAsync(texto.Temporada, cancellationToken);

        var documentos = (texto.Documentos ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.Clave) && datos.Archivos.ContainsKey(d.Clave))
            .Select(d =>
            {
                var archivo = $"docs/{datos.Archivos[d.Clave]}";
                return new DocumentoOrganizacionTorneosDto(d.Clave, d.Titulo, d.Descripcion, archivo, mediaUrl.Construir(archivo));
            })
            .ToArray();

        var envio = string.IsNullOrWhiteSpace(texto.Envio) ? EnvioPorDefecto : texto.Envio.Trim();
        var envioUrl = Uri.TryCreate(envio, UriKind.Absolute, out var absoluta)
            ? absoluta.AbsoluteUri
            : WebActual.Url(media.Value, envio);

        return Ok(new OrganizacionTorneosDto(
            texto.Temporada,
            abierta,
            texto.FechaLimite,
            texto.Titulo.Trim(),
            texto.Introduccion ?? [],
            texto.AvisoCerrado,
            documentos,
            (texto.Pasos ?? []).OrderBy(p => p.Orden).ToArray(),
            ReglasOrganizacionTorneos.Grupos.Select(g => g.Grupo).ToArray(),
            abierta ? datos.Torneos : [],
            abierta ? datos.Comunidades : [],
            texto.ProteccionDatos ?? [],
            envioUrl,
            texto.Actualizado));
    }

    /// <summary>
    /// Clubes que pueden ser sede en una provincia (no de baja ni independientes), por nombre: desplegable "Club
    /// Sede" del formulario. Si el club no está, el formulario admite escribirlo (<c>club2</c>).
    /// </summary>
    /// <param name="provincia">Id de la provincia (<c>SFEP_Provincias.IdProvincia</c>, 1–999; obligatorio).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Provincia ausente, no numérica o fuera de rango.</response>
    [HttpGet("clubes")]
    [ProducesResponseType(typeof(IReadOnlyList<ClubSedeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ClubSedeDto>>> GetClubes([FromQuery] int? provincia, CancellationToken cancellationToken)
    {
        if (provincia is not { } id || id is < ReglasOrganizacionTorneos.ProvinciaMinima or > ReglasOrganizacionTorneos.ProvinciaMaxima)
        {
            ModelState.AddModelError(nameof(provincia),
                $"Obligatorio: id de provincia entre {ReglasOrganizacionTorneos.ProvinciaMinima} y {ReglasOrganizacionTorneos.ProvinciaMaxima}.");
            return ValidationProblem(ModelState);
        }

        return Ok(await repositorio.ListarClubesAsync(id, cancellationToken));
    }
}
