# Análisis: `Ranking_Absoluto.asp` → `ranking-absoluto.html` + `GET /api/v1/competicion/rankings/absoluto` (+ `/normativa`)

URL actual: `/Ranking_Absoluto` (enlace "Ranking Único FEP" de `SFEP_Rankings_Web`, desde `rankings.html`; no hay regla
propia en `web.config`, responde igual con y sin `.asp`). Ruta nueva: `/ranking-absoluto` → `ranking-absoluto.html`
(no estaba en `redirecciones.csv`; se ha añadido siguiendo la convención del proyecto y el nombre ya previsto por
`rankings.js`).

La página es un "cascarón": la clasificación la cargaba por AJAX (`$(div).load(...)`) desde dos ficheros que **no
están en `pages_to_dev/`** y se han leído en `X:\TICS\WEB\` (solo lectura):

- `CargarRnkAbs.asp` (`?genero=M|F` o `?id={licencia}`), Windows-1252.
- `CargarRnkAbsNombre.asp` (`?id={texto-con-guiones}&genero=`), Windows-1252.

Includes: `conexionSQL.inc` (conexión; no se copia nada), `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc`,
`finconexion.inc` (comunes, ya migrados en `comun.js`).

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie, barra lateral | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` + `/api/v1/menu`, `/redes-sociales`, `/patrocinadores` (columna derecha) |
| `sql_noticias` (`SFEP_Noticias` destacadas) | Se consultaba **pero no se usaba** | Eliminado |
| Título "Ranking Único Absoluto" (`h5`) | HTML escrito a mano | `h1` "Ranking Único Absoluto." + entradilla + cifras (jugadores, pruebas, actualización) |
| Selector de género, nº de licencia y texto | HTML + JS (`rnkabs`, `rnkabs1`, `rnkabs2`) | Formulario: selector Masculino/Femenino + un único campo "Nombre y apellidos o número de licencia" |
| Recuadro azul "El ranking único tiene un total de N jugadores y están sumando a este ranking N pruebas. Fecha de Actualización… Descarga del Ranking completo [PDF]" | `SFEP_Ranking_Tipos` (`Jugadores`, `Pruebas`, `FechaAct`, `ArchivoPDF`) con `IdFederacion=0 AND TipoRanking={tipo de la 1.ª fila}` | `ListaRankingDto.jugadores`, `pruebas`, `actualizado`, `documento`/`documentoUrl` (para **cada** lista) |
| Tabla Puesto · Licencia · Jugador · Total Torneos · Total Puntos · Torneos Rnk · Puntos Rnk · Evolución | `CargarRnkAbs.asp`: `SELECT TOP 100 … FROM SFEP_RankingFinal INNER JOIN SFEP_Fichas ON NumLicencia = LicenciaJugador WHERE IdFederacion=0 AND TipoRanking={10000 si genero=M, si no 10001} ORDER BY Posicion, Puntos DESC`. Con `?id=`: la licencia en los tipos 10000 **o** 10001 (sin TOP). Pinta `Posicion`, `NumLicencia`, `Nombre Apellido1 Apellido2`, `TorneosJugador`, `PuntosTotal`, `Torneos`, `Puntos`, `Sube` (icono sube/baja/igual) y `JornadasPuesto` entre paréntesis | `GET /api/v1/competicion/rankings/absoluto?genero=&licencia=&limite=` → `ClasificacionRankingDto` (`listas[].puestos[]` = `PuestoRankingDto`) |
| Búsqueda por nombre | `CargarRnkAbsNombre.asp`: mismas tablas, `TipoRanking` del género, `SFEP_Fichas.NombreCompleto LIKE '%término%'` por cada palabra (separadas por "-"), `ORDER BY Apellido1, Apellido2`, sin límite y **sin** columna Evolución | El mismo endpoint con `busqueda=` (términos como parámetros, sin distinguir mayúsculas ni acentos) |
| Enlace de cada fila | `CargarRnkAbs.asp`: `Silla-Ruedas-Est?id=PS{licencia}`; `CargarRnkAbsNombre.asp`: `Estadisticas.asp?id={licencia}` | `PuestoRankingDto.estadisticasUrl` = `https://www.padelfederacion.es/Estadisticas?id={licencia}` (web actual; `ranking.js` usa la página nueva cuando `Estadisticas` se migre y esté en `PAGINAS_MIGRADAS`) |
| Nota "*Evolución: Numero de posiciones que sube o baja…" | HTML escrito a mano | Leyenda bajo la tabla (con "Torneos/puntos totales" y "ranking" explicados) |
| Desplegable "Información Cálculo del Ranking": Introducción, Sistema de Puntuación, Puntos, Valoración económica (tabla), Ranking Jugadores (2 tablas), Número Parejas (tabla + nota), Número Licencias Federación (tabla + nota), Campeonatos Absolutos, Pruebas World Padel Tour | HTML escrito a mano | `GET /api/v1/competicion/rankings/absoluto/normativa` → `NormativaRankingDto` desde `Contenidos/rankings/absoluto.json` (9 apartados, 5 tablas) |

## Diseño de la API (reutilizable por las demás `Ranking_*.asp`)

- `GET /api/v1/competicion/rankings` (índice, ya existía: `RankingsController`) **no se toca**.
- `GET /api/v1/competicion/rankings/{clave}` (`ClasificacionesRankingController`): la `clave` es la misma del índice
  (`absoluto`, `menores`, `veteranos`, `silla-ruedas`, `equipos-absolutos`). Cada ranking se declara en
  `ReglasClasificaciones.Rankings` como una lista de `TipoRanking` × género × categoría; hoy solo `absoluto`
  (10000 M, 10001 F). `CargarRnkMen.asp` y `CargarRnkVet.asp` (leídos para el diseño, **no migrados**) usan las mismas
  tablas con `TipoRanking` 4000–8001 (menores, por categoría) y 10035–11060 (veteranos), así que basta con añadirlos
  allí y declarar sus categorías: el contrato (`generos`, `categorias`, `?categoria=`) ya los contempla. Las claves
  aún no declaradas devuelven 404. `Ranking_Equipos_Absolutos.asp` (32 tablas) probablemente necesite otro recurso.
- `GET /api/v1/competicion/rankings/{clave}/normativa`: texto informativo de cada ranking
  (`Contenidos/rankings/{clave}.json`), separado de la clasificación porque no varía con los filtros.
- Sin `genero` se devuelven todas las listas del ranking (el mock es esa respuesta); el front siempre pide un género o
  una búsqueda. `limite` (1–500, por defecto 100) se aplica **por lista** con `ROW_NUMBER`; `coincidencias` da el
  total antes del corte.

## Lógica / visibilidad heredada

- `genero` distinto de `M` (incluido el `0` de la opción "----" del desplegable) mostraba el ranking **femenino**: al
  volver a "----" se veían las mujeres. La API solo admite `M`/`F` (o `masculino`/`femenino`); `0` → 400.
- Sin `genero` ni `id`, `CargarRnkAbs.asp` consultaba `TipoRanking = 0`; la página nunca hacía esa llamada (rama
  muerta): no se migra.
- La búsqueda por **licencia** ignoraba el género (10000 o 10001); la de **nombre** usaba el género del desplegable.
  En la web nueva ambas buscan en los dos rankings (**DECISIÓN FEP**, ver abajo); la API sí permite combinar
  `busqueda`/`licencia` con `genero`.
- **La búsqueda por nombre no funcionaba**: `rnkabs2()` usa `document.formulario.textornkabs`, pero la página no tiene
  ningún `<form name="formulario">` (error de JavaScript al cambiar el campo). Ahora funciona.
- Licencia inexistente: `rs_datos` quedaba sin asignar y `if not rs_datos.eof` lanzaba error → **HTTP 500**
  (comprobado: `CargarRnkAbs.asp?id=1` da 500). Ahora: 200 con la lista sin puestos y el mensaje "ningún resultado".
- Mensaje de tabla vacía con `colspan='7'` en una tabla de 8 columnas; `</td` sin cerrar en la última celda de cada
  fila (HTML inválido). Irrelevante en la web nueva.
- El recuadro de datos solo se pintaba si había filas y solo para el tipo de la primera fila.
- La búsqueda por nombre ordenaba por apellidos; ahora por puesto (es un ranking) — **DECISIÓN FEP** menor.
- Las funciones `rnkmen*`, `MesAnterior`, `CargarEvento`, `MeGusta` y `busqueda` del `<head>` no se usaban en esta
  página (copiadas de otras).

## Seguridad

- **Inyección SQL**: `NumLicencia=" & id` (sin comillas ni validación) en `CargarRnkAbs.asp`; en
  `CargarRnkAbsNombre.asp` el texto solo duplicaba comillas (`Replace(id,"'","''")`) y los comodines `%`/`_` no se
  escapaban. Ahora todo va parametrizado (`@Licencia` int, `@Tipos`, `@T0…@T5` con `LIKE` escapado), con validación
  previa (licencia entera ≥ 1, máx. 100 caracteres y 6 palabras, `limite` 1–500).
- **XSS**: nombres y cifras se escribían sin codificar en el HTML devuelto. En la web nueva todo va por
  `textContent`/atributos y `urlSegura`.
- La respuesta del 404 no repite la clave pedida.

## Mejoras respecto al original

- Una sola consulta (puestos + datos de las listas en un `QueryMultiple`), columnas explícitas, `IdFederacion` y
  `TipoRanking` como parámetros; `ROW_NUMBER() OVER (PARTITION BY TipoRanking …)` para limitar por lista y
  `COUNT(*) OVER` para las coincidencias. La licencia desempata el orden para que el corte sea estable.
- Masculino/femenino como selector segmentado (se ve el masculino al entrar en lugar de una tabla vacía con "Selecciona
  un género"); un único campo que distingue licencia (solo dígitos) y nombre; botón "Ver 100 puestos más" hasta 500;
  criterios en la URL (`?genero=F`, `?licencia=`, `?busqueda=`), enlazables.
- La búsqueda por nombre ahora incluye la columna Evolución (antes no la tenía) y no distingue acentos.
- Nombres mostrados en tipo título ("Enric Sanmarti Beleta"; la BD los guarda en mayúsculas) solo en el front.
- Evolución con ▲/▼/= y color además del número (antes iconos `Sube.png`/`Baja.png`/`igual.png` sin `alt`), con texto
  para lectores de pantalla ("Sube 15 puestos; 1 actualización en esta posición").
- Tabla accesible (`caption`, `th scope`, nombre como cabecera de fila); en móvil cada puesto es una ficha (puesto,
  nombre y cifras etiquetadas en dos columnas) sin scroll horizontal.
- La normativa pasa de un acordeón con `h5` y tablas de maquetación (`border="0"`, `class="pielineasPPT"`) a apartados
  con índice, tablas con `caption`/`th`, y notas.
- Título de la pestaña: "Ranking Absoluto | FEP" → "Ranking Único Absoluto · Federación Española de Pádel"; `meta
  description` propia (antes la genérica de la web).

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| Enlace del jugador `Silla-Ruedas-Est?id=PS{licencia}` | `Estadisticas?id={licencia}` | Error evidente: es la estadística de pádel en silla de ruedas y no muestra al jugador (comprobado con la licencia 3889: "Estadisticas" sí lo muestra). `CargarRnkAbsNombre.asp` ya enlazaba a `Estadisticas.asp` |
| "Entre 4.000 € y 4.99 9€" | "Entre 4.000 € y 4.999 €" | Errata |
| "Entre 19.000€ y 22.999€", "Menos de 1000 €" | "Entre 19.000 € y 22.999 €", "Menos de 1.000 €" | Formato homogéneo con el resto de la tabla |
| "2000", "1600", "1200" (tabla de parejas) | "2.000", "1.600", "1.200" | Separador de miles como en las demás tablas |
| "39.000 € ó más", "64 ó más", "7 ó menos", "10.000 ó más", "999 ó menos" | "o" sin tilde | Ortografía (RAE 2010) |
| Tabla de créditos: filas "10.000º → 1" **dos veces** y "... → 1" | "10.000º → 1" y "10.001º y siguientes → 1" | Fila duplicada; la última fila "…" con 1 crédito se interpreta como "a partir del 10.001º" (**SUPUESTO**) |
| "-sólo socios-", "sólo se permitirá", "sólo se tomarán" | "(solo socios)", "solo" | Ortografía (RAE 2010) |
| "Ránking Único Absoluto", "Ránking Único Nacional" | "Ranking …" | Grafía homogénea con el resto de la página |
| "-que no hubiese sido tenida en cuenta aún-" | "(que no hubiese sido tenida en cuenta aún)" | Puntuación |
| "seguirá el mismo criterio que los torneo por categorias, siendo el orden de las categorias, Primera…" | "se seguirá el mismo criterio que en los torneos por categorías, siendo el orden de las categorías: Primera…" | Concordancia y tildes |
| "*Evolución: Numero de posiciones…" | "Número de posiciones…" | Tilde |
| "(sin WOs en el primer partido)" | "(sin WO en el primer partido)" | Las siglas no llevan "s" de plural |
| Títulos "Sistema de Puntuación", "Ranking Jugadores", "Número Parejas", "Número Licencias Federación", "Campeonatos Absolutos", "Información Cálculo del Ranking" | "Sistema de puntuación", "Ranking de jugadores", "Número de parejas", "Número de licencias de la federación", "Campeonatos absolutos", "Información sobre el cálculo del ranking" | Títulos en estilo de frase, como el resto de la web nueva |
| `<title>Ranking Absoluto \| FEP</title>` | "Ranking Único Absoluto · Federación Española de Pádel" | Coherencia con el `h1` |

No se han cambiado cifras ni criterios de la normativa.

## Comprobación de enlaces (13/11/2026)

- `https://www.padelfederacion.es/Ranking_Absoluto`: 200. `CargarRnkAbs.asp?genero=M|F`: 200 (instantánea usada para el
  mock: 20.471 jugadores, 7.293 jugadoras, 190 pruebas, actualizado el 04/11/2026). `CargarRnkAbs.asp?id=1`: **500**.
- PDF del ranking completo (`ArchivoPDF` en BD): `refs/docs/Rnk_Unico_Masc_24_09_25.pdf` y
  `refs/docs/Rnk_Unico_Fem_24_09_25.pdf`: 200 (`Last-Modified` 25/09/2025) → **desactualizados** respecto a la fecha
  de actualización del ranking (04/11/2026).
- `Estadisticas?id=3889`: 200 (muestra al jugador). `Silla-Ruedas-Est?id=PS3889`: 200 pero sin el jugador.
- Iconos `refs/images/Pdf_descarga.png`, `Sube.png`, `Baja.png`, `igual.png`: 200 (ya no se usan: CSS y texto).

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: los PDF del ranking completo son de septiembre de 2025 y el ranking está actualizado a noviembre de
  2026. ¿Se siguen generando? Si no, retirar el enlace (basta con vaciar `ArchivoPDF` en `SFEP_Ranking_Tipos`) o
  generarlos de nuevo. La API sirve lo que haya en la BD.
- **DECISIÓN FEP**: el apartado "Pruebas World Padel Tour" y las menciones a WPT/Master/Challenger están obsoletos
  (el circuito WPT terminó en 2023). ¿Se actualiza la normativa (Premier Padel…)? Se ha migrado tal cual.
- **DECISIÓN FEP**: huecos en los límites de las tablas: "Más de 120.000 créditos" frente a "Entre 110.000 y 119.999"
  (¿y 120.000 exactos?) y "Menos de 1.249 créditos" frente a "Entre 1.250 y 2.499" (¿y 1.249?). Se han dejado como en
  el original.
- **DECISIÓN FEP**: la búsqueda (licencia o nombre) mira en los rankings masculino y femenino a la vez (en el ASP la de
  licencia ya lo hacía; la de nombre usaba el género elegido). Por nombre se ordena por puesto (antes por apellidos).
- **DECISIÓN FEP**: al entrar se muestra el ranking masculino (el ASP no mostraba nada hasta elegir género). Se puede
  invertir con `?genero=F` o cambiando el orden de `Tipos` en `ReglasClasificaciones`.
- **DECISIÓN FEP**: se muestran hasta 500 puestos por lista en la web (antes 100); el resto, en el PDF.
- **DECISIÓN FEP**: la tabla publica el número de licencia de cada jugador (como hoy). ¿Se mantiene por protección de
  datos? Basta con quitar la columna en `ranking.js` (la búsqueda por licencia seguiría funcionando).
- **SUPUESTO**: tipos de columna: `Posicion`, `NumLicencia`, `TorneosJugador`, `Torneos`, `Sube`, `JornadasPuesto`,
  `TipoRanking` enteros; `PuntosTotal`/`Puntos` numéricos (se redondean a entero; en la web pública siempre lo son);
  `SFEP_Ranking_Tipos.FechaAct` fecha o texto dd/mm/aaaa (`TRY_CONVERT(date, …, 103)`), `Jugadores`/`Pruebas`
  numéricos; `SFEP_Fichas.NombreCompleto` existe y está relleno (lo usaba `CargarRnkAbsNombre.asp`).
- **SUPUESTO**: `SFEP_Fichas` tiene una sola ficha por `LicenciaJugador` (si tuviera varias, el `INNER JOIN` del ASP,
  que se mantiene, duplicaría puestos).
- **SUPUESTO**: la intercalación de la BD distingue acentos; la búsqueda usa `COLLATE Latin1_General_CI_AI`.
- **SUPUESTO**: "Torneos/puntos totales" = todos los torneos del jugador en el periodo y "ranking" = los que cuentan
  (las 13 mejores puntuaciones), deducido de la normativa y de los datos (p. ej. 14 torneos totales, 13 de ranking).
- **SUPUESTO**: en el mock, `coincidencias` sin filtros = `jugadores` (la web pública no da el número de filas).
- **SUPUESTO**: ruta nueva `/ranking-absoluto` (no había fila en `redirecciones.csv`).

## Modelo SQL propuesto (normativa)

La clasificación ya está en BD. La normativa (hoy en `Contenidos/rankings/absoluto.json`) podría gestionarse así sin
cambiar el contrato:

```sql
CREATE TABLE SFEP_Rankings_Normativa (
    Clave        varchar(40)   NOT NULL PRIMARY KEY,      -- 'absoluto', 'menores'…
    Titulo       nvarchar(200) NOT NULL,
    Resumen      nvarchar(500) NULL
);
CREATE TABLE SFEP_Rankings_Normativa_Secciones (
    Clave        varchar(40)   NOT NULL REFERENCES SFEP_Rankings_Normativa (Clave),
    IdSeccion    varchar(60)   NOT NULL,                  -- ancla: 'valoracion-economica'
    Orden        int           NOT NULL,
    Titulo       nvarchar(200) NOT NULL,
    PRIMARY KEY (Clave, IdSeccion)
);
CREATE TABLE SFEP_Rankings_Normativa_Bloques (
    IdBloque     int IDENTITY  PRIMARY KEY,
    Clave        varchar(40)   NOT NULL,
    IdSeccion    varchar(60)   NOT NULL,
    Orden        int           NOT NULL,
    Tipo         varchar(10)   NOT NULL CHECK (Tipo IN ('parrafo', 'nota', 'lista', 'tabla')),
    Texto        nvarchar(max) NULL,                      -- parrafo / nota
    TablaTitulo  nvarchar(200) NULL,
    FOREIGN KEY (Clave, IdSeccion) REFERENCES SFEP_Rankings_Normativa_Secciones (Clave, IdSeccion)
);
CREATE TABLE SFEP_Rankings_Normativa_Elementos (       -- elementos de lista, columnas y celdas de tabla
    IdBloque     int           NOT NULL REFERENCES SFEP_Rankings_Normativa_Bloques (IdBloque),
    Fila         int           NOT NULL,                  -- lista: orden; tabla: 0 = cabecera, 1..n = filas
    Columna      int           NOT NULL,                  -- lista: 0 = destacado, 1 = texto; tabla: nº de columna
    Texto        nvarchar(500) NOT NULL,
    PRIMARY KEY (IdBloque, Fila, Columna)
);
```

Opcionalmente, para declarar los rankings en BD en lugar de en `ReglasClasificaciones.Rankings`:
`ALTER TABLE SFEP_Ranking_Tipos ADD ClaveWeb varchar(40) NULL, Genero char(1) NULL;` (`Categoria` y `Visible` ya
existen según `CargarRnkMen.asp`).

## Ficheros

- API: `Contracts/Competicion/ClasificacionRankingDto.cs`, `NormativaRankingDto.cs`, `ReglasClasificaciones.cs`;
  `Data/Repositories/Competicion/ClasificacionesRankingRepository.cs` (Dapper),
  `NormativasRankingRepository.cs` (JSON); `Controllers/Competicion/ClasificacionesRankingController.cs`;
  `Contenidos/rankings/absoluto.json`; registro en `Program.cs`; `genero` y `licencia` añadidos a `SetVaryByQuery`
  (`Infrastructure/ServiceCollectionExtensions.cs`); tests en `tests/FEP.Api.Tests/ClasificacionesRankingTests.cs`.
- Web: `wwwroot/ranking-absoluto.html`, `assets/js/ranking.js` (común a las páginas de ranking: clave en
  `<main data-ranking>`), estilos `.rkc-*` en la sección "Páginas interiores" de `assets/css/fep.css` (y su bloque
  responsive).
- Mock: `mock/competicion-rankings-absoluto.json` (instantánea de la web pública del 13/11/2026, top 100 de cada
  género), `mock/competicion-rankings-absoluto-normativa.json` (generado con `tools/generar-mock-rankings-normativa.ps1`)
  y `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`ranking_absoluto` → `ranking-absoluto.html`; así la tarjeta de
  `rankings.html` y cualquier enlace de la BD van a la página nueva); enlace "Absoluto · Masculino y femenino" del panel
  de ranking de `index.html`.
- Documentación: `docs/api/endpoints.md`, `docs/migracion/redirecciones.csv` (fila nueva), `docs/migracion/mapa-migracion.md`,
  fila de `tools/generar-mapa-web.ps1` (Desarrollado).
