namespace FEP.Api.Contracts.Asistente;

/// <summary>
/// Petición de <c>POST /api/v1/asistente/chat</c>. La API no guarda conversaciones: el navegador envía cada vez el
/// historial (los últimos mensajes) y la nueva pregunta al final.
/// </summary>
/// <param name="Mensajes">Conversación en orden, alternando <c>usuario</c> y <c>asistente</c>; el último, del usuario.</param>
/// <param name="Idioma"><c>es</c> o <c>en</c>: idioma de la interfaz (el asistente responde en el de la pregunta).</param>
public sealed record PeticionChatDto(IReadOnlyList<MensajeChatDto>? Mensajes, string? Idioma);

/// <summary>Un mensaje de la conversación.</summary>
/// <param name="Rol"><c>usuario</c> o <c>asistente</c>.</param>
/// <param name="Texto">Texto plano del mensaje.</param>
public sealed record MensajeChatDto(string? Rol, string? Texto);

/// <summary>Respuesta del asistente.</summary>
/// <param name="Texto">Respuesta en Markdown sencillo (negrita, listas y enlaces).</param>
/// <param name="Fuentes">Documentos y datos consultados para responder (para citarlos en el chat).</param>
/// <param name="Derivar">
/// <c>true</c> si el asistente no ha podido responder con la información disponible: el chat ofrece el formulario de
/// contacto (<paramref name="UrlContacto"/>).
/// </param>
/// <param name="UrlContacto">Formulario de contacto de la FEP.</param>
public sealed record RespuestaChatDto(string Texto, IReadOnlyList<FuenteChatDto> Fuentes, bool Derivar, string UrlContacto);

/// <summary>Fuente consultada.</summary>
/// <param name="Tipo"><c>documento</c>, <c>ranking</c> o <c>estadisticas</c>.</param>
/// <param name="Titulo">Título del documento o del ranking.</param>
/// <param name="Detalle">Páginas consultadas, lista del ranking… (<c>null</c> si no aplica).</param>
/// <param name="Url">Enlace al documento o a la página (<c>null</c> si no está publicado).</param>
public sealed record FuenteChatDto(string Tipo, string Titulo, string? Detalle, string? Url);

/// <summary>Límites de la petición de chat.</summary>
public static class ReglasChat
{
    /// <summary>Mensajes máximos por petición (el chat recorta el historial antes de enviarlo).</summary>
    public const int MensajesMaximos = 20;

    /// <summary>Caracteres máximos de cada mensaje del usuario.</summary>
    public const int LongitudMaximaPregunta = 1500;

    /// <summary>Caracteres máximos de cada mensaje previo del asistente (se recortan, no se rechazan).</summary>
    public const int LongitudMaximaRespuestaPrevia = 6000;

    /// <summary>Idiomas de la interfaz.</summary>
    public static readonly IReadOnlySet<string> Idiomas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "es", "en" };

    /// <summary>Rol de los mensajes del usuario.</summary>
    public const string RolUsuario = "usuario";

    /// <summary>Rol de los mensajes del asistente.</summary>
    public const string RolAsistente = "asistente";
}
