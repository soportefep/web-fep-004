# Análisis: `Ranking_Silla_Ruedas.asp` → `ranking-silla-ruedas.html` + `GET /api/v1/competicion/rankings/silla-ruedas`

URL actual: `/Ranking_Silla_Ruedas` (enlace "Ranking Silla Ruedas" de `SFEP_Rankings_Web`, desde `rankings.html`; responde
igual con y sin `.asp`). Ruta nueva: `/ranking-silla-ruedas` → `ranking-silla-ruedas.html` (no estaba en
`redirecciones.csv`; se ha añadido con la convención del proyecto y el nombre que ya preveían `rankings.js` y `Rankings.md`).

A diferencia de Ranking_Absoluto y Ranking_Menores, la página **no** cargaba la tabla por AJAX: la consulta va en el
propio `.asp` (`SELECT SFEP_RankingFinal.* … WHERE IdFederacion=0 AND TipoRanking=15 ORDER BY Posicion`, sin `TOP` ni
JOIN). Se reutiliza **el mismo diseño** (`ReglasClasificaciones`, `ClasificacionesRankingRepository`,
`ClasificacionesRankingController`, `ClasificacionRankingDto`, `ranking.js`, estilos `.rkc-*`) con tres extensiones
genéricas, compatibles con lo ya migrado:

1. `LicenciasRanking.Silla` en la definición del ranking: `NumLicencia` es la **licencia de silla**
   (`SFEP_Fichas.LicenciaSilla`, otra numeración: 1001–1166), así que no se cruza con `SFEP_Fichas.LicenciaJugador`
   (daría otra persona); el nombre sale de `SFEP_RankingFinal`, como en el ASP. La búsqueda por nombre usa esas columnas.
2. `ClasificacionRankingDto.prefijoLicencia` (`PS`; `null` en el resto) y enlace de estadísticas
   `Silla-Ruedas-Est?id=PS{licencia}`.
3. `TipoClasificacion.Titulo` opcional (la lista única se llama "General" en lugar de "Mixto").

Includes: `conexionSQL.inc` (conexión; no se copia nada), `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc`,
`finconexion.inc` (comunes, ya migrados en `comun.js`). El `.asp` de `pages_to_dev/` es idéntico al de `X:\TICS\WEB\`.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie, barra lateral | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` + `/api/v1/menu`, `/redes-sociales`, `/patrocinadores` (columna derecha) |
| `sql_noticias` (`SFEP_Noticias` destacadas) | Se consultaba **pero no se usaba** | Eliminado |
| Título "Ranking silla ruedas" (`h5`) | HTML escrito a mano | `h1` "Ranking de Pádel en Silla de Ruedas." + entradilla + cifras de `SFEP_Ranking_Tipos` si las hay |
| Nota "*Average: los 10 mejores torneos" | HTML escrito a mano | Entradilla ("cuentan los 10 mejores torneos") y leyenda de "Torneos y puntos ranking" |
| Tabla Puesto · Licencia · Jugador · Total Torneos · Total Puntos · Torneos Average · Puntos Average | `sql_rnk`: `SELECT SFEP_RankingFinal.* FROM SFEP_RankingFinal WHERE IdFederacion=0 AND TipoRanking=15 ORDER BY Posicion` (**sin TOP**: 92 filas hoy). Nombre = `Nombre & " " & Apellido1 & " " & Apellido2` **de la propia `SFEP_RankingFinal`**; licencia pintada como `"PS" & NumLicencia` | `GET /api/v1/competicion/rankings/silla-ruedas?licencia=&busqueda=&limite=` → `ClasificacionRankingDto` con una `ListaRankingDto` (`tipoRanking` 15, `genero` `X`, `titulo` "General"). Columnas explícitas (`torneosTotales`, `puntosTotales`, `torneosRanking`, `puntosRanking`). Licencia "PS1008" = `prefijoLicencia` + `licencia` |
| Enlace de cada celda (puesto, licencia, nombre) | `Silla-Ruedas-Est?id=PS{licencia}` | `PuestoRankingDto.estadisticasUrl` = `https://www.padelfederacion.es/Silla-Ruedas-Est?id=PS{licencia}` (web actual hasta que se migre); el nombre es el enlace |
| Variables `sube`, `ico` (`Sube.png`/`Baja.png`/`igual.png`) | Calculadas en cada fila **pero nunca pintadas** (no hay columna de evolución) | `evolucion`/`jornadasPuesto` en la API; `ranking.js` solo pinta la columna si algún puesto la trae (DECISIÓN FEP) |
| Nota "*Evolución: Numero de posiciones que sube o baja…" | HTML escrito a mano (bajo una tabla sin esa columna) | Leyenda bajo la tabla ("Número…") |
| Funciones JS `busqueda`, `MesAnterior`, `CargarEvento`, `MeGusta`, `rnkabs*`, `rnkmen*` | `<head>` copiado de otras páginas | No se usaban: eliminadas |

No hay normativa de cálculo: `/api/v1/competicion/rankings/silla-ruedas/normativa` responde 404 con `urlWebActual` y
la página no tiene sección `#calculo`. Tampoco había búsqueda, selector de género ni datos de `SFEP_Ranking_Tipos`.

## Lógica / visibilidad heredada

- **Evolución calculada y no mostrada**: el bucle calculaba `sube = Sube & " (" & JornadasPuesto & ")"` y el icono, pero
  la tabla no tiene esa columna; sí se pintaba la nota "*Evolución…". Se desconoce si `Sube`/`JornadasPuesto` están al
  día para el tipo 15 (no se ven en ningún sitio). La API los devuelve como en los demás rankings y el front muestra la
  columna **solo si algún puesto la trae** (cambio genérico de `ranking.js`, que no afecta al absoluto ni a menores).
  En el mock van a `null` (no hay dato público): no se ve la columna.
- **Sin `SFEP_Ranking_Tipos`**: el ASP no pintaba jugadores, pruebas, fecha ni PDF. La fila `TipoRanking=15` existe
  (`Silla-Ruedas-Est.asp` usa su `FechaAct`). La API la lee como en los demás rankings y el front la muestra si viene.
  En el mock van a `null` (no hay dato público).
- **Una sola lista mixta**: no hay género ni categoría; en la clasificación hay jugadores y jugadoras (p. ej. PS1064).
  Se declara con género `X` (convención de `ReglasClasificaciones`); `genero=M|F` y `categoria` dan 400.
- **Empates**: el ASP ordenaba solo por `Posicion` (orden de los empatados indeterminado: 28.º, 36.º, 40.º, 42.º, 49.º…).
  La API desempata por `Puntos DESC` y licencia, estable.
- `rs_rnk` se recorría sin `TOP`: con 92 filas no es un problema; la API sirve 100 por defecto (todas) y hasta 500.
- `</td` sin problemas aquí; la tabla no tenía `caption` ni `th scope` y los enlaces repetían la misma URL tres veces por
  fila. Irrelevante en la web nueva.

## Seguridad

- La página no recibía parámetros (sin inyección SQL en el ASP). La API añade búsqueda: todo parametrizado (`@Licencia`
  int, `@T0…` con comodines escapados, `@Tipos`), SQL compuesto solo con fragmentos fijos.
- **XSS**: los nombres se escribían sin codificar. En la web nueva todo va por `textContent`/atributos y `urlSegura`.
- `Silla-Ruedas-Est.asp` (destino de los enlaces, sin migrar) borra e inserta en `SFEP_estadisticasWeb` en cada visita
  (`DELETE … WHERE Licencia=" & id`, concatenado). A tener en cuenta cuando se migre.

## Mejoras respecto al original

- Una consulta parametrizada con columnas explícitas (antes `SELECT *` + consulta de noticias sin uso).
- **Búsqueda por nombre o licencia** (no existía), con o sin el prefijo "PS" (`PS1008`, `ps1008` o `1008`), sin
  distinguir mayúsculas ni acentos; criterios en la URL (`?licencia=1008`, `?busqueda=`).
- Datos de la lista (jugadores, pruebas, actualización y PDF) si `SFEP_Ranking_Tipos` los tiene.
- Tabla accesible (`caption`, `th scope`), nombres en tipo título solo en el front, podio destacado; en móvil una ficha
  por puesto, sin scroll horizontal.
- `ranking.js` admite ahora rankings **sin selector de género** y con **prefijo de licencia**, y oculta la columna de
  evolución si no hay datos: sirve tal cual para otros rankings de lista única.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "Ranking silla ruedas" | "Ranking de Pádel en Silla de Ruedas." | Falta "de"; estilo de los demás títulos. El índice (`SFEP_Rankings_Web.Texto`) sigue diciendo "Ranking Silla Ruedas" (DECISIÓN FEP ya anotada en `Rankings.md`) |
| "Torneos Average" / "Puntos Average" + "*Average: los 10 mejores torneos" | "Torneos ranking" / "Puntos ranking" + leyenda "Los 10 mejores torneos (average)…" | Mismas columnas (`Torneos`, `Puntos`) y mismos encabezados que el resto de rankings; el significado se conserva en la leyenda |
| "*Evolución: Numero de posiciones…" | "Número de posiciones…" | Tilde |
| "FRANCISCO JOSÃ‰ ORTEGA ARRIBAS" (PS1063) | "FRANCISCO JOSÉ ORTEGA ARRIBAS" **solo en el mock** | Codificación rota en la BD (UTF-8 guardado como Windows-1252). La API devuelve lo que haya en `SFEP_RankingFinal`: hay que corregirlo en la BD (DECISIÓN FEP) |
| "JAVIER  NAVARRO  FERNANDEZ  " | "Javier Navarro Fernandez" | Espacios de relleno (`NombreJugador`), como en el resto de rankings |
| `<title>Ranking Silla Ruedas \| FEP</title>` | "Ranking de Pádel en Silla de Ruedas · Federación Española de Pádel" | Coherencia con el `h1`; `meta description` propia |

No se han cambiado cifras ni puestos.

## Comprobación de enlaces (13/11/2026)

- `https://www.padelfederacion.es/Ranking_Silla_Ruedas` y `/Ranking_Silla_Ruedas.asp`: 200 (92 puestos; instantánea
  usada para el mock).
- `Silla-Ruedas-Est?id=PS1008`, `Silla-Ruedas-Est.asp?id=PS1008`, `Silla-Ruedas-Est?id=PS1064`: 200 y muestran al
  jugador o jugadora.
- `refs/images/Sube.png`, `igual.png` (iconos calculados y no usados): 200; no se copian (el front usa ▲/▼/=).
- No hay PDF ni imágenes propias de la página.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: evolución (`Sube`, `JornadasPuesto`) del tipo 15: el ASP la calculaba pero no la mostraba, aunque
  sí la nota explicativa. Ahora se muestra si la BD la trae. ¿Está al día? ¿Se publica?
- **DECISIÓN FEP**: datos de `SFEP_Ranking_Tipos` (jugadores, pruebas, fecha de actualización y PDF completo) del tipo
  15, que la página no publicaba: se muestran si existen. Revisar que `ArchivoPDF` (si lo hay) está al día.
- **DECISIÓN FEP**: corregir en la BD el nombre de PS1063 ("JOSÃ‰" → "JOSÉ") y revisar si hay más textos con la
  codificación rota en `SFEP_RankingFinal`/`SFEP_Fichas` de silla.
- **DECISIÓN FEP**: se añade búsqueda por nombre y licencia (no existía en esta página).
- **DECISIÓN FEP**: la tabla publica el número de licencia, como el original.
- **DECISIÓN FEP**: encabezados "Torneos/Puntos ranking" en lugar de "Average" (el significado se explica en la leyenda).
- **SUPUESTO**: `SFEP_RankingFinal` tiene `Nombre`, `Apellido1`, `Apellido2` (el ASP los leía de `SELECT
  SFEP_RankingFinal.*` y la web pública los muestra), y `NumLicencia` del tipo 15 es numérico (la licencia de silla sin
  "PS": `Silla-Ruedas-Est.asp` la compara con `SFEP_Fichas.LicenciaSilla` sin comillas).
- **SUPUESTO**: el ranking 15 es único y mixto (aparecen jugadores y jugadoras); se declara con género `X`.
- **SUPUESTO**: tipos de columna de `SFEP_RankingFinal`/`SFEP_Ranking_Tipos` como en `Ranking_Absoluto.md`.
- **SUPUESTO**: ruta nueva `/ranking-silla-ruedas` (no había fila en `redirecciones.csv`).

## Modelo SQL propuesto

No aplica: la clasificación ya está en BD y la página no tiene texto informativo propio. Si se declarasen los rankings
en BD en lugar de en `ReglasClasificaciones.Rankings`, ver la propuesta de `Ranking_Absoluto.md`
(`ALTER TABLE SFEP_Ranking_Tipos ADD ClaveWeb varchar(40) NULL, Genero char(1) NULL;`) más, para este caso, una columna
`TipoLicencia char(1) NULL` (`J` jugador, `S` silla) que sustituya a `LicenciasRanking`.

## Ficheros

- API: `Contracts/Competicion/ReglasClasificaciones.cs` (ranking `silla-ruedas` con el `TipoRanking` 15, `LicenciasRanking`,
  `TipoClasificacion.Titulo`, `PrefijoLicencia`, `RutaEstadisticas` con licencias de silla),
  `Contracts/Competicion/ClasificacionRankingDto.cs` (`prefijoLicencia`, opcional y al final: no rompe el contrato),
  `Data/Repositories/Competicion/ClasificacionesRankingRepository.cs` (nombre y búsqueda desde `SFEP_RankingFinal` sin
  JOIN para licencias de silla), comentarios en `Controllers/Competicion/ClasificacionesRankingController.cs` y
  `Program.cs`; tests en `tests/FEP.Api.Tests/ClasificacionesRankingTests.cs` (definición, enlace `Silla-Ruedas-Est`,
  validación, `prefijoLicencia` y 404 de la normativa).
- Web: `wwwroot/ranking-silla-ruedas.html`, `assets/js/ranking.js` (selector de género opcional, prefijo de licencia,
  columna de evolución solo con datos). Sin CSS nuevo (`.rkc-*`) ni imágenes propias.
- Mock: `mock/competicion-rankings-silla-ruedas.json` (instantánea de la web pública del 13/11/2026: los 92 puestos, en el
  orden de la API; sin datos de `SFEP_Ranking_Tipos` ni evolución), `prefijoLicencia: null` añadido a
  `competicion-rankings-absoluto.json` y `competicion-rankings-menores.json` (misma forma que el DTO), `mock/mocks.js`
  regenerado.
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`ranking_silla_ruedas`), enlace "Pádel en silla de ruedas" de
  `index.html` (las tarjetas de `rankings.html` ya usan `PAGINAS_MIGRADAS`).
- Documentación: `docs/api/endpoints.md`, `docs/migracion/redirecciones.csv` (fila nueva),
  `docs/migracion/mapa-migracion.md`, fila de `tools/generar-mapa-web.ps1` (Desarrollado).
