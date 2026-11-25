namespace FEP.Api.Contracts.Competicion;

/// <summary>Opción de un filtro de la clasificación (género o categoría).</summary>
/// <param name="Valor">Valor a enviar en la query (<c>M</c>, <c>F</c>…).</param>
/// <param name="Nombre">Texto para mostrar ("Masculino"…).</param>
public sealed record OpcionRankingDto(string Valor, string Nombre);

/// <summary>
/// Clasificación de un ranking (<c>GET /api/v1/competicion/rankings/{clave}</c>). Sustituye a la tabla que
/// <c>Ranking_Absoluto.asp</c> cargaba por AJAX desde <c>CargarRnkAbs.asp</c> (por género o licencia) y
/// <c>CargarRnkAbsNombre.asp</c> (por nombre). Una lista por género (y categoría, en los rankings que la tengan).
/// </summary>
/// <param name="Clave">Clave del ranking (<c>absoluto</c>).</param>
/// <param name="Titulo">Título del ranking ("Ranking Único Absoluto").</param>
/// <param name="Generos">Géneros que tiene el ranking (valores admitidos en <c>genero</c>).</param>
/// <param name="Categorias">Categorías que tiene el ranking (vacía en el absoluto).</param>
/// <param name="Genero">Filtro de género aplicado (<c>M</c>/<c>F</c>) o <c>null</c> (todos).</param>
/// <param name="Categoria">Filtro de categoría aplicado o <c>null</c>.</param>
/// <param name="Licencia">Licencia buscada o <c>null</c>.</param>
/// <param name="Busqueda">Nombre buscado (términos normalizados, separados por espacios) o <c>null</c>.</param>
/// <param name="Limite">Puestos máximos por lista.</param>
/// <param name="Listas">Una lista por <c>TipoRanking</c> que cumple los filtros de género/categoría, aunque no tenga puestos.</param>
/// <param name="PrefijoLicencia">
/// Prefijo con el que se muestran las licencias del ranking (<c>PS</c> en silla de ruedas: "PS1008") o <c>null</c>.
/// <c>licencia</c> (query y puestos) es siempre el número sin prefijo.
/// </param>
public sealed record ClasificacionRankingDto(
    string Clave,
    string Titulo,
    IReadOnlyList<OpcionRankingDto> Generos,
    IReadOnlyList<OpcionRankingDto> Categorias,
    string? Genero,
    string? Categoria,
    int? Licencia,
    string? Busqueda,
    int Limite,
    IReadOnlyList<ListaRankingDto> Listas,
    string? PrefijoLicencia = null);

/// <summary>Una lista del ranking (un <c>TipoRanking</c>) con sus datos de <c>SFEP_Ranking_Tipos</c> y sus puestos.</summary>
/// <param name="TipoRanking">Valor de <c>TipoRanking</c> en la BD (10000 absoluto masculino, 10001 femenino).</param>
/// <param name="Genero"><c>M</c>, <c>F</c> o <c>X</c>.</param>
/// <param name="Categoria">Clave de la categoría o <c>null</c>.</param>
/// <param name="Titulo">Nombre de la lista ("Masculino", "Femenino"; con la categoría delante si la hay).</param>
/// <param name="Jugadores">Jugadores en el ranking (<c>SFEP_Ranking_Tipos.Jugadores</c>).</param>
/// <param name="Pruebas">Pruebas que suman para el ranking (<c>Pruebas</c>).</param>
/// <param name="Actualizado">Fecha de actualización (<c>FechaAct</c>).</param>
/// <param name="Documento">Ruta original del PDF del ranking completo bajo <c>refs/</c> (<c>docs/{ArchivoPDF}</c>).</param>
/// <param name="DocumentoUrl">URL absoluta del PDF (<c>Media:BaseUrl</c>).</param>
/// <param name="Coincidencias">Puestos de la lista que cumplen los filtros (antes de aplicar <c>limite</c>).</param>
/// <param name="Puestos">Puestos, por posición (como mucho <c>limite</c>).</param>
public sealed record ListaRankingDto(
    int TipoRanking,
    string Genero,
    string? Categoria,
    string Titulo,
    int? Jugadores,
    int? Pruebas,
    DateOnly? Actualizado,
    string? Documento,
    string? DocumentoUrl,
    int Coincidencias,
    IReadOnlyList<PuestoRankingDto> Puestos);

/// <summary>
/// Un puesto de la clasificación (<c>SFEP_RankingFinal</c> + nombre de <c>SFEP_Fichas</c>; en silla de ruedas, el nombre
/// de la propia <c>SFEP_RankingFinal</c>).
/// </summary>
/// <param name="Posicion">Puesto (<c>Posicion</c>).</param>
/// <param name="Licencia">Número de licencia (<c>NumLicencia</c>), sin el prefijo <see cref="ClasificacionRankingDto.PrefijoLicencia"/>.</param>
/// <param name="Nombre">Nombre y apellidos tal como están en la ficha.</param>
/// <param name="TorneosTotales">Torneos jugados en el periodo (<c>TorneosJugador</c>, "Total Torneos").</param>
/// <param name="PuntosTotales">Puntos de todos esos torneos (<c>PuntosTotal</c>, "Total Puntos").</param>
/// <param name="TorneosRanking">Torneos que cuentan para el ranking (<c>Torneos</c>, "Torneos Rnk").</param>
/// <param name="PuntosRanking">Puntos del ranking (<c>Puntos</c>, "Puntos Rnk").</param>
/// <param name="Evolucion">Posiciones que sube (positivo) o baja (negativo) respecto al ranking anterior (<c>Sube</c>).</param>
/// <param name="JornadasPuesto">Actualizaciones del ranking en esa posición (<c>JornadasPuesto</c>).</param>
/// <param name="EstadisticasUrl">
/// Estadísticas del jugador en la web actual (<c>Estadisticas?id={licencia}</c>; en silla de ruedas,
/// <c>Silla-Ruedas-Est?id=PS{licencia}</c>).
/// </param>
public sealed record PuestoRankingDto(
    int Posicion,
    int Licencia,
    string Nombre,
    int? TorneosTotales,
    int? PuntosTotales,
    int? TorneosRanking,
    int? PuntosRanking,
    int? Evolucion,
    int? JornadasPuesto,
    string EstadisticasUrl);
