# Análisis: `CalendarioFT.asp` → `calendarioft.html` + `GET /api/v1/competicion/calendario/federaciones[/{id}]`

URL actual: `/CalendarioFT` (regla `^CalendarioFT` de `web.config` → `CalendarioFT.asp`; opción de menú "Calendario Fed.
Territoriales" bajo Competición, `SFEP_Web_Menu.Link = 'CalendarioFT'`, id 36). Ruta nueva propuesta en
`redirecciones.csv`: `/calendarioft` → `calendarioft.html`. El formulario hacía `POST` a `CalendarioFT` con `fed` y `ano`;
`Request(...)` también los lee de la query (`/CalendarioFT?fed=6&ano=2026`, comprobado). La página nueva acepta
`?federacion=&anio=` y los nombres antiguos (`?fed=&ano=`), y `comun.js` traslada los enlaces antiguos.

Includes: `conexionSQL.inc` (no se copia), `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (ya migrados en
`comun.js`), `finconexion.inc`. Codificación del `.asp`: Windows-1252. Es **casi el mismo fichero** que
`CalendarioFEP.asp` (ver `CalendarioFEP.md`): mismas 13 consultas por mes, mismas columnas, mismo `irCamp()`; cambian el
desplegable de federación, el valor por defecto de `fed` (1000 = ninguna) y de `ano` (la temporada FEP más reciente), y
que no hay banner de la convocatoria ni PDF para las federaciones. Por eso **se reutiliza todo** lo del calendario FEP:
`CalendarioCompeticionRepository` (mismas consultas con otra `@Federacion`), `ReglasCalendario`, los DTO
`TemporadaCalendarioDto`/`PruebaCalendarioCompeticionDto` y la presentación de `calendario-fep.js` (tarjetas por mes).

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, barra lateral, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (`activo=8`) | Ya migrado: `comun.js` (+ columna de patrocinadores) |
| `sql_noticias` (`SFEP_Noticias`, `SELECT *`) | Se consultaba **pero no se usaba** | Eliminado |
| `sql_archivo` (`SFEP_Calendario_Archivo`, `SELECT *`) | `<%if HabitaSolicitud="S" then%><%end if%>` **vacío**; `Fecha` y `ArchivosLicitacion` solo con `fed = 0` | Nada (ver `fed = 0` abajo) |
| Título "calendarios federaciones territoriales" + "Filtro y Datos del Calendario (N)" | HTML a mano + `sql_num` recorrido entero solo para contar (**sin** filtrar `Visible`) | `h1` "Calendarios de las federaciones territoriales." + contador real en `role="status"` |
| Desplegable "Federación" | `sql_fed`: `SFEP_Federaciones` `IdFederacion BETWEEN 1 AND 21` `ORDER BY Abreviatura` (pinta `Abreviatura`) + opción "-----" (`1000`) | `GET /api/v1/competicion/calendario/federaciones` (ids 1–22, con territorio y nombre oficial de `Contenidos/federaciones-autonomicas.json` y sus temporadas) |
| Desplegable "Año del Torneo" | `sql_ano`: años de `SFEP_Calendario` con **`Federacion = 0`** (los de la FEP, 2012–2027), por defecto el más reciente (2027) | `temporadas` de la federación (años con alguna prueba visible); por defecto la del año en curso (`ReglasCalendario.TemporadaPorDefecto`) |
| Mensaje "Selecciona una Federación Territorial para ver su Calendario" | Con `fed = 1000` (sin elegir) | Igual (`#calt-elige`) + rejilla "Calendarios por federación" |
| Tabla por meses (ENERO … DICIEMBRE + "SIN FECHA ASIGNADA") | 13 consultas iguales (`sql_ene`…`sql_dic` y `FechaINi IS NULL`): `SFEP_Calendario ⨝ SFEP_Calendario_Categorias` con `Año='<ano>'`, `month(FechaINi)=N`, `Federacion=<fed>`, `VIsible='S'`, `ORDER BY FechaIni` | `items` (una consulta, la misma que el calendario FEP); el front agrupa por mes |
| Columnas Inicio / Fin / Prueba / Categoría / Sede | `day()` + mes abreviado (26 `select case`), `Campeonato`, `TextoCategoria`, `Sede` | `fechaInicio`, `fechaFin`, `titulo`, `categoria`, `sede` (mismo `PruebaCalendarioCompeticionDto`) |
| Enlace de cada celda | Si `IdPrueba <> 0` y `Link <> '#'`: `javascript:irCamp(IdPrueba)` → `POST Campeonatos_Previo.asp` | `idTorneo` + `enlace` (ficha de `Torneos_Previo.asp`) + `nuevaVentana` (`ReglasCalendario.Enlazable`) |
| `fed = 0` (solo por URL) | Calendario FEP con "Fecha de Actualización" (`SFEP_Calendario_Archivo.Fecha`) y descarga de `ArchivosLicitacion` | `calendariofep.html` (redirige la página y `comun.js`) |

Columnas usadas: las mismas que `CalendarioFEP.asp` (`SFEP_Calendario`: `Año`, `Federacion`, `Visible`, `FechaIni`,
`FechaFin`, `Campeonato`, `Categoria`, `Sede`, `IdPrueba`, `Link`; `SFEP_Calendario_Categorias.TextoCategoria`) más
`SFEP_Federaciones.IdFederacion` y `Abreviatura`. La página **no escribe** en la BD.

## Parámetros (`Request`) y validación

| ASP | API nueva | Validación |
|---|---|---|
| `fed` (`""` → 1000 = ninguna; `0` = FEP) | `{id}` en la ruta (`/federaciones/{id}`) | entero 1–22; si no, **400** `ValidationProblemDetails` (0 → usar `/competicion/calendario`); si no existe en `SFEP_Federaciones`, **404** con `urlWebActual` |
| `ano` (`""` → la temporada FEP más reciente) | `anio` (opcional) | entero 1901–2100; si no, **400** |
| `idtorneo` (a `Campeonatos_Previo.asp`) | — | El enlace a la ficha lo calcula la API |

En la página: `fed=1000` o una federación que no existe → "Selecciona una federación"; `fed=0` → `calendariofep.html`; una
temporada que la federación no tiene (enlace antiguo con un año de la FEP) → su temporada por defecto.

## Lógica / visibilidad heredada

- **Temporadas de la FEP en el desplegable**: `sql_ano` filtraba `Federacion = 0`, así que se ofrecían 2012–2027 para
  todas las federaciones y se abría por defecto **2027** (la temporada FEP más reciente), en la que solo Andalucía y
  Aragón tienen pruebas: el resto veía la tabla vacía sin explicación. Ahora cada federación ofrece sus temporadas y
  abre la del año en curso (o la más reciente anterior). Las temporadas de una federación que no estén entre las de la
  FEP (no ha pasado) no se veían en el ASP; ahora sí.
- **Castilla-La Mancha oculta**: `sql_fed` filtraba `IdFederacion BETWEEN 1 AND 21` y Castilla-La Mancha es la **22**
  (existe en `SFEP_Federaciones`: sale en el buscador de clubes). Tiene calendario de 2015, 2016 y 2023 (86 pruebas),
  solo accesible escribiendo `?fed=22` a mano. Decidido por la FEP (13/11/2026): **hay 22 federaciones, de la 1 a la
  22**; el listado y el endpoint usan ese rango (`ReglasCalendario.IdFederacionTerritorialMaximo = 22`; `?fed=23` o
  más → 400), con Castilla-La Mancha incluida como id 22.
- **`<%if HabitaSolicitud="S" then%><%end if%>`**: bloque vacío (restos de copiar `CalendarioFEP.asp`). Nada que migrar.
- **`<%IF fed=0 then%>`**: la fecha de actualización (`SFEP_Calendario_Archivo.Fecha`) y la descarga de
  `ArchivosLicitacion` solo salían con `fed = 0`, valor que el desplegable no ofrecía (solo por URL): era el calendario FEP
  con otro PDF. Se redirige a `calendariofep.html`, que ya publica el PDF de cada temporada.
- **Documentos de las federaciones**: el ASP no consultaba `SFEP_Calendario_Documentos` con `fed <> 0`. La API los
  devuelve si la gestión sube un documento "Calendario" con `Fed = {id}` (**SUPUESTO**: hoy no se ve ninguno) y la
  página solo pinta el PDF si existe.
- **Fecha comodín 1 ene – 31 dic** y **agrupación por mes y año**: mismas reglas que el calendario FEP (hoy ninguna prueba
  territorial usa la fecha comodín; 33 están en "SIN FECHA ASIGNADA" → "Fecha por confirmar").
- `activo=8`, funciones JS sin uso (`MesAnterior`, `CargarEvento`, `MeGusta`, `busqueda`), fila vacía tras cada mes,
  anchos de columna distintos en enero: como en `CalendarioFEP.asp`.

## Problemas del original

1. **Enlaces rotos en todas las pruebas con torneo**: `irCamp()` enviaba a `Campeonatos_Previo.asp`, que no existe (la
   reescritura `^Campeonatos` lleva al listado general de campeonatos). Igual que en el calendario FEP, la API enlaza a la
   ficha de `Torneos_Previo.asp` (`ReglasCampeonatos.RutaFicha`) o a `LinkCuadros`.
2. **Inyección SQL**: `fed` se concatenaba **sin comillas** en 14 consultas (y `ano` entre comillas tras `limpiar()`).
   Ahora todo va con parámetros Dapper (`@Federacion`, `@Anio`, `@AnioDesde`, `@Desde`, `@Hasta`).
3. **18 consultas por página** (más las de `Menus.inc`): noticias sin usar, `sql_archivo` sin usar, `sql_num` solo para
   contar y 13 iguales por mes. Ahora: listado = 1 ida (`QueryMultiple`: federaciones + temporadas de todas); calendario
   = 2 idas (federación, temporadas y documentos; pruebas con su torneo en `OUTER APPLY`).
4. **Contador "(N)" erróneo**: `sql_num` no filtraba `Visible`. Andalucía 2026 dice 276 y se ven 186 (2025: 283/220; 2024:
   280/258; 2023: 251/214; 2021: 128/126; 2020: 54/52); Aragón 2021: 65/64 y 2020: 47/46.
5. **Error 500 sin configuración**: `rs_archivo("HabitaSolicitud")` se leía sin comprobar EOF; con
   `SFEP_Calendario_Archivo` vacía fallaba toda la página. Ya no se consulta.
6. **XSS**: `Campeonato`, `TextoCategoria`, `Sede` y `Abreviatura` sin `Server.HTMLEncode`. El front usa `textContent`.
7. **Sin enlaces navegables** (`javascript:irCamp(id)`), **formulario que solo funcionaba con JavaScript**
   (`onChange="cambiar()"`), tabla de maquetación sin `th`/`caption`, `<meta charset=iso-8859-1>` tras los scripts.
8. **Fichas que fallan en la web actual**: 8 de las 19 fichas `Camp_Liga.asp?IdTorneo=` (circuitos provinciales de
   Andalucía 2022–2024) devuelven **500** (comprobado el 13/11/2026). Es un problema de esa página de la web actual (no
   migrada); se enlazan igual que hacía `Torneos_Previo.asp`.

## Mejoras respecto al original

- Elección de federación con su **nombre de territorio** ("Andalucía", "Comunidad Valenciana") en lugar de la abreviatura
  de la BD ("ANDALUZA", "COM.VALENCIANA"), ordenadas alfabéticamente; las que no tienen calendario se marcan "(sin
  calendario)".
- Rejilla **"Calendarios por federación"** con todas las federaciones, cuántas temporadas tienen y su rango; las que no
  tienen calendario aparecen atenuadas ("Sin calendario publicado") y, al elegirlas, se remite a su ficha
  (`federacion.html?id=`).
- Misma presentación que el calendario FEP: pruebas agrupadas por mes en tarjetas con fecha, etiqueta de categoría,
  sede y enlace a la ficha; filtros de **categoría**, **prueba o sede** y **"solo pendientes de celebrar"**; todo en la
  URL (`?federacion=6&anio=2026&categoria=Menores`).
- Índice lateral (chips en móvil) con la elección, los meses de la temporada y las federaciones; cifras en la cabecera.
- Respuestas cacheadas 5 min (output cache por ruta y `anio`); sin paginar (hasta ~260 pruebas por temporada).

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| Desplegable con las temporadas FEP 2012–2027 para todas | Las temporadas de cada federación | El ASP consultaba `Federacion = 0` |
| Temporada por defecto 2027 | La del año en curso (2026) o la más reciente anterior; cambia sola el 1 de enero (hora de Madrid) | Decidido por la FEP (13/11/2026), igual que el calendario FEP |
| Castilla-La Mancha fuera del desplegable | Incluida (id 22); federaciones 1–22 | `BETWEEN 1 AND 21`; decidido por la FEP (13/11/2026) |
| Sede en mayúsculas ("CLUB DE PADEL …") | "Club de Padel …"; las que ya mezclan mayúsculas y minúsculas, tal cual | Decidido por la FEP (13/11/2026): formato "Nombre Propio", como el calendario FEP |
| "Filtro y Datos del Calendario (276)" (Andalucía 2026) | 186 pruebas | El contador incluía pruebas no visibles |
| "ANDALUZA", "COM.VALENCIANA", "CEUTI"… | "Andalucía", "Comunidad Valenciana", "Ceuta"… (la abreviatura sigue en `abreviatura`) | Presentación; nombres de `Contenidos/federaciones-autonomicas.json` |
| "SIN FECHA ASIGNADA" | "Fecha por confirmar", al final | Igual que el calendario FEP |
| "SILLA RUEDAS" | "Silla de ruedas" (solo el texto visible) | Presentación |

Registros de la gestión que parecen mal introducidos. La web los publica tal como los guarda cada federación y la
migración no los toca: si son errores, hay que corregirlos en la gestión, no en la web. **DECISIÓN FEP**: para cada
uno, indicar si se corrige en la gestión (o se pide a la federación) o si es correcto así.

- **Fecha de fin anterior a la de inicio**: dos pruebas "terminan" antes de empezar (del 27 de febrero al 5 de
  febrero): "OPEN 24*-26* (2)" de la Comunidad Valenciana (2017) y "Territorial Coincidente" del País Vasco (2023).
  Seguramente terminan el 5 de **marzo** y se eligió mal el mes. En la web se ven con un rango de fechas imposible.
- **Títulos con códigos internos**: algunas federaciones escriben el título en mayúsculas o con asteriscos y códigos
  de uso interno ("… 1000 MATERIAL DEPORTIVO 19*", "GS - Araba", "CVM - II (no clasificatorio)"). Al público le
  resultan difíciles de entender. La web los muestra tal cual (a diferencia de la sede, no se cambia el formato del
  título); si se quieren más claros, hay que pedírselo a cada federación.

## Comprobación de enlaces (13/11/2026)

- `https://www.padelfederacion.es/CalendarioFT` y `?fed=6&ano=2026` → 200.
- Fichas de las 750 pruebas enlazadas en las 19 federaciones (744 resueltas con `Torneos_Previo.asp?IdTorneo=` → 302):
  `Camp_Parejas.asp` (394), `circuitomenores.padelfederacion.es/Campeonato` (215, solo http), `Camp_Equipos.asp` (94),
  `Camp_Clasificatorio.asp` (28), `Camp_Liga.asp` (19). Muestras de `Camp_Parejas`, `Camp_Clasificatorio`, `Camp_Equipos`
  y circuito de menores → 200; `Camp_Liga.asp`: 11 → 200 y **8 → 500** (problema 8).
- La página no enlaza PDF ni imágenes propias.

## Modo prototipo (mocks)

`mock/competicion-calendario-federaciones.json` (listado) y `mock/competicion-calendario-federaciones-{id}.json` (una por
federación, las 19) son una instantánea de la web pública del 13/11/2026 con **todas** las temporadas: **4.499 pruebas**.
Los genera `tools/generar-mock-calendario-ft.ps1` (solo lectura sobre la web pública: recorre las 16 temporadas del
desplegable para cada federación; federaciones de `mock/clubes-filtros.json`, que es la misma consulta a
`SFEP_Federaciones`; territorio y nombre de `Contenidos/federaciones-autonomicas.json`; los enlaces a fichas se
reutilizan de los otros mocks o se resuelven con `Torneos_Previo.asp`). Los ficheros por federación se escriben
compactos (Andalucía sola son 2.353 pruebas, ~650 KB; en total ~1,2 MB, que `mocks.js` incrusta). Como el mock no
filtra, `calendario-ft.js` se queda siempre con las pruebas de la temporada mostrada.

| Federación (id) | Temporadas | Pruebas |
|---|---|---|
| Andalucía (6) | 2013–2027 | 2.353 |
| Aragón (11) | 2019–2027 | 563 |
| Castilla y León (21) | 2017–2026 | 494 |
| Comunidad Valenciana (14) | 2016–2021 | 227 |
| Comunidad de Madrid (1) | 2013–2015 | 204 |
| Canarias (18) | 2018, 2026 | 179 |
| Cantabria (13) | 2020, 2022–2026 | 131 |
| Principado de Asturias (7) | 2023–2026 | 114 |
| Castilla-La Mancha (22) | 2015, 2016, 2023 | 86 |
| Cataluña (2) | 2023–2024 | 57 |
| Galicia (8) | 2021–2022 | 21 |
| País Vasco (3) | 2023 | 21 |
| Baleares (17) | 2014–2015 | 19 |
| Extremadura (12) | 2021 | 17 |
| Región de Murcia (15) | 2019 | 13 |
| Ceuta (16), Melilla (9), Navarra (10), La Rioja (19) | — | 0 |

Limitaciones del mock: solo se consultan las temporadas del desplegable de la web (las de la FEP, 2012–2027); fechas
reconstruidas con día y mes de la web y el año de la temporada (si el fin cae en un mes anterior al inicio, al año
siguiente); `actualizado`/`archivo`/`url` siempre `null` (la web no los muestra); `idTorneo` es el `IdPrueba` de
`irCamp()` y se deja `null` si `Torneos_Previo.asp` no redirige (torneo inexistente o borrado, como el `OUTER APPLY`).

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: mostrar las federaciones sin calendario publicado (Ceuta, Melilla, Navarra, La Rioja) atenuadas y
  remitir a su ficha, en lugar de ocultarlas.
- **DECISIÓN FEP**: publicar el PDF de una federación si la gestión lo sube a `SFEP_Calendario_Documentos` con
  `Fed = {id}` (el ASP nunca los mostraba).
- **DECISIÓN FEP**: los enlaces de las pruebas van a la ficha del torneo (antes, por error, al listado de campeonatos); 8
  fichas `Camp_Liga.asp` dan error 500 en la web actual.
- **DECISIÓN FEP**: corregir en la gestión los registros que parecen mal introducidos (fechas de fin anteriores al
  inicio y títulos con códigos internos; explicados en "Correcciones de datos"). La web no los cambia.
- **SUPUESTO**: tipos de columnas como en `CalendarioFEP.md`; además `SFEP_Federaciones.IdFederacion` entero y
  `Abreviatura` texto; `SFEP_Calendario.Federacion` entero.
- **SUPUESTO**: los ids de federación de `Contenidos/federaciones-autonomicas.json` son los de `SFEP_Federaciones`
  (comprobado con el buscador de clubes y las fichas); si falta alguno, se muestra la abreviatura.
- Las fichas de los torneos (`Camp_Parejas.asp`, `Camp_Clasificatorio.asp`, `Camp_Liga.asp`…) y el circuito de menores
  (solo http) siguen en la web actual; `Camp_Equipos.asp` ya está migrada (`campeonato-equipos.html`, vía `paginaMigrada`).

## Modelo SQL propuesto

No hace falta: todo el contenido sale de tablas que ya existen y no hay nada escrito a mano en la página. Índice
recomendado (el mismo que para el calendario FEP, ahora con más federaciones): `SFEP_Calendario (Federacion, [Año],
Visible) INCLUDE (FechaIni, FechaFin, Campeonato, Categoria, Sede, IdPrueba, Link)` (**SUPUESTO**: no se ha podido ver el
esquema real). Si se quiere que el territorio y el nombre oficial salgan de la BD, ver las columnas propuestas en
`Federaciones.md`; el contrato no cambia.

## Ficheros

- API: `Contracts/Competicion/CalendarioTerritorialDto.cs` (`CalendarioTerritorialDto`,
  `FederacionCalendarioTerritorialDto`, `DatosFederacionCalendarioDto`, `DatosCalendarioTerritorialDto`),
  `Contracts/Competicion/ReglasCalendario.cs` (rango de ids territoriales y años sin límite inferior),
  `Data/Repositories/Competicion/CalendarioCompeticionRepository.cs` (`ListarFederacionesAsync`,
  `BuscarFederacionAsync`; las consultas de temporadas, documentos y pruebas se comparten con el calendario FEP),
  `Controllers/Competicion/CalendarioTerritorialController.cs` (reutiliza `IFederacionesAutonomicasRepository` para el
  territorio y el nombre); comentario en `Program.cs` (el repositorio ya estaba registrado); tests en
  `tests/FEP.Api.Tests/CalendarioTerritorialTests.cs` (validación 400, 404, parámetros al repositorio, nombres desde
  `Contenidos/` y forma del JSON; sin SQL Server) y el repositorio falso de `CalendarioTests.cs` ampliado.
- Web: `wwwroot/calendarioft.html`, `assets/js/calendario-ft.js`, estilos `.calt-*` en la sección "Páginas interiores"
  de `assets/css/fep.css` (y sus bloques responsive); reutiliza `.clubs-search`, `.clubs-form`, `.camp-form`,
  `.clubs-results`, `.camp-month`, `.camp-item`, `.camp-tag--*`, `.calf-season`, `.calf-section`, `.calf-date-tbd`,
  `.calf-tag--silla`, `.doc-list`.
- Mock: `mock/competicion-calendario-federaciones.json` y `mock/competicion-calendario-federaciones-{1,2,3,6,7,8,9,10,11,
  12,13,14,15,16,17,18,19,21,22}.json` (generados con `tools/generar-mock-calendario-ft.ps1`) + `mock/mocks.js`.
- Común: `PAGINAS_MIGRADAS` y `QUERY_MIGRADA` en `assets/js/comun.js` (`calendarioft` → `calendarioft.html` con
  `fed`/`ano`; `CalendarioFEP?fed=N` y `CalendarioFT?fed=N` → `calendarioft.html?federacion=N`, `fed=0` →
  `calendariofep.html`, `fed=1000` → sin federación; nueva opción `pagina` y función `paginaCalendario`); chip
  "Territoriales" de `index.html` y nota "Más competición" de `calendariofep.html`.
- Documentación: `docs/api/endpoints.md`, `tools/generar-mapa-web.ps1` (fila "Calendario Fed. Territoriales" →
  Desarrollado), `docs/migracion/mapa-migracion.md` (área `competicion-cuadros`) y notas de `CalendarioFEP.md`.
