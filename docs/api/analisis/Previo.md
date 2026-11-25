# Análisis: `Previo.asp` → `cuadro.html` + `GET /api/v1/competicion/cuadros/{idTorneo}/{genero}/{categoria}/{nivel}/{cuadro}`

URL actual: `/Previo.asp?IdTorneo={id}&Genero={M|F|X}&Cat={categoria}&Nivel={n}&Cuadro={cuadro}` (parejas) y
`/PrevioEquipos.asp?…` (equipos). Se llega desde las fichas de campeonato (`Camp_Parejas.asp`, `Camp_Equipos.asp`,
`Camp_Silla.asp`; hoy desde la API de `campeonatos-equipos` y `campeonatos-silla`, que devuelven la URL de `Previo*.asp`)
y desde enlaces externos. `Previo*.asp` **no pinta nada**: cuenta las filas del cuadro y redirige (302) a una de ~40
páginas que leen las mismas tablas y solo cambian el tamaño de la rejilla. Por eso se migran **todas a un único
recurso** y a una única página (`cuadro.html`, que escribe en paralelo el equipo de front; este análisis cubre la API).

Páginas cubiertas (`pages_to_dev/Cuadros_Torneos/`, Windows-1252):

| Página | Qué es | Destino |
|---|---|---|
| `Previo.asp`, `PrevioEquipos.asp` | Reparto por número de filas (302) | Regla `ReglasCuadros.Formato` → `formato` + `plantilla` |
| `Cuadro4/8/16/32/64/128.asp` | Eliminatoria de parejas de 4…128 | `GET /api/v1/competicion/cuadros/{…}` |
| `CuadroPrevia8.asp`, `CuadroPrevia16-4.asp`, `CuadroPrevia32-4.asp`, `CuadroPrevia32-8.asp`, `CuadroPrevia64-8.asp` | Previa (nivel 21): cuadro que no llega a final, de N a M clasificados | Ídem (`formato: eliminatoria`, sin campeón) |
| `CuadroPrePrevia16/32/128.asp` | Pre-previa (nivel 31) | Ídem |
| `CuadroEquipos4/8/16/32.asp`, `CuadroEquipos82.asp` | Eliminatoria de equipos (`CuadroEquipos8` pintaba además "3y4", "Descenso" y "DescensoC") | Ídem (cada cuadro por separado, `otrosCuadros` para cambiar) |
| `Liguilla4.asp`, `Liguilla3Equipos.asp`, `Liguilla4Equipos.asp` | Liguillas (grupos) de parejas y equipos con clasificación | Ídem (`formato: liguilla`, `clasificacion`) |
| `EquiposEnfrentamientos.asp` | Partidos de un enfrentamiento entre equipos | `GET …/{cuadro}/enfrentamientos/{avance}/{posicion}` |
| `*Excell.asp` (19 ficheros) | Exportación del mismo cuadro a Excel (`Content-Type` de Excel sobre HTML) | **No migrado (exportación Excel)** |
| `Previook.asp` | Versión antigua de `Previo.asp` (redirigía a `CuadroPrevia8/16/32.asp`, `CuadroPrePrevia*.asp` y `PPTSinSortear.asp`) | No migrado: nada la enlaza; sus plantillas se cubren con el recurso único |
| `PPTSinSortear.asp` | "Cuadro final aún sin sortear": lista de las 24 (masc.) / 12 (fem.) primeras parejas por `NumPareja` con puntos | **Analizado, no implementado** (ver abajo) |
| `Fichas_Transferencias.asp` | Página de **gestión** con cookie `ControlAccesoSFEP`: transferencias de licencia pendientes; escribe en `SFEP_HistorialCambios` y `SFEP_Licencia` | **Analizado, no implementado**: no es un cuadro; pertenece al back-office (fase 5) |

Endpoints:

- `GET /api/v1/competicion/cuadros/{idTorneo}` → `CuadrosTorneoDto` (cuadros del torneo con su publicación; para enlazar
  desde listados sin conocer género, categoría, nivel ni cuadro).
- `GET /api/v1/competicion/cuadros/{idTorneo}/{genero}/{categoria}/{nivel}/{cuadro}` → `CuadroTorneoDto`.
- `GET /api/v1/competicion/cuadros/{idTorneo}/{genero}/{categoria}/{nivel}/{cuadro}/enfrentamientos/{avance}/{posicion}`
  → `EnfrentamientoEquiposDto`.

Los parámetros van en la ruta (el modo prototipo del front descarta la query). Includes: `conexionSQL.inc` y
`finconexion.inc` (no se copian; `PPTSinSortear.asp` y `Fichas_Transferencias.asp` usan `/Paginas/Nueva/include/`).
Las páginas de cuadro no usan `Menus.inc` ni `PiePagina.inc`: tienen cabecera propia con el logo y la imagen fija
`images/BarraPatrocinadores.jpg` (`BarraPatrocinadores2.jpg` escrito a mano para el torneo 8456).

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Reparto | `Previo.asp`: `sql_tipo` (`TipoTorneo`, `LiguillaVueltas`) + cuenta de `SFEP_Torneos_Cuadros` (`IdPrueba`, `Genero`, `Categoria`, `Nivel`, `Cuadro`) → `select case i` → `Response.Redirect`. `PrevioEquipos.asp`: igual con otra tabla y filtros incompletos | `formato` (`eliminatoria`/`liguilla`) y `plantilla` (nombre de la página del ASP, informativo) |
| Cabecera | `SFEP_Torneos.*` ⨝ `SFEP_Torneos_Desglose.TextoCuadros` + textos fijos por `Nivel` ("Primera"… "Pre-Previa") y `Cuadro` ("Cuadro Final", "Consolación") | `torneo` (`id`, `titulo`, `tipo`, fechas, `lugar`) + `genero`, `categoria`, `nivel`, `cuadro`; los textos los compone el front |
| Observaciones | `TextoCuadros` (HTML) | `notas` (HTML sin sanear: lo sanea el front) |
| Publicación | `sql_ver` (`SFEP_Torneos_Desglose.*`) + `select case Nivel` → `VerPrimera`…`VerDecima`, `VerPrevia` (21), `VerPreprevia` (31); `ver="N"` → "No están disponibles los cuadros de esta categoría" | `publicado` (y listas vacías) |
| Rondas | Una consulta por ronda (`Avance` = 7…0) de `SFEP_Torneos_Cuadros` (`Posicion`, `Pareja`, `CDS`, `Letra`, `Lucky`, `Exento`) + **una consulta por hueco** a `SFEP_Torneos_Jugadores` (o `SFEP_Torneos_Equipos`) | `participantes` (una lectura de los huecos y una de los nombres) |
| Huecos sin pareja | `Pareja=0`: `Exento='E'` → "EXENTO"; `Letra` → "PREVIA - X" ("PRE-PREVIA - X" en las previas); `Lucky` → "LUCKY LOOSER" (en `Cuadro32` numerado) | `tipo` `exento` / `previa` (+ `letra`) / `lucky`; los vacíos no se devuelven |
| C.S. en la rejilla | `CDS` en la primera ronda; "LS" si la pareja entró como *lucky loser* (`Cuadro32`) | `cabezaSerie` (solo primera ronda) + `luckyLoser` |
| Resultados | `SFEP_Torneos_Resultados` (`Avance`, `Posicion`, `resultado`) en arrays `Result1…Result7` | `partidos[].resultado` |
| Horarios | `SFEP_Torneos_Horarios` (`Dia`, `Lugar`, `Obs`, `VS`) → "Lugar-Viernes 26 Junio - 10:00" (o `Obs` en lugar de la hora) + icono `VS.jpg`; solo si no hay resultado | `partidos[].horario` (`dia`, `lugar`, `obs`, `vs`); el texto lo compone el front |
| Enfrentamientos (equipos) | `sql_parejas` (`SFEP_Torneos_ResultadosEquipos` agrupado) → icono `partidos.jpg` enlazado a `EquiposEnfrentamientos.asp` | `partidos[].enfrentamiento` + endpoint de enfrentamiento |
| Cabezas de serie | `sql_cs`: `CDS` + `SUM(Puntos)` (+ `Wildcard`) de la primera ronda y `sql_quien` (una consulta por cabeza de serie) → tabla C.S. / Pareja / Puntos ("Wildcard" en lugar de los puntos) | `cabezasSerie` (`cabezaSerie`, `nombre` "A/B", `puntos`, `wildcard`) |
| Clasificación de liguilla | `SFEP_Torneos_Liguilla.*` (`Posicion`, `Pareja`, `Puntos`, `PG`, `PP`, `SetG`, `SetP`, `JG`, `JP`; `EG`/`EP` en equipos) + diferencias calculadas | `clasificacion` (las diferencias las calcula el front) |
| Participantes de liguilla | `sql_cs` → tabla "C.S." con `NumPareja` y puntos | `cabezasSerie` con `cabezaSerie` = número de pareja/equipo |
| Enlace "Descargar Cuadros" | `Cuadro*Excell.asp` (Excel) | No migrado |
| Detalle de enfrentamiento | `EquiposEnfrentamientos.asp`: `SFEP_Torneos_ResultadosEquipos` + 2 consultas de equipo y 4 de `SFEP_Fichas.NombreCompleto` **por partido**; filas hasta `SFEP_Torneos_Desglose.numpartidos` | `EnfrentamientoEquiposDto` (una consulta con `OUTER APPLY`) |
| Selector de cuadros | No existía (había que volver a la ficha) | `otrosCuadros` + `GET /cuadros/{idTorneo}` |

Ninguna página **escribe** en la BD salvo `Fichas_Transferencias.asp` (gestión, no migrada).

## Parámetros (`Request`) y validación

| ASP | API nueva | Validación |
|---|---|---|
| `IdTorneo` / `idtorneo` | `idTorneo` (ruta) | Entero ≥ 1 (no numérico → 400 del *model binding*) |
| `Genero` (`left(…,1)` en unas consultas, completo en otras) | `genero` | `M`, `F` o `X`, sin distinguir mayúsculas |
| `Cat` / `cat` | `categoria` | 1–30 caracteres: letras, cifras, espacio, `.`, `-`, `+`, `_`; empieza por letra o cifra |
| `Nivel` / `nivel` | `nivel` | 1–99 |
| `Cuadro` / `cuadro` | `cuadro` (codificado: `Grupo%20A`) | Como `categoria` |
| `Avance`, `Posicion`, `tipocuadro` (enfrentamientos) | `avance` (0–64), `posicion` (1–256); `tipocuadro` sobra (el front vuelve al cuadro) | Enteros en rango |

Respuestas: `200`; `400` `ValidationProblemDetails`; `404` `ProblemDetails` con `urlWebActual` = listado de
Campeonatos si el torneo no existe, está borrado o `Visible='N'`; `404` con `urlWebActual` = `Previo.asp?…` /
`PrevioEquipos.asp?…` si el cuadro no tiene ninguna fila en `SFEP_Torneos_Cuadros`, o (enfrentamientos) si el torneo no es
por equipos, el cuadro no está publicado o no hay detalle. `200` con `publicado: false` y listas vacías si la columna
`Ver…` del nivel vale `'N'`.

## Lógica / visibilidad heredada

- **Formato y plantilla** (`ReglasCuadros.Formato`), copia de `Previo.asp`: 3 filas → `Cuadro2`; 6 → `Liguilla3`;
  7 → `Cuadro4`; 14 → `CuadroPrevia8-2`; 15 → `Cuadro8`; 20 y 40 → `Liguilla5`; 28 → `CuadroPrevia16-4`; 31 → `Cuadro16`;
  56/60/62 → `CuadroPrevia32-8/-4/-2`; 63 → `Cuadro32`; 112/120/124 → `CuadroPrevia64-16/-8/-4`; 127 → `Cuadro64`;
  240/248 → `CuadroPrevia128-16/-8`; 255 → `Cuadro128`. Con 12, 24 o 30 filas decide el avance más bajo distinto de 0:
  si es 1 es una liguilla (`Liguilla4`, o `Liguilla3` a doble vuelta si `LiguillaVueltas = 2`; `Liguilla4`; `Liguilla6`),
  si no, una previa (`CuadroPrevia8-4`, `16-8`, `16-2`). Equipos (`PrevioEquipos.asp`): 6 → `Liguilla3Equipos`,
  12 → `Liguilla4Equipos`, 7/15/31/63/127 → `CuadroEquipos4/8/16/32/64`.
  Con un número de filas que el ASP no contemplaba, redirigía a `.asp?…` (404) o a `""` (error): ahora `plantilla: null`
  y el formato se deduce de la forma (liguilla si no hay avance 0 y cada avance tiene como mucho 2 huecos).
- **Rondas**: `Avance` 0 = campeón, 1 = final, 2 = semifinales…; la primera ronda es el mayor avance (5 en un cuadro de
  32). El partido `p` de la ronda `a` lo juegan los huecos `2p−1` y `2p` de esa ronda, y su resultado y horario están en
  `SFEP_Torneos_Resultados`/`SFEP_Torneos_Horarios` con `Avance = a`, `Posicion = p`. En las liguillas, `Avance` es el
  número de partido y `Posicion` 1/2 los dos rivales (resultado y horario con `Posicion = 1`).
- **Publicación**: solo `'N'` en la columna del nivel oculta el cuadro (vacía o `NULL` se ve; un nivel sin columna, p. ej.
  11, también). Las páginas de liguilla no lo comprobaban igual: `Liguilla4.asp` y `Liguilla3Equipos.asp` nunca ocultaban
  y `Liguilla4Equipos.asp` mostraba si alguna de `VerLiguilla`, `VerGrupoA`…`VerGrupoE` era `'S'`. Ahora se aplica la regla
  del nivel a todos los cuadros, también en la lista de cuadros del torneo (ver pendientes).
- La consolación usa la columna del nivel (`VerPrimera`), no `VerPrimeraC`, como las páginas de cuadro (la ficha
  `Camp_Equipos.asp` sí miraba `VerPrimeraC` para pintar el enlace). Se mantiene.
- **Sin fila de desglose** para el género y la categoría el ASP daba error 500 (`rs_ver` sin comprobar EOF): ahora
  `publicado: false`.
- **Torneo**: las páginas de cuadro no miraban `Visible` ni `Borrado`; ahora un torneo con `Borrado='S'` o `Visible='N'`
  responde 404 (mismo criterio que la ficha de campeonato; ver pendientes).
- **Horarios con `Visible = 'N'`**: el ASP leía la columna pero **no la usaba** (se pintaban todos). Ahora `horario: null`
  (ver pendientes).
- **Cabezas de serie**: en las eliminatorias, `CDS` de la primera ronda agrupado por número, con la suma de `Puntos` de los
  jugadores (equipos: `Puntos` del equipo); `wildcard` si algún jugador tiene `Wildcard = 'S'` (`CuadroPrevia32-8` pintaba
  "Wildcard" en lugar de los puntos). En las liguillas, el ASP listaba bajo "C.S." el `NumPareja` de cada pareja: se
  conserva (`cabezaSerie` = número de pareja o equipo).
- **Huecos sin pareja**: el ASP evaluaba `Exento`, `Letra` y `Lucky` en ese orden y ganaba el último. `Letra` →
  "PREVIA - X" salvo en los cuadros de previa (nivel 21), donde la letra viene de la pre-previa ("PRE-PREVIA - X";
  `CuadroPrevia64-8.asp` decía "PREVIA" también ahí: se unifica).
- **Tercer set a 0-0** (enfrentamientos): el ASP lo dejaba en blanco; se omite.
- **`ParticipaInf`** (`CuadroPrevia16-4.asp`) se leía y no se usaba. `Bloqueado` (`Liguilla3Equipos`, `Liguilla4Equipos`)
  tampoco. No se devuelven.
- **`numpartidos`** (`EquiposEnfrentamientos.asp`): fijaba cuántas filas se pintaban (vacías si faltaban datos). Ahora se
  devuelven los partidos que existen.

## Problemas del original

1. **Inyección SQL**: `IdTorneo`, `Genero`, `Cat`, `Nivel`, `Cuadro`, `Avance` y `Posicion` se concatenaban **sin limpiar**
   en todas las consultas de las ~45 páginas (`'...Categoria='" & request("Cat") & "'"`). Ahora todo va como parámetro
   (`@Id`, `@Genero`, `@Categoria`, `@Nivel`, `@Cuadro`, `@Avance`, `@Posicion`) y además se valida el formato en la ruta.
   Las columnas `Ver…` salen de constantes.
2. **N+1**: una consulta por ronda y otra por hueco (un cuadro de 128 lanzaba ~400 consultas; `sql_quien` repetía una por
   cabeza de serie; `EquiposEnfrentamientos.asp`, 6 por partido). Ahora: 1 consulta del torneo + 1 lote (+ 1 para la
   clasificación de las liguillas).
3. **Error 500** con un torneo inexistente (comprobado: `Previo.asp?IdTorneo=99999999…` → 500 y
   `Cuadro32.asp?idtorneo=99999999…` → 500) y con un número de filas no previsto (redirección a `.asp?…`). Ahora 404.
4. **Filtros que faltaban o fijos** (mezclaban cuadros):
   - `Cuadro4.asp` leía siempre `Nivel=1` y los resultados/horarios de `Cuadro='Final'` aunque se pidiera otro nivel o la consolación.
   - `CuadroPrePrevia16.asp` leía los resultados **solo por género** (de todo el torneo) y los jugadores sin categoría.
   - `sql_cs` no filtraba por `Cuadro` (mezclaba las cabezas de serie del final y de la consolación) y `sql_quien` fijaba `Nivel=1`.
   - `PrevioEquipos.asp` contaba las filas sin `Categoria` (y la consolación y los grupos sin `Nivel`).
   - `Liguilla4.asp` fijaba `Nivel=1` en huecos, resultados y horarios.
   - `EquiposEnfrentamientos.asp` buscaba los equipos sin género y pintaba los de la **última** fila para todos los partidos.
   Ahora todas las consultas filtran por torneo, género, categoría, nivel y cuadro.
5. **Nombre heredado**: la variable `pareja` no se reiniciaba; una pareja sin jugadores (o sin `OrdenPareja = 1`)
   mostraba el nombre de la anterior. Ahora cada pareja tiene sus jugadores (vacío si no hay).
6. **`Liguilla3Equipos.asp`**: la lista "C.S." de un torneo por equipos salía de `SFEP_Torneos_Jugadores` (parejas con el
   mismo número que el equipo). Ahora, de `SFEP_Torneos_Equipos`.
7. **Lucky**: `Cuadro4`, `CuadroPrevia16-4` y otras comprobaban `Lucky <> ""` (una `'N'` guardada salía como "LUCKY
   LOOSER"); `Cuadro32` comprobaba `= 'S'`. Ahora `'S'`.
8. **Erratas y XSS**: "LUCKY LOOSER", "Desxcargar Cuadros", "NIvel", "Miercoles" sin tilde; `Torneo`, nombres, resultados,
   `Lugar` y `Obs` se pintaban sin codificar. La API devuelve datos; el front usa `textContent` y sanea `notas`.
9. `EquiposEnfrentamientos.asp`: la observación del equipo 1 del partido 4 nunca salía (`P43Jug1Obs`, errata de variable)
   y la cabecera decía siempre "Cuadro: ABSOLUTO" y "Femenino" para cualquier género que no fuera `M`.
10. HTML de 2010: tablas de maquetación de 960 px, `<meta charset>` tras los estilos, Cufon, aviso de IE6, imagen de
    patrocinadores fija sin `alt`.

## Mejoras respecto al original

- Un único contrato para los ~40 formatos: el front dibuja la rejilla con `avance`/`posicion` (sin una página por tamaño).
- Selector de cuadros del torneo (`otrosCuadros` y `GET /cuadros/{idTorneo}`) con su estado de publicación.
- Horario estructurado (fecha ISO, pista, observación, `vs`) en lugar de un texto ya formateado.
- Enfrentamientos en una sola consulta; sets como números.
- Respuestas cacheadas 5 min (output cache + `Cache-Control: public`).

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "LUCKY LOOSER" (en `Cuadro32`, "LUCKY LOOSER 1", "2"…) | "LUCKY LOSER" sin número | Errata; la numeración solo existía en `Cuadro32` |
| `CuadroPrevia64-8.asp`: "PREVIA - X" en un cuadro de previa | "PRE-PREVIA - X" como el resto de previas | Uniformidad (la letra viene de la pre-previa) |
| Nombres de cabezas de serie "A/B" y de la rejilla "A - B" | Igual que el ASP | Se respeta el formato actual |
| Ciudad del torneo (no se pintaba en los cuadros) | `lugar` en formato "Nombre Propio" si está todo en mayúsculas o minúsculas | Regla ya decidida por la FEP para las sedes (`Texto.NombrePropio`) |

## Comprobación de enlaces (16/11/2026)

- `Previo.asp?IdTorneo=28714&Genero=M&Cat=Cat50&Nivel=1&Cuadro=Final` → 302 → `Cuadro32.asp?…` (200).
- `PrevioEquipos.asp?IdTorneo=28676&Genero=M&Cat=ABSOLUTO&Nivel=1&Cuadro=Final` → 302 → `CuadroEquipos16.asp?…` (200).
- `Previo.asp?IdTorneo=99999999…` → **500**; `Cuadro32.asp?idtorneo=99999999…` → **500**.

## `PPTSinSortear.asp` (analizado, no implementado)

Página a la que redirigía `Previook.asp` cuando el cuadro final de un torneo `PPT` aún no estaba sorteado: "Cuadro final
aún sin sortear. El sorteo se realizará el lunes del torneo." y una tabla Orden / Pareja / Puntos con las parejas
`NumPareja <= 24` (masculino) o `<= 12` (femenino) de `SFEP_Torneos_Jugadores` (`SUM(Puntos)` por pareja, sin categoría).
`Previo.asp` ya no redirige a ella (nada la enlaza en la web actual) y los límites 24/12 y el texto del "lunes" están
escritos a mano. Si se quisiera recuperar, sería un `estado: "sin-sortear"` del mismo recurso con la lista de inscritos
por puntos.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: la publicación de todas las liguillas y grupos sigue la columna `Ver…` del nivel (antes
  `Liguilla4.asp` y `Liguilla3Equipos.asp` no ocultaban nunca y `Liguilla4Equipos.asp` miraba `VerLiguilla`/`VerGrupoA–E`).
  ¿Deberían usar sus columnas propias (`VerLiguilla`, `VerGrupoX`, `VerPrimeraC`…)?
- **DECISIÓN FEP**: un torneo con `Borrado='S'` o `Visible='N'` ya no muestra sus cuadros (404); antes se veían si se
  conocía la URL. Mismo criterio pendiente en P0104.
- **DECISIÓN FEP**: los horarios con `Visible='N'` ya no se muestran (el ASP los pintaba todos).
- **DECISIÓN FEP**: las exportaciones `*Excell.asp` no se migran. ¿Hace falta una descarga (CSV/XLSX) del cuadro?
- **DECISIÓN FEP**: `PPTSinSortear.asp` (lista previa al sorteo) no se migra; nada la enlaza hoy.
- **SUPUESTO**: tipos de columnas: `Avance`, `Posicion`, `Nivel`, `Pareja`, `NumPareja`, `NumEquipo`, `OrdenPareja`,
  `Partido` enteros o convertibles; `CDS` texto numérico; `Letra`, `Lucky`, `Exento`, `VS`, `Visible`, `Wildcard`, `Ver…`
  `char`/`varchar`; `Genero`, `Categoria`, `Cuadro` `char`/`varchar` (se pasan como parámetros no Unicode); `Dia`
  `datetime`; `Puntos` numérico; `SFEP_Torneos_Liguilla.Pareja` texto con el nombre y columnas `PG`…`JP`, `EG`, `EP`
  numéricas.
- **SUPUESTO**: `SFEP_Torneos_Liguilla` no distingue nivel (el ASP no lo filtraba) y su columna de categoría se llama
  `[Categoría]` con tilde, como en el ASP.
- **SUPUESTO**: `SFEP_Torneos_Horarios.Dia` en hora local de España (mismo supuesto que P0112).
- **SUPUESTO**: una fila de `SFEP_Torneos_Desglose` por género y categoría (mismo supuesto que P0110); si hay varias se
  usa la primera.
- **SUPUESTO**: los nombres de equipo se buscan por género (`SFEP_Torneos_Equipos.Genero`) y los de los jugadores por
  género y categoría, también en las pre-previas (`CuadroPrePrevia16.asp` no filtraba la categoría).
- Activar las 301 `/Previo.asp?…` y `/PrevioEquipos.asp?…` (y las páginas de cuadro, que reciben enlaces directos
  `Cuadro32.asp?idtorneo=…`) a `cuadro.html`; no hay filas en `redirecciones.csv`.
- `Fichas_Transferencias.asp` es una página de gestión: pasa al back-office (fase 5), no a la web pública.
- Las fichas de campeonato de la API (`campeonatos-equipos`, `campeonatos-silla`) devuelven aún la URL de
  `PrevioEquipos.asp`/`Previo.asp`: cambiarla por la del cuadro nuevo cuando `cuadro.html` esté publicado.

## Modelo SQL propuesto

No hay contenido escrito a mano que trasladar (salvo la imagen de patrocinadores del 8456, que ya no aplica: la columna de
patrocinadores la pinta `comun.js`). Índices recomendados para las consultas nuevas (**SUPUESTO**: no se ha podido ver el
esquema real):

```sql
CREATE INDEX IX_SFEP_Torneos_Cuadros_Cuadro ON SFEP_Torneos_Cuadros (IdTorneo, IdFederacion, Genero, Categoria, Nivel, Cuadro)
    INCLUDE (Avance, Posicion, Pareja, CDS, Letra, Lucky, Exento);
CREATE INDEX IX_SFEP_Torneos_Jugadores_Pareja ON SFEP_Torneos_Jugadores (Torneo, IdFederacion, Genero, Categoria, NumPareja)
    INCLUDE (OrdenPareja, Jugador, Puntos, Wildcard);
CREATE INDEX IX_SFEP_Torneos_Resultados_Cuadro ON SFEP_Torneos_Resultados (IdPrueba, Genero, Categoria, Nivel, Cuadro);
CREATE INDEX IX_SFEP_Torneos_Horarios_Cuadro ON SFEP_Torneos_Horarios (IdPrueba, Genero, Categoria, Nivel, Cuadro);
CREATE INDEX IX_SFEP_Torneos_ResultadosEquipos_Cuadro ON SFEP_Torneos_ResultadosEquipos (IdPrueba, Genero, Categoria, Nivel, Cuadro, Avance, Posicion);
```

## Ficheros

- API: `Contracts/Competicion/CuadroTorneoDto.cs` (DTO y sub-DTO, `ClaveCuadro`, `ConsultaCuadros<T>`),
  `Contracts/Competicion/ReglasCuadros.cs` (validación, publicación por nivel, formato/plantilla de `Previo*.asp`, huecos
  sin pareja, orden), `Data/Repositories/Competicion/CuadrosRepository.cs` (Dapper + composición en memoria),
  `Controllers/Competicion/CuadrosController.cs`; registro en `Program.cs`; tests en `tests/FEP.Api.Tests/CuadrosTests.cs`
  (validación 400, 404 con `urlWebActual`, ruta decodificada con repositorio falso, reglas heredadas y composición de
  eliminatoria, liguilla de equipos, enfrentamiento y lista de cuadros; no necesitan SQL Server).
- Web (en paralelo, fuera de este trabajo): `wwwroot/cuadro.html`, `assets/js/cuadro.js` y los mocks
  `mock/competicion-cuadros-*.json`.
