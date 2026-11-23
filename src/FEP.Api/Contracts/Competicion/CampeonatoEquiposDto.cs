namespace FEP.Api.Contracts.Competicion;

/// <summary>
/// Ficha pública de un campeonato por equipos (página <c>Camp_Equipos.asp?IdTorneo={id}</c>): datos generales,
/// cuadros, notificaciones con adjuntos, inscripción de equipos, marcadores en directo, cartel y noticias
/// relacionadas. Datos de <c>SFEP_Torneos</c>, <c>SFEP_Torneos_Desglose</c>, <c>SFEP_Torneos_Cuadros_Nombres</c>,
/// <c>SFEP_Torneos_Adjuntos</c>, <c>SFEP_Torneos_Arbitraje</c>, <c>SFEP_Torneos_Noticias</c>/<c>SFEP_Noticias</c>,
/// <c>SFEP_CategoriasMenores</c>, <c>SFEP_Inscripciones</c> y <c>SFEP_Clubes</c>; los vídeos y las excepciones
/// que el ASP tenía escritas a mano salen de <c>Contenidos/campeonatos-equipos.json</c>.
/// Los campos <c>…Html</c> son HTML heredado del editor de la gestión, <b>sin sanear</b>: el cliente debe
/// limpiarlo con una lista blanca antes de pintarlo.
/// </summary>
/// <param name="Id"><c>IdPrueba</c>.</param>
/// <param name="Anio">Temporada (<c>Año</c>).</param>
/// <param name="Titulo">Nombre del campeonato (<c>Torneo</c>), sin espacios repetidos.</param>
/// <param name="Categoria"><c>absoluto</c> | <c>menores</c> | <c>veteranos</c> (de <c>ModalidadTorneo</c>) o <c>null</c> si es otro valor.</param>
/// <param name="Seleccion"><c>true</c> en los campeonatos de selecciones autonómicas (<c>SEL_*</c>).</param>
/// <param name="SillaRuedas"><c>true</c> si es de pádel en silla de ruedas (<c>ABSOLUTO</c> + <c>Circuito = 'SILLA'</c>).</param>
/// <param name="FechaInicio">Primer día (<c>FechaInicio</c>).</param>
/// <param name="FechaFin">Último día (<c>FechaFin</c>).</param>
/// <param name="Ciudad">Lugar (<c>Ciudad</c>), tal cual en la BD.</param>
/// <param name="Sedes">Club sede (<c>SFEP_Clubes</c> por <c>Club</c>, con web y coordenadas) y segunda sede (<c>Club2</c>, solo nombre).</param>
/// <param name="Cuotas">Cuotas de inscripción distintas de las categorías visibles, de mayor a menor (euros). El ASP solo pintaba la mayor.</param>
/// <param name="Generos">Géneros con categorías visibles (masculino, femenino, mixto) y, en cada uno, las categorías de edad de <c>SFEP_CategoriasMenores</c>.</param>
/// <param name="NotaPrensaHtml">Nota de prensa (<c>NotaPrensa</c>) si <c>VisibleNotaPrensa = 'S'</c>.</param>
/// <param name="Marcadores">Partidos con marcador en directo (<c>SFEP_Torneos_Arbitraje</c> visibles), por <c>IdPartido</c>.</param>
/// <param name="Videos">Retransmisiones de YouTube del campeonato (en el ASP, escritas a mano para el 28133).</param>
/// <param name="CuadrosPublicados"><c>true</c> si el torneo tiene <c>VerCuadros = 'S'</c> (aunque todavía no haya cuadros visibles).</param>
/// <param name="Cuadros">Cuadros visibles por género y categoría (vacío si <see cref="CuadrosPublicados"/> es <c>false</c>).</param>
/// <param name="NotificacionesHtml">Texto de notificaciones (<c>Notificaciones</c>).</param>
/// <param name="Adjuntos">Documentos de las notificaciones (<c>SFEP_Torneos_Adjuntos</c>), del más reciente al más antiguo.</param>
/// <param name="InscritosParejas">Parejas inscritas por género (<c>SFEP_Inscripciones</c>); vacío si no hay ninguna.</param>
/// <param name="Inscripcion">Plazo, información y pasos de la inscripción de equipos (siguen en la web actual).</param>
/// <param name="InfoAdicionalHtml">Información adicional (<c>InfoAdicional</c>).</param>
/// <param name="Cartel">Nombre original del cartel (<c>FotoCartel</c>) si <c>VisibleCartel = 'S'</c>.</param>
/// <param name="CartelUrl">URL absoluta y codificada del cartel (<c>Paginas/Torneos/{año}/{FotoCartel}</c> en la web actual).</param>
/// <param name="Noticias">Noticias relacionadas publicadas, de la más reciente a la más antigua.</param>
/// <param name="UrlWebActual">La misma ficha en la web actual (<c>Camp_Equipos.asp?IdTorneo={id}</c>).</param>
/// <param name="Consultado">Momento de la consulta (hora local del servidor, sin zona): referencia de los plazos.</param>
public sealed record CampeonatoEquiposDto(
    int Id,
    int Anio,
    string Titulo,
    string? Categoria,
    bool Seleccion,
    bool SillaRuedas,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string? Ciudad,
    IReadOnlyList<SedeCampeonatoDto> Sedes,
    IReadOnlyList<decimal> Cuotas,
    IReadOnlyList<GeneroCampeonatoDto> Generos,
    string? NotaPrensaHtml,
    IReadOnlyList<MarcadorDirectoDto> Marcadores,
    IReadOnlyList<VideoCampeonatoDto> Videos,
    bool CuadrosPublicados,
    IReadOnlyList<GrupoCuadrosDto> Cuadros,
    string? NotificacionesHtml,
    IReadOnlyList<AdjuntoCampeonatoDto> Adjuntos,
    IReadOnlyList<InscritosGeneroDto> InscritosParejas,
    InscripcionEquiposDto Inscripcion,
    string? InfoAdicionalHtml,
    string? Cartel,
    string? CartelUrl,
    IReadOnlyList<NoticiaRelacionadaDto> Noticias,
    string UrlWebActual,
    DateTime Consultado);

/// <summary>Sede del campeonato.</summary>
/// <param name="Nombre">Nombre del club.</param>
/// <param name="Web">Web del club como URL absoluta (se antepone <c>https://</c> solo si falta el esquema).</param>
/// <param name="Latitud">Latitud (<c>SFEP_Clubes.Latitud</c>) si es un número válido.</param>
/// <param name="Longitud">Longitud (<c>SFEP_Clubes.Longitud</c>) si es un número válido.</param>
public sealed record SedeCampeonatoDto(string Nombre, string? Web, double? Latitud, double? Longitud);

/// <summary>Género con categorías visibles en el campeonato.</summary>
/// <param name="Genero"><c>masculino</c> | <c>femenino</c> | <c>mixto</c>.</param>
/// <param name="Categorias">Categorías de edad (<c>Tipo</c> presente en <c>SFEP_CategoriasMenores</c>, de la más joven a la mayor); vacío en absoluto y veteranos.</param>
public sealed record GeneroCampeonatoDto(string Genero, IReadOnlyList<string> Categorias);

/// <summary>Marcador en directo de un partido (página <c>ArbitrajeCtoFep.asp</c> de la web actual, para incrustar).</summary>
/// <param name="Codigo">Código del partido (<c>Aleatorio</c>).</param>
/// <param name="Url">URL absoluta del marcador.</param>
public sealed record MarcadorDirectoDto(string Codigo, string Url);

/// <summary>Retransmisión en vídeo.</summary>
/// <param name="Titulo">Título (p. ej. "Semifinales, sábado por la mañana").</param>
/// <param name="Url">URL de inserción de YouTube (<c>https://www.youtube.com/embed/…</c>).</param>
public sealed record VideoCampeonatoDto(string Titulo, string Url);

/// <summary>Cuadros de un género y categoría (una fila de <c>SFEP_Torneos_Desglose</c> con <c>VerCuadros = 'S'</c>).</summary>
/// <param name="Genero"><c>masculino</c> | <c>femenino</c> | <c>mixto</c>.</param>
/// <param name="Categoria">Categoría del desglose (<c>Tipo</c>, p. ej. <c>ABSOLUTO</c>, <c>JUNIOR</c>).</param>
/// <param name="Cuadros">Cuadros visibles en el orden del ASP.</param>
public sealed record GrupoCuadrosDto(string Genero, string Categoria, IReadOnlyList<CuadroEquiposDto> Cuadros);

/// <summary>Enlace a un cuadro de juego (sigue en la web actual: <c>PrevioEquipos.asp</c>).</summary>
/// <param name="Titulo">Nombre del cuadro (personalizado en <c>SFEP_Torneos_Cuadros_Nombres</c> o el fijo del ASP).</param>
/// <param name="Nivel">Nivel (1–10 categorías; 21 previa; 31 pre-previa).</param>
/// <param name="Cuadro"><c>Final</c> | <c>Consola</c> | <c>Liguilla</c> | <c>GrupoA</c>…<c>GrupoZ</c> (valor que espera <c>PrevioEquipos.asp</c>).</param>
/// <param name="Tipo"><c>final</c> | <c>consolacion</c> | <c>liguilla</c> | <c>grupo</c> | <c>previa</c>.</param>
/// <param name="Url">URL absoluta del cuadro en la web actual.</param>
public sealed record CuadroEquiposDto(string Titulo, int Nivel, string Cuadro, string Tipo, string Url);

/// <summary>Documento adjunto a las notificaciones del campeonato.</summary>
/// <param name="Titulo">Texto del enlace (<c>Link</c>).</param>
/// <param name="Tipo">Tipo declarado en la gestión (<c>Icono</c>: <c>pdf</c>, <c>xls</c>, <c>pic</c>, <c>web</c>…) o <c>null</c> si no consta.</param>
/// <param name="Archivo">Nombre original (<c>Archivo</c>).</param>
/// <param name="Url">URL absoluta y codificada (<c>Paginas/Torneos/{año}/{Archivo}</c> en la web actual).</param>
public sealed record AdjuntoCampeonatoDto(string Titulo, string? Tipo, string? Archivo, string? Url);

/// <summary>Parejas inscritas de un género (listado en <c>Camp_Parejas_Inscritos.asp</c> de la web actual).</summary>
/// <param name="Genero"><c>masculino</c> | <c>femenino</c> | <c>mixto</c>.</param>
/// <param name="Parejas">Número de inscripciones no borradas.</param>
/// <param name="Url">Listado en la web actual.</param>
public sealed record InscritosGeneroDto(string Genero, int Parejas, string Url);

/// <summary>
/// Inscripción de equipos. Los pasos (pago, alta y modificación de equipos) siguen en la web actual: aquí solo
/// se enlazan. El cliente debe volver a comprobar <see cref="FechaLimite"/> con su reloj (la respuesta se cachea).
/// </summary>
/// <param name="Abierta"><c>true</c> si <c>FechaInscripcion</c> es posterior a <see cref="CampeonatoEquiposDto.Consultado"/>.</param>
/// <param name="FechaLimite">Fin del plazo de inscripción (<c>FechaInscripcion</c>, hora local de España).</param>
/// <param name="Por"><c>clubes</c> (<c>ClaseEquipos = 'CLUBES'</c>) o <c>federaciones</c>: quién recibe el código de inscripción.</param>
/// <param name="InformacionHtml">Información para la inscripción (<c>InfoInscripcion</c>).</param>
/// <param name="Acciones">Pasos de la inscripción en el orden del ASP (solo se muestran con el plazo abierto).</param>
/// <param name="EquiposInscritosUrl">Equipos inscritos e integrantes (<c>Camp_Equipos_Modificar.asp</c> en la web actual).</param>
/// <param name="FechaLimitePuntuacion">Fin del plazo para cambiar los jugadores que puntúan (<c>FechaFijarPuntua</c>).</param>
public sealed record InscripcionEquiposDto(
    bool Abierta,
    DateTime? FechaLimite,
    string Por,
    string? InformacionHtml,
    IReadOnlyList<AccionInscripcionDto> Acciones,
    string EquiposInscritosUrl,
    DateTime? FechaLimitePuntuacion);

/// <summary>Paso de la inscripción de equipos (página de la web actual).</summary>
/// <param name="Clave"><c>preinscripcion-club</c> | <c>pago-equipos</c> | <c>clubes-preinscritos</c> | <c>clubes-pagados</c> | <c>excel-clasificados</c> | <c>nuevo-equipo</c> | <c>modificar-equipo</c>.</param>
/// <param name="Texto">Texto del enlace (el del ASP).</param>
/// <param name="Nota">Aclaración que el ASP mostraba en un recuadro azul bajo el enlace.</param>
/// <param name="Url">URL absoluta en la web actual.</param>
/// <param name="Hasta">Si el paso tiene un plazo propio (preinscripción), su fin.</param>
public sealed record AccionInscripcionDto(string Clave, string Texto, string? Nota, string Url, DateTime? Hasta);

/// <summary>Noticia relacionada con el campeonato.</summary>
/// <param name="IdNoticia"><c>IdNoticia</c>.</param>
/// <param name="Titulo">Titular (las <c>``</c> de la gestión pasan a comillas dobles).</param>
/// <param name="Fecha">Fecha de publicación.</param>
/// <param name="Resumen">Entradilla (<c>DescriCorta</c>, puede traer HTML heredado).</param>
/// <param name="Imagen">Foto original (<c>Foto</c>, bajo <c>refs/Noticias/</c>).</param>
/// <param name="ImagenUrl">URL absoluta de la foto.</param>
/// <param name="Enlace">Enlace de la noticia (<c>Link</c>, relativo a la web actual o absoluto).</param>
/// <param name="Ventana"><c>_blank</c> si es externa (<c>TipoNoticia = 1</c>).</param>
public sealed record NoticiaRelacionadaDto(
    int IdNoticia,
    string Titulo,
    DateTime? Fecha,
    string? Resumen,
    string? Imagen,
    string? ImagenUrl,
    string? Enlace,
    string? Ventana);

/// <summary>
/// Resultado de buscar una ficha: la ficha, o por qué no se puede mostrar aquí.
/// </summary>
/// <param name="Ficha">Ficha del campeonato por equipos (<c>null</c> si no existe, no es visible o no es por equipos).</param>
/// <param name="FichaWebActual">
/// Si el torneo existe pero no es por equipos, su ficha en la web actual (según <c>Torneos_Previo.asp</c>);
/// <c>null</c> si no existe o no es visible.
/// </param>
public sealed record ConsultaCampeonatoEquipos(CampeonatoEquiposDto? Ficha, string? FichaWebActual);

/// <summary>
/// Contenido escrito a mano en <c>Camp_Equipos.asp</c> (vídeos y excepciones por torneo), ahora en
/// <c>Contenidos/campeonatos-equipos.json</c>.
/// </summary>
/// <param name="Actualizado">Fecha de revisión del contenido.</param>
/// <param name="Videos">Retransmisiones por torneo.</param>
/// <param name="SinListadoClubes">Torneos (<c>IdPrueba</c>) en los que no se enlaza el listado de clubes preinscritos.</param>
/// <param name="SinPagoEquipos">Torneos en los que no se enlaza el pago de equipos del club.</param>
/// <param name="NombresCuadros">Nombres de cuadro fijados a mano para un torneo (tienen prioridad sobre el fijo del ASP).</param>
public sealed record ContenidoCampeonatosEquipos(
    DateOnly? Actualizado,
    IReadOnlyList<VideoTorneoContenido> Videos,
    IReadOnlyList<int> SinListadoClubes,
    IReadOnlyList<int> SinPagoEquipos,
    IReadOnlyList<NombreCuadroContenido> NombresCuadros)
{
    /// <summary>Contenido vacío (sin excepciones ni vídeos).</summary>
    public static ContenidoCampeonatosEquipos Vacio { get; } = new(null, [], [], [], []);
}

/// <summary>Vídeo de un torneo.</summary>
/// <param name="Torneo"><c>IdPrueba</c>.</param>
/// <param name="Titulo">Título.</param>
/// <param name="Url">URL de inserción de YouTube.</param>
public sealed record VideoTorneoContenido(int Torneo, string Titulo, string Url);

/// <summary>Nombre de cuadro fijado a mano.</summary>
/// <param name="Torneo"><c>IdPrueba</c>.</param>
/// <param name="Genero"><c>M</c> | <c>F</c> | <c>X</c>.</param>
/// <param name="Nivel">Nivel del cuadro.</param>
/// <param name="Cuadro"><c>Final</c>, <c>Consola</c>, <c>Liguilla</c>, <c>GrupoA</c>…</param>
/// <param name="Titulo">Nombre a mostrar.</param>
public sealed record NombreCuadroContenido(int Torneo, string Genero, int Nivel, string Cuadro, string Titulo);
