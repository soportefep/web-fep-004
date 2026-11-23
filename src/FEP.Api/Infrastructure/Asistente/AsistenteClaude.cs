using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using FEP.Api.Contracts.Asistente;
using FEP.Api.Contracts.Competicion;
using FEP.Api.Data.Repositories.Asistente;
using Microsoft.Extensions.Options;

namespace FEP.Api.Infrastructure.Asistente;

/// <summary>Responde a una conversación del chat de la web.</summary>
public interface IAsistente
{
    /// <summary>Respuesta a la última pregunta de <paramref name="peticion"/> (ya validada).</summary>
    /// <exception cref="AsistenteNoDisponibleException">Sin clave configurada, sin presupuesto o el servicio no responde.</exception>
    Task<RespuestaChatDto> ResponderAsync(PeticionChatDto peticion, CancellationToken cancellationToken);
}

/// <summary>El asistente no puede responder ahora (el chat muestra el formulario de contacto).</summary>
public sealed class AsistenteNoDisponibleException(string motivo, Exception? interna = null) : Exception(motivo, interna);

/// <summary>
/// Asistente con Claude (API de Anthropic, SDK oficial) y las herramientas de <see cref="HerramientasAsistente"/>.
/// Bucle de herramientas propio y sin estado: cada petición trae su historial.
/// <list type="bullet">
/// <item>Instrucciones + catálogo + herramientas forman un prefijo estable que se cachea (abarata cada pregunta).</item>
/// <item>Respaldo en el servidor ante rechazos del filtro de seguridad (<c>fallbacks: "default"</c>).</item>
/// <item>Esfuerzo bajo (<see cref="AsistenteOptions.Esfuerzo"/>): respuestas de chat rápidas.</item>
/// </list>
/// </summary>
public sealed class AsistenteClaude : IAsistente
{
    /// <summary>Beta del respaldo en el servidor ante rechazos.</summary>
    private const string BetaRespaldo = "server-side-fallback-2026-07-01";

    private readonly AsistenteOptions _opciones;
    private readonly AnthropicClient? _cliente;
    private readonly HerramientasAsistente _herramientas;
    private readonly PresupuestoAsistente _presupuesto;
    private readonly TimeProvider _reloj;
    private readonly ILogger<AsistenteClaude> _logger;
    private readonly string _instrucciones;
    private readonly List<BetaToolUnion> _definiciones;

    public AsistenteClaude(
        IOptions<AsistenteOptions> opciones,
        HerramientasAsistente herramientas,
        ICorpusNormativa corpus,
        PresupuestoAsistente presupuesto,
        TimeProvider reloj,
        ILogger<AsistenteClaude> logger)
    {
        _opciones = opciones.Value;
        _herramientas = herramientas;
        _presupuesto = presupuesto;
        _reloj = reloj;
        _logger = logger;

        var clave = string.IsNullOrWhiteSpace(_opciones.ApiKey)
            ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            : _opciones.ApiKey;
        _cliente = string.IsNullOrWhiteSpace(clave) ? null : new AnthropicClient { ApiKey = clave };

        _instrucciones = InstruccionesAsistente.Construir(corpus.Documentos);
        _definiciones = herramientas.Definiciones.Select(ComoHerramienta).ToList();
    }

    /// <inheritdoc />
    public async Task<RespuestaChatDto> ResponderAsync(PeticionChatDto peticion, CancellationToken cancellationToken)
    {
        if (_cliente is null)
        {
            throw new AsistenteNoDisponibleException("Falta la clave de la API de Anthropic (Asistente:ApiKey).");
        }

        if (!_presupuesto.HayPresupuesto())
        {
            throw new AsistenteNoDisponibleException("Se ha agotado el presupuesto diario del asistente.");
        }

        var idioma = peticion.Idioma?.ToLowerInvariant() ?? "es";
        var mensajes = Conversacion(peticion.Mensajes!);
        var sistema = new List<BetaTextBlockParam>
        {
            new() { Text = _instrucciones, CacheControl = new BetaCacheControlEphemeral() },
            new() { Text = InstruccionesAsistente.Contexto(ReglasOrganizacionTorneos.AhoraEnMadrid(_reloj), idioma) },
        };

        var fuentes = new List<FuenteChatDto>();
        for (var vuelta = 0; vuelta < _opciones.IteracionesMaximas; vuelta++)
        {
            var respuesta = await LlamarAsync(sistema, mensajes, cancellationToken);

            if (respuesta.StopReason == "refusal")
            {
                return Derivada(idioma, fuentes,
                    idioma == "en"
                        ? "Sorry, I can't help with that request. If you need anything from the FEP, please use the contact form."
                        : "Lo siento, no puedo ayudarte con esa petición. Si necesitas algo de la FEP, escríbenos desde el formulario de contacto.");
            }

            var (eco, llamadas, texto) = Desglosar(respuesta);
            if (llamadas.Count == 0)
            {
                return Final(texto, fuentes);
            }

            // Ejecuta las herramientas pedidas (en paralelo) y devuelve todos los resultados en un único mensaje.
            var resultados = await Task.WhenAll(llamadas.Select(l => _herramientas.EjecutarAsync(l.Name, l.Input, cancellationToken)));
            var bloques = new List<BetaContentBlockParam>();
            for (var i = 0; i < llamadas.Count; i++)
            {
                fuentes.AddRange(resultados[i].Fuentes);
                bloques.Add(new BetaToolResultBlockParam
                {
                    ToolUseID = llamadas[i].ID,
                    Content = resultados[i].Contenido,
                    IsError = resultados[i].EsError,
                });
            }

            mensajes.Add(new BetaAssistantMessageParam { Content = eco });
            mensajes.Add(new BetaUserMessageParam { Content = bloques });
        }

        _logger.LogWarning("El asistente agotó las {Vueltas} vueltas de herramientas sin responder", _opciones.IteracionesMaximas);
        return Derivada(idioma, fuentes, idioma == "en"
            ? "I couldn't find a reliable answer to your question. Please contact the FEP through the contact form."
            : "No he encontrado una respuesta fiable a tu pregunta. Puedes consultarlo a la FEP desde el formulario de contacto.");
    }

    private async Task<BetaMessage> LlamarAsync(List<BetaTextBlockParam> sistema, List<BetaMessageParam> mensajes, CancellationToken ct)
    {
        var parametros = new MessageCreateParams
        {
            Model = _opciones.Modelo,
            MaxTokens = _opciones.MaxTokens,
            Betas = [BetaRespaldo],
            Fallbacks = new Default(),
            System = sistema,
            Tools = _definiciones,
            OutputConfig = new BetaOutputConfig { Effort = Esfuerzo(_opciones.Esfuerzo) },
            Messages = [.. mensajes],
        };

        try
        {
            var respuesta = await _cliente!.Beta.Messages.Create(parametros, ct);
            var u = respuesta.Usage;
            _presupuesto.Registrar(u.InputTokens + u.OutputTokens + (u.CacheCreationInputTokens ?? 0) + (u.CacheReadInputTokens ?? 0));
            _logger.LogInformation(
                "Asistente: stop={Stop} entrada={Entrada} salida={Salida} cache_leida={CacheLeida} cache_creada={CacheCreada}",
                respuesta.StopReason, u.InputTokens, u.OutputTokens, u.CacheReadInputTokens, u.CacheCreationInputTokens);
            return respuesta;
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new AsistenteNoDisponibleException("Límite de peticiones de la API de Anthropic.", ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw new AsistenteNoDisponibleException("La API de Anthropic no responde.", ex);
        }
        catch (AnthropicApiException ex)
        {
            // 400/401/403/404: error de configuración (clave, modelo) o de la petición. Se registra con detalle.
            _logger.LogError(ex, "Error de la API de Anthropic en el asistente");
            throw new AsistenteNoDisponibleException("Error de la API de Anthropic.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AsistenteNoDisponibleException("Sin conexión con la API de Anthropic.", ex);
        }
    }

    /// <summary>Historial del chat → mensajes de la API.</summary>
    private static List<BetaMessageParam> Conversacion(IReadOnlyList<MensajeChatDto> historial)
    {
        var mensajes = new List<BetaMessageParam>();
        foreach (var m in historial)
        {
            var texto = m.Texto!.Trim();
            if (m.Rol == ReglasChat.RolAsistente)
            {
                if (texto.Length > ReglasChat.LongitudMaximaRespuestaPrevia)
                {
                    texto = texto[..ReglasChat.LongitudMaximaRespuestaPrevia];
                }

                mensajes.Add(new BetaAssistantMessageParam { Content = texto });
            }
            else
            {
                mensajes.Add(new BetaUserMessageParam { Content = texto });
            }
        }

        return mensajes;
    }

    /// <summary>
    /// Separa la respuesta en: el eco para el siguiente turno (todos los bloques, con el razonamiento intacto y su
    /// firma), las llamadas a herramientas y el texto visible.
    /// </summary>
    private static (List<BetaContentBlockParam> Eco, List<BetaToolUseBlock> Llamadas, string Texto) Desglosar(BetaMessage respuesta)
    {
        var eco = new List<BetaContentBlockParam>();
        var llamadas = new List<BetaToolUseBlock>();
        var texto = new StringBuilder();
        foreach (var bloque in respuesta.Content)
        {
            if (bloque.TryPickText(out var t))
            {
                eco.Add(new BetaTextBlockParam { Text = t.Text });
                texto.Append(t.Text);
            }
            else if (bloque.TryPickThinking(out var th))
            {
                eco.Add(new BetaThinkingBlockParam { Thinking = th.Thinking, Signature = th.Signature });
            }
            else if (bloque.TryPickRedactedThinking(out var r))
            {
                eco.Add(new BetaRedactedThinkingBlockParam { Data = r.Data });
            }
            else if (bloque.TryPickToolUse(out var tu))
            {
                eco.Add(new BetaToolUseBlockParam { ID = tu.ID, Name = tu.Name, Input = tu.Input });
                llamadas.Add(tu);
            }
        }

        return (eco, llamadas, texto.ToString());
    }

    private RespuestaChatDto Final(string texto, List<FuenteChatDto> fuentes)
    {
        var derivar = texto.Contains(InstruccionesAsistente.MarcaDerivar, StringComparison.Ordinal);
        texto = texto.Replace(InstruccionesAsistente.MarcaDerivar, string.Empty, StringComparison.Ordinal).Trim();
        return new RespuestaChatDto(texto, Unicas(fuentes), derivar, _opciones.UrlContacto);
    }

    private RespuestaChatDto Derivada(string idioma, List<FuenteChatDto> fuentes, string texto)
        => new(texto, Unicas(fuentes), true, _opciones.UrlContacto);

    /// <summary>Fuentes sin repetir (la misma búsqueda puede devolver el mismo documento varias veces).</summary>
    private static IReadOnlyList<FuenteChatDto> Unicas(List<FuenteChatDto> fuentes)
        => fuentes
            .GroupBy(f => (f.Tipo, f.Titulo))
            .Select(g => g.First() with
            {
                Detalle = string.Join("; ", g.Select(f => f.Detalle).Where(d => d is not null).Distinct()) is { Length: > 0 } d ? d : null,
            })
            .Take(8)
            .ToArray();

    private static BetaToolUnion ComoHerramienta(DefinicionHerramienta d)
    {
        var propiedades = d.Esquema.GetProperty("properties").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.Clone());
        var requeridas = d.Esquema.TryGetProperty("required", out var r)
            ? r.EnumerateArray().Select(x => x.GetString()!).ToList()
            : [];
        return new BetaTool
        {
            Name = d.Nombre,
            Description = d.Descripcion,
            InputSchema = new() { Properties = propiedades, Required = requeridas },
        };
    }

    private static Effort Esfuerzo(string valor) => valor.ToLowerInvariant() switch
    {
        "medium" => Effort.Medium,
        "high" => Effort.High,
        _ => Effort.Low,
    };
}
