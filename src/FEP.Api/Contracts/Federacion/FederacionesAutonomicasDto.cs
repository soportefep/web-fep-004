namespace FEP.Api.Contracts.Federacion;

/// <summary>
/// Federaciones autonómicas que forman la FEP y reserva de la herramienta Zoom
/// (página <c>Federaciones.asp</c>).
/// </summary>
/// <param name="Actualizado">Fecha de la última revisión del contenido.</param>
/// <param name="Introduccion">Texto de presentación de la página.</param>
/// <param name="Federaciones">
/// Federaciones en el orden de la página: las 19 autonómicas y, al final, la Federación Española
/// (<see cref="FederacionTerritorialDto.Nacional"/> = <c>true</c>).
/// </param>
/// <param name="ReservaZoom">Bloque de reserva de Zoom con su calendario de Google, si se publica.</param>
public sealed record FederacionesAutonomicasDto(
    DateOnly? Actualizado,
    string? Introduccion,
    IReadOnlyList<FederacionTerritorialDto> Federaciones,
    ReservaZoomDto? ReservaZoom);

/// <summary>Federación territorial (o la propia FEP).</summary>
/// <param name="Id">
/// <c>SFEP_Federaciones.IdFederacion</c>: identifica la ficha (<c>/api/v1/federacion/federaciones-autonomicas/{id}</c>,
/// antes <c>Datos_Federacion.asp?Id={id}</c>) y el filtro <c>federacion</c> de <c>/api/v1/clubes</c>.
/// </param>
/// <param name="Territorio">Comunidad o ciudad autónoma (p. ej. "Andalucía").</param>
/// <param name="Nombre">Nombre oficial de la federación.</param>
/// <param name="Nacional"><c>true</c> solo para la Federación Española de Pádel (Id 0).</param>
/// <param name="Imagen">Ruta original de la bandera bajo <c>refs/</c> (p. ej. <c>images/6.jpg</c>).</param>
/// <param name="ImagenUrl">URL absoluta de la bandera, calculada con <c>Media:BaseUrl</c>.</param>
/// <param name="Web">Web oficial de la federación (URL absoluta), si la tiene.</param>
public sealed record FederacionTerritorialDto(
    int Id,
    string Territorio,
    string Nombre,
    bool Nacional,
    string? Imagen,
    string? ImagenUrl,
    string? Web);

/// <summary>Reserva de la herramienta Zoom que la FEP comparte con las federaciones territoriales.</summary>
/// <param name="Titulo">Título del bloque.</param>
/// <param name="Parrafos">Texto explicativo, en párrafos.</param>
/// <param name="Email">Correo al que se solicita la reserva.</param>
/// <param name="Imagen">Ruta original de la imagen bajo <c>refs/</c> (<c>images/zoom.jpg</c>).</param>
/// <param name="ImagenUrl">URL absoluta de la imagen, calculada con <c>Media:BaseUrl</c>.</param>
/// <param name="ImagenAlt">Texto alternativo de la imagen.</param>
/// <param name="Calendario">Calendario de Google incrustado con la disponibilidad, si se publica.</param>
public sealed record ReservaZoomDto(
    string Titulo,
    IReadOnlyList<string> Parrafos,
    string? Email,
    string? Imagen,
    string? ImagenUrl,
    string? ImagenAlt,
    CalendarioEmbebidoDto? Calendario);

/// <summary>Calendario de Google Calendar para incrustar en un <c>iframe</c>.</summary>
/// <param name="Titulo">Título accesible del <c>iframe</c>.</param>
/// <param name="Url">URL de inserción (<c>https://calendar.google.com/calendar/embed?…</c>).</param>
/// <param name="ZonaHoraria">Zona horaria IANA en la que se muestran las reservas.</param>
public sealed record CalendarioEmbebidoDto(string Titulo, string Url, string? ZonaHoraria);
