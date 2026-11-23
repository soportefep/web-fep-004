namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Ficha pública de un torneo de pádel en silla de ruedas por parejas (página <c>Camp_Silla.asp?IdTorneo={id}</c>, a la
/// que <c>Camp_Parejas.asp</c> redirigía los torneos con <c>Circuito = 'SILLA'</c>): datos generales, marcadores en
/// directo, cuadros, cuadro de honor, notificaciones con adjuntos, parejas inscritas, inscripción, cartel y noticias
/// relacionadas. Datos de <c>SFEP_Torneos</c>, <c>SFEP_Torneos_Desglose</c>, <c>SFEP_Torneos_Cuadros_Nombres</c>,
/// <c>SFEP_RankingTorneos</c>/<c>SFEP_Fichas</c>, <c>SFEP_Torneos_Adjuntos</c>, <c>SFEP_Torneos_Arbitraje</c>,
/// <c>SFEP_Torneos_Noticias</c>/<c>SFEP_Noticias</c>, <c>SFEP_CategoriasMenores</c>, <c>SFEP_Inscripciones</c>,
/// <c>SFEP_Licencia</c> y <c>SFEP_Clubes</c>. Reutiliza los sub-DTO de <see cref="CampeonatoEquiposDto"/>.
/// Los campos <c>…Html</c> son HTML heredado del editor de la gestión, <b>sin sanear</b>: el cliente debe
/// limpiarlo con una lista blanca antes de pintarlo.
/// </summary>
/// <param name="Id"><c>IdPrueba</c>.</param>
/// <param name="Anio">Temporada (<c>Año</c>).</param>
/// <param name="Titulo">Nombre del torneo (<c>Torneo</c>), sin espacios repetidos.</param>
/// <param name="Categoria"><c>absoluto</c> (de <c>ModalidadTorneo</c>) o <c>null</c> si es otro valor.</param>
/// <param name="Seleccion"><c>true</c> si es de selecciones autonómicas (<c>SEL_ABS</c>).</param>
/// <param name="FechaInicio">Primer día (<c>FechaInicio</c>).</param>
/// <param name="FechaFin">Último día (<c>FechaFin</c>).</param>
/// <param name="Ciudad">Lugar (<c>Ciudad</c>), tal cual en la BD.</param>
/// <param name="Sedes">Club sede (<c>SFEP_Clubes</c> por <c>Club</c>, con web y coordenadas) y segunda sede (<c>Club2</c>, solo nombre).</param>
/// <param name="Cuotas">Cuotas de inscripción distintas de las categorías visibles, de mayor a menor (euros). El ASP solo pintaba la mayor.</param>
/// <param name="Generos">Géneros con categorías visibles y, en cada uno, las categorías de edad de <c>SFEP_CategoriasMenores</c>.</param>
/// <param name="NotaPrensaHtml">Nota de prensa (<c>NotaPrensa</c>) si <c>VisibleNotaPrensa = 'S'</c>.</param>
/// <param name="Marcadores">Partidos con marcador en directo (<c>SFEP_Torneos_Arbitraje</c> visibles), por <c>IdPartido</c>.</param>
/// <param name="CuadrosPublicados"><c>true</c> si el torneo tiene <c>VerCuadros = 'S'</c> y es <c>ABSOLUTO</c> (aunque aún no haya cuadros visibles).</param>
/// <param name="Cuadros">Cuadros visibles por género (vacío si <see cref="CuadrosPublicados"/> es <c>false</c>); enlazan a <c>Previo.asp</c>.</param>
/// <param name="CuadroHonor">Clasificación final con los puntos de ranking (<c>SFEP_RankingTorneos</c>, ranking de silla 15), de más a menos puntos.</param>
/// <param name="NotificacionesHtml">Texto de notificaciones (<c>Notificaciones</c>).</param>
/// <param name="Adjuntos">Documentos de las notificaciones (<c>SFEP_Torneos_Adjuntos</c>), del más reciente al más antiguo.</param>
/// <param name="InscritosParejas">Parejas inscritas por género visible (<c>SFEP_Inscripciones</c>); vacío si no hay ninguna.</param>
/// <param name="Entrenadores">Entrenadores inscritos con licencia de técnico tramitada en la temporada; <c>null</c> si no hay ninguno o no hay parejas.</param>
/// <param name="Inscripcion">Plazo, condiciones y enlace al formulario de inscripción (sigue en la web actual).</param>
/// <param name="InfoAdicionalHtml">Información adicional (<c>InfoAdicional</c>) si <c>VisibleCartel = 'S'</c> (como en el ASP).</param>
/// <param name="Cartel">Nombre original del cartel (<c>FotoCartel</c>) si <c>VisibleCartel = 'S'</c>.</param>
/// <param name="CartelUrl">URL absoluta y codificada del cartel (<c>Paginas/Torneos/{año}/{FotoCartel}</c> en la web actual).</param>
/// <param name="Noticias">Noticias relacionadas publicadas, de la más reciente a la más antigua.</param>
/// <param name="UrlWebActual">La misma ficha en la web actual (<c>Camp_Silla.asp?IdTorneo={id}</c>).</param>
/// <param name="Consultado">Momento de la consulta (hora local del servidor, sin zona): referencia del plazo.</param>
public sealed record CampeonatoSillaDto(
    int Id,
    int Anio,
    string Titulo,
    string? Categoria,
    bool Seleccion,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string? Ciudad,
    IReadOnlyList<SedeCampeonatoDto> Sedes,
    IReadOnlyList<decimal> Cuotas,
    IReadOnlyList<GeneroCampeonatoDto> Generos,
    string? NotaPrensaHtml,
    IReadOnlyList<MarcadorDirectoDto> Marcadores,
    bool CuadrosPublicados,
    IReadOnlyList<GrupoCuadrosDto> Cuadros,
    IReadOnlyList<PuestoHonorDto> CuadroHonor,
    string? NotificacionesHtml,
    IReadOnlyList<AdjuntoCampeonatoDto> Adjuntos,
    IReadOnlyList<InscritosGeneroDto> InscritosParejas,
    EntrenadoresInscritosDto? Entrenadores,
    InscripcionSillaDto Inscripcion,
    string? InfoAdicionalHtml,
    string? Cartel,
    string? CartelUrl,
    IReadOnlyList<NoticiaRelacionadaDto> Noticias,
    string UrlWebActual,
    DateTime Consultado);

/// <summary>Jugador o jugadora del cuadro de honor (una fila por persona; la pareja comparte puesto y puntos).</summary>
/// <param name="Puesto">Texto del puesto tal cual en la gestión (<c>Ronda</c>: "Campeón", "Finalista", "3º Clasificado", "11avo"…).</param>
/// <param name="Licencia">Licencia de pádel en silla (<c>IdJugador</c>, sin el prefijo "PS").</param>
/// <param name="Nombre">Nombre completo (<c>SFEP_Fichas.NOMBRECOMPLETO</c>).</param>
/// <param name="Puntos">Puntos para el ranking de silla de ruedas.</param>
/// <param name="EstadisticasUrl">Estadísticas del jugador en la web actual (<c>Silla-Ruedas-Est?id=PS{licencia}</c>).</param>
public sealed record PuestoHonorDto(string? Puesto, int Licencia, string Nombre, decimal Puntos, string EstadisticasUrl);

/// <summary>Entrenadores inscritos (columna "Inscripciones Coach" del ASP).</summary>
/// <param name="Numero">Entrenadores distintos con licencia de técnico tramitada en la temporada del torneo.</param>
/// <param name="Url">Listado en la web actual (<c>Camp_Parejas_Inscritos.asp?tipo=C</c>).</param>
public sealed record EntrenadoresInscritosDto(int Numero, string Url);

/// <summary>
/// Inscripción en el torneo. El formulario (comprobación de licencias, clasificación funcional, fecha de nacimiento,
/// DNI, contacto y entrenador) sigue en la web actual: aquí solo se enlaza. El cliente debe volver a comprobar
/// <see cref="FechaLimite"/> con su reloj (la respuesta se cachea).
/// </summary>
/// <param name="Abierta"><c>true</c> si <c>FechaInscripcion</c> es posterior a <see cref="CampeonatoSillaDto.Consultado"/>.</param>
/// <param name="FechaLimite">Fin del plazo de inscripción (<c>FechaInscripcion</c>, hora local de España).</param>
/// <param name="Individual"><c>true</c> si se inscribe una sola persona (<c>InscripcionSolo</c> distinto de <c>'N'</c>); si no, por parejas.</param>
/// <param name="InformacionHtml">Información para la inscripción (<c>InfoInscripcion</c>).</param>
/// <param name="CondicionesHtml">Condiciones de inscripción (<c>infFormulario</c>).</param>
/// <param name="FormularioUrl">Formulario de inscripción en la web actual (<c>Camp_Silla.asp?IdTorneo={id}</c>).</param>
public sealed record InscripcionSillaDto(
    bool Abierta,
    DateTime? FechaLimite,
    bool Individual,
    string? InformacionHtml,
    string? CondicionesHtml,
    string FormularioUrl);

/// <summary>Resultado de buscar una ficha de silla de ruedas: la ficha, o por qué no se puede mostrar aquí.</summary>
/// <param name="Ficha">Ficha del torneo (<c>null</c> si no existe, no es visible o no es de silla de ruedas por parejas).</param>
/// <param name="FichaWebActual">
/// Si el torneo existe pero tiene otra ficha (según las redirecciones de <c>Camp_Silla.asp</c>/<c>Camp_Parejas.asp</c>
/// y <c>Torneos_Previo.asp</c>), su URL absoluta; <c>null</c> si no existe o no es visible.
/// </param>
public sealed record ConsultaCampeonatoSilla(CampeonatoSillaDto? Ficha, string? FichaWebActual);
