# Genera docs/migracion/mapa-web-fep.xlsx: mapa de páginas de www.padelfederacion.es
# y su estado en la nueva web (FEP.Api + FEP.Web). Requiere Microsoft Excel instalado.
# Rastreo de la web actual: 12/11/2026. Actualiza las filas (columna Estado) al migrar páginas.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$salida = Join-Path $raiz 'docs\migracion\mapa-web-fep.xlsx'

# Rutas nuevas propuestas (docs/migracion/redirecciones.csv)
$redir = @{}
Import-Csv (Join-Path $raiz 'docs\migracion\redirecciones.csv') | ForEach-Object { $redir[$_.url_antigua.ToLower()] = $_ }

# Sección | Página | Nivel | URL actual | Origen ASP | Tipo | Estado web actual | Fase | Estado | Endpoint API | Front nuevo | Notas
$filas = @'
Portada|Inicio (Home)|Menú|/Home|index.asp|Portada dinámica (BD)|OK|1|Desarrollado|/api/v1/portada (+ menu, banners, avisos, noticias/destacadas, eventos/destacados, formacion/convocatorias, patrocinadores, fotos/ultimas)|index.html + home.js|Diseño Figma. Falta quitar mock/mocks.js antes de producción y activar 301 /Home → /
Portada|Cabecera, menú y redes sociales|Común|(todas)|Menus.inc|Componente común|OK|1|Desarrollado|/api/v1/menu, /api/v1/redes-sociales|comun.js|Enlace Google+ de la cabecera está muerto (servicio cerrado): no migrarlo
Portada|Barra lateral (avisos, patrocinadores, banners)|Común|(todas)|BarraLateral.inc + variantes|Componente común|OK|1|En proceso|/api/v1/avisos, /api/v1/patrocinadores, /api/v1/banners|comun.js|Endpoints hechos; faltan las 14 variantes por sección (Censo, Licitación, Microsite, MyP, Adaptado…) en un solo componente
Portada|Pie de página (últimas fotos, enlaces)|Común|(todas)|PiePagina.inc|Componente común|OK|1|Desarrollado|/api/v1/multimedia/fotos/ultimas, /api/v1/banners?tipo=interes|comun.js|Textos del pie con codificación rota en la web actual ("POLÏ¿½TICA")
Federación|Órganos de Gobierno|Menú|/Organos_De_Gobierno|Organos_Gobierno.asp|Informativa + documentos|OK|2|Desarrollado|/api/v1/federacion/organos-gobierno|organos-de-gobierno.html|Contenido en Contenidos/organos-gobierno.json (no estaba en BD). Pendiente activar 301
Federación|Ley de Transparencia|Menú|/Ley_De_Transparencia|Ley_De_Transparencia.asp|Informativa + documentos (12 tablas)|OK|2|Desarrollado|/api/v1/federacion/transparencia|ley-de-transparencia.html|Contenido en Contenidos/transparencia.json (no estaba en BD): 22 apartados, 39 PDF (todos 200). Pendiente activar 301 y decisiones FEP (PDF Babolat duplicado, datos desactualizados)
Federación|Equipo FEP|Menú|/Equipo_FEP|Equipo_FEP.asp|Informativa|OK|2|Desarrollado|/api/v1/federacion/equipo|equipo-fep.html|Solo eran dos imágenes: datos transcritos a Contenidos/equipo-fep.json (no estaba en BD). Pendiente activar 301
Federación|Federaciones Autonómicas|Menú|/Federaciones|Federaciones.asp|Listado (escrito a mano, no BD) + calendario Google embebido|OK|2|Desarrollado|/api/v1/federacion/federaciones-autonomicas|federaciones.html|Contenido en Contenidos/federaciones-autonomicas.json (no consultaba la BD). Google Calendar en iframe con title y carga diferida. El enlace "Ficha" lleva a federacion.html?id={id}. Pendiente activar 301 (/Federaciones y /Federaciones_Territoriales)
Federación|Ficha de federación autonómica|Detalle|/Datos_Federacion.asp?Id=6|Datos_Federacion.asp|Detalle BD (20 federaciones)|OK|2|Desarrollado|/api/v1/federacion/federaciones-autonomicas/{id} (+ /api/v1/federacion/federaciones-autonomicas para nombre oficial y lista)|federacion.html?id=6 + federacion.js|SQL parametrizado (el ASP concatenaba id en 9 consultas y hacía SELECT * de SFEP_Federaciones, con Secret_Key). Estadísticas agregadas en SQL (el ASP traía ~120.000 filas para las edades). Highcharts sustituido por barras HTML/CSS accesibles + tabla. Clubes de la FEP: 1.274 (la web actual da 1.518 por duplicados del JOIN). comun.js traslada Datos_Federacion.asp?Id= y /Federacion/{id}/{nombre}. Pendiente activar 301 a /federacion
Federación|Clubes|Menú|/Clubes|Clubes.asp|Buscador BD|OK|2|Desarrollado|/api/v1/clubes, /api/v1/clubes/filtros|clubes.html|SQL parametrizado (el ASP concatenaba fed/provincia sin comillas), paginado (50) y criterios en la URL. La web actual muestra 0 clubes al entrar (error del ASP); la nueva, todos (DECISIÓN FEP). Ficha Club.asp sigue en la web actual. Pendiente activar 301
Federación|Ficha de club|Detalle|/Club.asp?Id=…|Club.asp|Detalle BD|OK|2|Pendiente|||Enlazada 250 veces desde Ranking Equipos Absolutos
Federación|Homologaciones (microsite)|Menú|/microsite.asp?id=7|Microsite.asp|Microsite CMS|OK|2|Desarrollado|/api/v1/micrositios/{id}, /api/v1/micrositios/{id}/opciones/{idOpcion}|micrositio.html?id=7 + micrositio.js|Página genérica para todos los micrositios (Id/IdOpcion → id/opcion; comun.js traslada la query). HTML de la BD saneado con lista blanca en el front. Los /single.php (WordPress) estaban en la barra lateral (patrocinadores): no se migran; si aparecen en un contenido se quita el enlace. Pendiente activar 301 /Microsite.asp → /micrositio
Federación|Homologaciones · Información|Subpágina|/Microsite.asp?Id=7&IdOpcion=19|Microsite.asp|Microsite CMS|OK|2|Desarrollado|/api/v1/micrositios/7/opciones/19|micrositio.html?id=7&opcion=19|Mismo front que el micrositio; navegación entre apartados sin recargar
Federación|Bienvenida (carta del presidente)|Mapa web|/Bienvenida|Bienvenida.asp|Informativa|OK|2|Pendiente|||No aparece en el menú, solo en Mapa Web
Federación|Pádel Adaptado|Enlace home|/Padel-Adaptado|Padel-Adaptado.asp|Noticias filtradas|OK|2|Pendiente|||
Federación|Microsite Asamblea / Censos|Subpágina|/Microsite.asp?Id=24&IdOpcion=234|Microsite.asp + Censo_*|Microsite + acceso privado|OK|6|En proceso|/api/v1/micrositios/24/opciones/234|micrositio.html?id=24&opcion=234|El micrositio 24 (Elecciones 2024) es público y ya se ve en micrositio.html. Sigue en la web actual la parte privada: consulta de censos (CensoJugadores, AccesoClubes… → Censo_*.asp con identificación) y micrositios 25/26 (cookie ControlAsambleaSFEP; la API responde 403)
Competición|Campeonatos|Menú|/Campeonatos|Campeonatos.asp|Listado BD con filtros (año, modalidad, tipo)|OK|2|Desarrollado|/api/v1/competicion/campeonatos|campeonatos.html + campeonatos.js|SQL parametrizado (el ASP concatenaba ano/modalidad/tipo y Torneos_Previo.asp el IdTorneo), sin N+1 de clubes. Enlace directo a la ficha (reglas de Torneos_Previo.asp en la API) en lugar de javascript:irCamp. Agrupado por mes, filtro "inscripción abierta". Fichas Camp_*.asp siguen en la web actual; circuito de menores solo http. Pendiente activar 301 y DECISIONES FEP (CEM 2021, selecciones en categoría)
Competición|Ficha torneo por parejas|Detalle|/Camp_Parejas.asp?IdTorneo=…|Camp_Parejas.asp|Detalle BD + inscripción|OK|3|Pendiente|||
Competición|Ficha torneo por equipos|Detalle|/Camp_Equipos.asp?IdTorneo=…|Camp_Equipos.asp|Detalle BD + inscripción|OK|3|Desarrollado|/api/v1/competicion/campeonatos-equipos/{id}|campeonato-equipos.html?id=28676 + campeonato-equipos.js|SQL parametrizado (el ASP concatenaba IdTorneo en ~20 consultas, la mitad sin limpiar) y sin N+1 (un nombre de cuadro por consulta). Cuadros por género con las reglas del ASP (ReglasCampeonatoEquipos), notificaciones y nota de prensa con HTML saneado, pasos de inscripción y equipos inscritos (siguen en la web actual), marcadores, vídeos del 28133 y excepciones por IdTorneo en Contenidos/campeonatos-equipos.json. 404 en vez de error 500 con id inexistente. Web del club "https://http://…" corregida. Campeonatos, Programa Impulso y portada enlazan ya aquí. Pendiente 301 (sin fila en redirecciones.csv) y DECISIONES FEP (Borrado, textos de cuadro)
Competición|Ficha torneo silla de ruedas|Detalle|/Camp_Silla.asp?IdTorneo=…|Camp_Silla.asp|Detalle BD + inscripción|OK|3|Desarrollado|/api/v1/competicion/campeonatos-silla/{id}|campeonato-silla.html?id=30180 + campeonato-silla.js|SQL parametrizado (el ASP concatenaba IdTorneo en ~25 consultas, las de nombres de cuadro sin limpiar) y sin N+1. Redirecciones del ASP (20255 copa, INCLUSIVO, menores, veteranos) y de Camp_Parejas.asp (solo Circuito='SILLA') como 404 con urlWebActual; 404 en vez de error 500 con id inexistente. Cuadros por género con las reglas del ASP (ReglasCampeonatoSilla; grupo mixto titulado "Femeninos" corregido), cuadro de honor (SFEP_RankingTorneos ranking 15) plegable y enlazado a las estadísticas, inscritos con entrenadores (sin "0 Coach"), nota de prensa y notificaciones con HTML saneado. El formulario de inscripción (Camp_Silla_intro.asp, escribe en BD) sigue en la web actual: se enlaza con plazo, condiciones y pasos. Campeonatos, banners y eventos enlazan ya aquí. Pendiente 301 (sin fila en redirecciones.csv) y DECISIONES FEP (Visible, marcadores de torneos terminados, textos de cuadro; ver Camp_Silla.md)
Competición|Inscritos (parejas)|Detalle|/Camp_Parejas_Inscritos.asp?tipo=X&id=…|Camp_Parejas_Inscritos.asp|Listado BD|OK|3|Pendiente|||Enlaza a cláusulas y descarga de certificado
Competición|Equipos inscritos / modificar|Detalle|/Camp_Equipos_Modificar.asp?idtorneo=…|Camp_Equipos_ModificarCOPA.asp|Listado BD + área privada|OK|4|Pendiente|||Redirige a la variante COPA; enlaza Integrantes, Abono Fed. y certificado árbitros
Competición|Cuadro de parejas|Detalle|/Previo.asp → /Cuadro16.asp|Previo.asp, Cuadro*.asp, CuadroPrevia*.asp, CuadroPrePrevia*.asp, Liguilla4.asp|Cuadro BD|OK|3|Desarrollado|/api/v1/competicion/cuadros/{idTorneo}/{genero}/{categoria}/{nivel}/{cuadro} (+ /api/v1/competicion/cuadros/{idTorneo})|cuadro.html + cuadro.js (front en paralelo)|Un único recurso para las ~40 páginas de cuadro (solo cambiaba el tamaño de la rejilla): formato eliminatoria/liguilla y plantilla con las reglas de Previo.asp, participantes por avance/posición, resultados, horarios, cabezas de serie y clasificación. SQL parametrizado (el ASP concatenaba IdTorneo, Genero, Cat, Nivel y Cuadro sin limpiar) y sin N+1 (~400 consultas en un cuadro de 128). Filtros corregidos (Cuadro4 y Liguilla4 con Nivel=1 fijo, CuadroPrePrevia16 con resultados de todo el torneo, cabezas de serie mezclando final y consolación). 404 en vez de error 500. Las exportaciones *Excell.asp no se migran; PPTSinSortear.asp y Previook.asp, analizadas y no migradas. Pendiente 301 y DECISIONES FEP (ver Previo.md)
Competición|Cuadro de equipos|Detalle|/PrevioEquipos.asp → /CuadroEquipos16.asp|PrevioEquipos.asp, CuadroEquipos*.asp, Liguilla3Equipos.asp, Liguilla4Equipos.asp, EquiposEnfrentamientos.asp|Cuadro BD|OK|3|Desarrollado|/api/v1/competicion/cuadros/{idTorneo}/{genero}/{categoria}/{nivel}/{cuadro} (+ …/enfrentamientos/{avance}/{posicion})|cuadro.html + cuadro.js (front en paralelo)|Mismo recurso que el cuadro de parejas (tipo "equipos"): nombres de SFEP_Torneos_Equipos, marca de enfrentamiento por partido y detalle de EquiposEnfrentamientos.asp en una consulta (antes 6 por partido, con los equipos de la última fila para todos). PrevioEquipos.asp contaba filas sin categoría; ahora con todos los filtros. Liguilla3Equipos listaba parejas de SFEP_Torneos_Jugadores como "C.S."; ahora los equipos. Pendiente 301 y DECISIONES FEP (ver Previo.md)
Competición|Alineaciones de equipos|Detalle|/EquiposAlineaciones…|Equipos-Alineaciones-*.asp|Detalle BD|OK|3|Pendiente|||
Competición|Programa IMPULSO|Menú|/Programa_Impulso|Impulso.asp|Informativa + documentos + listado BD por año|OK|2|Desarrollado|/api/v1/competicion/programa-impulso|programa-impulso.html + programa-impulso.js|Imagen y 2 PDF (bases y calendario 2026, ambos 200) en Contenidos/programa-impulso.json; torneos SFEP_Torneos con ProgramaImpulso='S' (SQL parametrizado, sin N+1 de clubes, enlace directo a cuadros/ficha). alt de la imagen corregido ("Cto. España Empresas"). El torneo FIP Silver Alcalá 2026 enlaza a /Paginas/fapN/Prueba_Cuadros.asp (LinkCuadros en BD; responde 200): DECISIÓN FEP. Pendiente activar 301 /Programa_Impulso → /programa-impulso
Competición|Calendario Menores FFAA|Menú|/Calendario_Menores_FFAA|Calendario_Menores_FFAA.asp|Informativa (escrita a mano; las 5 consultas a BD no se pintaban)|OK|2|Desarrollado|/api/v1/competicion/calendarios-autonomicos/menores|calendario-menores-ffaa.html + calendario-autonomico.js|Contenido en Contenidos/calendarios-autonomicos/menores.json: 17 federaciones, 75 pruebas con fechas, sede, campeonato autonómico y "en el ranking" (antes solo color verde). Vistas por federación y por fecha. Eliminadas consultas sin uso (una con inyección SQL en fed). Endpoint y JS comunes con Veteranos. Título con errata ("Menors") corregido. Pendiente activar 301 y DECISIÓN FEP (pruebas en verde: el .asp difiere de la web pública)
Competición|Calendario Veteranos FFAA|Menú|/Calendario_Veteranos_FFAA|Calendario_Veteranos_FFAA.asp|Informativa (escrita a mano; las 5 consultas a BD no se pintaban)|OK|2|Desarrollado|/api/v1/competicion/calendarios-autonomicos/veteranos|calendario-veteranos-ffaa.html + calendario-autonomico.js|Mismo endpoint, JS y estilos que Menores. Contenido en Contenidos/calendarios-autonomicos/veteranos.json: 17 federaciones (8 sin pruebas), 19 pruebas (11 en el ranking, 9 campeonatos autonómicos). Sin momentos de incorporación (actualizacionesRanking null). Erratas corregidas ("VETERNAOS", "11 al 13 diciembre", "s" suelta tras la tabla). Eliminadas consultas sin uso (una con inyección SQL en fed). Pendiente activar 301 y DECISIÓN FEP (significado de "35*"/"30*", campeonatos autonómicos marcados por el nombre)
Competición|Calendario FEP|Menú|/CalendarioFEP|CalendarioFEP.asp|Calendario BD + PDF|OK|2|Desarrollado|/api/v1/competicion/calendario|calendariofep.html + calendario-fep.js|Pruebas de SFEP_Calendario (Federacion=0, Visible='S') en 2 idas a la BD en lugar de 19 consultas (SQL parametrizado; ano/fed se concatenaban). Por defecto la temporada del año en curso (el ASP tenía ano=2026 escrito a mano). PDF y fecha de actualización por temporada (SFEP_Calendario_Documentos) y archivo de los PDF de todas las temporadas; 4 temporadas (2021-2024) enlazaban a la carpeta refs/docs/ (403). Enlaces de las pruebas arreglados: iban a Campeonatos_Previo.asp, que no existe (la reescritura ^Campeonatos llevaba al listado general); ahora a la ficha de Torneos_Previo.asp. Banner de la convocatoria (HabitaSolicitud) → Contenidos/calendario-fep.json. Fecha comodín 1 ene-31 dic = "Fecha por confirmar". Filtros nuevos (categoría, texto, pendientes). El contador "(N)" contaba también las no visibles. Pendiente activar 301 y DECISIONES FEP (ver CalendarioFEP.md)
Competición|Calendario Fed. Territoriales|Menú|/CalendarioFT|CalendarioFT.asp|Calendario BD|OK|2|Desarrollado|/api/v1/competicion/calendario/federaciones (+ /{id}?anio=)|calendarioft.html + calendario-ft.js|Mismo repositorio y forma de prueba que el calendario FEP con otra federación (SQL parametrizado; fed iba sin comillas). Temporadas de cada federación (el ASP ofrecía las de la FEP y abría 2027 aunque la federación no tuviera pruebas). Incluye Castilla-La Mancha (id 22), que el ASP dejaba fuera del desplegable (BETWEEN 1 AND 21) aunque tiene calendario. Rejilla de federaciones con y sin calendario, filtros nuevos (categoría, texto, pendientes). Enlaces de las pruebas a la ficha de Torneos_Previo.asp (iban a Campeonatos_Previo.asp, que no existe). comun.js lleva CalendarioFEP?fed=N y CalendarioFT?fed=N a calendarioft.html (fed=0 → calendariofep.html). Pendiente activar 301 y DECISIONES FEP (ver CalendarioFT.md)
Competición|Calendario (antiguo)|Mapa web|/Calendario|—|—|Error 404|—|No aplica|||Enlace roto en Mapa Web: retirar
Competición|Rankings (índice)|Menú|/Rankings|Rankings.asp|Índice BD (SFEP_Rankings_Web)|OK|2|Desarrollado|/api/v1/competicion/rankings|rankings.html + rankings.js|5 rankings de SFEP_Rankings_Web (Visible='S', por Orden) con columnas explícitas (el ASP hacía SELECT *, recorría el recordset dos veces y consultaba noticias sin usarlas). Título de la pestaña corregido ("Archivos Multimedia" → "Rankings") y errata "Asbolutos" corregida al servir. Imágenes decorativas (solo repiten el título). Las tarjetas enlazan a las páginas nuevas previsibles ranking-{absoluto,menores,equipos-absolutos,silla-ruedas,veteranos}.html (SUPUESTO, aún sin migrar). Pendiente activar 301 y DECISIONES FEP (ver Rankings.md)
Competición|Ranking Absoluto|Subpágina|/Ranking_Absoluto|Ranking_Absoluto.asp|Tabla BD (AJAX: CargarRnkAbs.asp, CargarRnkAbsNombre.asp) + normativa escrita a mano|OK|2|Desarrollado|/api/v1/competicion/rankings/absoluto (+ /normativa)|ranking-absoluto.html + ranking.js|Clasificación (SFEP_RankingFinal + SFEP_Fichas + SFEP_Ranking_Tipos, TipoRanking 10000/10001) con SQL parametrizado: el ASP concatenaba la licencia sin comillas (inyección SQL) y daba error 500 con licencias inexistentes. Masculino/femenino (top 100, ampliable a 500), búsqueda por licencia o nombre en ambos géneros, datos de cada lista y PDF completo. Enlace del jugador corregido (Silla-Ruedas-Est → Estadisticas). Normativa de cálculo en Contenidos/rankings/absoluto.json con erratas corregidas. Endpoint y JS pensados para el resto de Ranking_*. Pendiente activar 301 y DECISIONES FEP (PDF de 2025 desactualizado, texto WPT obsoleto, límites de tablas; ver Ranking_Absoluto.md)
Competición|Ranking Menores|Subpágina|/Ranking_Menores|Ranking_Menores.asp|Tabla BD (AJAX: CargarRnkMen.asp)|OK|2|Desarrollado|/api/v1/competicion/rankings/menores|ranking-menores.html + ranking.js|Mismo endpoint, repositorio, JS y estilos que Ranking Absoluto: solo se declaran los 10 TipoRanking (categoría × género, 8000–4001) en ReglasClasificaciones. SQL parametrizado (el ASP concatenaba la licencia: inyección SQL; error 500 con licencias inexistentes, con "----" y con el mixto de benjamín/alevín). Selector de categoría y género (benjamín masculino al entrar), búsqueda por licencia y por nombre (nueva) en todas las categorías, datos y PDF (al día) de cada lista, top 100 ampliable a 500. Aviso oculto (if a=5, nunca visible) y rankings mixtos x002 no migrados. Banner CNM 2026 copiado. Pendiente activar 301 y DECISIONES FEP (mixtos, Jugadores de SFEP_Ranking_Tipos distinto de las filas, licencias de menores publicadas; ver Ranking_Menores.md)
Competición|Ranking Equipos Absolutos|Subpágina|/Ranking_Equipos_Absolutos|Ranking_Equipos_Absolutos.asp|Tabla BD (32 consultas: temporada × categoría × género)|OK|2|Desarrollado|/api/v1/competicion/rankings/equipos-absolutos|ranking-equipos-absolutos.html + ranking-equipos.js|Una consulta parametrizada (SFEP_Ranking_Equipos con OUTER APPLY a SFEP_Clubes, SFEP_Provincias y SFEP_Federaciones IdFederacion 1–99) en lugar de 32 escritas a mano para 2017–2024: las temporadas nuevas se publican sin tocar código. Corrige los 5 equipos duplicados de 2017 (federaciones 1001/1002) y los equipos que el INNER JOIN perdía. Selector de temporada (?anio=), tabla por categoría con escudo (iniciales si no hay: muchos 404), bandera y provincia, e historial de campeones. Eliminada la consulta de noticias sin uso. Fichas de club en la web actual (Club.asp, sin migrar). Pendiente activar 301 y DECISIONES FEP (temporadas posteriores a 2024, sin 2025 en la web; ver Ranking_Equipos_Absolutos.md)
Competición|Ranking Silla de Ruedas|Subpágina|/Ranking_Silla_Ruedas|Ranking_Silla_Ruedas.asp|Tabla BD|OK|2|Desarrollado|/api/v1/competicion/rankings/silla-ruedas|ranking-silla-ruedas.html + ranking.js|Mismo endpoint, repositorio, JS y estilos que Ranking Absoluto y Menores: se declara el TipoRanking 15 (lista única mixta) en ReglasClasificaciones con licencias de silla (prefijo "PS", nombre de SFEP_RankingFinal sin JOIN con SFEP_Fichas: otra numeración). SQL parametrizado con columnas explícitas (el ASP hacía SELECT * y consultaba noticias sin usarlas). Búsqueda por nombre o licencia (con o sin "PS", nueva), datos de SFEP_Ranking_Tipos si existen, enlace a Silla-Ruedas-Est?id=PS{licencia} (web actual). Evolución calculada pero no pintada en el ASP: se muestra solo si hay datos. Nombre con codificación rota en la BD (JOSÃ‰). Pendiente activar 301 y DECISIONES FEP (ver Ranking_Silla_Ruedas.md)
Competición|Ranking Veteranos|Subpágina|/Ranking_Veteranos|Ranking_Veteranos.asp|Tabla BD (AJAX: CargarRnkVet.asp)|OK|2|Desarrollado|/api/v1/competicion/rankings/veteranos|ranking-veteranos.html + ranking.js|Mismo endpoint, repositorio, JS y estilos que Absoluto, Menores y Silla de Ruedas: solo se declaran los 12 TipoRanking (categorías +35…+60 × género, 10035–11060) en ReglasClasificaciones. El ASP mezclaba los cargadores: el género y la licencia llamaban a CargarRnkMen.asp (menores: error 500 o el ranking de menores) y solo la categoría a CargarRnkVet.asp; "X" servía mixtos de menores; +60 femenino sin filas (500). SQL parametrizado (inyección SQL en la licencia). Selector de categoría y género (+35 masculino al entrar), búsqueda por licencia y por nombre (nueva) en todas las categorías, top 100 ampliable a 500. SFEP_Ranking_Tipos sin jugadores, pruebas ni PDF (enlace roto refs/docs/): no se muestran. Pendiente activar 301 y DECISIONES FEP (ver Ranking_Veteranos.md)
Competición|Estadísticas|Menú|/Estadisticas|Estadisticas.asp (+ CargarPruebas.asp)|Buscador BD (licencia o DNI)|OK|2|Desarrollado|/api/v1/competicion/jugadores/{licencia}/estadisticas|estadisticas.html + estadisticas.js|Buscador por número de licencia (la búsqueda por DNI no se migra: DECISIÓN FEP por protección de datos; el formulario enlaza a la web actual). SQL parametrizado en 2 idas a la BD de solo lectura: el ASP concatenaba licencia y DNI en ~15 consultas (inyección SQL; ?id=abc da error 500), hacía N+1 por prueba y ESCRIBÍA en la BD en cada visita (DELETE/INSERT en SFEP_estadisticasWeb y UPDATE de SFEP_RankingTorneos en CargarPruebas.asp). Pendiente activar 301 y DECISIONES FEP (ver Estadisticas.md)
Competición|Estadísticas de jugador|Detalle|/Estadisticas.asp?Id=…|Estadisticas.asp + CargarPruebas.asp|Detalle BD|OK|2|Desarrollado|/api/v1/competicion/jugadores/{licencia}/estadisticas|estadisticas.html?licencia=12762 + estadisticas.js|Ficha (foto, categoría por edad, clubes), balance de partidos/sets/juegos del último año en el Ranking Único, última pareja, mejor clasificación, rankings con las pruebas que puntúan (antes CargarPruebas.asp por AJAX; incluye el 40 % de la categoría de menores anterior), evolución (barras HTML/CSS en lugar de Highcharts) y campeonatos por equipos con enlace a la ficha (iba a Campeonatos_Previo.asp, que no existe). comun.js traslada Estadisticas?id= a estadisticas.html?licencia= (enlaces de los rankings). Correcciones: categoría vacía de 24 a 34 años, histórico mezclado de los dos géneros, nombre de ranking heredado de la fila anterior, CargarPruebas.asp con error 500 en tipos sin fecha
Competición|Solicitud Arbitraje|Menú|/Solicitud_Arbitraje|Solicitud_Arbitraje.asp|Formulario (escritura)|OK|4|Pendiente|||
Competición|Oferta Hotelera Torneos|Menú|/hoteles_Concertados|Hoteles_Concertados.asp|Informativa BD|OK|2|Pendiente|||
Competición|Dopaje|Menú|/Dopaje|Dopaje.asp|Informativa + enlaces|OK|2|Pendiente|||
Competición|Registro Árbitros / Categorías|Menú|/Registro_Arbitros_Categorias|Registro_Arbitros_Categorias.asp|Buscador BD|OK|2|Pendiente|||
Competición|Retransmisiones|Menú|/Retransmisiones|Retransmisiones.asp|Informativa + vídeo YouTube|OK|2|Pendiente|||
Competición|Organización de Torneos (licitación)|Enlace home|/Organizacion_Torneos|Organizacion_Torneos.asp (+ Organizacion_FueraPlazo.asp, cargarfechastorneo.asp, comboProvincias.asp, comboclubesLicitacion.asp)|Convocatoria (BD + escrita a mano) + formulario|OK|4|Desarrollado|/api/v1/competicion/organizacion-torneos, /api/v1/competicion/organizacion-torneos/clubes?provincia=|organizacion-torneos.html + organizacion-torneos.js|Pruebas licitables (SFEP_Calendario, Licitable='S'), comunidades/provincias y documentos (SFEP_Calendario_Archivo) en 1 ida a la BD en lugar de 10 consultas + 1 por mes (SQL parametrizado; los 3 ficheros auxiliares concatenaban id sin comillas). Temporada, fecha límite, textos y cláusula de datos (escritos a mano) → Contenidos/organizacion-torneos.json; cierre del plazo en hora de Madrid (sustituye a Organizacion_FueraPlazo.asp). Fechas y fianza del torneo en la respuesta (sin cargarfechastorneo.asp). El formulario conserva los campos y se envía a Organizacion_P0.asp de la web actual (accept-charset Windows-1252). Pendiente activar 301 y DECISIONES FEP (ver Organizacion_Torneos.md)
Competición|Organización de Torneos · pasos 1 a 4 y pago|Subpágina|/Organizacion_P1.asp?Id=…|Organizacion_P0.asp … P5, Organizacion_Final.asp, Organizacion_TPV*.asp, Organizacion_email.asp|Formulario con escritura en BD + subida de ficheros + TPV|OK|4|Pendiente|||Siguen en la web actual: la página nueva envía el formulario a Organizacion_P0.asp (INSERT en SFEP_Licitaciones con inyección SQL). Migrar con escritura en BD (POST con validación y antispam) en la fase de área privada
Competición|Circuito de Menores|Enlace home|circuitomenores.padelfederacion.es/Campeonato|—|Subdominio aparte|Externo|—|Fuera de alcance|||Otro sitio; solo mantener el enlace
Licencias|Tu licencia / Modificación|Menú|/Consulta_Tu_Licencia|Licencias_TuLicencia*.asp|Área privada (login)|OK|4|En proceso||buscar-licencia.html#consulta|Contraseñas en claro en BD (riesgo 3). Unida a Buscar licencia: la sección #consulta explica el servicio y enlaza al acceso con clave de la web actual (login con inyección SQL y ficha abierta con una cookie IdFicha falsificable: no se replica). Falta el área privada (Mi Portal, fase 4)
Licencias|Solicitud de clave|Subpágina|/Licencias_SolicitaClave.asp|Licencias_SolicitaClave.asp|Formulario (escritura)|OK|4|Pendiente|||
Licencias|Buscar Licencia|Menú|/Licencias_Buscar|Licencias_Buscar.asp|Buscador BD|OK|2|Desarrollado|/api/v1/licencias (+ /api/v1/federacion/federaciones-autonomicas para el desplegable)|buscar-licencia.html + buscar-licencia.js|SQL parametrizado (el ASP concatenaba fed sin comillas, texto y fecha como texto 'd/m/aaaa': error 500 con fed=abc o con una fecha incompleta) en una ida (COUNT + TOP 100) en lugar de 12 variantes. Solo licencia, apellidos y nombre (el ASP leía fecha de nacimiento y género sin pintarlos); sin caché y con límite de búsquedas por IP (429). Silla de ruedas corregido (una variante unía LicenciaJugador con LicenciaSilla: 0 resultados; otra daba 500). Sin tildes ni mayúsculas. La página une "Buscar licencia" y "Tu licencia / Modificación" (sección #consulta, acceso con clave en la web actual); menú con una sola entrada "Buscar / consulta tu licencia". Pendiente 301 (sin fila en redirecciones.csv) y DECISIONES FEP (ver Licencias_Buscar.md)
Licencias|Registro Nacional Árbitros|Menú|/Registro_Nacional_Arbitros|Registro_Nacional_Arbitros.asp|Buscador BD + PDF|OK|2|Pendiente|||
Licencias|Registro Nacional Técnicos|Menú|/Registro_Nacional_Tecnicos|Registro_Nacional_Tecnicos.asp|Buscador BD + PDF|OK|2|Pendiente|||
Licencias|Solicitud / Renovación de licencia|Mapa web|/Licencias_Solicitud|Licencias_Solicitud.asp|Formulario (escritura)|OK|4|Pendiente|||No está en el menú; enlaza Licencias_Extranjeros
Licencias|Licencia extranjeros|Subpágina|/Licencias_Extranjeros.asp|Licencias_Extranjeros.asp|Formulario (escritura)|OK|4|Pendiente|||
Licencias|Nueva licencia|Subpágina|/Licencias_Nueva.asp|Licencias_Nueva.asp|Formulario + pago|OK|4|Pendiente|||
Licencias|Importes y formas de pago|Mapa web|/Importes_Licencias|Importes_Licencias.asp|Informativa (tablas)|OK|2|Pendiente|||
Licencias|Ventajas federados/as|Mapa web|/Licencias_Ventajas|Licencias_Ventajas.asp|Informativa|OK|2|Pendiente|||El título de la pestaña dice "Seguro Médico"
Licencias|Seguro / Protocolo accidente|Mapa web|/Seguro_Medico|Seguro_Medico.asp|Informativa|OK|2|Pendiente|||
Licencias|Portal del Federado/a|Enlace home|/Portal-del-Federado|Portal_Federado.asp|Formulario (consultas)|OK|4|Pendiente|||
Documentos|Circulares|Menú|/Circulares|Documentos_Circulares.asp|Documentos (21 PDF)|OK|2|Pendiente|||
Documentos|Memorias Deportivas|Menú|/Memorias_Deportivas|Documentos_Memorias.asp|Documentos (36 PDF)|OK|2|Pendiente|||Enlace a /refs/docs/mini.php
Documentos|Estatutos|Menú|/Estatutos|Documentos_Estatutos.asp|Documentos|OK|2|Pendiente|||
Documentos|Reglamentos y Normativas|Menú|/Reglamentos|Documentos_Reglamentos.asp|Documentos (16 PDF)|OK|2|Pendiente|||Enlaza al listado del directorio /refs/docs/
Documentos|Normativas|Mapa web|/Normativas|Documentos_Normativas.asp|Documentos|OK|2|Pendiente|||
Documentos|Mantenimiento Pistas|Menú|/Mantenimiento_Pistas|Mantenimiento_Pistas.asp|Documentos|OK|2|Pendiente|||
Documentos|Solicitud Certificados|Menú|/Certificados|Certificado*.asp|Formulario + pago|OK|4|Pendiente|||
Formación|Calendario Cursos|Menú|/Formacion_Calendario|Formacion_Calendario.asp|Calendario BD|OK|2|En proceso|/api/v1/formacion/convocatorias||Endpoint creado (se usa en la home); falta la página
Formación|Detalle de curso|Detalle|/Formacion_Detalle.asp?Id=…|Formacion_Detalle.asp|Detalle BD|OK|2|Pendiente|||
Formación|Inscripciones Cursos|Menú|/Formacion_Inscripciones|Formacion_Inscripciones.asp|Formulario + pago|OK|4|Pendiente|||
Formación|Cursos de Formación (acciones formativas)|Menú|/Acciones_Formativas|Formacion_Acciones.asp|Informativa BD|OK|2|Pendiente|||También accesible como /Formacion_Acciones.asp
Formación|Acceso Plataforma (campus)|Menú|formacionfep.es/campus|—|Plataforma externa|Externo|—|Fuera de alcance|||Solo enlace
Formación|Acceso Exámenes|Menú|/Acceso-Examenes|Acceso-Examenes.asp|Área privada|OK|4|Pendiente|||El título de la pestaña dice "Buscar Licencia"
Formación|Solicitud Prácticas|Menú|/Solicitud_Practicas|Solicitud_Practicas.asp|Formulario (escritura)|OK|4|Pendiente|||El título de la pestaña dice "Solicitud Arbitraje"
Formación|Acceso Vídeos Formación|Menú|/Acceso-VideosFormacion|Acceso-VideosFormacion.asp|Área privada|OK|4|Pendiente|||
Formación|Bolsa de Trabajo|Mapa web|/Bolsa_De_Trabajo|Formacion_BolsaTrabajo.asp|Informativa|OK|2|Pendiente|||
Hemeroteca|Multimedia (galerías)|Menú|/Archivos_Multimedia|Archivos_Multimedia.asp|Listado BD|OK|2|En proceso|/api/v1/multimedia/fotos/ultimas||Solo existe "últimas fotos" para el pie; falta listado de galerías y página
Hemeroteca|Galería de fotos|Detalle|/Documentos_Multimedia_Previo.asp?id=…|Documentos_Multimedia_Fotos.asp|Detalle BD|OK|2|Pendiente|||Redirige a Documentos_Multimedia_Fotos.asp
Hemeroteca|Noticias|Menú|/Noticias|News.asp|Listado BD|OK|2|En proceso|/api/v1/noticias/destacadas||Existe NoticiasController solo con destacadas; faltan listado paginado y página
Hemeroteca|Noticia (detalle)|Detalle|/noticia.asp?Idnoticia=…|Noticia.asp|Detalle BD|OK|2|Pendiente|||Las noticias enlazan a /Prueba_UltimaHora.asp (404)
Mujer y Pádel|Multimedia (eventos MyP)|Menú|/mUJERyDeporte|MujerYPadel.asp + MyP_*|Listado BD + vídeos|OK|2|Pendiente|||
Mujer y Pádel|Noticias Mujer y Pádel|Menú|/Post_Mujer_y_Padel|MyP_Noticias.asp|Listado BD|OK|2|Pendiente|||
Mujer y Pádel|Formulario evento MyP|Subpágina|/Form-MYD?Id=…|Form-MYD.asp, Form_EventosMYD.asp|Formulario (escritura)|OK|4|Pendiente|||
Mujer y Pádel|Formulario evento (Madre-Hija)|Subpágina|/Form-Evento|Form_Evento*.asp|Formulario (escritura)|OK|4|Pendiente|||
Contacto|Contacto|Menú|/Contacto|Contacto.asp|Informativa + formulario|OK|2|Pendiente|||Envío del formulario en fase 4
Legal (pie)|Política de Privacidad|Pie|/Politica_Privacidad|Politica_Privacidad.asp|Informativa|OK|2|Pendiente|||
Legal (pie)|Política de Cookies|Pie|/Politica_Cookies|Politica_Cookies.asp|Informativa|OK|2|Pendiente|||El título de la pestaña dice "Política de Privacidad"
Legal (pie)|Aviso Legal|Pie|/Aviso_Legal|Aviso_Legal.asp|Informativa|OK|2|Pendiente|||El título de la pestaña dice "Política de Privacidad"
Legal (pie)|Ejercicio de derechos ARSOL|Pie|/Arsol.html|Arsol.html|Formulario|OK|2|Pendiente|||HTML estático; el título dice "Política de Privacidad"
Legal (pie)|Mapa Web|Pie|/Mapa_Web|Mapa_Web.asp|Índice|OK|2|Pendiente|||
Legal (pie)|Canal de denuncias|Pie|canaldenunciasgfm.es|—|Servicio externo|Externo|—|Fuera de alcance|||Solo enlace
Gestión|Extranet (back-office)|Pie|/gESTION/ControlAcceso.asp|gestion\ (1.180 ficheros)|Área de gestión|OK|5|Pendiente|||Sesión falsificable (riesgo 2); requiere Identity + 2FA
Otros|Página de pruebas FAP (cuadros)|Enlace interno|/Paginas/fapN/Prueba_Cuadros.asp|Paginas/fapN/*|Pruebas publicadas|OK|—|No aplica|||No migrar; retirar el enlace desde Programa IMPULSO
Otros|Prueba Última Hora|Enlace interno|/Prueba_UltimaHora.asp|—|—|Error 404|—|No aplica|||Enlace roto en el pie de las noticias
'@ -split "`r?`n" | Where-Object { $_ }

$cab = 'ID','Sección','Página','Nivel','URL actual','Origen ASP','Tipo de página','Estado web actual','Fase (plan)','Estado desarrollo','Ruta nueva propuesta','Endpoint API nuevo','Front nuevo (wwwroot)','Observaciones'
$n = $filas.Count
$datos = New-Object 'object[,]' ($n + 1), $cab.Count
for ($c = 0; $c -lt $cab.Count; $c++) { $datos[0, $c] = $cab[$c] }
for ($i = 0; $i -lt $n; $i++) {
  $f = $filas[$i].Split('|')
  $ruta = ''
  $clave = $f[3].ToLower()
  if ($redir.ContainsKey($clave)) { $ruta = $redir[$clave].nueva_ruta_propuesta }
  elseif ($f[8] -eq 'Desarrollado' -and $f[3].StartsWith('/') -and $f[10]) { $ruta = '/' + ($f[10] -split ' ')[0] }
  $vals = @(('P{0:000}' -f ($i + 1)), $f[0], $f[1], $f[2], $f[3], $f[4], $f[5], $f[6], $f[7], $f[8], $ruta, $f[9], $f[10], $f[11])
  for ($c = 0; $c -lt $vals.Count; $c++) { $datos[($i + 1), $c] = [string]$vals[$c] }
}

$xl = New-Object -ComObject Excel.Application
$xl.Visible = $false; $xl.DisplayAlerts = $false
try {
  $wb = $xl.Workbooks.Add()
  while ($wb.Worksheets.Count -lt 3) { [void]$wb.Worksheets.Add([Type]::Missing, $wb.Worksheets.Item($wb.Worksheets.Count)) }
  $res = $wb.Worksheets.Item(1); $res.Name = 'Resumen'
  $ws = $wb.Worksheets.Item(2); $ws.Name = 'Mapa web'
  $ley = $wb.Worksheets.Item(3); $ley.Name = 'Leyenda'
  foreach ($h in @($res, $ws, $ley)) { $h.Cells.Font.Name = 'Arial'; $h.Cells.Font.Size = 10 }

  # --- Mapa web ---
  $ult = $n + 1
  $rng = $ws.Range($ws.Cells(1, 1), $ws.Cells($ult, $cab.Count))
  $rng.Value2 = $datos
  $lo = $ws.ListObjects.Add(1, $rng, $null, 1)
  $lo.Name = 'MapaWeb'; $lo.TableStyle = 'TableStyleLight9'
  $anchos = 7, 13, 34, 12, 34, 28, 26, 13, 9, 16, 24, 40, 24, 60
  for ($c = 1; $c -le $anchos.Count; $c++) { $ws.Columns($c).ColumnWidth = $anchos[$c - 1] }
  $ws.Range("A2:N$ult").WrapText = $true
  $ws.Range("A2:N$ult").VerticalAlignment = -4160
  $ws.Range('A1:N1').Font.Bold = $true
  $ws.Range("I2:J$ult").HorizontalAlignment = -4108
  # Lista desplegable de estado
  $v = $ws.Range("J2:J$ult").Validation; $v.Delete()
  $v.Add(3, 1, 1, 'Desarrollado,En proceso,Pendiente,No aplica,Fuera de alcance')
  # Colores por estado
  $col = @{ 'Desarrollado' = 0xCEEFC6; 'En proceso' = 0x9CEBFF; 'Pendiente' = 0xCEC7FF; 'No aplica' = 0xD9D9D9; 'Fuera de alcance' = 0xF2E6DD }
  $j = $ws.Range("J2:J$ult")
  foreach ($k in $col.Keys) {
    $fc = $j.FormatConditions.Add(1, 3, "=""$k""")
    $fc.Interior.Color = $col[$k]
  }
  $fc = $ws.Range("H2:H$ult").FormatConditions.Add(1, 3, '="Error 404"'); $fc.Font.Color = 0x0000C0; $fc.Font.Bold = $true
  $ws.Activate(); $xl.ActiveWindow.SplitRow = 1; $xl.ActiveWindow.SplitColumn = 3; $xl.ActiveWindow.FreezePanes = $true

  # --- Resumen (fórmulas sobre la tabla) ---
  $res.Cells(1, 1).Value2 = 'Mapa web FEP: estado de la migración'
  $res.Cells(1, 1).Font.Size = 14; $res.Cells(1, 1).Font.Bold = $true
  $res.Cells(2, 1).Value2 = 'Fuente: rastreo de www.padelfederacion.es (12/11/2026) cruzado con src/FEP.Api y src/FEP.Web. Los totales se recalculan al cambiar la columna "Estado desarrollo" de la hoja Mapa web.'
  $res.Cells(2, 1).Font.Italic = $true; $res.Cells(2, 1).Font.Color = 0x595959
  $estados = 'Desarrollado', 'En proceso', 'Pendiente', 'No aplica', 'Fuera de alcance'
  $secciones = 'Portada', 'Federación', 'Competición', 'Licencias', 'Documentos', 'Formación', 'Hemeroteca', 'Mujer y Pádel', 'Contacto', 'Legal (pie)', 'Gestión', 'Otros'
  $res.Cells(4, 1).Value2 = 'Sección'
  for ($e = 0; $e -lt $estados.Count; $e++) { $res.Cells(4, 2 + $e).Value2 = $estados[$e] }
  $res.Cells(4, 7).Value2 = 'Total'; $res.Cells(4, 8).Value2 = '% desarrollado'
  $res.Cells(4, 9).Value2 = '% desarrollado + en proceso'
  $r = 5
  foreach ($s in $secciones) {
    $res.Cells($r, 1).Value2 = $s
    for ($e = 0; $e -lt $estados.Count; $e++) {
      $L = [char](66 + $e)
      $res.Cells($r, 2 + $e).Formula = "=COUNTIFS(MapaWeb[Sección],`$A$r,MapaWeb[Estado desarrollo],$L`$4)"
    }
    $res.Cells($r, 7).Formula = "=SUM(B${r}:F$r)"
    $res.Cells($r, 8).Formula = "=IFERROR(B$r/(G$r-E$r-F$r),0)"
    $res.Cells($r, 9).Formula = "=IFERROR((B$r+C$r)/(G$r-E$r-F$r),0)"
    $r++
  }
  $tot = $r
  $res.Cells($tot, 1).Value2 = 'TOTAL'
  foreach ($c in 2..7) { $L = [char](64 + $c); $res.Cells($tot, $c).Formula = "=SUM(${L}5:${L}$($tot-1))" }
  $res.Cells($tot, 8).Formula = "=IFERROR(B$tot/(G$tot-E$tot-F$tot),0)"
  $res.Cells($tot, 9).Formula = "=IFERROR((B$tot+C$tot)/(G$tot-E$tot-F$tot),0)"
  $res.Range("A${tot}:I$tot").Font.Bold = $true
  $res.Range("A${tot}:I$tot").Borders.Item(8).Weight = -4138
  $res.Range("H5:I$tot").NumberFormat = '0%'
  $res.Cells($tot + 1, 1).Value2 = 'Los % se calculan sin contar "No aplica" ni "Fuera de alcance".'
  $res.Cells($tot + 1, 1).Font.Italic = $true; $res.Cells($tot + 1, 1).Font.Color = 0x595959
  $hdr = $res.Range('A4:I4'); $hdr.Font.Bold = $true; $hdr.Interior.Color = 0x5E3A1F; $hdr.Font.Color = 0xFFFFFF; $hdr.WrapText = $true
  for ($e = 0; $e -lt $estados.Count; $e++) { $res.Cells(4, 2 + $e).Interior.Color = $col[$estados[$e]]; $res.Cells(4, 2 + $e).Font.Color = 0 }
  $res.Range("A4:I$tot").Borders.LineStyle = 1; $res.Range("A4:I$tot").Borders.Color = 0xBFBFBF
  $res.Columns(1).ColumnWidth = 16; foreach ($c in 2..7) { $res.Columns($c).ColumnWidth = 13 }; $res.Columns(8).ColumnWidth = 14; $res.Columns(9).ColumnWidth = 16
  $res.Range("B4:I$tot").HorizontalAlignment = -4108
  # Barra de progreso en % desarrollado + en proceso
  [void]$res.Range("I5:I$tot").FormatConditions.AddDatabar()

  # Resumen por fase
  $f0 = $tot + 3
  $res.Cells($f0, 1).Value2 = 'Fase del plan'
  for ($e = 0; $e -lt 3; $e++) { $res.Cells($f0, 2 + $e).Value2 = $estados[$e] }
  $res.Cells($f0, 5).Value2 = 'Total'; $res.Cells($f0, 6).Value2 = 'Qué cubre'
  $fases = @(('1', 'Home y layout común'), ('2', 'Contenido público de solo lectura'), ('3', 'Competición pública (cuadros, inscritos)'), ('4', 'Área privada, formularios y pagos'), ('5', 'Back-office de gestión'), ('6', 'Censo electoral / retirada'))
  $r = $f0 + 1
  foreach ($fa in $fases) {
    $res.Cells($r, 1).Value2 = "'" + $fa[0]
    foreach ($e in 0..2) { $L = [char](66 + $e); $res.Cells($r, 2 + $e).Formula = "=COUNTIFS(MapaWeb[Fase (plan)],`$A$r,MapaWeb[Estado desarrollo],$L`$$f0)" }
    $res.Cells($r, 5).Formula = "=SUM(B${r}:D$r)"
    $res.Cells($r, 6).Value2 = $fa[1]
    $r++
  }
  $h2 = $res.Range("A${f0}:F$f0"); $h2.Font.Bold = $true; $h2.Interior.Color = 0x5E3A1F; $h2.Font.Color = 0xFFFFFF
  foreach ($e in 0..2) { $res.Cells($f0, 2 + $e).Interior.Color = $col[$estados[$e]]; $res.Cells($f0, 2 + $e).Font.Color = 0 }
  $res.Range("A${f0}:F$($r-1)").Borders.LineStyle = 1; $res.Range("A${f0}:F$($r-1)").Borders.Color = 0xBFBFBF
  $res.Range("A$($f0+1):E$($r-1)").HorizontalAlignment = -4108

  # --- Leyenda ---
  $leyenda = @(
    @('Columna / valor', 'Significado'),
    @('Estado desarrollo · Desarrollado', 'Página y endpoint hechos en la nueva solución (FEP.Web + FEP.Api), con mock. Puede faltar activar la redirección 301.'),
    @('Estado desarrollo · En proceso', 'Hay parte hecha (endpoint o componente) pero falta la página o parte de la funcionalidad.'),
    @('Estado desarrollo · Pendiente', 'Nada hecho todavía en la nueva solución.'),
    @('Estado desarrollo · No aplica', 'Página rota (404) o de pruebas en la web actual: no se migra, se retira el enlace.'),
    @('Estado desarrollo · Fuera de alcance', 'Sitio o servicio externo (campus, circuito de menores, canal de denuncias): solo se mantiene el enlace.'),
    @('Nivel', 'Menú = opción del menú principal · Subpágina = enlazada desde una página del menú · Detalle = ficha con parámetro (?Id=) · Común = componente presente en todas las páginas · Pie / Mapa web / Enlace home = solo enlazada desde ahí.'),
    @('Estado web actual', 'Resultado al abrir la URL en www.padelfederacion.es el 12/11/2026 (OK, Error 404, Externo).'),
    @('Fase (plan)', 'Fase de docs/migracion/plan-fases.md en la que está prevista la página.'),
    @('Ruta nueva propuesta', 'De docs/migracion/redirecciones.csv (301 de la URL antigua a la nueva).'),
    @('Cómo actualizar', 'Cambia la columna "Estado desarrollo" en la hoja Mapa web (lista desplegable); el Resumen se recalcula solo. Para regenerar desde cero: tools\generar-mapa-web.ps1.')
  )
  for ($i = 0; $i -lt $leyenda.Count; $i++) { $ley.Cells($i + 1, 1).Value2 = $leyenda[$i][0]; $ley.Cells($i + 1, 2).Value2 = $leyenda[$i][1] }
  $ley.Columns(1).ColumnWidth = 34; $ley.Columns(2).ColumnWidth = 110
  $ley.Range("B1:B$($leyenda.Count)").WrapText = $true
  $ley.Range("A1:B$($leyenda.Count)").VerticalAlignment = -4160
  $h3 = $ley.Range('A1:B1'); $h3.Font.Bold = $true; $h3.Interior.Color = 0x5E3A1F; $h3.Font.Color = 0xFFFFFF
  $ley.Range("A1:B$($leyenda.Count)").Borders.LineStyle = 1; $ley.Range("A1:B$($leyenda.Count)").Borders.Color = 0xBFBFBF
  $estIdx = @{ 2 = 'Desarrollado'; 3 = 'En proceso'; 4 = 'Pendiente'; 5 = 'No aplica'; 6 = 'Fuera de alcance' }
  foreach ($k in $estIdx.Keys) { $ley.Cells($k, 1).Interior.Color = $col[$estIdx[$k]] }

  $res.Activate()
  $xl.Calculate()
  if (Test-Path $salida) { Remove-Item $salida -Force }
  $wb.SaveAs($salida, 51)
  $wb.Close($false)
  "Guardado: $salida ($n filas)"
} finally {
  $xl.Quit()
  [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
}
