# Análisis: `Ranking_Equipos_Absolutos.asp` → `ranking-equipos-absolutos.html` + `GET /api/v1/competicion/rankings/equipos-absolutos`

URL actual: `/Ranking_Equipos_Absolutos` (enlace "Ranking Equipos Asbolutos" de `SFEP_Rankings_Web`, desde
`rankings.html`; responde igual con y sin `.asp`). Ruta nueva: `/ranking-equipos-absolutos` →
`ranking-equipos-absolutos.html` (no estaba en `redirecciones.csv`; se ha añadido con el nombre que ya preveía
`rankings.js` y siguiendo `/ranking-absoluto`).

Fichero original en Windows-1252. Includes: `conexionSQL.inc` (conexión; no se copia nada), `Menus.inc`,
`BarraLateral.inc`, `PiePagina.inc`, `finconexion.inc` (comunes, disponibles en `X:\TICS\WEB\include\` y ya migrados
en `comun.js`). No carga nada por AJAX: las funciones `rnkabs*`/`rnkmen*` del `<head>` son de otras páginas y aquí no se
usan.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie, barra lateral | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` + `/api/v1/menu`, `/redes-sociales`, `/patrocinadores` (columna derecha) |
| `sql_noticias` (`SFEP_Noticias` destacadas, `SELECT *`) | Se consultaba **pero no se usaba** | Eliminado |
| Título "RANKING EQUIPOS ABSOLUTOS" y frase "En esta pantalla se muestra el resultado de los Campeonatos Absolutos del año correspondiente" | HTML escrito a mano | `h1` "Ranking de Equipos Absolutos." + entradilla + cifras (temporadas, clubes, última temporada) calculadas con los datos |
| 8 bloques "CLASIFICADOS ABSOLUTOS - 2024" … "CLASIFICACIÓN CAMPEONATOS ABSOLUTOS - 2017", uno debajo de otro | HTML escrito a mano, **un bloque por año copiado y pegado** | Selector de temporada (`?anio=`) con las temporadas que hay en la BD; por defecto, la más reciente |
| 4 tablas por año: 1.ª masculina, 1.ª femenina, 2.ª masculina, 2.ª femenina | **32 consultas** casi idénticas (`sql_EqAbs{1,2}{M,F}{17..24}`): `SFEP_Ranking_Equipos` ⨝ `SFEP_Clubes` (`Idclub`) ⨝ `SFEP_Comunidades` (`Comunidad`) ⨝ `SFEP_Federaciones` (`Comunidad`) ⨝ `SFEP_Provincias` (`Provincia`), `WHERE Modalidad='EQU' AND Tipo='ABS' AND Genero=… AND Categoria=… AND ano=…` (desde 2018 además `IdFederacion<100`), `ORDER BY Puesto` | Una consulta parametrizada → `RankingEquiposDto.temporadas[].listas[]` (`ListaRankingEquiposDto`, en el mismo orden) |
| Columnas de cada fila: puesto · bandera de la federación · provincia · escudo del club · nombre del equipo (enlace) | `Puesto`; `refs/images/{IdFederacion}.jpg`; `SFEP_Provincias.Provincia`; `refs/images/{IdClub}.jpg`; `Equipo` con enlace `Club.asp?id={IdClub}` (`target="_parent"`) | `PuestoEquipoDto`: `puesto`, `federacion` (`id`, `territorio`, `nombre`, `abreviatura`, `bandera`/`banderaUrl`), `provincia`, `escudo`/`escudoUrl`, `equipo`, `club`/`clubUrl` |
| Columnas seleccionadas y no pintadas | `Modalidad`, `Tipo`, `Categoria`, `SFEP_Clubes.Web` | No se leen |
| — | — | Nuevo: "Campeones por temporada" (los primeros de cada categoría y año, calculado en el front con la misma respuesta) |

## Diseño de la API

- Recurso propio y no una clave más de `/api/v1/competicion/rankings/{clave}` (`ClasificacionesRankingController`):
  sus puestos son **equipos de club por temporada**, no jugadores con licencia, puntos y evolución. Se publica en la
  ruta literal `/api/v1/competicion/rankings/equipos-absolutos` (`RankingEquiposController`), que en ASP.NET Core tiene
  prioridad sobre la plantilla `{clave}`; la `clave` coincide con la del índice (`RankingWebDto.clave`).
- `?anio=` opcional (1901–2100, como `/competicion/campeonatos`; `anio` ya está en `SetVaryByQuery`). Sin él se
  devuelven todas las temporadas (~250 equipos): el front pide una sola vez y cambia de temporada sin más peticiones.
  `anios` siempre trae todas las temporadas publicadas, también cuando se filtra.
- Territorio y nombre oficial de cada federación salen de `Contenidos/federaciones-autonomicas.json`
  (`IFederacionesAutonomicasRepository`, los mismos de `federaciones.html`); si un id no está, se usa la `Abreviatura`
  de la BD.
- Composición y reglas (orden de listas, filtros, nombre del equipo, rutas) en `ReglasRankingEquipos`, probadas sin BD.

## Lógica / visibilidad heredada

- **Años escritos en el código**: cada temporada nueva obligaba a copiar 4 consultas y ~80 líneas de HTML. La web
  pública sigue terminando en 2024. Ahora se publica cualquier temporada `>= 2017` que haya en la BD (**DECISIÓN FEP**,
  ver abajo). Las anteriores a 2017, si existen, no se mostraban y siguen sin mostrarse (`AnioMinimo`).
- **Duplicados de 2017**: las consultas de 2017 no filtraban `IdFederacion<100` y los clubes de comunidades con una
  segunda federación (ids 1001/1002) salen **dos veces** en la web actual: LA RED 21 - GALISPORT y CLUB SATO SPORT
  (1.ª masculina), MONTECARLO-ARENAENTRENAPADEL… y REAL ZARAGOZA CLUB DE TENIS (1.ª femenina) y C.TENIS Y PADEL LA
  CAPELLANIA (2.ª femenina): 250 filas en la web frente a 245 equipos reales. Ahora el filtro 1–99 se aplica a todas
  las temporadas y la federación se toma con `OUTER APPLY TOP (1)`, así que no puede duplicar.
- **Equipos perdidos por los `INNER JOIN`**: un equipo cuyo club no existiera en `SFEP_Clubes` o no tuviera provincia,
  comunidad o federación no se pintaba. Ahora sale igualmente, sin bandera ni provincia (`null`, "—" en la web).
  No se puede saber sin acceso a la BD si hoy hay alguno (**SUPUESTO**: la instantánea pública no los muestra).
- **Empates**: `ORDER BY Puesto` sin desempate (orden indefinido entre los dos 3.º, 5.º o 7.º). Ahora se desempata por
  nombre del equipo.
- Las listas sin equipos pintaban igualmente su título y una tabla vacía; ahora no se publican (no ocurre en
  2017–2024: todas tienen equipos).
- No recibe parámetros de `Request`: no había inyección SQL. Sí **XSS** almacenado posible: `Equipo` y `Provincia` se
  escribían sin codificar y `IdClub` iba sin codificar en el `href`. En la web nueva todo va por
  `textContent`/atributos, `urlSegura` y `Uri.EscapeDataString`.
- Imágenes sin `alt` y **escudos rotos**: el ASP pintaba `refs/images/{IdClub}.jpg` aunque no existiera (37 de 58
  clubes dan 404). Ahora, si el escudo no carga, se ponen las iniciales del equipo; banderas con `alt=""` (el territorio
  va escrito al lado).
- Dos títulos distintos para lo mismo: "CLASIFICADOS ABSOLUTOS - año" (2019–2024) y "CLASIFICACIÓN CAMPEONATOS
  ABSOLUTOS - año" (2017–2018). Unificado.
- Un nombre de equipo de 2024 empieza por tabulador ("\tC.D. LA RED XXI PADEL CENTER"); se normalizan espacios.

## Mejoras respecto al original

- Una sola consulta (`QueryMultiple`: temporadas + equipos) en lugar de 32 + la de noticias; columnas explícitas,
  filtros fijos (`EQU`, `ABS`, categorías, géneros, año mínimo, rango de federación) como parámetros.
- Selector de temporada enlazable (`?anio=2023`), en lugar de una página de ~8 pantallas con todas las temporadas.
- Tabla accesible por categoría (`caption`, `th scope`, nombre del equipo como cabecera de fila), puesto con podio
  destacado y número de equipos; en móvil cada equipo es una ficha (puesto, escudo y nombre; debajo federación y
  provincia) sin scroll horizontal.
- La bandera enlaza a la ficha nueva de la federación (`federacion.html?id=`) con su territorio escrito (antes solo una
  imagen sin texto); provincia en tipo título ("Barcelona"; la BD la guarda en mayúsculas) solo en el front.
- Historial "Campeones por temporada" (tabla año × categoría) con enlace a la clasificación de cada año.
- Título de la pestaña: "Ranking Equipos Absolutos | FEP" → "Ranking de Equipos Absolutos · Federación Española de
  Pádel"; `meta description` propia (antes la genérica de la web).

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| 2017: cinco equipos repetidos (federaciones 1001/1002) | Una fila por equipo, con la federación autonómica (1–99) | Error evidente del `JOIN` sin el filtro que sí tenían 2018–2024 |
| "CLASIFICADOS ABSOLUTOS - 2024" / "CLASIFICACIÓN CAMPEONATOS ABSOLUTOS - 2017" | "Temporada 2024" (selector) | Homogeneidad |
| "1ª CATEGORÍA MASCULINA" | "1.ª categoría masculina" | Ordinal abreviado con punto (RAE) y estilo de frase |
| "\tC.D. LA RED XXI PADEL CENTER" (y espacios repetidos) | "C.D. LA RED XXI PADEL CENTER" | Espacios sobrantes en la BD |
| `<title>Ranking Equipos Absolutos \| FEP</title>` | "Ranking de Equipos Absolutos · Federación Española de Pádel" | Coherencia con el `h1` |

No se han cambiado nombres de equipos ni puestos: se publican tal como están en la BD (p. ej.
"MONTECARLO-ARENAENTRENAPADEL los boliches ENERGYSUR", con minúsculas, o listas con puestos que faltan, como la
1.ª masculina de 2018 sin 4.º ni 8.º o la 2.ª femenina de 2017 con 4 equipos).

## Comprobación de enlaces (13/11/2026)

- `https://www.padelfederacion.es/Ranking_Equipos_Absolutos`: 200 (instantánea usada para el mock: 8 temporadas,
  250 filas = 245 equipos + 5 duplicados).
- Banderas `refs/images/{1..21}.jpg` (15 federaciones distintas): todas 200.
- Escudos `refs/images/{IdClub}.jpg` (58 clubes distintos): **21 dan 200 y 37 dan 404** (la web actual pinta la imagen
  rota; la nueva, las iniciales).
- `Club.asp?id=0908009`: 200 (la ficha del club sigue en la web actual; cuando se migre, basta con añadirla a
  `PAGINAS_MIGRADAS` y `ranking-equipos.js` la usará).

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: la web actual se quedó en 2024 porque cada año había que tocar el código. La nueva publica todas
  las temporadas `>= 2017` que haya en `SFEP_Ranking_Equipos` (si ya hay 2025 en la BD, aparecerá). ¿Es correcto o hay
  temporadas cargadas que no deben verse? Si hiciera falta, se puede añadir un año máximo o una columna `Visible`.
- **DECISIÓN FEP**: al entrar se muestra solo la temporada más reciente (el ASP mostraba las 8 seguidas) y se añade el
  historial "Campeones por temporada". ¿Se mantiene?
- **DECISIÓN FEP**: 37 de 58 clubes no tienen escudo en `refs/images/`. ¿Se suben los escudos o se quita la columna?
  Hoy se muestran las iniciales.
- **DECISIÓN FEP**: ¿publicar también temporadas anteriores a 2017 si existen en la BD? Hoy no (como el ASP).
- **DECISIÓN FEP**: en los empates el orden es alfabético (en el ASP era indefinido). ¿Hay un criterio de desempate
  oficial (p. ej. resultado del cruce de consolación)?
- **SUPUESTO**: `ano`, `Categoria` y `Puesto` son numéricos (el ASP los comparaba sin comillas); `Idclub` se lee como
  texto porque tiene ceros a la izquierda (`0908009`), igual que `SFEP_Clubes.IdClub` en el resto de la API; `Genero`
  puede venir con espacios de relleno (se recorta).
- **SUPUESTO**: la federación de un club es la de su comunidad con `IdFederacion` 1–99 (si una comunidad tuviera dos,
  se toma la de id menor); el mapeo comunidad → federación se hace como en el ASP (`SFEP_Federaciones.Comunidad =
  SFEP_Clubes.Comunidad`, `SFEP_Comunidades` solo hacía de puente).
- **SUPUESTO**: el mock es una instantánea de la web pública (no de la BD): `club`, `provincia` y federación salen del
  HTML, y los equipos que la web perdía por los `INNER JOIN` (si los hay) no están.
- **SUPUESTO**: ruta nueva `/ranking-equipos-absolutos` (no había fila en `redirecciones.csv`).

## Modelo SQL propuesto

No aplica: todo el contenido ya está en la BD (`SFEP_Ranking_Equipos`) y no hay texto escrito a mano más allá del
título. Opcional, para la consulta única:

```sql
CREATE INDEX IX_SFEP_Ranking_Equipos_Web
    ON SFEP_Ranking_Equipos (Modalidad, Tipo, ano, Categoria, Puesto)
    INCLUDE (Genero, Equipo, Idclub);
-- Si la FEP quiere controlar qué temporadas se publican (DECISIÓN FEP):
-- ALTER TABLE SFEP_Ranking_Equipos ADD Visible char(1) NOT NULL DEFAULT 'S';
```

## Ficheros

- API: `Contracts/Competicion/RankingEquiposDto.cs`, `ReglasRankingEquipos.cs` (con `FilaRankingEquipo`);
  `Data/Repositories/Competicion/RankingEquiposRepository.cs` (Dapper);
  `Controllers/Competicion/RankingEquiposController.cs`; registro en `Program.cs`; tests en
  `tests/FEP.Api.Tests/RankingEquiposTests.cs`.
- Web: `wwwroot/ranking-equipos-absolutos.html`, `assets/js/ranking-equipos.js`, estilos `.rke-*` en la sección
  "Páginas interiores" de `assets/css/fep.css` (reutiliza `.rkc-*`/`.org-table`, y su bloque responsive).
- Mock: `mock/competicion-rankings-equipos-absolutos.json` (instantánea de la web pública del 13/11/2026 sin los
  duplicados de 2017) y `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`ranking_equipos_absolutos` → `ranking-equipos-absolutos.html`;
  así la tarjeta de `rankings.html` y cualquier enlace de la BD van a la página nueva); comentario de `rankings.js`.
- Documentación: `docs/api/endpoints.md`, `docs/migracion/redirecciones.csv` (fila nueva),
  `docs/migracion/mapa-migracion.md`, fila de `tools/generar-mapa-web.ps1` (Desarrollado).
