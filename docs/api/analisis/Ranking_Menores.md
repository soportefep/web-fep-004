# Análisis: `Ranking_Menores.asp` → `ranking-menores.html` + `GET /api/v1/competicion/rankings/menores`

URL actual: `/Ranking_Menores` (enlace "Ranking Menores" de `SFEP_Rankings_Web`, desde `rankings.html`; responde igual con
y sin `.asp`). Ruta nueva: `/ranking-menores` → `ranking-menores.html` (no estaba en `redirecciones.csv`; se ha añadido
con la convención del proyecto y el nombre que ya preveían `rankings.js` y `Rankings.md`).

Como `Ranking_Absoluto.asp`, la página es un "cascarón": la clasificación la cargaba por AJAX (`$(div).load(...)`) desde
`CargarRnkMen.asp`, que **no está en `pages_to_dev/`** y se ha leído en `X:\TICS\WEB\` (solo lectura, Windows-1252).
Se reutiliza **íntegramente** el diseño de Ranking_Absoluto (`ReglasClasificaciones`, `ClasificacionesRankingRepository`,
`ClasificacionesRankingController`, `ClasificacionRankingDto`, `ranking.js` y estilos `.rkc-*`): basta con declarar los
`TipoRanking` de menores. No hay endpoint, DTO, repositorio ni CSS nuevos.

Includes: `conexionSQL.inc` (conexión; no se copia nada), `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc`,
`finconexion.inc` (comunes, ya migrados en `comun.js`).

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie, barra lateral | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` + `/api/v1/menu`, `/redes-sociales`, `/patrocinadores` (columna derecha) |
| `sql_noticias` (`SFEP_Noticias` destacadas) | Se consultaba **pero no se usaba** | Eliminado |
| Título "RANKING MENORES CIRCUITO FEP" (`h5`) | HTML escrito a mano | `h1` "Ranking de Menores." + entradilla + cifras de la lista elegida (jugadores, pruebas, actualización) |
| Aviso "Ciclo clasificatorio 2… ranking en vigor a fecha del 12/02/2026… PAREJAS MIXTAS" + tabla de 10 PDF "Ranking a 04/03/26" | HTML dentro de `<% if a=5 then %>` | **No se migra**: `a` nunca se asigna, así que **nunca se mostraba** (comprobado en la web pública). Ver "Lógica heredada" |
| "Se está actualizando el ranking de Menores… Normativa: 1- … hasta un máximo de 2 torneos autonómicos" | HTML dentro de `<% if a=5 then %>` | **No se migra** (nunca se mostraba) |
| Banner "Circuito Nacional de Menores 2026 Babolat" | `refs/images/FEP banner CNM 2026.jpg` | `assets/img/fep/ranking-menores/fep-banner-cnm-2026.jpg` (`figure.org-figure` con `alt` descriptivo) |
| Desplegables "Selecciona un género" (`----`/MASCULINO/FEMENINO) y "Selecciona una categoría" (`----`/BENJAMIN/ALEVIN/INFANTIL/CADETE/JUNIOR) + "Indica un número de licencia" | HTML + JS (`rnkmen`, `rnkmen1`, `rnkmen2`) con dos `hidden` (`gen`, valor inicial `X`; `cat`, valor inicial `0`) | Formulario: selector segmentado de categoría (5) y de género (2) + un campo "Nombre y apellidos o número de licencia" |
| Recuadro azul "El ranking único tiene un total de N jugadores y están sumando a este ranking N pruebas. Fecha de Actualización… Descarga del Ranking completo [PDF]" | `SFEP_Ranking_Tipos` (`Jugadores`, `Pruebas`, `FechaAct`, `ArchivoPDF`) con `IdFederacion=0 AND TipoRanking={tipo de la 1.ª fila}` | `ListaRankingDto.jugadores`, `pruebas`, `actualizado`, `documento`/`documentoUrl` (para **cada** lista) |
| Tabla Categoría · Puesto · Jugador · Total Torneos · Total Puntos · Torneos Rnk · Puntos Rnk · Evolución | `CargarRnkMen.asp?cat=&genero=`: `SELECT … FROM SFEP_RankingFinal INNER JOIN SFEP_Fichas ON NumLicencia = LicenciaJugador WHERE IdFederacion=0 AND TipoRanking={rnk} ORDER BY Posicion` (**sin TOP**: hasta 1.277 filas). `rnk` según categoría × género: Benjamín 8000/8001, Alevín 7000/7001, Infantil 6000/6001, Cadete 5000/5001, Júnior 4000/4001 (M/F); mixtos `X` 8002…4002 | `GET /api/v1/competicion/rankings/menores?categoria=&genero=&limite=` → `ClasificacionRankingDto` (una `ListaRankingDto` por categoría × género). La categoría va en el título de cada lista ("Ranking benjamín masculino") en lugar de repetirse en cada fila |
| Búsqueda por licencia | `CargarRnkMen.asp?id=`: mismas tablas + `SFEP_Ranking_Tipos` (`Visible='S'`, `TipoRanking BETWEEN 3999 AND 8999` sin los mixtos x002), sin orden; pinta la `Categoria` de `SFEP_Ranking_Tipos` | El mismo endpoint con `licencia=` (sin género ni categoría: todas las listas); una lista por categoría con resultados |
| Enlace de cada celda | `Estadisticas.asp?id={licencia}` | `PuestoRankingDto.estadisticasUrl` = `https://www.padelfederacion.es/Estadisticas?id={licencia}` (web actual hasta que se migre) |
| Nota "*Evolución: Numero de posiciones que sube o baja…" | HTML escrito a mano | Leyenda bajo la tabla |
| `linko` (`BENM01012026.pdf`…) | Variable de `CargarRnkMen.asp` **nunca usada** (el PDF sale de `ArchivoPDF`) | Eliminada |

No hay normativa de cálculo (a diferencia del absoluto): la página no la tenía. `/api/v1/competicion/rankings/menores/normativa`
responde 404 con `urlWebActual` y `ranking-menores.html` no tiene la sección `#calculo` (`ranking.js` solo la pide si existe).

## Lógica / visibilidad heredada

- **Contenido oculto**: los dos bloques `<% if a=5 then %>` (aviso del Ciclo clasificatorio 2 con la tabla de PDF a
  04/03/2026, y "Se está actualizando el ranking…") nunca se pintaban porque `a` no se asigna (vale `Empty`; `a=""` es
  verdadero y `a=5` falso). Mismo patrón que la variable `a` de Órganos de Gobierno. Los 10 PDF del 04/03/2026 existen
  (200), pero están superados por los del 04/11/2026 que sirve `SFEP_Ranking_Tipos`. **DECISIÓN FEP** si se quiere
  recuperar algún aviso (se publicaría como texto en la página, no en la API).
- **Género "X" (mixto) por accidente**: el `hidden` `gen` empezaba en `X`, así que si se elegía primero la categoría,
  `CargarRnkMen.asp` servía el ranking **mixto** (`TipoRanking` x002): en infantil, cadete y júnior devuelve filas (sin
  evolución y con un recuadro vacío o de 2015: "un total de jugadores… Fecha de Actualización: 09/09/2015", PDF
  `refs/docs/` roto); en benjamín y alevín, **error 500** (sin filas, `rs_datos` sin asignar). La búsqueda por licencia
  excluía los mixtos. **No se publican** (DECISIÓN FEP: si se quieren, basta con declararlos en `ReglasClasificaciones`).
- **"----"** en cualquiera de los desplegables: `rnk` vacío → `TipoRanking=` → error SQL → 500. La API solo admite
  categorías y géneros declarados (400 en otro caso) y el front siempre pide una categoría y un género.
- Licencia inexistente: `rs_datos` sin asignar → **HTTP 500** (comprobado: `CargarRnkMen.asp?id=1`). Ahora: 200 con
  las listas sin puestos y el mensaje "ningún resultado".
- La búsqueda por licencia exigía `SFEP_Ranking_Tipos.Visible='S'`; la de categoría no. La API no filtra por `Visible`
  (los 10 tipos declarados se publican; **SUPUESTO**: todos están visibles, como se ve en la web pública).
- Al buscar por licencia, el desplegable de género y categoría volvía a "----" pero el `hidden` conservaba el valor
  anterior: al elegir después solo uno de los dos se mezclaba con el antiguo. En la web nueva el estado es explícito.
- `</td` sin cerrar en la última celda de cada fila (HTML inválido) e icono de evolución sin `alt`. Irrelevante en la
  web nueva.
- Las funciones `rnkabs*`, `MesAnterior`, `CargarEvento`, `MeGusta` y `busqueda` del `<head>` no se usaban en esta página.

## Seguridad

- **Inyección SQL**: `Numlicencia=" & id` sin comillas ni validación en `CargarRnkMen.asp`. Ahora todo va parametrizado
  (`@Licencia` int, `@Tipos`, `@T0…`) con validación previa; `categoria` y `genero` se comprueban contra la lista
  blanca de `ReglasClasificaciones` (no llegan al SQL).
- **XSS**: nombres y cifras se escribían sin codificar. En la web nueva todo va por `textContent`/atributos y `urlSegura`.

## Mejoras respecto al original

- Una consulta parametrizada por petición (la del absoluto), columnas explícitas, `ROW_NUMBER` para limitar por lista.
- Al entrar se ve el ranking benjamín masculino (antes una tabla vacía con "Selecciona un género y una categoría…").
- Categoría y género como selectores segmentados accesibles (`fieldset`/`legend`, foco visible); criterios en la URL
  (`?categoria=cadete&genero=F`, `?licencia=`, `?busqueda=`), enlazables.
- **Búsqueda por nombre** (no existía en menores) además de por licencia, en todas las categorías y sin distinguir
  acentos; los resultados se agrupan por categoría (un jugador puede estar en varias: p. ej. la licencia 310047 aparece
  en alevín e infantil).
- Datos de cada lista (jugadores, pruebas, actualización y PDF completo) en lugar de solo los del tipo de la primera fila.
- Nombres de categoría con tilde ("Benjamín", "Alevín", "Júnior"), nombres de jugador en tipo título solo en el front.
- Tabla accesible (`caption`, `th scope`) y en móvil una ficha por puesto, sin scroll horizontal.
- `ranking.js` admite ahora categorías de forma genérica (`#ranking-categorias-opciones`, `?categoria=`) y la
  normativa es opcional: sirve tal cual para Veteranos (categorías por edad) sin tocar el JS.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "RANKING MENORES CIRCUITO FEP" | "Ranking de Menores." | Título en estilo de frase, como el resto; "Circuito FEP" en la entradilla |
| "BENJAMIN", "ALEVIN", "JUNIOR" | "Benjamín", "Alevín", "Júnior" | Tildes (RAE), como en el calendario de menores ya migrado |
| "El ranking único tiene un total de N jugadores…" | "N jugadores/as en el ranking", "N pruebas suman para el ranking" | El texto se copió del absoluto: este no es el "ranking único" |
| "*Evolución: Numero de posiciones…" | "Número de posiciones…" | Tilde |
| Columna "Categoría" repetida en cada fila | Categoría en el título de cada lista | Una lista = una categoría y un género |
| `<title>Ranking Menores \| FEP</title>` | "Ranking de Menores · Federación Española de Pádel" | Coherencia con el `h1`; `meta description` propia |

No se han cambiado cifras.

## Comprobación de enlaces (13/11/2026)

- `https://www.padelfederacion.es/Ranking_Menores` y `/Ranking_Menores.asp`: 200 (sin el aviso oculto; con el banner).
- `CargarRnkMen.asp?genero=M|F&cat=…` (10 listas): 200 (instantánea usada para el mock; actualización 04/11/2026,
  112 pruebas). `?genero=X&cat=BENJAMIN|ALEVIN`: **500**; `?genero=X&cat=INFANTIL|CADETE|JUNIOR`: 200 con recuadro vacío
  o de 2015. `?id=1`: **500**. `?id=310047`: 200.
- PDF del ranking completo (`ArchivoPDF`): `refs/docs/Rnk_{Benjamin|Alevin|Infantil|Cadete|Junior}_{Masc|Fem}_04_11_26.pdf`:
  los 10 dan 200 (`Last-Modified` 04/11/2026: **al día**).
- PDF de la tabla oculta `Rnk_*_04_03_26.pdf` (10): 200. `BENM01012026.pdf`, `JUNF01012026.pdf` (`linko`, sin uso): 200.
- `refs/images/FEP banner CNM 2026.jpg`: 200 (copiado). `Estadisticas?id=310047`: 200.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: rankings **mixtos** (x002) por categoría: existen en `SFEP_RankingFinal` (infantil, cadete y júnior
  con filas; benjamín y alevín vacíos), sin datos en `SFEP_Ranking_Tipos`, sin evolución ni PDF, y solo se veían por
  un fallo del formulario. El aviso oculto mencionaba a las "parejas mixtas". ¿Se publican? Hoy no.
- **DECISIÓN FEP**: los avisos ocultos (`a=5`) no se migran. ¿Hay que publicar algún aviso vigente sobre el ranking que
  cuenta en cada ciclo clasificatorio?
- **DECISIÓN FEP**: `SFEP_Ranking_Tipos.Jugadores` no coincide con las filas de la clasificación (benjamín femenino:
  141 frente a 85 filas; infantil masculino: 1.210 frente a 1.125; benjamín masculino: 323 frente a 333). Ninguna
  licencia está repetida. Se muestra el dato de `Jugadores` como hacía el ASP; ¿se recalcula o se muestra el número de
  filas (`coincidencias`)?
- **DECISIÓN FEP**: la web muestra hasta 500 puestos por lista (100 al entrar, ampliables); antes se pintaban todos
  (hasta 1.277 en cadete masculino). El resto está en el PDF de cada lista (al día).
- **DECISIÓN FEP**: al entrar se muestra benjamín masculino (primer valor de cada desplegable del ASP).
- **DECISIÓN FEP**: la tabla publica el número de licencia de **menores de edad** (en el ASP iba en el enlace, no como
  columna; en el absoluto ya se publicaba). ¿Se mantiene por protección de datos? Basta con quitar la columna en
  `ranking.js` (la búsqueda por licencia seguiría funcionando). Ídem para el nombre completo.
- **DECISIÓN FEP**: se añade búsqueda por nombre (no existía en menores; sí en el absoluto).
- **SUPUESTO**: los 10 `TipoRanking` de menores tienen `Visible='S'` en `SFEP_Ranking_Tipos` (la API no filtra por
  `Visible`, igual que la consulta por categoría del ASP).
- **SUPUESTO**: tipos de columna y unicidad de `SFEP_Fichas.LicenciaJugador`, como en `Ranking_Absoluto.md`.
- **SUPUESTO**: ruta nueva `/ranking-menores` (no había fila en `redirecciones.csv`).

## Modelo SQL propuesto

No aplica: la clasificación ya está en BD y la página no tiene texto informativo propio. Si se quisiera declarar los
rankings en BD en lugar de en `ReglasClasificaciones.Rankings`, ver la propuesta de `Ranking_Absoluto.md`
(`ALTER TABLE SFEP_Ranking_Tipos ADD ClaveWeb varchar(40) NULL, Genero char(1) NULL;` — `Categoria` y `Visible` ya
existen).

## Ficheros

- API: `Contracts/Competicion/ReglasClasificaciones.cs` (ranking `menores` con sus 10 `TipoRanking` y nombres de
  categoría con tilde); comentarios en `Data/Repositories/Competicion/ClasificacionesRankingRepository.cs`,
  `Controllers/Competicion/ClasificacionesRankingController.cs` y `Program.cs`; tests en
  `tests/FEP.Api.Tests/ClasificacionesRankingTests.cs` (menores ya no da 404; tipos, categorías, validación, normalización
  y 404 de la normativa).
- Web: `wwwroot/ranking-menores.html`, `assets/js/ranking.js` (categorías y normativa opcional, sin cambiar el
  comportamiento del absoluto), `assets/img/fep/ranking-menores/fep-banner-cnm-2026.jpg`. Sin CSS nuevo (`.rkc-*`,
  `.org-figure`).
- Mock: `mock/competicion-rankings-menores.json` (instantánea de la web pública del 13/11/2026: top 100 de cada una de
  las 10 listas sin filtrar, en el orden de la API; `coincidencias` = filas reales de cada lista) y `mock/mocks.js`
  regenerado.
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`ranking_menores`), enlace "Menores · Todas las categorías" de
  `index.html`, ejemplo del comentario de `rankings.js`.
- Documentación: `docs/api/endpoints.md`, `docs/migracion/redirecciones.csv` (fila nueva),
  `docs/migracion/mapa-migracion.md`, fila de `tools/generar-mapa-web.ps1` (Desarrollado).
