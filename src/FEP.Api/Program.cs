using FEP.Api.Data.Repositories.Avisos;
using FEP.Api.Data.Repositories.Banners;
using FEP.Api.Data.Repositories.Clubes;
using FEP.Api.Data.Repositories.Competicion;
using FEP.Api.Data.Repositories.Federacion;
using FEP.Api.Data.Repositories.Formacion;
using FEP.Api.Data.Repositories.Licencias;
using FEP.Api.Data.Repositories.Micrositios;
using FEP.Api.Data.Repositories.Multimedia;
using FEP.Api.Data.Repositories.Navegacion;
using FEP.Api.Data.Repositories.Noticias;
using FEP.Api.Data.Repositories.Patrocinadores;
using FEP.Api.Infrastructure;
using FEP.Api.Infrastructure.Asistente;

var builder = WebApplication.CreateBuilder(args);

// --- Servicios -------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Conexión a SQL Server, URLs de medios, CORS del front y caché de salida.
builder.Services.AddFepInfraestructura(builder.Configuration);

// Repositorios (Dapper, solo lectura). Migrados desde index.asp + Menus.inc,
// BarraLateral.inc y PiePagina.inc.
builder.Services.AddScoped<IMenuRepository, MenuRepository>();
builder.Services.AddScoped<IRedesSocialesRepository, RedesSocialesRepository>();
builder.Services.AddScoped<INoticiasRepository, NoticiasRepository>();
builder.Services.AddScoped<IBannersRepository, BannersRepository>();
builder.Services.AddScoped<IConvocatoriasRepository, ConvocatoriasRepository>();
builder.Services.AddScoped<IAvisosRepository, AvisosRepository>();
builder.Services.AddScoped<IPatrocinadoresRepository, PatrocinadoresRepository>();
builder.Services.AddScoped<IFotosRepository, FotosRepository>();

// Organos_Gobierno.asp: contenido estructurado en Contenidos/organos-gobierno.json (no hay tablas en BD).
builder.Services.AddSingleton<IOrganosGobiernoRepository, OrganosGobiernoRepository>();

// Equipo_FEP.asp: datos transcritos de las imágenes del organigrama a Contenidos/equipo-fep.json (no hay tablas en BD).
builder.Services.AddSingleton<IEquipoFepRepository, EquipoFepRepository>();

// Ley_De_Transparencia.asp: contenido estructurado en Contenidos/transparencia.json (no hay tablas en BD).
builder.Services.AddSingleton<ITransparenciaRepository, TransparenciaRepository>();

// Federaciones.asp: listado y reserva de Zoom escritos a mano en el HTML → Contenidos/federaciones-autonomicas.json.
builder.Services.AddSingleton<IFederacionesAutonomicasRepository, FederacionesAutonomicasRepository>();

// Datos_Federacion.asp (/Federacion/{id}/{nombre}): ficha de federación con estadísticas de licencias
// (Dapper; SFEP_Federaciones, SFEP_Licencia, SFEP_Fichas, SFEP_Clubes, SFEP_Clubes_Licencia, SFEP_Web_Historial).
builder.Services.AddScoped<IFichaFederacionRepository, FichaFederacionRepository>();

// Clubes.asp: buscador de clubes federados (Dapper; SFEP_Clubes, SFEP_Clubes_Licencia, SFEP_Federaciones, SFEP_Provincias).
builder.Services.AddScoped<IClubesRepository, ClubesRepository>();
builder.Services.AddScoped<IFichaClubRepository, FichaClubRepository>();

// Microsite.asp (+ BarraLateralMicrosite.inc): mini-CMS de micrositios (Dapper; SFEP_Microsites,
// SFEP_Microsites_Menus, SFEP_Microsites_Posts, SFEP_Microsites_Archivos).
builder.Services.AddScoped<IMicrositiosRepository, MicrositiosRepository>();

// Campeonatos.asp (+ Torneos_Previo.asp para el enlace de cada torneo): listado de campeonatos por
// temporada (Dapper; SFEP_Torneos, SFEP_Clubes).
builder.Services.AddScoped<ICampeonatosRepository, CampeonatosRepository>();

// Impulso.asp (/Programa_Impulso): presentación y PDF escritos a mano en el HTML → Contenidos/programa-impulso.json;
// torneos del programa por temporada (Dapper; SFEP_Torneos con ProgramaImpulso = 'S', SFEP_Clubes).
builder.Services.AddSingleton<IContenidoProgramaImpulsoRepository, ContenidoProgramaImpulsoRepository>();
builder.Services.AddScoped<ITorneosProgramaImpulsoRepository, TorneosProgramaImpulsoRepository>();

// Camp_Equipos.asp (?IdTorneo=): ficha de campeonato por equipos (Dapper; SFEP_Torneos, SFEP_Torneos_Desglose,
// SFEP_Torneos_Cuadros_Nombres, SFEP_Torneos_Adjuntos, SFEP_Torneos_Arbitraje, SFEP_Torneos_Noticias, SFEP_Noticias,
// SFEP_CategoriasMenores, SFEP_Inscripciones, SFEP_Clubes). Vídeos y excepciones por torneo escritos a mano en el
// ASP → Contenidos/campeonatos-equipos.json.
builder.Services.AddSingleton<IContenidoCampeonatosEquiposRepository, ContenidoCampeonatosEquiposRepository>();
builder.Services.AddScoped<ICampeonatoEquiposRepository, CampeonatoEquiposRepository>();

// Camp_Silla.asp (?IdTorneo=; Camp_Parejas.asp redirigía aquí los torneos con Circuito='SILLA'): ficha de torneo de
// pádel en silla de ruedas por parejas (Dapper; SFEP_Torneos, SFEP_Torneos_Desglose, SFEP_Torneos_Cuadros_Nombres,
// SFEP_RankingTorneos + SFEP_Fichas (cuadro de honor), SFEP_Torneos_Adjuntos, SFEP_Torneos_Arbitraje,
// SFEP_Torneos_Noticias, SFEP_Noticias, SFEP_CategoriasMenores, SFEP_Inscripciones, SFEP_Licencia, SFEP_Clubes).
// Sin contenido escrito a mano que conservar (el bloque fijo del torneo 12578 no era de silla).
builder.Services.AddScoped<ICampeonatoSillaRepository, CampeonatoSillaRepository>();

// Calendario_Menores_FFAA.asp y Calendario_Veteranos_FFAA.asp: pruebas autonómicas puntuables
// escritas a mano en el HTML → Contenidos/calendarios-autonomicos/{categoria}.json (las consultas a SFEP_Calendario
// del ASP no se pintaban).
builder.Services.AddSingleton<ICalendariosAutonomicosRepository, CalendariosAutonomicosRepository>();

// CalendarioFEP.asp (/CalendarioFEP): calendario oficial de competición por temporada (Dapper; SFEP_Calendario,
// SFEP_Calendario_Categorias, SFEP_Calendario_Documentos, SFEP_Calendario_Archivo y SFEP_Torneos para el enlace a
// la ficha). El banner de la convocatoria para organizar torneos, escrito a mano en el ASP → Contenidos/calendario-fep.json.
// CalendarioFT.asp (/CalendarioFT): el mismo repositorio sirve los calendarios de las federaciones territoriales
// (SFEP_Federaciones + las mismas tablas con otra Federacion); territorio y nombre oficial de cada federación, de
// IFederacionesAutonomicasRepository (Contenidos/federaciones-autonomicas.json).
builder.Services.AddScoped<ICalendarioCompeticionRepository, CalendarioCompeticionRepository>();
builder.Services.AddSingleton<IContenidoCalendarioRepository, ContenidoCalendarioRepository>();

// Organizacion_Torneos.asp (/Organizacion_Torneos) + Organizacion_FueraPlazo.asp, cargarfechastorneo.asp,
// comboProvincias.asp y comboclubesLicitacion.asp: convocatoria para organizar las pruebas del calendario FEP (Dapper;
// SFEP_Calendario con Licitable = 'S', SFEP_Comunidades, SFEP_Provincias, SFEP_Calendario_Archivo y SFEP_Clubes).
// Temporada, plazo, textos y cláusula de protección de datos, escritos a mano en el ASP → Contenidos/organizacion-torneos.json.
// La solicitud se sigue enviando a Organizacion_P0.asp de la web actual (la API es de solo lectura).
builder.Services.AddScoped<IOrganizacionTorneosRepository, OrganizacionTorneosRepository>();
builder.Services.AddSingleton<IContenidoOrganizacionTorneosRepository, ContenidoOrganizacionTorneosRepository>();

// Rankings.asp (/Rankings): índice de rankings publicados (Dapper; SFEP_Rankings_Web con Visible = 'S').
builder.Services.AddScoped<IRankingsWebRepository, RankingsWebRepository>();

// Ranking_Absoluto.asp (/Ranking_Absoluto) + CargarRnkAbs.asp y CargarRnkAbsNombre.asp: clasificación por género,
// licencia o nombre (Dapper; SFEP_RankingFinal, SFEP_Fichas, SFEP_Ranking_Tipos). La normativa de cálculo, escrita a
// mano en el HTML → Contenidos/rankings/{clave}.json. Pensado para reutilizarse en el resto de Ranking_*.asp.
// Ranking_Menores.asp (/Ranking_Menores) + CargarRnkMen.asp: mismo repositorio (clave "menores", categoría × género
// declarados en ReglasClasificaciones; sin normativa: el ASP no la tenía).
// Ranking_Silla_Ruedas.asp (/Ranking_Silla_Ruedas): mismo repositorio (clave "silla-ruedas", TipoRanking 15, una lista
// mixta con licencias de silla "PS…" y el nombre de SFEP_RankingFinal; sin normativa).
// Ranking_Veteranos.asp (/Ranking_Veteranos) + CargarRnkVet.asp: mismo repositorio (clave "veteranos", categorías
// +35…+60 × género, TipoRanking 10035–11060 declarados en ReglasClasificaciones; sin normativa).
builder.Services.AddScoped<IClasificacionesRankingRepository, ClasificacionesRankingRepository>();
builder.Services.AddSingleton<INormativasRankingRepository, NormativasRankingRepository>();

// Ranking_Equipos_Absolutos.asp (/Ranking_Equipos_Absolutos): resultado de los Campeonatos Absolutos por equipos de
// cada temporada (Dapper; SFEP_Ranking_Equipos, SFEP_Clubes, SFEP_Provincias, SFEP_Federaciones). Una consulta en
// lugar de las 32 del ASP; territorio y nombre de cada federación, de IFederacionesAutonomicasRepository.
builder.Services.AddScoped<IRankingEquiposRepository, RankingEquiposRepository>();

// Estadisticas.asp (/Estadisticas?id={licencia}) + CargarPruebas.asp: estadísticas de un jugador o jugadora (Dapper,
// solo lectura; SFEP_Fichas, SFEP_Licencia, SFEP_Clubes, SFEP_RankingFinal, SFEP_Ranking_Tipos, SFEP_RankingTorneos,
// SFEP_Torneos, SFEP_Torneos_Jugadores, SFEP_Torneos_Resultados, SFEP_Torneos_Cuadros, SFEP_Ranking_Historico,
// SFEP_CategoriasMenores, SFEP_Inscripciones_Equipos(_Jugadores)). El ASP escribía en SFEP_estadisticasWeb y
// SFEP_RankingTorneos en cada visita: aquí no. Nombre de cada federación, de IFederacionesAutonomicasRepository.
builder.Services.AddScoped<IEstadisticasJugadorRepository, EstadisticasJugadorRepository>();

// Licencias_Buscar.asp (/Licencias_Buscar): buscador público "Busca tu licencia" por tipo (jugador/a, técnico/a,
// juez-árbitro, silla de ruedas), federación, nombre y/o fecha de nacimiento (Dapper, solo lectura; SFEP_Fichas,
// JugadorUltimoAno, SillaUltimoAno, SFEP_Licencia). Datos personales: sin caché, máximo 100 resultados y límite de
// búsquedas por IP (LimiteBusquedaLicencias).
builder.Services.AddScoped<ILicenciasRepository, LicenciasRepository>();
builder.Services.AddLimiteBusquedaLicencias();

// Previo.asp, PrevioEquipos.asp y las páginas de cuadro a las que redirigían (Cuadro4…128.asp, CuadroPrevia*.asp,
// CuadroPrePrevia*.asp, CuadroEquipos*.asp, Liguilla3Equipos/Liguilla4/Liguilla4Equipos.asp) + EquiposEnfrentamientos.asp:
// un único recurso de cuadros (Dapper; SFEP_Torneos, SFEP_Torneos_Desglose, SFEP_Torneos_Cuadros, SFEP_Torneos_Jugadores,
// SFEP_Torneos_Equipos, SFEP_Torneos_Resultados, SFEP_Torneos_Horarios, SFEP_Torneos_ResultadosEquipos,
// SFEP_Torneos_Liguilla, SFEP_Fichas). Las variantes *Excell.asp (exportación a Excel) no se migran.
builder.Services.AddScoped<ICuadrosRepository, CuadrosRepository>();

// Asistente virtual (burbuja de chat): corpus de normativa (Contenidos/asistente, generado con
// tools/generar-corpus-asistente.ps1), herramientas sobre los repositorios de rankings y estadísticas, cliente de Claude,
// presupuesto diario y límite de preguntas por IP. Clave en user-secrets: "Asistente:ApiKey".
builder.Services.AddFepAsistente(builder.Configuration);

var app = builder.Build();

// --- Canalización HTTP -----------------------------------------------------
// Cualquier excepción no controlada se devuelve como ProblemDetails (RFC 9457).
app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicies.Front);
app.UseRateLimiter();
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    // Documento OpenAPI en /openapi/v1.json
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

/// <summary>Punto de entrada (visible para los tests de integración con WebApplicationFactory).</summary>
public partial class Program { }
