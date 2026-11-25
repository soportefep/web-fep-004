namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Cuadro de un torneo (sustituye a <c>Previo.asp</c>/<c>PrevioEquipos.asp</c> y a las ~40 páginas a las que redirigían:
/// <c>Cuadro4…128.asp</c>, <c>CuadroPrevia*.asp</c>, <c>CuadroPrePrevia*.asp</c>, <c>CuadroEquipos*.asp</c>,
/// <c>Liguilla*.asp</c>). Todas leían las mismas tablas y solo cambiaba el tamaño de la rejilla, así que hay un único
/// contrato: el cliente dibuja la rejilla a partir de <see cref="Participantes"/> y <see cref="Partidos"/>.
/// Datos de <c>SFEP_Torneos</c>, <c>SFEP_Torneos_Desglose</c>, <c>SFEP_Torneos_Cuadros</c>, <c>SFEP_Torneos_Jugadores</c>
/// (parejas) o <c>SFEP_Torneos_Equipos</c> (equipos), <c>SFEP_Torneos_Resultados</c>, <c>SFEP_Torneos_Horarios</c>,
/// <c>SFEP_Torneos_ResultadosEquipos</c> y <c>SFEP_Torneos_Liguilla</c>.
/// </summary>
/// <param name="Torneo">Datos generales del torneo.</param>
/// <param name="Genero"><c>M</c> | <c>F</c> | <c>X</c>.</param>
/// <param name="Categoria">Categoría pedida (<c>ABSOLUTO</c>, <c>Cat50</c>, <c>JUNIOR</c>…; <c>Tipo</c> del desglose).</param>
/// <param name="Nivel">Nivel: 1–10 (primera a décima categoría), 21 (previa), 31 (pre-previa).</param>
/// <param name="Cuadro"><c>Final</c>, <c>Consola</c>, <c>Grupo A</c>, <c>Descenso</c>, <c>3y4</c>…</param>
/// <param name="Publicado">
/// <c>false</c> si la columna <c>Ver…</c> del nivel en <c>SFEP_Torneos_Desglose</c> vale <c>'N'</c> (o no hay fila de
/// desglose): entonces <see cref="Participantes"/>, <see cref="Partidos"/>, <see cref="CabezasSerie"/> y
/// <see cref="Clasificacion"/> llegan vacíos ("No están disponibles los cuadros de esta categoría").
/// </param>
/// <param name="Notas">Observaciones del cuadro (<c>TextoCuadros</c>): HTML heredado de la gestión, <b>sin sanear</b>.</param>
/// <param name="Formato"><c>eliminatoria</c> | <c>liguilla</c> (decisión de <c>Previo.asp</c> por número de filas).</param>
/// <param name="Plantilla">Página a la que habría redirigido el ASP (<c>Cuadro32</c>, <c>Liguilla4Equipos</c>…); informativo, <c>null</c> si ninguna.</param>
/// <param name="Participantes">Ocupantes de cada hueco del cuadro (los huecos vacíos no se devuelven).</param>
/// <param name="Partidos">Resultado, horario y detalle de enfrentamiento de cada partido que tenga alguno.</param>
/// <param name="CabezasSerie">Cabezas de serie con sus puntos (en las liguillas, las parejas o equipos participantes, como el ASP).</param>
/// <param name="Clasificacion">Clasificación de la liguilla (<c>SFEP_Torneos_Liguilla</c>); vacía en las eliminatorias.</param>
/// <param name="OtrosCuadros">Todos los cuadros del torneo (también el pedido), para el selector.</param>
public sealed record CuadroTorneoDto(
    TorneoCuadroDto Torneo,
    string Genero,
    string Categoria,
    int Nivel,
    string Cuadro,
    bool Publicado,
    string? Notas,
    string Formato,
    string? Plantilla,
    IReadOnlyList<ParticipanteCuadroDto> Participantes,
    IReadOnlyList<PartidoCuadroDto> Partidos,
    IReadOnlyList<CabezaSerieCuadroDto> CabezasSerie,
    IReadOnlyList<ClasificacionLiguillaDto> Clasificacion,
    IReadOnlyList<CuadroDisponibleDto> OtrosCuadros);

/// <summary>Datos generales del torneo en las respuestas de cuadros.</summary>
/// <param name="Id"><c>IdPrueba</c>.</param>
/// <param name="Titulo">Nombre (<c>Torneo</c>), sin espacios repetidos.</param>
/// <param name="Tipo"><c>parejas</c> | <c>equipos</c> (<c>TipoTorneo = 'EQUIPOS'</c>).</param>
/// <param name="FechaInicio">Primer día.</param>
/// <param name="FechaFin">Último día.</param>
/// <param name="Lugar">Ciudad (<c>Ciudad</c>), en formato "Nombre Propio" si en la BD está todo en mayúsculas o minúsculas.</param>
public sealed record TorneoCuadroDto(
    int Id,
    string Titulo,
    string Tipo,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string? Lugar);

/// <summary>Ocupante de un hueco del cuadro (<c>SFEP_Torneos_Cuadros</c>).</summary>
/// <param name="Avance">
/// Ronda: 0 = campeón, 1 = final, 2 = semifinales, 3 = cuartos…; la primera ronda es el mayor avance. En las
/// liguillas, número de partido.
/// </param>
/// <param name="Posicion">Hueco dentro de la ronda (1…). El partido <c>p</c> de la ronda lo juegan los huecos <c>2p−1</c> y <c>2p</c>; en las liguillas, 1 y 2.</param>
/// <param name="Tipo"><c>pareja</c> | <c>equipo</c> | <c>exento</c> | <c>previa</c> (llega de la previa con <see cref="Letra"/>) | <c>lucky</c> (plaza de <i>lucky loser</i>).</param>
/// <param name="Id"><c>NumPareja</c> / <c>NumEquipo</c> (<c>Pareja</c> en el cuadro); <c>null</c> en los huecos sin pareja.</param>
/// <param name="Nombre">Texto que pintaba el ASP: "JUGADOR 1 - JUGADOR 2", nombre del equipo, "EXENTO", "PREVIA - A" / "PRE-PREVIA - A", "LUCKY LOSER".</param>
/// <param name="Jugadores">Jugadores de la pareja por <c>OrdenPareja</c>; <c>null</c> si no es una pareja.</param>
/// <param name="CabezaSerie">Cabeza de serie (<c>CDS</c>), solo en la primera ronda de las eliminatorias.</param>
/// <param name="Letra">Letra de la previa (<c>Letra</c>) en los huecos <c>previa</c>.</param>
/// <param name="LuckyLoser"><c>true</c> si la pareja o equipo entró como <i>lucky loser</i> (<c>Lucky = 'S'</c>; el ASP pintaba "LS" en la columna C.S.).</param>
public sealed record ParticipanteCuadroDto(
    int Avance,
    int Posicion,
    string Tipo,
    int? Id,
    string Nombre,
    IReadOnlyList<string>? Jugadores,
    int? CabezaSerie,
    string? Letra,
    bool LuckyLoser);

/// <summary>Partido del cuadro con resultado, horario o detalle de enfrentamiento.</summary>
/// <param name="Avance">Ronda (o número de partido en las liguillas).</param>
/// <param name="Posicion">Número de partido dentro de la ronda (en las liguillas, 1).</param>
/// <param name="Resultado">Resultado tal cual (<c>SFEP_Torneos_Resultados.resultado</c>: "6/4-6/2", "3/0 y lesión"…); <c>null</c> si no se ha jugado.</param>
/// <param name="Horario">Día, pista y observaciones (<c>SFEP_Torneos_Horarios</c>); <c>null</c> si no hay o <c>Visible = 'N'</c>.</param>
/// <param name="Enfrentamiento"><c>true</c> si hay detalle por partidos (<c>SFEP_Torneos_ResultadosEquipos</c>; solo equipos).</param>
public sealed record PartidoCuadroDto(
    int Avance,
    int Posicion,
    string? Resultado,
    HorarioPartidoCuadroDto? Horario,
    bool Enfrentamiento);

/// <summary>Horario de un partido.</summary>
/// <param name="Dia">Fecha y hora (hora local de España, sin zona).</param>
/// <param name="Lugar">Pista o sede (<c>Lugar</c>).</param>
/// <param name="Obs">Observación que el ASP pintaba en lugar de la hora ("A continuación"…).</param>
/// <param name="Vs"><c>true</c> si <c>VS = 'S'</c> (el ASP añadía el icono "VS").</param>
public sealed record HorarioPartidoCuadroDto(DateTime? Dia, string? Lugar, string? Obs, bool Vs);

/// <summary>Cabeza de serie (tabla "Cabezas de Serie" del ASP).</summary>
/// <param name="CabezaSerie">Número (<c>CDS</c>; en las liguillas, <c>NumPareja</c>/<c>NumEquipo</c>, como el ASP).</param>
/// <param name="Nombre">"JUGADOR 1/JUGADOR 2" o nombre del equipo.</param>
/// <param name="Puntos">Suma de <c>Puntos</c> de los jugadores (o <c>Puntos</c> del equipo), redondeada.</param>
/// <param name="Wildcard"><c>true</c> si algún jugador tiene <c>Wildcard = 'S'</c> (el ASP pintaba "Wildcard" en lugar de los puntos).</param>
public sealed record CabezaSerieCuadroDto(int CabezaSerie, string Nombre, int? Puntos, bool Wildcard);

/// <summary>Fila de la clasificación de una liguilla (<c>SFEP_Torneos_Liguilla</c>).</summary>
/// <param name="Posicion">Puesto.</param>
/// <param name="Nombre">Pareja o equipo (<c>Pareja</c>; si está vacío, el nombre del participante <c>NumPareja</c>).</param>
/// <param name="Puntos">Puntos.</param>
/// <param name="Pg">Partidos ganados.</param>
/// <param name="Pp">Partidos perdidos.</param>
/// <param name="SetsGanados">Sets ganados.</param>
/// <param name="SetsPerdidos">Sets perdidos.</param>
/// <param name="JuegosGanados">Juegos ganados.</param>
/// <param name="JuegosPerdidos">Juegos perdidos.</param>
/// <param name="EnfrentamientosGanados">Enfrentamientos ganados (<c>EG</c>, liguillas de equipos).</param>
/// <param name="EnfrentamientosPerdidos">Enfrentamientos perdidos (<c>EP</c>, liguillas de equipos).</param>
public sealed record ClasificacionLiguillaDto(
    int? Posicion,
    string Nombre,
    int? Puntos,
    int? Pg,
    int? Pp,
    int? SetsGanados,
    int? SetsPerdidos,
    int? JuegosGanados,
    int? JuegosPerdidos,
    int? EnfrentamientosGanados,
    int? EnfrentamientosPerdidos);

/// <summary>Un cuadro del torneo (combinación distinta de género, categoría, nivel y cuadro en <c>SFEP_Torneos_Cuadros</c>).</summary>
/// <param name="Genero"><c>M</c> | <c>F</c> | <c>X</c>.</param>
/// <param name="Categoria">Categoría (<c>ABSOLUTO</c>, <c>Cat50</c>…).</param>
/// <param name="Nivel">Nivel.</param>
/// <param name="Cuadro">Cuadro.</param>
/// <param name="Publicado">Columna <c>Ver…</c> del nivel distinta de <c>'N'</c> (y con fila de desglose).</param>
public sealed record CuadroDisponibleDto(string Genero, string Categoria, int Nivel, string Cuadro, bool Publicado);

/// <summary>Cuadros de un torneo (para enlazarlos desde listados sin conocer género, categoría, nivel ni cuadro).</summary>
/// <param name="Torneo">Datos generales del torneo.</param>
/// <param name="Cuadros">Cuadros por género (M, F, X), categoría, nivel y cuadro (Final, Consola y el resto); vacío si aún no hay.</param>
public sealed record CuadrosTorneoDto(TorneoCuadroDto Torneo, IReadOnlyList<CuadroDisponibleDto> Cuadros);

/// <summary>Detalle de un enfrentamiento entre equipos (sustituye a <c>EquiposEnfrentamientos.asp</c>).</summary>
/// <param name="Equipo1">Equipo de arriba (<c>Equipo1</c> → <c>SFEP_Torneos_Equipos</c>).</param>
/// <param name="Equipo2">Equipo de abajo.</param>
/// <param name="Partidos">Partidos del enfrentamiento por <c>Partido</c>.</param>
public sealed record EnfrentamientoEquiposDto(string? Equipo1, string? Equipo2, IReadOnlyList<PartidoEnfrentamientoDto> Partidos);

/// <summary>Partido de un enfrentamiento entre equipos (<c>SFEP_Torneos_ResultadosEquipos</c>).</summary>
/// <param name="Partido">Número de partido.</param>
/// <param name="Pareja1">Jugadores del equipo 1 (<c>SFEP_Fichas.NombreCompleto</c> de <c>Equipo1_Lic1/2</c>).</param>
/// <param name="Pareja2">Jugadores del equipo 2.</param>
/// <param name="Sets">Juegos de cada equipo por set; el tercero se omite si es 0-0 (como el ASP).</param>
/// <param name="Obs1">Observación del equipo 1 (<c>Obs_Equipo1</c>: "Abandono"…).</param>
/// <param name="Obs2">Observación del equipo 2.</param>
public sealed record PartidoEnfrentamientoDto(
    int Partido,
    IReadOnlyList<string> Pareja1,
    IReadOnlyList<string> Pareja2,
    IReadOnlyList<SetEnfrentamientoDto> Sets,
    string? Obs1,
    string? Obs2);

/// <summary>Juegos de un set.</summary>
/// <param name="E1">Juegos del equipo 1.</param>
/// <param name="E2">Juegos del equipo 2.</param>
public sealed record SetEnfrentamientoDto(int? E1, int? E2);

/// <summary>Clave de un cuadro (parámetros de ruta ya validados y normalizados).</summary>
/// <param name="IdTorneo"><c>IdPrueba</c>.</param>
/// <param name="Genero"><c>M</c> | <c>F</c> | <c>X</c>.</param>
/// <param name="Categoria">Categoría recortada.</param>
/// <param name="Nivel">Nivel.</param>
/// <param name="Cuadro">Cuadro recortado.</param>
public sealed record ClaveCuadro(int IdTorneo, string Genero, string Categoria, int Nivel, string Cuadro);

/// <summary>
/// Resultado de una consulta de cuadros: <paramref name="Torneo"/> <c>null</c> = el torneo no existe, está borrado o no
/// es visible; <paramref name="Datos"/> <c>null</c> con torneo = el cuadro (o el enfrentamiento) no existe.
/// </summary>
public sealed record ConsultaCuadros<T>(TorneoCuadroDto? Torneo, T? Datos) where T : class;
