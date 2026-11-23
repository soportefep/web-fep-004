namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Estadísticas de un jugador o jugadora con licencia: ficha, balance de partidos, sets y juegos del último año en el
/// Ranking Único, rankings nacionales en los que participa (con las pruebas que puntúan en cada uno), evolución en el
/// Ranking Único y campeonatos de España por equipos. Sustituye a <c>Estadisticas.asp?id={licencia}</c> y a
/// <c>CargarPruebas.asp?Rnk=&amp;id=</c>, que la página cargaba por AJAX al pulsar un ranking.
/// </summary>
/// <param name="Licencia">Número de licencia de jugador/a (<c>SFEP_Fichas.LicenciaJugador</c>).</param>
/// <param name="Nombre">Nombre y apellidos de la ficha, tal cual (en mayúsculas en la BD).</param>
/// <param name="Foto">
/// Ruta original de la foto en la web actual (<c>gESTION/Fotos/{licencia}.jpg</c>) o <c>null</c> si la ficha no tiene
/// foto (<c>Foto &lt;&gt; 'S'</c>: el ASP pintaba entonces la silueta genérica <c>0.jpg</c>).
/// </param>
/// <param name="FotoUrl">URL absoluta de la foto (raíz de la web actual) o <c>null</c>.</param>
/// <param name="Categoria">
/// Categoría por edad como la calculaba el ASP (<c>BENJAMIN</c>, <c>ALEVIN</c>, <c>INFANTIL</c>, <c>CADETE</c>,
/// <c>JUNIOR</c>, <c>SUB-23</c>, <c>SENIOR</c>, <c>CAT-35</c> … <c>CAT-55</c>): año en curso menos año de nacimiento.
/// <c>null</c> sin fecha de nacimiento.
/// </param>
/// <param name="AnioLicencia">Temporada de la última licencia de jugador/a (de ella salen los clubes); <c>null</c> si no tiene.</param>
/// <param name="ClubAutonomico">Club de la licencia para la competición autonómica (tal cual, en mayúsculas).</param>
/// <param name="ClubNacional">Club de la licencia para la competición nacional (tal cual, en mayúsculas).</param>
/// <param name="Desde">
/// Inicio del periodo del balance, la pareja y la mejor clasificación: un año antes de la última actualización del
/// Ranking Único (<c>SFEP_Ranking_Tipos.FechaAct</c> del 10000). <c>null</c> si no hay fecha (sin límite).
/// </param>
/// <param name="Balance">Partidos, sets y juegos ganados y perdidos en las pruebas del Ranking Único del periodo.</param>
/// <param name="UltimaPareja">Pareja en la prueba más reciente del periodo; <c>null</c> si no consta.</param>
/// <param name="MejorClasificacion">Mejor ronda alcanzada en el periodo (nivel más alto y ronda más avanzada) o <c>null</c>.</param>
/// <param name="Rankings">Rankings nacionales visibles en los que figura, por <c>TipoRanking</c>.</param>
/// <param name="Evolucion">Últimas 12 publicaciones del Ranking Único (de la más antigua a la más reciente) o <c>null</c>.</param>
/// <param name="Equipos">Campeonatos nacionales por equipos desde 2013 en los que juega o puntúa, del más reciente al más antiguo.</param>
/// <param name="UrlWebActual">La misma página en la web actual (<c>Estadisticas?id={licencia}</c>).</param>
/// <param name="Consultado">Momento de la consulta (hora de Madrid, sin zona).</param>
public sealed record EstadisticasJugadorDto(
    int Licencia,
    string Nombre,
    string? Foto,
    string? FotoUrl,
    string? Categoria,
    int? AnioLicencia,
    string? ClubAutonomico,
    string? ClubNacional,
    DateOnly? Desde,
    BalancePartidosDto Balance,
    ParejaJugadorDto? UltimaPareja,
    MejorClasificacionDto? MejorClasificacion,
    IReadOnlyList<RankingJugadorDto> Rankings,
    EvolucionRankingDto? Evolucion,
    IReadOnlyList<TorneoEquiposJugadorDto> Equipos,
    string UrlWebActual,
    DateTime Consultado);

/// <summary>Balance de partidos, sets y juegos (bloque "Partidos/Sets/Juegos Ganados/Perdidos" del ASP).</summary>
public sealed record BalancePartidosDto(RecuentoDto Partidos, RecuentoDto Sets, RecuentoDto Juegos);

/// <summary>Ganados y perdidos con su porcentaje (un decimal; <c>null</c> si no hay ninguno).</summary>
/// <param name="Ganados">Número ganado.</param>
/// <param name="Perdidos">Número perdido.</param>
/// <param name="PorcentajeGanados">Porcentaje ganado con un decimal (p. ej. 14,3) o <c>null</c> si el total es 0.</param>
/// <param name="PorcentajePerdidos">100 menos <paramref name="PorcentajeGanados"/>, como el ASP; <c>null</c> si el total es 0.</param>
public sealed record RecuentoDto(int Ganados, int Perdidos, decimal? PorcentajeGanados, decimal? PorcentajePerdidos)
{
    /// <summary>Total (ganados + perdidos).</summary>
    public int Total => Ganados + Perdidos;
}

/// <summary>Compañero o compañera de pareja ("Última Pareja" del ASP).</summary>
/// <param name="Licencia">Número de licencia.</param>
/// <param name="Nombre">Nombre y apellidos de la ficha, tal cual.</param>
/// <param name="EstadisticasUrl">Sus estadísticas en la web actual (<c>Estadisticas?id={licencia}</c>).</param>
public sealed record ParejaJugadorDto(int Licencia, string Nombre, string EstadisticasUrl);

/// <summary>Mejor clasificación en torneo ("Mejor clasificación Torneo" del ASP: "16's Primera Categoría (1)").</summary>
/// <param name="Nivel"><c>SFEP_Torneos_Cuadros.Nivel</c> (1 = Primera categoría … 10 = Décima).</param>
/// <param name="Avance"><c>SFEP_Torneos_Cuadros.Avance</c> (0 = campeón, 1 = final, 2 = semifinal… 7 = 64 parejas).</param>
/// <param name="Ronda">Ronda alcanzada ("Campeón", "Finalista", "Semifinalista", "Cuartos de final"…) o <c>null</c> si el avance no es 0–7.</param>
/// <param name="Categoria">Categoría del cuadro ("Primera categoría"…) o <c>null</c> si el nivel no es 1–10.</param>
/// <param name="Veces">Veces que ha alcanzado esa ronda en ese nivel en el periodo.</param>
public sealed record MejorClasificacionDto(int Nivel, int Avance, string? Ronda, string? Categoria, int Veces);

/// <summary>Puesto del jugador o jugadora en un ranking nacional (fila de la tabla "RANKINGS" del ASP).</summary>
/// <param name="TipoRanking"><c>SFEP_RankingFinal.TipoRanking</c> (10000/10001 único, 4000–8001 menores, 10035–11060 veteranos…).</param>
/// <param name="Clave">
/// Clave del ranking con clasificación migrada (<c>absoluto</c>, <c>menores</c>, <c>veteranos</c>: página
/// <c>ranking-{clave}.html</c>) o <c>null</c> si el tipo no está declarado en <see cref="ReglasClasificaciones"/>.
/// </param>
/// <param name="Ranking">Nombre del ranking ("Ranking Único Absoluto", "Ranking de Menores", "Ranking de Veteranos"…).</param>
/// <param name="Lista">Lista dentro del ranking ("Masculino", "Alevín femenino", "+35 masculino") o <c>null</c>.</param>
/// <param name="Genero"><c>M</c>, <c>F</c> o <c>X</c> si el tipo está declarado; si no, <c>null</c>.</param>
/// <param name="CategoriaBd"><c>SFEP_Ranking_Tipos.Categoria</c> tal cual, sin espacios (<c>ABSOLUTO</c>, <c>ALEVIN</c>, <c>CAT35</c>…).</param>
/// <param name="Posicion">Puesto; <c>null</c> si la BD trae 0 (sin puesto).</param>
/// <param name="TorneosTotales">"Torneos jugados" (<c>TorneosJugador</c>).</param>
/// <param name="PuntosTotales">"Puntos totales" (<c>PuntosTotal</c>, redondeados).</param>
/// <param name="TorneosRanking">"Torneos average": los que cuentan (<c>Torneos</c>).</param>
/// <param name="PuntosRanking">"Puntos average": los del ranking (<c>Puntos</c>, redondeados).</param>
/// <param name="Actualizado">Fecha de actualización de la lista (<c>SFEP_Ranking_Tipos.FechaAct</c>) o <c>null</c>.</param>
/// <param name="Desde">
/// Inicio del periodo de las pruebas que se muestran: un año antes de <paramref name="Actualizado"/> o, en los rankings
/// de menores, la fecha fija de <see cref="ReglasEstadisticas.InicioPeriodoMenores"/>. <c>null</c> = todas.
/// </param>
/// <param name="Pruebas">Pruebas del periodo con su ronda y puntos (desglose de <c>CargarPruebas.asp</c>), de la más reciente a la más antigua.</param>
public sealed record RankingJugadorDto(
    int TipoRanking,
    string? Clave,
    string Ranking,
    string? Lista,
    string? Genero,
    string? CategoriaBd,
    int? Posicion,
    int? TorneosTotales,
    int? PuntosTotales,
    int? TorneosRanking,
    int? PuntosRanking,
    DateOnly? Actualizado,
    DateOnly? Desde,
    IReadOnlyList<PruebaRankingJugadorDto> Pruebas);

/// <summary>Prueba disputada que puntúa (o puede puntuar) en un ranking.</summary>
/// <param name="IdPrueba"><c>SFEP_Torneos.IdPrueba</c>.</param>
/// <param name="Torneo">Nombre del torneo, tal cual.</param>
/// <param name="Fecha">Fecha de fin del torneo (<c>FechaFin</c>) o <c>null</c>.</param>
/// <param name="Federacion">Federación organizadora (<c>SFEP_Torneos.IdFederacion</c>; 0 = FEP) o <c>null</c>.</param>
/// <param name="Ronda">Ronda alcanzada (<c>SFEP_RankingTorneos.Ronda</c>, tal cual: "Campeón", "Cuartos", "16avos"…).</param>
/// <param name="Observaciones">Observaciones (<c>Obs</c>: "Previa", "2ª Cat"…) o <c>null</c>.</param>
/// <param name="Puntos">Puntos obtenidos en la prueba (redondeados).</param>
/// <param name="Porcentaje">
/// Parte de los puntos que cuenta: 100 o, para las pruebas de la categoría de menores anterior, 40
/// (<see cref="ReglasEstadisticas.PorcentajeCategoriaAnterior"/>).
/// </param>
/// <param name="PuntosComputados">Puntos que cuentan (<paramref name="Puntos"/> × <paramref name="Porcentaje"/> %).</param>
/// <param name="CategoriaAnterior"><c>true</c> si la prueba es de la categoría de menores anterior (ranking <c>TipoRanking</c> de esa categoría).</param>
public sealed record PruebaRankingJugadorDto(
    int IdPrueba,
    string Torneo,
    DateOnly? Fecha,
    FederacionPruebaDto? Federacion,
    string? Ronda,
    string? Observaciones,
    int? Puntos,
    int Porcentaje,
    int? PuntosComputados,
    bool CategoriaAnterior);

/// <summary>Federación organizadora de una prueba (el ASP pintaba solo su bandera, sin texto alternativo).</summary>
/// <param name="Id"><c>SFEP_Federaciones.IdFederacion</c> (0 = FEP).</param>
/// <param name="Territorio">Territorio ("Andalucía"; "España" para la FEP) o <c>null</c> si no está en el listado de federaciones.</param>
/// <param name="Nombre">Nombre oficial o <c>null</c>.</param>
/// <param name="Bandera">Ruta original de la bandera bajo <c>refs/</c> (<c>images/{id}.jpg</c>).</param>
/// <param name="BanderaUrl">URL absoluta de la bandera.</param>
public sealed record FederacionPruebaDto(int Id, string? Territorio, string? Nombre, string Bandera, string? BanderaUrl);

/// <summary>Evolución en el Ranking Único (gráfico "Evolución Ranking Único" del ASP).</summary>
/// <param name="TiposRanking">Tipos consultados (10000 masculino, 10001 femenino).</param>
/// <param name="Puntos">Publicaciones, de la más antigua a la más reciente (como mucho 12).</param>
public sealed record EvolucionRankingDto(IReadOnlyList<int> TiposRanking, IReadOnlyList<PuntoEvolucionDto> Puntos);

/// <summary>Una publicación del ranking en el histórico (<c>SFEP_Ranking_Historico</c>).</summary>
/// <param name="Fecha">Fecha de la publicación.</param>
/// <param name="Posicion">Puesto (<c>null</c> si la BD trae 0).</param>
/// <param name="Puntos">Puntos (redondeados).</param>
public sealed record PuntoEvolucionDto(DateOnly Fecha, int? Posicion, int? Puntos);

/// <summary>Campeonato nacional por equipos en el que juega o puntúa (tabla "Torneos por equipos" del ASP).</summary>
/// <param name="IdPrueba"><c>SFEP_Torneos.IdPrueba</c>.</param>
/// <param name="Anio">Temporada (<c>Año</c>).</param>
/// <param name="Fecha">Fecha de fin (<c>FechaFin</c>; el ASP pintaba solo el mes) o <c>null</c>.</param>
/// <param name="Torneo">Nombre del campeonato, tal cual.</param>
/// <param name="Puntua"><c>Puntua = 'S'</c> en la inscripción del equipo.</param>
/// <param name="Juega"><c>Juega = 'S'</c> en la inscripción del equipo.</param>
/// <param name="Enlace">
/// Ficha del campeonato (URL absoluta: <c>LinkCuadros</c> o la ficha a la que redirigía <c>Torneos_Previo.asp</c>, p. ej.
/// <c>Camp_Equipos.asp?IdTorneo=</c>). El ASP enviaba a <c>Campeonatos_Previo.asp</c>, que no existe.
/// </param>
/// <param name="NuevaVentana"><c>true</c> si <paramref name="Enlace"/> es un enlace externo (<c>LinkCuadros</c>).</param>
public sealed record TorneoEquiposJugadorDto(
    int IdPrueba,
    int Anio,
    DateOnly? Fecha,
    string Torneo,
    bool Puntua,
    bool Juega,
    string? Enlace,
    bool NuevaVentana);
