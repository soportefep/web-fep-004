using FEP.Api.Contracts.Asistente;
using FEP.Api.Infrastructure.Asistente;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FEP.Api.Controllers.Asistente;

/// <summary>
/// Asistente virtual de la web (burbuja de chat): responde preguntas sobre la normativa, las circulares, los estatutos
/// y los rankings con los documentos de <c>Contenidos/asistente</c> y los datos públicos de la API. Nuevo: no existía
/// en el ASP.
/// </summary>
[ApiController]
[Route("api/v1/asistente")]
public sealed class AsistenteController(IAsistente asistente, IOptions<AsistenteOptions> opciones, ILogger<AsistenteController> logger)
    : ControllerBase
{
    /// <summary>
    /// Responde a la última pregunta de la conversación. Sin estado: el navegador envía el historial en cada petición.
    /// </summary>
    /// <response code="400">Conversación no válida (vacía, demasiado larga, roles o textos incorrectos).</response>
    /// <response code="429">Demasiadas preguntas seguidas desde la misma dirección (<c>Retry-After</c>, <c>urlContacto</c>).</response>
    /// <response code="503">Asistente no disponible ahora (sin configurar, presupuesto agotado o servicio caído): <c>urlContacto</c>.</response>
    [HttpPost("chat")]
    [EnableRateLimiting(LimitesAsistente.Politica)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RespuestaChatDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RespuestaChatDto>> Chat([FromBody] PeticionChatDto peticion, CancellationToken cancellationToken)
    {
        Validar(peticion);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            return Ok(await asistente.ResponderAsync(peticion, cancellationToken));
        }
        catch (AsistenteNoDisponibleException ex)
        {
            logger.LogWarning(ex, "Asistente no disponible: {Motivo}", ex.Message);
            var problema = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status503ServiceUnavailable,
                "Asistente no disponible",
                detail: "El asistente no está disponible en este momento. Puedes escribir a la FEP desde el formulario de contacto.");
            problema.Extensions["urlContacto"] = opciones.Value.UrlContacto;
            return StatusCode(StatusCodes.Status503ServiceUnavailable, problema);
        }
    }

    private void Validar(PeticionChatDto? peticion)
    {
        var mensajes = peticion?.Mensajes;
        if (mensajes is null || mensajes.Count == 0)
        {
            ModelState.AddModelError("mensajes", "Envía al menos un mensaje.");
            return;
        }

        if (mensajes.Count > ReglasChat.MensajesMaximos)
        {
            ModelState.AddModelError("mensajes", $"Máximo {ReglasChat.MensajesMaximos} mensajes: envía solo los últimos.");
        }

        for (var i = 0; i < mensajes.Count; i++)
        {
            var m = mensajes[i];
            var esperado = (mensajes.Count - 1 - i) % 2 == 0 ? ReglasChat.RolUsuario : ReglasChat.RolAsistente;
            if (m is null || m.Rol != esperado)
            {
                ModelState.AddModelError($"mensajes[{i}].rol",
                    $"Los mensajes alternan '{ReglasChat.RolUsuario}' y '{ReglasChat.RolAsistente}' y el último es del usuario.");
                break;
            }

            if (string.IsNullOrWhiteSpace(m.Texto))
            {
                ModelState.AddModelError($"mensajes[{i}].texto", "El mensaje está vacío.");
            }
            else if (m.Rol == ReglasChat.RolUsuario && m.Texto.Length > ReglasChat.LongitudMaximaPregunta)
            {
                ModelState.AddModelError($"mensajes[{i}].texto", $"Máximo {ReglasChat.LongitudMaximaPregunta} caracteres por pregunta.");
            }
        }

        if (peticion!.Idioma is { } idioma && !ReglasChat.Idiomas.Contains(idioma))
        {
            ModelState.AddModelError("idioma", "Debe ser 'es' o 'en'.");
        }
    }
}
