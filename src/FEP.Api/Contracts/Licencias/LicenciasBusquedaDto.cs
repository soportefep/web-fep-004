namespace FEP.Api.Contracts.Licencias;

/// <summary>
/// Resultado del buscador público de licencias (página <c>Licencias_Buscar.asp</c>, "Busca tu licencia"): número de
/// licencia, apellidos y nombre de las personas que cumplen los criterios, ordenadas por apellidos.
/// <para>
/// Protección de datos: solo se publican las cuatro columnas que pintaba el ASP. La fecha de nacimiento es un
/// <b>criterio</b> (no se devuelve: la respuesta solo dice si se ha usado) y no se exponen DNI, género, federación,
/// email ni teléfono, aunque el ASP leyera algunas de esas columnas.
/// </para>
/// </summary>
/// <param name="Tipo">Tipo de licencia buscado: <c>jugador</c>, <c>tecnico</c>, <c>arbitro</c> o <c>silla-ruedas</c>.</param>
/// <param name="Federacion">Federación aplicada (<c>SFEP_Licencia.Federacion</c>), si la hay.</param>
/// <param name="Busqueda">Texto buscado ya normalizado (términos separados por un espacio), si lo hay.</param>
/// <param name="ConFechaNacimiento">Si se ha filtrado por fecha de nacimiento (la fecha no se repite en la respuesta).</param>
/// <param name="Total">Número total de licencias que cumplen los criterios.</param>
/// <param name="Limite">Máximo de licencias devueltas (100): si <c>Total</c> es mayor, hay que afinar la búsqueda.</param>
/// <param name="Items">Licencias encontradas (como mucho <c>Limite</c>).</param>
public sealed record LicenciasBusquedaDto(
    string Tipo,
    int? Federacion,
    string? Busqueda,
    bool ConFechaNacimiento,
    int Total,
    int Limite,
    IReadOnlyList<LicenciaEncontradaDto> Items);

/// <summary>Licencia encontrada (una fila de la tabla del ASP).</summary>
/// <param name="Licencia">
/// Número de licencia tal como se muestra: <c>LicenciaJugador</c>, <c>LicenciaTecnico</c> o <c>LicenciaArbitro</c>, o
/// <c>"PS"</c> + <c>LicenciaSilla</c> en silla de ruedas.
/// </param>
/// <param name="Apellido1">Primer apellido (<c>SFEP_Fichas.Apellido1</c>, como en la BD).</param>
/// <param name="Apellido2">Segundo apellido (<c>Apellido2</c>), si lo hay.</param>
/// <param name="Nombre">Nombre (<c>Nombre</c>).</param>
/// <param name="EstadisticasUrl">
/// Solo jugador/a: estadísticas en la web actual (<c>Estadisticas?id={licencia}</c>, como el enlace del ASP); el front
/// la lleva a <c>estadisticas.html?licencia=</c>, ya migrada. <c>null</c> en el resto de tipos.
/// </param>
public sealed record LicenciaEncontradaDto(
    string Licencia,
    string? Apellido1,
    string? Apellido2,
    string? Nombre,
    string? EstadisticasUrl);
