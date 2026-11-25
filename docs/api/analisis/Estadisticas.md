# Análisis: `Estadisticas.asp` → `estadisticas.html` + `GET /api/v1/competicion/jugadores/{licencia}/estadisticas`

URL actual: `/Estadisticas` (regla `^Estadisticas` de `web.config` → `Estadisticas.asp`; menú "Competición ›
Estadísticas") y `/Estadisticas?id={licencia}` (enlaces de los rankings absoluto, de menores y de veteranos). Ruta
nueva: `/estadisticas` → `estadisticas.html?licencia={licencia}` (fila ya prevista en `redirecciones.csv`).

La página tenía dos partes: un formulario (licencia **o DNI**, enviado por `POST` al cambiar el campo) y, con una
licencia válida, la ficha del jugador/a. Al pulsar un ranking cargaba por AJAX (`$(div).load(...)`) el desglose de
pruebas desde **`CargarPruebas.asp`** (`?Rnk={TipoRanking}&id={licencia}`), que no está en `pages_to_dev/` y se ha
leído en `X:\TICS\WEB\` (solo lectura). Ambos ficheros en Windows-1252.

Includes: `conexionSQL.inc` (conexión; no se copia nada), `Menus.inc` (menú, redes y `limpiar()`), `BarraLateral.inc`,
`PiePagina.inc`, `finconexion.inc` (comunes, ya migrados en `comun.js`). La gráfica usaba Highcharts
(`refs/js/Graficos/highcharts.js`) y jQuery 1.8.2 cargado por segunda vez.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie, barra lateral | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` + columna de patrocinadores |
| `sql_noticias` (`SFEP_Noticias` destacadas) | Se consultaba **pero no se usaba** | Eliminado |
| Formulario "Indica un número de licencia" / "Indica un DNI" | HTML + `onChange="cambiar()"` (envío al cambiar el campo) | Buscador por número de licencia (botón "Ver estadísticas", validación en el navegador, `?licencia=` en la URL). El DNI no se migra (ver Pendiente) |
| DNI → licencia | `sql_dni`: `SFEP_Fichas.DNI = '{dni}' OR DNI = '0{dni}'` y `Fallecido='N'`, concatenado | No se migra: enlace a la web actual |
| Comprobación de la licencia | `sql_lic`: `SFEP_Fichas.LicenciaJugador = {id}` y `Fallecido='N'` | Primera consulta del repositorio; si no existe → `404` con `motivo = licencia-no-encontrada` |
| Mensajes "Introduce un número de licencia…" (amarillo) y de error (rojo) | HTML escrito a mano + `Terror` | Texto de inicio y aviso de error de `estadisticas.js` (mismos textos, con el correo `web@padelfederacion.es`) |
| Nombre, foto, licencia, categoría, club autonómico y nacional | `sql_elejigo`: `SFEP_Fichas.*` ⨝ `SFEP_Licencia` (`Tipo='J'`, la de `Año` más reciente) ⨝ `SFEP_Clubes` ×2 (`ClubLicencia`, `ClubNacional`); foto `https://padelfederacion.es/gESTION/Fotos/{id}.jpg` si `Foto='S'` (si no, `0.jpg`); categoría calculada con `year(now()) - year(fechanac)` | `nombre`, `foto`/`fotoUrl`, `categoria`, `anioLicencia`, `clubAutonomico`, `clubNacional` (`ReglasEstadisticas.CategoriaPorEdad`) |
| Partidos, sets y juegos ganados/perdidos con % | `sql_cuentan` (pruebas del Ranking Único 10000/10001 con `FechaFin > fechainicio`) y, **por cada prueba**, `sql_numpareja` y `sql_part` (`SFEP_Torneos_Resultados`) | `balance.partidos/sets/juegos` (`ReglasEstadisticas.Balance`), en una consulta |
| Última pareja | `sql_ultima` por cada prueba; se quedaba la última asignada | `ultimaPareja` (con su licencia: enlace a sus estadísticas) |
| Mejor clasificación en torneo ("16's Primera Categoría (1)") | `sql_mejor` (`SFEP_Torneos_Cuadros`, `ORDER BY Nivel, Avance`) + `sql_nummejor` | `mejorClasificacion` (una consulta agrupada) |
| Tabla RANKINGS (Ranking, Categoría, Posición, Torneos jugados, Puntos totales, Torneos average, Puntos average) | `sql_rnks`: `SFEP_RankingFinal` ⨝ `SFEP_Ranking_Tipos` ⨝ `SFEP_Fichas`, `IdFederacion=0`, `Visible='S'`, `SumaDeRankings='N'`, por `TipoRanking`; nombre del ranking según `Categoria` | `rankings[]` (`RankingJugadorDto`); nombre y lista según `ReglasClasificaciones` (los mismos rankings ya migrados) |
| Desglose al pulsar un ranking | `CargarPruebas.asp`: `SFEP_RankingTorneos` ⨝ `SFEP_Torneos` del ranking con `FechaFin > FechaAct − 1 año` (en menores, `04/11/25` fijo); bandera `refs/images/{IdFederacion}.jpg`, Torneo, Ronda (Avance), Puntos, Obs; en menores, las pruebas de la categoría anterior al 40 % si el año de nacimiento es el primero o el último de la categoría (`SFEP_CategoriasMenores`) | `rankings[].pruebas[]` en la misma respuesta (desplegables "Pruebas · …") |
| Nota "*Pincha en un ranking…" y "*En verde… LAS 6 MEJORES PUNTUACIONES" | HTML escrito a mano | Texto del periodo en cada desplegable; la frase de las 6 mejores no se migra (ver Pendiente) |
| "Evolución temporal en el Ranking único" (Highcharts, 12 publicaciones) | `sql_EvRU`: `TOP 12` de `SFEP_Ranking_Historico` (10000/10001) → `DELETE` + `INSERT` en `SFEP_estadisticasWeb` → `sql_FRU` para leerlas ordenadas | `evolucion.puntos[]` (sin tabla intermedia); barras HTML/CSS con el puesto debajo de cada fecha y tabla de datos |
| "Torneos por equipos" (Año, Mes, Campeonato, Puntúa, Juega) | `sql_equipos`: `SFEP_Inscripciones_Equipos(_Jugadores)` ⨝ `SFEP_Torneos` ⨝ `SFEP_Fichas`, `IdFederacion=0`, `Año > '2012'`, `Juega='S' OR Puntua='S'`, `ORDER BY Año DESC`; enlace `javascript:irCamp(id)` → `POST Campeonatos_Previo.asp` | `equipos[]` con enlace a la ficha del campeonato (`Camp_Equipos.asp?IdTorneo=` → `campeonato-equipos.html`, migrada) |
| `sql_pruebas` (pruebas del Ranking Único por año y mes) | Se consultaba **pero no se usaba** en `Estadisticas.asp` | Eliminada |

## Lógica / visibilidad heredada

- **Todo el bloque de estadísticas dependía de `if not rs_rnks.eof`**: una licencia válida que no figura en ningún
  ranking nacional visible veía solo el formulario, sin ningún mensaje (y la fila "Este/a jugador/a no está participando
  en ningún ranking nacional" de la tabla nunca se pintaba). La API responde `404` con `motivo = sin-ranking` y la página
  lo explica (sin publicar más datos de esa persona que el ASP).
- Variable `A` nunca asignada (como en Órganos de Gobierno): `IF A=5` ocultaba siempre el aviso "EN PROCESO DE REVISION Y
  ACTUALIZACIÓN" e `IF A=""` mostraba siempre el formulario. No se migra el aviso.
- La categoría de la ficha **no es la de competición**: es la edad (año en curso − año de nacimiento) con los tramos del
  ASP. De 24 a 34 años no se asignaba ninguna (se veía "Categoría:" vacío) y la edad 0 se llamaba `SENIOR`.
- El balance, la pareja y la mejor clasificación **solo miran el Ranking Único** (10000/10001) del último año
  (`FechaAct` del 10000 − 1 año, también para las jugadoras). Por eso un jugador de menores o el número 1 del ranking
  absoluto (cuyas pruebas son torneos autonómicos sin resultados registrados) salen con 0 partidos. La página nueva lo
  explica bajo el balance.
- Los partidos se buscaban por `NumPareja` dentro de la prueba **sin filtrar por género ni categoría**; se mantiene.
- La "última pareja" era la de la prueba más reciente (por año y mes) en la que constaba el jugador; si en esa prueba no
  tenía compañero, salía vacía.
- `CargarPruebas.asp` hacía `UPDATE` de `SFEP_RankingTorneos.EstaSumando` y `Topavergae` (marca de las 13 mejores y de las
  que cuentan para el average) **en cada clic**, pero la columna que las pintaba estaba comentada: escrituras sin efecto
  visible. No se migran (la API es de solo lectura).
- `CargarPruebas.asp` filtraba por fecha dos veces y marcaba en verde las filas con `FechaFin > fechaini`, que eran todas
  las mostradas: el verde no distinguía nada.
- El nombre del ranking se guardaba en una variable que no se reiniciaba: una categoría desconocida heredaba el nombre de
  la fila anterior (en la web pública, la licencia 12762 muestra "RANKING VETERANOS · CAT · posición 0" para el
  `TipoRanking` 10100). Ahora es "Otro ranking (CAT)" y "Sin puesto".
- `CargarPruebas.asp?Rnk=10100&id=12762` da **error 500** (`cdate` de una `FechaAct` vacía); la API muestra todas las
  pruebas de ese tipo cuando no tiene fecha de actualización.
- El gráfico mezclaba el histórico de los dos géneros (`Ranking=10001 or Ranking=10000`): para la licencia 12762, la mitad
  de los 12 puntos son filas de posición 0 y 2 puntos (otra lista), así que solo se ven 7 publicaciones reales en zigzag.
- Funciones `MesAnterior`, `CargarEvento`, `MeGusta` y `busqueda` del `<head>` no se usaban; `onlyNumbersDano` solo
  bloqueaba teclas (se podía pegar texto).

## Seguridad

- **Inyección SQL**: la licencia se concatenaba sin comillas en 12 consultas (`… = " & id`) y el DNI con comillas
  (`limpiar()` cambia `'` por `´`, es una lista negra). `?id=abc` devuelve **error 500** en la web actual. Ahora: licencia
  entera validada por el enrutado (`400` si no es un número positivo) y todo como parámetros Dapper.
- **Escritura en la BD desde una página pública**: cada visita borraba e insertaba filas en `SFEP_estadisticasWeb` y cada
  clic en un ranking actualizaba `SFEP_RankingTorneos`, con valores concatenados. Eliminado.
- **XSS**: nombres, clubes, torneos y mensajes (`Terror` incluía el DNI tecleado) se escribían sin codificar. En la web
  nueva todo va por `textContent`.
- **Protección de datos**: el formulario por DNI permitía saber, sin identificarse, el nombre, la edad aproximada, la foto
  y el club de cualquier persona a partir de su DNI. No se publica en la API (ver Pendiente).

## Mejoras respecto al original

- Dos idas a la BD de solo lectura en lugar de ~15 consultas + 2 por prueba disputada (N+1), sin tablas intermedias ni
  `UPDATE`; fechas como `DateTime` (el ASP comparaba `FechaFin > 'd/m/aaaa'`, dependiente del idioma del servidor, y
  fallaba el 29 de febrero al restar un año).
- El desglose de pruebas de todos los rankings llega en la misma respuesta: desplegables accesibles en lugar de
  `javascript:verpruebas()` (7 enlaces por fila, sin teclado ni lector de pantalla).
- Puntos de la categoría anterior señalados con etiqueta y "(40 % de 1.480)", no solo "592 (40%)".
- Bandera de cada prueba con el nombre de la federación (antes imagen sin `alt`); "FEP" para las nacionales.
- Evolución con barras HTML/CSS que se leen como texto (puesto y puntos de cada publicación) y tabla con el cambio de
  puesto, en lugar de Highcharts + jQuery cargado dos veces.
- Campeonatos por equipos con enlace real a su ficha (`campeonato-equipos.html`) y fecha completa; los diez más recientes
  y "Ver N campeonatos más".
- Cada ranking enlaza a su clasificación filtrada por la licencia (`ranking-absoluto.html?licencia=`…) y la última pareja
  a sus propias estadísticas.
- `comun.js` traslada `Estadisticas?id=` (rankings, BD) a `estadisticas.html?licencia=`; la página acepta también `?id=`.
- Porcentajes con coma decimal en todos los casos (el ASP mezclaba "14,3%" y "85.7%").
- Nombres en tipo título solo en el front ("Alvaro Matador Garcia de Vinuesa"), como en los rankings.
- Título de la pestaña con el nombre del jugador/a; `meta description` propia.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| Categoría vacía de 24 a 34 años | `SENIOR` | Hueco en los tramos del ASP; se interpreta como sénior (DECISIÓN FEP: confirmar el nombre) |
| "16's", "32's", "64's", "Cuartos", "Octavos" (mejor clasificación) | "Dieciseisavos de final", "Treintaidosavos de final", "Sesentaicuatroavos de final", "Cuartos de final", "Octavos de final" | Legibilidad; la ronda de cada prueba (`SFEP_RankingTorneos.Ronda`: "Campeon", "16avos"…) se muestra tal cual |
| "Primera Categoría" … "Décima Categoría" | "Primera categoría" … | Estilo de frase, como el resto de la web nueva |
| "Toneos Jugados" | "Torneos jugados" | Errata |
| "Torneos Average" / "Puntos Average" | "Torneos ranking" / "Puntos ranking" | Mismo nombre que en las páginas de ranking migradas |
| "RANKING ÚNICO", "RANKING MENORES", "RANKING VETERANOS" | "Ranking Único Absoluto · Masculino", "Ranking de Menores · Alevín femenino", "Ranking de Veteranos · +35 masculino" | Mismos títulos que `ranking-*.html`; la lista sustituye a la columna "Categoría" (`ABSOLUTO`, `ALEVIN`, `CAT35`) |
| "14,3%" y "85.7%" | "14,3 %" y "85,7 %" | El ASP usaba dos formatos en la misma línea |
| Enlace del campeonato a `Campeonatos_Previo.asp` (no existe: la regla `^Campeonatos` lleva al listado general) | Ficha del campeonato (`Camp_Equipos.asp?IdTorneo=` → `campeonato-equipos.html?id=`) | Enlace roto |
| Gráfico con los dos rankings únicos mezclados y puntos de posición 0 | Solo el `TipoRanking` del Ranking Único en el que figura el jugador/a y publicaciones con puesto | Mezcla de listas (ver Pendiente) |
| Nombre de ranking heredado de la fila anterior; posición 0 | "Otro ranking (CAT)"; "Sin puesto" | Variable no reiniciada en el ASP |

Los textos de la BD (torneos, rondas, clubes, nombres) no se cambian, aunque algunos tienen la codificación rota
("CT D‚ARO", "AURIAL AUGUSTA PÑDEL", "CE HISPANO FRANCéS").

## Comprobación de enlaces (16/11/2026)

- `https://www.padelfederacion.es/Estadisticas?id=3889`, `?id=12762`, `?id=310047`: 200 (instantáneas usadas para los
  mocks). `?id=1`: 200 con el mensaje de licencia inexistente. `?id=abc`: **500**.
- `CargarPruebas.asp?Rnk=10000&id=3889`, `?Rnk=10000&id=12762`, `?Rnk=10035&id=12762`, `?Rnk=7001&id=310047`,
  `?Rnk=6001&id=310047`: 200. `?Rnk=10100&id=12762`: **500**.
- `https://www.padelfederacion.es/gESTION/Fotos/0.jpg` (y sin `www`): 200. `refs/images/0.jpg`, `2.jpg`, `12.jpg`
  (banderas): 200. `refs/images/PuntuaSi.png` y `refs/js/Graficos/highcharts.js`: 200 (ya no se usan).
- `Licencias_SolicitaClave.asp` (recuperar la licencia): 200. `Campeonatos_Previo.asp`: 200 pero es el listado general
  de campeonatos (reescritura `^Campeonatos`), no la ficha.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: búsqueda por DNI. El ASP permitía consultar, sin identificarse, el nombre, la foto, la edad aproximada
  y el club de cualquier persona a partir de su DNI (y confirmaba si un DNI existía "pero no tiene licencia de jugador/a").
  No se ha publicado en la API (sería un `GET` con el DNI en la URL, cacheable y registrado en los logs); el formulario
  enlaza a la web actual para buscar por DNI. Si se quiere mantener, propuesta: hacerlo en el área privada (Mi Portal)
  o con un `POST` sin caché y límite de peticiones. Relacionado con P0314 (licencias publicadas en los rankings).
- **DECISIÓN FEP**: foto del jugador/a. Se publica como hoy (`gESTION/Fotos/{licencia}.jpg` si `Foto='S'`). ¿Hay
  consentimiento para mostrarla en una página pública? En los tres casos revisados no hay foto (silueta `0.jpg`).
- **DECISIÓN FEP**: licencias que existen pero no están en ningún ranking nacional. El ASP no mostraba nada (ni un mensaje);
  la API responde `404` (`sin-ranking`) y la página explica que no hay estadísticas publicadas. ¿Se quieren mostrar al
  menos sus campeonatos por equipos?
- **DECISIÓN FEP**: la nota de `CargarPruebas.asp` decía "EL RANKING FINAL LO COMPONEN LAS 6 MEJORES PUNTUACIONES", pero la
  normativa del Ranking Único habla de 13 y el propio `CargarPruebas.asp` marcaba 13 (`i>12`). No se ha migrado la frase;
  cada desplegable indica el periodo de las pruebas. ¿Cuántas cuentan en cada ranking (absoluto, menores, veteranos)?
- **DECISIÓN FEP**: el inicio del periodo de los rankings de menores está escrito a mano en `CargarPruebas.asp`
  (`04/11/25`, 4 de noviembre de 2025) en lugar de calcularse como el resto (actualización − 1 año). Se mantiene en
  `ReglasEstadisticas.InicioPeriodoMenores`. ¿Hay que actualizarlo cada temporada o calcularlo?
- **DECISIÓN FEP**: `TipoRanking` 10100 (`Categoria = 'CAT'`, visible y con puesto 0) aparece en las estadísticas de la
  licencia 12762 como un tercer ranking. ¿Es un ranking real o hay que ocultarlo (`Visible='N'` en `SFEP_Ranking_Tipos`)?
- **DECISIÓN FEP**: el balance de partidos, la pareja y la mejor clasificación solo cuentan el Ranking Único y los
  partidos con resultado en `SFEP_Torneos_Resultados`; para la mayoría (menores, jugadores de circuitos autonómicos) sale
  vacío. ¿Se amplía a los demás rankings o se retira el bloque? Se ha mantenido con una nota explicativa.
- **DECISIÓN FEP**: clubes y nombres se muestran como están en la BD (clubes en mayúsculas; nombres en tipo título solo
  en el front, como en los rankings). Mismo criterio pendiente que P0123.
- **SUPUESTO**: tipos de columna de `SFEP_RankingFinal` y unicidad de `SFEP_Fichas.LicenciaJugador`, mismo supuesto que
  P0315 y P0316. Además: `SFEP_Ranking_Historico.Fecha` es fecha (se ordena por ella, como el ASP), `Posicion`/`Puntos`
  numéricos; `SFEP_Torneos_Cuadros.Nivel`/`Avance` enteros; `Set{n}Jug{m}`, `IdPareja1/2` y `NumPareja` numéricos (se leen
  con `TRY_CONVERT`); `SFEP_Fichas.FechaNac` fecha o texto dd/mm/aaaa; `SFEP_CategoriasMenores.AnoInicial/AnoFinal` años.
- **SUPUESTO**: un set sin dato (`NULL`) vale 0 juegos y un set empatado (0-0, no jugado) no cuenta; el ASP daba error con
  `NULL` (`cint`) y tampoco contaba los empates.
- **SUPUESTO**: `NumPareja` no se repite dentro de una prueba entre géneros o categorías (los partidos se buscan solo por
  prueba y pareja, como en el ASP). Si se repitiera, el balance sumaría partidos de otra pareja.
- **SUPUESTO**: en el histórico, las filas de posición 0 son de la otra lista o publicaciones sin puesto; se muestran solo
  las del `TipoRanking` del Ranking Único en el que figura el jugador/a (si no figura en ninguno, los dos, como el ASP).
- **SUPUESTO**: en los mocks, las pruebas autonómicas llevan `idPrueba` 0 y fecha el día 1 del mes (la web pública solo
  publica año y mes); la licencia de la última pareja de 12762 (210156) sale de la búsqueda por nombre del ranking
  absoluto; `anioLicencia` va a `null` (la web no lo muestra).
- P0408: con la página ya migrada, `rankings.html` podría enlazarla (tarjeta en `SFEP_Rankings_Web`); hoy se llega desde
  el menú "Competición › Estadísticas" y desde el nombre de cada jugador en los rankings.
- Activar el 301 `/Estadisticas` → `/estadisticas` (con `?id=` → `?licencia=`). Las estadísticas de pádel en silla de
  ruedas (`Silla-Ruedas-Est.asp`) siguen en la web actual.

## Modelo SQL propuesto

No hace falta contenido nuevo: todo sale de la BD. Para retirar las escrituras del ASP sin perder nada:

```sql
-- SFEP_estadisticasWeb solo la usaba Estadisticas.asp para ordenar el gráfico (DELETE + INSERT por visita): se puede
-- eliminar cuando se apague la página antigua.
-- DROP TABLE SFEP_estadisticasWeb;

-- Índices que aprovechan las consultas de la API (si no existen):
CREATE INDEX IX_RankingTorneos_Jugador ON SFEP_RankingTorneos (IdJugador, Federacion, Ranking) INCLUDE (IdPrueba, Puntos, Ronda, Obs);
CREATE INDEX IX_RankingHistorico_Licencia ON SFEP_Ranking_Historico (Licencia, Federacion, Ranking, Fecha DESC) INCLUDE (Posicion, Puntos);
CREATE INDEX IX_TorneosJugadores_Ficha ON SFEP_Torneos_Jugadores (IdFicha) INCLUDE (Torneo, IdFederacion, NumPareja, Genero, Categoria);
```

Opcional, para no tener la fecha de menores escrita en el código: `ALTER TABLE SFEP_Ranking_Tipos ADD InicioPeriodo date NULL;`
(si se rellena, sustituiría a `FechaAct − 1 año` y a la fecha fija de menores sin cambiar el contrato).

## Ficheros

- API: `Contracts/Competicion/EstadisticasJugadorDto.cs`, `Contracts/Competicion/ReglasEstadisticas.cs`;
  `Data/Repositories/Competicion/EstadisticasJugadorRepository.cs` (Dapper, dos idas de solo lectura; reutiliza
  `IFederacionesAutonomicasRepository`, `ReglasClasificaciones` y `ReglasCampeonatos.RutaFicha`);
  `Controllers/Competicion/EstadisticasJugadorController.cs`; registro en `Program.cs`; tests en
  `tests/FEP.Api.Tests/EstadisticasJugadorTests.cs`.
- Web: `wwwroot/estadisticas.html`, `assets/js/estadisticas.js`, estilos `.est-*` en la sección "Páginas interiores" de
  `assets/css/fep.css` (y su bloque responsive).
- Mock: `mock/competicion-jugadores-{3889,12762,310047}-estadisticas.json` (instantánea de la web pública del 16/11/2026)
  y `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS` y `QUERY_MIGRADA` en `assets/js/comun.js` (`estadisticas` → `estadisticas.html`,
  `id` → `licencia`); nota de `ranking-absoluto.html`, `ranking-menores.html` y `ranking-veteranos.html` y título del
  enlace en `ranking.js`.
- Documentación: `docs/api/endpoints.md`, `docs/migracion/mapa-migracion.md`, filas de `tools/generar-mapa-web.ps1`
  (Desarrollado).
