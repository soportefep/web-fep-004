namespace FEP.Api.Infrastructure.Asistente;

/// <summary>
/// Configuración del asistente (sección <c>Asistente</c>). La clave de la API de Anthropic <b>nunca</b> va en
/// <c>appsettings*.json</c>: en desarrollo, <c>dotnet user-secrets set "Asistente:ApiKey" "..."</c>; en servidores, la
/// variable de entorno <c>Asistente__ApiKey</c> (o <c>ANTHROPIC_API_KEY</c>).
/// </summary>
public sealed class AsistenteOptions
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Asistente";

    /// <summary>Clave de la API de Anthropic. Vacía = asistente no disponible (503 con el formulario de contacto).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Modelo de Claude.</summary>
    public string Modelo { get; set; } = "claude-opus-5-5";

    /// <summary>Esfuerzo (<c>low</c>, <c>medium</c>, <c>high</c>): <c>low</c> basta para preguntas de chat y es lo más rápido.</summary>
    public string Esfuerzo { get; set; } = "low";

    /// <summary>Tokens máximos de cada respuesta del modelo (incluye el razonamiento interno).</summary>
    public int MaxTokens { get; set; } = 8000;

    /// <summary>Vueltas máximas de herramientas por pregunta (cada búsqueda o consulta es una vuelta).</summary>
    public int IteracionesMaximas { get; set; } = 6;

    /// <summary>Preguntas por minuto y dirección IP (protege de abusos y de bots).</summary>
    public int PreguntasPorMinuto { get; set; } = 6;

    /// <summary>Preguntas por hora y dirección IP.</summary>
    public int PreguntasPorHora { get; set; } = 60;

    /// <summary>
    /// Presupuesto diario de tokens (entrada + salida) de todo el asistente, como red de seguridad ante un ataque o un
    /// error. Se reinicia a medianoche (hora de Madrid). 0 = sin límite.
    /// </summary>
    public long TokensDiarios { get; set; } = 20_000_000;

    /// <summary>Formulario de contacto al que se deriva lo que el asistente no puede responder.</summary>
    public string UrlContacto { get; set; } = "https://www.padelfederacion.es/Contacto";
}
