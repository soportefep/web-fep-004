using System.Globalization;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Contracts.Licencias;
using FEP.Api.Data.Repositories.Licencias;
using FEP.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FEP.Api.Controllers.Licencias;

/// <summary>
/// Buscador público de licencias, "Busca tu licencia" (sustituye a <c>Licencias_Buscar.asp</c>, <c>/Licencias_Buscar</c>).
/// <para>
/// Devuelve datos de personas: sin caché (ni de servidor ni de navegador o proxies, porque la fecha de nacimiento viaja
/// en la URL) y con límite de búsquedas por IP (<see cref="LimiteBusquedaLicencias"/>).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/licencias")]
[EnableRateLimiting(LimiteBusquedaLicencias.Politica)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LicenciasController(ILicenciasRepository repositorio, TimeProvider reloj) : ControllerBase
{
    /// <summary>
    /// Licencias que cumplen los criterios (número de licencia, apellidos y nombre), ordenadas por apellidos; como mucho
    /// 100, con el total. Hay que indicar el nombre/apellidos, la fecha de nacimiento o ambos.
    /// </summary>
    /// <param name="tipo">
    /// <c>jugador</c> (por defecto), <c>tecnico</c>, <c>arbitro</c> o <c>silla-ruedas</c>; también las letras del ASP
    /// <c>J</c>, <c>T</c>, <c>A</c>, <c>S</c>.
    /// </param>
    /// <param name="federacion">
    /// Federación autonómica que expidió la licencia (1–22): obligatoria para jugador/a, opcional en silla de ruedas y no
    /// admitida en técnico/a ni juez-árbitro (sus licencias no se buscan por federación).
    /// </param>
    /// <param name="busqueda">
    /// Parte del nombre y/o apellidos (2–100 caracteres). Se parte por espacios y guiones: deben estar todos los términos
    /// (máx. 6), sin distinguir mayúsculas ni acentos.
    /// </param>
    /// <param name="fechaNacimiento">Fecha de nacimiento exacta, <c>aaaa-mm-dd</c> (desde 1900 hasta hoy).</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <response code="400">Criterios no válidos o sin nombre ni fecha de nacimiento.</response>
    /// <response code="429">Demasiadas búsquedas desde la misma IP (cabecera <c>Retry-After</c>).</response>
    [HttpGet]
    [ProducesResponseType(typeof(LicenciasBusquedaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LicenciasBusquedaDto>> Get(
        [FromQuery] string? tipo,
        [FromQuery] int? federacion,
        [FromQuery] string? busqueda,
        [FromQuery] string? fechaNacimiento,
        CancellationToken cancellationToken)
    {
        if (!ReglasLicencias.TryParseTipo(tipo, out var tipoLicencia))
        {
            ModelState.AddModelError(nameof(tipo), "Debe ser jugador, tecnico, arbitro o silla-ruedas (o J, T, A, S).");
        }
        else if (!ReglasLicencias.UsaFederacion(tipoLicencia) && federacion is not null)
        {
            ModelState.AddModelError(nameof(federacion), "Las licencias de técnico/a y de juez-árbitro no se buscan por federación (omítelo).");
        }
        else if (ReglasLicencias.FederacionObligatoria(tipoLicencia) && federacion is null)
        {
            ModelState.AddModelError(nameof(federacion), "Indica la federación autonómica que expidió la licencia.");
        }
        else if (federacion is < ReglasLicencias.FederacionMinima or > ReglasLicencias.FederacionMaxima)
        {
            ModelState.AddModelError(nameof(federacion),
                $"Debe estar entre {ReglasLicencias.FederacionMinima} y {ReglasLicencias.FederacionMaxima}.");
        }

        var terminos = ReglasLicencias.DividirTerminos(busqueda);
        if (busqueda?.Length > ReglasLicencias.LongitudMaximaBusqueda)
        {
            ModelState.AddModelError(nameof(busqueda), $"Máximo {ReglasLicencias.LongitudMaximaBusqueda} caracteres.");
        }
        else if (terminos.Count > ReglasLicencias.TerminosMaximos)
        {
            ModelState.AddModelError(nameof(busqueda), $"Máximo {ReglasLicencias.TerminosMaximos} palabras.");
        }
        else if (terminos.Count > 0 && terminos.Sum(t => t.Length) < ReglasLicencias.LongitudMinimaBusqueda)
        {
            ModelState.AddModelError(nameof(busqueda), $"Escribe al menos {ReglasLicencias.LongitudMinimaBusqueda} letras.");
        }

        DateOnly? fecha = null;
        if (!string.IsNullOrWhiteSpace(fechaNacimiento))
        {
            // Hoy en Madrid (la FEP), como el año en curso del desplegable del ASP.
            var hoy = DateOnly.FromDateTime(ReglasOrganizacionTorneos.AhoraEnMadrid(reloj));
            if (!DateOnly.TryParseExact(fechaNacimiento.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
            {
                ModelState.AddModelError(nameof(fechaNacimiento), "Usa el formato aaaa-mm-dd (por ejemplo, 1980-03-15).");
            }
            else if (f.Year < ReglasLicencias.AnioNacimientoMinimo || f > hoy)
            {
                ModelState.AddModelError(nameof(fechaNacimiento), $"Debe estar entre {ReglasLicencias.AnioNacimientoMinimo} y hoy.");
            }
            else
            {
                fecha = f;
            }
        }

        // Como el ASP ("Selecciona una fecha de nacimiento o un texto de búsqueda"), pero sin listar a todo el mundo.
        if (ModelState.IsValid && terminos.Count == 0 && fecha is null)
        {
            ModelState.AddModelError(nameof(busqueda), "Indica el nombre o los apellidos, la fecha de nacimiento o ambos.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var criterios = new BusquedaLicencias(tipoLicencia, federacion, terminos, fecha);
        return Ok(await repositorio.BuscarAsync(criterios, cancellationToken));
    }
}
