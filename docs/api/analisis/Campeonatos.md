# Análisis: `Campeonatos.asp` → `campeonatos.html` + `GET /api/v1/competicion/campeonatos`

URL actual: `/Campeonatos` (regla `^Campeonatos` de `web.config` → `Campeonatos.asp`; opción de menú
"Campeonatos" bajo Competición). Ruta nueva propuesta en `redirecciones.csv`: `/campeonatos` →
`campeonatos.html`. El formulario hacía `POST` a `Campeonatos` con `ano`, `modalidad` y `tipo`; la página
nueva acepta esos nombres en la query (`?ano=2025&modalidad=EQUIPOS&tipo=MENORES`) además de los nuevos
(`?anio=2025&tipo=equipos&categoria=menores`), y `comun.js` traslada los enlaces antiguos.

Al pulsar un torneo, el ASP enviaba otro formulario `POST` a `Torneos_Previo.asp` (con `idtorneo`), que
consultaba de nuevo `SFEP_Torneos` y redirigía a la ficha. Esa lógica de reparto se ha leído también
(`X:\TICS\WEB\Torneos_Previo.asp`) y se ha llevado a la API para enlazar directamente a la ficha.

Includes: `conexionSQL.inc` (no se copia), `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (ya migrados
en `comun.js`), `finconexion.inc`. Codificación del `.asp`: Windows-1252.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, barra lateral, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (`activo=3`) | Ya migrado: `comun.js` (+ columna de patrocinadores) |
| `sql_noticias` (`SFEP_Noticias`, `SELECT *`) | Se consultaba **pero no se usaba** | Eliminado |
| Título "campeonatos y torneos" + "Filtros del listado de Campeonatos (N)" | HTML a mano + contador recorriendo el recordset | `h1` "Campeonatos y torneos." + contador en `role="status"` y cifras en la cabecera |
| Desplegable "Año del Torneo" | `sql_anotorneos`: `SFEP_Torneos.Año` con `IdFederacion=0`, `Borrado='N'`, `Visible='S'`, `PPT='N'`, `Empresas='N'`, `ProgramaImpulso='N'`, `GROUP BY Año HAVING Año>1900 ORDER BY Año DESC`; por defecto el primero | `anios` (y `anio` = el más reciente si no se pide) |
| Desplegable "Tipo de Torneo" (`modalidad`) | Fijo: `--TODAS--`, `PAREJAS`, `EQUIPOS` → `tipoTorneo='…'` | Parámetro `tipo` (`parejas` \| `equipos`) |
| Desplegable "Categoría de juego" (`tipo`) | Fijo: `--TODOS--`, `ABSOLUTO`, `MENORES`, `VETERANOS` → `ModalidadTorneo='…'` | Parámetro `categoria` (`absoluto` \| `menores` \| `veteranos`) |
| Listado de torneos | `sql_torneos` (ver abajo) `ORDER BY FechaInicio DESC` | `items` |
| Etiquetas de color de cada torneo | `TipoTorneo` (PAREJAS/EQUIPOS) y `ModalidadTorneo` (ABSOLUTO, SEL_ABS, MENORES, SEL_MEN, VETERANOS, SEL_VET); `Circuito='SILLA'` → "SILLA RUEDAS" | `tipo`, `categoria`, `seleccion`, `sillaRuedas` |
| Nombre del torneo (enlace) | `Torneo`; si `LinkCuadros` → `https:\\{LinkCuadros}` en ventana nueva; si no, `javascript:irCamp(IdPrueba)` → `POST Torneos_Previo.asp` | `titulo` + `enlace` (URL absoluta) + `nuevaVentana` |
| Fechas "27 NOV - 29 NOV" | `day()` + mes abreviado de `FechaInicio`/`FechaFin` (dos `select case` de 12 ramas) | `fechaInicio`, `fechaFin` (`yyyy-MM-dd`); el front usa `bloqueFecha` de `comun.js` |
| "Ciudad - Club" | `Ciudad` + `sql_club` (**una consulta por torneo** a `SFEP_Clubes` por `IdClub = Club`) + `Club2` ("X y Y"; "Sin definir" si no hay) | `ciudad` + `clubes` (`OUTER APPLY`, sin N+1) |
| "Fecha Tope de Inscripción / Pre-Inscripción / Inscripción Equipos" | `FechaInscripcion`/`FechaPreInscripcion` comparadas con `now()` | `inscripcion` (`tipo` + `fechaLimite`) solo si el plazo sigue abierto |

`sql_torneos` (columnas realmente pintadas o usadas): `IdPrueba`, `Torneo`, `TipoTorneo`, `ModalidadTorneo`,
`Circuito`, `FechaInicio`, `FechaFin`, `Ciudad`, `FechaInscripcion`, `FechaPreInscripcion`, `LinkCuadros`,
`Club`, `Club2`. No se usaban `PPT`, `TorneoPadre` (lo usa `Torneos_Previo.asp`) ni la segunda `FechaInicio`
repetida en el `SELECT`. Filtros:

```sql
WHERE (IdFederacion = 0 AND Año = '<ano>' AND Borrado = 'N' AND Visible = 'S' AND Empresas = 'N'
       AND ProgramaImpulso = 'N' AND TorneoLigadoFEP <> 19721 [AND tipoTorneo = '<modalidad>'] [AND ModalidadTorneo = '<tipo>'])
   OR (IdPrueba = 19721 AND Año = '<ano>')
ORDER BY FechaInicio DESC
```

`Torneos_Previo.asp` (reparto a la ficha, ahora `ReglasCampeonatos.RutaFicha`):

| Condición | Destino |
|---|---|
| `ModalidadTorneo = 'MENORES'` y `TorneoPadre <> 0` | `Camp_Clasificatorio.asp?IdTorneo=` (el 19721: `http://circuitomenores.padelfederacion.es/CEM-2021`) |
| `MENORES`, sin padre, `EQUIPOS` | `Camp_Equipos.asp?IdTorneo=` |
| `MENORES`, sin padre, parejas | `http://circuitomenores.padelfederacion.es/Campeonato?IdTorneo=` |
| Resto, `EQUIPOS` | `Empr_Equipos.asp` si `Empresas='S'`, si no `Camp_Equipos.asp` |
| Resto, parejas | `Camp_Liga.asp` si `Liga='S'`, si no `Camp_Parejas.asp` (que a su vez redirige a `Camp_Silla.asp` en silla) |

La página **no escribe** en la BD.

## Parámetros (`Request`) y validación

| ASP | API nueva | Validación |
|---|---|---|
| `ano` (`""`/`--` → el año más reciente) | `anio` (opcional; sin él, la temporada más reciente) | entero 1901–2100; si no, **400** `ValidationProblemDetails` |
| `modalidad` (`--` = todas) | `tipo` | `parejas` \| `equipos` (sin distinguir mayúsculas); otro valor → 400 |
| `tipo` (`--` = todos) | `categoria` | `absoluto` \| `menores` \| `veteranos`; otro valor → 400 |
| `listado` | — | Se leía y no se usaba: eliminado |
| `idtorneo` (a `Torneos_Previo.asp`) | — | El enlace a la ficha lo calcula la API |

Los no numéricos (`anio=abc`) los rechaza el *model binding* de `[ApiController]` con el mismo 400.
Una temporada válida sin torneos devuelve `items: []`; la página, si la temporada no está en `anios`
(enlace antiguo), vuelve a la más reciente.

## Lógica / visibilidad heredada

- **CEM 2021 (`IdPrueba = 19721`)**: el Campeonato de España de Menores 2021 se añadía siempre a su temporada
  con un `OR` **sin** filtros de visibilidad (`Borrado`, `Visible`, `Empresas`…) y se excluían los torneos
  ligados a él (`TorneoLigadoFEP <> 19721`), que eran sus pruebas internas. Se mantiene igual, con una
  corrección: el `OR` ignoraba también los filtros de tipo y categoría (salía con "Equipos" o "Veteranos");
  ahora los respeta. **DECISIÓN FEP**.
- **`TorneoLigadoFEP <> 19721` también excluye los `NULL`** (lógica de tres valores de SQL): si la columna
  admite `NULL`, los torneos sin valor no salen hoy. Se mantiene tal cual para no cambiar el listado
  (**SUPUESTO**: la gestión rellena la columna, p. ej. con 0; ver pendientes).
- **El año del desplegable filtraba `PPT = 'N'` y el listado no**: un año con solo torneos PPT no aparece en el
  desplegable, pero sus torneos PPT sí salen en el listado de los demás años. Se mantiene.
- **Categoría exacta**: elegir "ABSOLUTO" filtraba `ModalidadTorneo = 'ABSOLUTO'`, así que no salen las
  selecciones (`SEL_ABS`), aunque se pintan con el mismo color; sí salen los de silla de ruedas
  (`ABSOLUTO` + `Circuito='SILLA'`). Se mantiene y la página lo explica bajo el filtro. **DECISIÓN FEP**.
- **Plazo de inscripción** (equipos con preinscripción): si `FechaPreInscripcion > ahora` y es por equipos, se
  muestra la preinscripción ("Inscripción Equipos" si ambas fechas coinciden); si no, la inscripción. Se
  calcula en la API con la hora local del servidor (`TimeProvider`) y el front lo vuelve a comprobar con el
  reloj del navegador (la respuesta puede estar 5 min en caché y el mock es una instantánea).
- `activo=3`: opción de menú activa (plantilla). Funciones JS `MesAnterior`, `CargarEvento`, `MeGusta`,
  `busqueda`: no se usan. `<span class="solicitaclave2">` vacío en cada torneo: restos de plantilla.

## Problemas del original

1. **Inyección SQL**: `ano`, `modalidad` y `tipo` se concatenaban entre comillas tras pasar por `limpiar()`
   (lista negra). `Torneos_Previo.asp` concatenaba `Request("IdTorneo")` **sin comillas ni limpieza**. Ahora
   todo va con parámetros Dapper (`@Anio`, `@Tipo`, `@Modalidad`, tipados como `varchar`) y la ficha se
   calcula en la API (ya no hace falta pasar por `Torneos_Previo.asp`).
2. **Texto basura** "sfasdfsfs" cuando no había torneos con los filtros (comprobado en la web actual el
   13/11/2026 con `ano=2030`: "(0)" y "sfasdfsfs"). Ahora: "No hay campeonatos con estos filtros en esta temporada…".
3. **N+1**: una consulta a `SFEP_Clubes` por torneo (47 en 2026), concatenando el código del club. Ahora
   `OUTER APPLY` en la misma consulta.
4. **Doble recorrido** del recordset (contar y pintar) y `SELECT *` de noticias sin usar.
5. **XSS**: `Torneo`, `Ciudad`, `Club` y `Club2` se pintaban sin `Server.HTMLEncode`. El front usa `textContent`.
6. **Enlace a los cuadros con barras invertidas** (`https:\\padelcv.com/…`): funciona porque los navegadores
   lo corrigen, pero no es una URL válida. Ahora `https://…` normalizada y validada (`ReglasCampeonatos.EnlaceCuadros`).
7. **Torneos sin enlace navegable**: el título era `javascript:irCamp(id)` (no se puede abrir en otra pestaña,
   copiar ni indexar, y no funciona sin JS). Ahora es un enlace normal a la ficha.
8. **Fecha de inscripción pasada**: en un torneo por parejas con la inscripción ya cerrada pero la
   preinscripción abierta, el ASP pintaba "Fecha Tope de Inscripción" con la fecha **vencida**. Ahora se
   muestra la preinscripción (caso raro; corregido en `ReglasCampeonatos.Plazo`).
9. **Color heredado**: el `select case` de `ModalidadTorneo` no tenía `case else`; con un valor nuevo el torneo
   se pintaba con el color del anterior. Ahora `categoria = null` y sin etiqueta de categoría.
10. HTML: `<meta charset=iso-8859-1>` tras los scripts, `<script>` fuera de `<head>`, sin `<main>` ni
    encabezados por torneo, formulario que solo funcionaba con JavaScript (`onChange="cambiar()"`).

## Mejoras respecto al original

- Enlaces directos a la ficha de cada torneo (sin el paso por `Torneos_Previo.asp`); los externos se abren
  en pestaña nueva con aviso para lectores de pantalla.
- Torneos agrupados por mes (encabezados "Noviembre de 2026" con contador) y tarjeta con fecha grande,
  etiquetas (tipo, categoría, selecciones, silla de ruedas), sede y plazo de inscripción destacado.
- Filtro nuevo **"Solo con inscripción abierta"** (en el navegador) y cifras de la temporada en la cabecera.
- Índice lateral con las temporadas (chips en móvil); criterios en la URL para enlazar y compartir.
- Sin `limpiar()` ni POST: el estado vive en la URL; los enlaces antiguos con `ano/modalidad/tipo` funcionan.
- Respuesta cacheada 5 min (output cache varía por `anio`, `tipo`, `categoria`); una sola ida a la BD.
- Títulos con espacios repetidos o emojis (👨‍🦽, 📢, ❌) se muestran limpios.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "sfasdfsfs" (sin resultados) | Mensaje de lista vacía | Texto de prueba olvidado |
| `https:\\padelcv.com/…` | `https://padelcv.com/…` | URL mal formada |
| "Sin definir" (sin club) | Solo la ciudad | No aporta información; el original lo mostraba |
| `X OPEN … DE PADEL  &#128104;&#8205;&#129469;` | `X OPEN … DE PADEL 👨‍🦽` | Espacios dobles y entidades por la página en iso-8859-1 |
| Mayúsculas/minúsculas de `Ciudad` ("barcelona", "ZARAGOZA") | Tal cual | Dato de la BD: **DECISIÓN FEP** si se normaliza en la gestión |

## Comprobación de enlaces (13/11/2026)

- `https://www.padelfederacion.es/Camp_Equipos.asp?IdTorneo=28854` → 200.
- `https://www.padelfederacion.es/Camp_Parejas.asp?IdTorneo=31277` → 302 a `Camp_Silla.asp` (silla de ruedas).
- `http://circuitomenores.padelfederacion.es/Campeonato?IdTorneo=31292` → 200 por **http**; por **https** la
  conexión falla (sin certificado válido). Se mantiene `http://` (16 torneos de menores en el mock).
- `http://circuitomenores.padelfederacion.es/CEM-2021` → 200 (solo http).
- `https://padelcv.com/competicion/torneos/fip-senior-europadel-cup-open-pairs/` → 200.
- `Torneos_Previo.asp?IdTorneo=19721` → 302 a `CEM-2021` (confirma la regla).

## Modo prototipo (mocks)

`mock/competicion-campeonatos.json` es una instantánea de la web pública (13/11/2026) con **todas** las
temporadas del desplegable (2012–2026) y **635 torneos** sin filtrar, cada uno con el `enlace` real obtenido de
la redirección de `Torneos_Previo.asp`. Lo genera `tools/generar-mock-campeonatos.ps1` (solo lectura sobre la
web pública; reutiliza los enlaces ya resueltos; `-Anios 2026` para refrescar solo una temporada). El recuento
por temporada coincide con el "(N)" de la web (2026: 47 · 2025: 39 · 2024: 41 · 2023: 46 · 2022: 47 · 2021: 55 ·
2020: 8 · 2019: 49 · 2018: 45 · 2017: 43 · 2016: 46 · 2015: 46 · 2014: 45 · 2013: 43 · 2012: 35), y se ha
contrastado un filtro (2012, equipos, veteranos: 11 en ambas). Como el mock no puede filtrar, `campeonatos.js`
aplica siempre en el navegador los filtros de temporada, tipo y categoría sobre la respuesta (con la API no
cambia nada porque ya llega filtrada).

Dato erróneo detectado en la BD: "TyC PREMIUM 1" (2024) termina antes de empezar (23 FEB – 10 FEB). Se deja
tal cual (**DECISIÓN FEP**: corregir en la gestión).

Limitaciones del mock: la web no muestra el `IdPrueba` de los torneos con `LinkCuadros` (el "FIP SENIOR
EUROPADEL CUP OPEN PAIRS" de 2025 lleva `id: 0`); `fechaInicio`/`fechaFin` se han reconstruido con día y
mes de la web y el año de la temporada; la sede combinada ("Club y Club2") va como un único elemento de
`clubes`. Solo el "X OPEN NACIONAL FEKOOR BIZKAIA" tenía plazo abierto (hasta el 20/11/2026 12:00).

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: el CEM 2021 (`IdPrueba = 19721`) ahora respeta los filtros de tipo y categoría.
  ¿Sigue haciendo falta esa excepción en el código? Lo limpio sería marcarlo como visible en la BD y quitarla.
- **DECISIÓN FEP**: ¿la categoría "Absoluto/Menores/Veteranos" debe incluir los campeonatos de selecciones
  (`SEL_*`)? Hoy no los incluye.
- **DECISIÓN FEP**: ¿filtro propio para pádel en silla de ruedas? Hoy van dentro de "Absoluto".
- **SUPUESTO**: tipos de columnas (`IdPrueba`, `[Año]`, `TorneoPadre`, `TorneoLigadoFEP` enteros; fechas
  `datetime`; `TipoTorneo`, `ModalidadTorneo`, `Circuito`, `Liga`, `Empresas` `char`/`varchar`;
  `SFEP_Torneos.Club` = código de `SFEP_Clubes.IdClub`, comparado como texto con `CAST(... AS varchar(20))`).
- **SUPUESTO**: `TorneoLigadoFEP` no tiene `NULL` en torneos que deban verse (si los tiene, hoy no salen y
  seguirán sin salir; cambiarlo a `ISNULL(TorneoLigadoFEP, 0) <> 19721` sería una decisión de negocio).
- **SUPUESTO**: `FechaInscripcion`/`FechaPreInscripcion` están en hora local de España (como `now()` del IIS).
- El circuito de menores (`circuitomenores.padelfederacion.es`) solo responde por http: conviene activar https.
- Las fichas (`Camp_Parejas.asp`, `Camp_Equipos.asp`, `Camp_Liga.asp`, `Camp_Clasificatorio.asp`,
  `Empr_Equipos.asp`, `Camp_Silla.asp`) y la inscripción siguen en la web actual (grupo
  `inscripciones-campeonatos` del mapa de migración).

## Modelo SQL propuesto

No aplica: el contenido ya está en BD (`SFEP_Torneos`). Índice recomendado para la consulta nueva:
`SFEP_Torneos (IdFederacion, [Año]) INCLUDE (Borrado, Visible, Empresas, ProgramaImpulso, TorneoLigadoFEP,
TipoTorneo, ModalidadTorneo, FechaInicio)` (**SUPUESTO**: no se ha podido ver el esquema real).

## Ficheros

- API: `Contracts/Competicion/CampeonatosDto.cs` (`CampeonatosDto`, `CampeonatoDto`, `PlazoInscripcionDto`),
  `Contracts/Competicion/ReglasCampeonatos.cs` (`BusquedaCampeonatos`, claves de tipo/categoría, plazo de
  inscripción, enlace a los cuadros y reparto de `Torneos_Previo.asp`),
  `Data/Repositories/Competicion/CampeonatosRepository.cs` (Dapper), `Controllers/Competicion/CampeonatosController.cs`;
  registro en `Program.cs`; `anio` añadido a las claves de output cache en `Infrastructure/ServiceCollectionExtensions.cs`;
  tests en `tests/FEP.Api.Tests/CampeonatosTests.cs` (validación 400, criterios normalizados con un repositorio
  falso, JSON y reglas heredadas; no necesitan SQL Server).
- Web: `wwwroot/campeonatos.html`, `assets/js/campeonatos.js`, estilos `.camp-*` en la sección "Páginas
  interiores" de `assets/css/fep.css` (y su bloque responsive); reutiliza `.clubs-search`, `.clubs-form`,
  `.clubs-results`, `.org-tag`, `.event__days`/`.event__month`.
- Mock: `mock/competicion-campeonatos.json` (instantánea de la web pública de todas las temporadas, generada con
  `tools/generar-mock-campeonatos.ps1`) + `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS` y `QUERY_MIGRADA` en `assets/js/comun.js` (`campeonatos` → `campeonatos.html`, con
  `ano`/`modalidad`/`tipo`).
