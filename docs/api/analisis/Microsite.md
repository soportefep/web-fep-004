# Análisis: `Microsite.asp` → `micrositio.html` + `GET /api/v1/micrositios/{id}` y `GET /api/v1/micrositios/{id}/opciones/{idOpcion}`

`Microsite.asp` es un mini-CMS: con `Id` (micrositio) e `IdOpcion` (apartado) pinta el menú del
micrositio y el contenido del apartado desde la BD. Lo gestiona `gestion/Microsites*.asp` (editor
CKEditor). URLs en uso en la web pública (12/11/2026): `/microsite.asp?id=7` (Homologaciones, desde el
menú principal), `/Microsite.asp?Id=7&IdOpcion=19` (banner "HOMOLOGACIONES" del pie) y
`/Microsite.asp?Id=24&IdOpcion=…` (Elecciones 2024, 72 apartados).

`redirecciones.csv` no tiene fila para `Microsite.asp` (se llama directamente, sin regla de `web.config`).
Ruta nueva: **`micrositio.html?id=7&opcion=19`** (acepta también `Id`/`IdOpcion`, sin distinguir
mayúsculas, como `Request()`). Redirección 301 propuesta: `/Microsite.asp?Id=X[&IdOpcion=Y]` →
`/micrositio?id=X[&opcion=Y]`, salvo los micrositios privados 25 y 26.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie | `Menus.inc`, `PiePagina.inc` (la barra de navegación superior de esta página está **vacía**: solo el menú lateral móvil) | Ya migrado: `comun.js` (+ columna de patrocinadores) |
| `sql_noticias` (`SFEP_Noticias`) | Se consultaba **pero no se usaba** | Eliminado |
| Título del micrositio | `sql_microsite`: `SELECT * FROM SFEP_Microsites WHERE IdMicrosite=<id>` → `Microsite` | `GET /api/v1/micrositios/{id}` → `titulo` (`h1`, miga, título de la pestaña) |
| Menú "OPCIONES" (barra lateral) | `BarraLateralMicrosite.inc` con `sql_menus`: `SFEP_Microsites_Menus WHERE IdMicrosite=<id> AND Borrado='N' AND Visible='S' ORDER BY Orden` → `IdMenu`, `Menu`; la opción activa en `<strong>` | `opciones` (índice lateral `page-toc`; la activa con `aria-current="page"`) |
| Opción por defecto | Sin `IdOpcion`: la primera de `rs_menus` | `opcionInicial` |
| Comprobación de la opción | `sql_menu`: misma tabla `AND IdMenu=<idmenu>` → `Tipo` | `GET …/opciones/{idOpcion}` (404 si no es del micrositio o no es visible) |
| Subtítulo y texto | `sql_post`: `SFEP_Microsites_Posts WHERE IdMicrosite=<id> AND IdMenu=<idmenu>` → `Subtitulo` (`H3`) y `Post` (HTML, pintado sin escapar) | `subtitulo` (`h2`) y `html` (saneado en el front, ver más abajo) |
| Archivos adjuntos | `sql_noticiaarchivos`: `SFEP_Microsites_Posts ⨝ SFEP_Microsites_Archivos ON IdPost WHERE IdMenu=<idmenu>` → `Archivo` (texto), `Link` (fichero), `Icono` (tipo) · enlace `Paginas/Microsites/<Link>` | `archivos` (`titulo`, `tipo`, `archivo`, `url` absoluta) → lista de documentos con tipo y botón Descargar/Abrir |
| Tipo `FORMULARIO` | Formulario "INSCRIPCIÓN AL SIMPOSIO" **escrito a mano en el ASP** (precios 170/150/85/105 €, DNI, datos, resguardo de pago) que envía a `Microsite_Inscripcion.asp` (escribe en `SFEP_Microsites_Inscripciones`) | **No se migra** (escribe en BD y sube ficheros): `tipo = "formulario"` → aviso con enlace al formulario de la web actual |
| "Patrocinadores FEP" (barra lateral) | Carrusel con 3 imágenes fijas (Head, CSD, Bullpadel) que enlazan a **`single.php`** (404, restos de WordPress) | No se migra: columna de patrocinadores común (`/api/v1/patrocinadores`) |
| "Cerrar Sesión" | Enlace a `Asamblea_CerrarSesion.asp` en **todos** los micrositios salvo `IdOpcion=110` (también en Homologaciones) | No se migra (solo tiene sentido en los micrositios privados, que siguen en la web actual) |
| Micrositio no visible | `Visible<>'S'` → "El MIcrosite no está visible aún." sin menú | `404` |

Columnas realmente pintadas: `Microsite`, `Visible`; `IdMenu`, `Menu`, `Tipo`; `Subtitulo`, `Post`;
`Archivo`, `Link`, `Icono`. La página **no escribe** en la BD (el formulario envía a otra página).

Includes: `conexionSQL.inc` (no se copia), `Menus.inc`, `BarraLateralMicrosite.inc` (leído en
`X:\TICS\WEB\include\`), `PiePagina.inc`, `finconexion.inc`. Esquema deducido también de
`gestion/Microsites*.asp` (altas y modificaciones).

## Parámetros (`Request`) y validación

| ASP | API nueva | Validación |
|---|---|---|
| `id` / `Id` (micrositio) | ruta `{id}` | entero ≥ 1; no numérico o < 1 → **400** `ValidationProblemDetails` (el ASP daba **500**: `cint(request("id"))`; comprobado con `Id=abc` e `Id=9999`) |
| `IdOpcion` (opcional) | ruta `{idOpcion}`; si falta, el front usa `opcionInicial` | entero ≥ 1 → si no, 400 |
| `accion` (formulario) | — | No se migra |
| Cookie `ControlAsambleaSFEP` (micrositios 25 y 26) | — | Micrositio privado → **403** `ProblemDetails` con `urlWebActual` (sin consultar la BD) |

`404` `ProblemDetails` (con `urlWebActual`) si el micrositio no existe, está borrado o no es visible, o si
la opción no existe, no es visible o es de otro micrositio.

## Lógica / visibilidad heredada

- **Micrositios privados**: si `id` es 25 o 26 y no hay cookie `ControlAsambleaSFEP`, el ASP redirige a la
  opción de acceso (176 / 189). Son asambleas con documentación para asambleístas (el 25,
  "Asamblea General Extraordinaria 22 Abril 2024", hoy además **no visible**). Hay restos comentados de
  la misma regla para el 20 (opción 138). **No se migra la parte privada**: la API responde 403, `comun.js`
  no traslada los enlaces a 25/26 (siguen yendo a la web actual) y `micrositio.html?id=25` muestra un
  aviso con enlace a la web actual. La lista está en `ReglasMicrositios.IdsPrivados` y en
  `MICROSITIOS_PRIVADOS` (`comun.js`).
- **Censos electorales** (micrositio 24, apartado 234 "Publicación Censos Iniciales"): el contenido es
  público, pero enlaza a la consulta de censos con identificación (`/CensoJugadores`, `/CensoTecnicos`,
  `/CensoArbitros`, `/AccesoClubes`, `/AccesoFed`, `/AccesoTAD`, `/AccesoCSD` → `Censo_*.asp`). Esos
  enlaces se mantienen hacia la web actual (no están en `PAGINAS_MIGRADAS`).
- **Micrositio borrado**: el ASP no filtraba `SFEP_Microsites.Borrado` (la gestión "elimina" con
  `Borrado='S'` sin tocar `Visible`). **Cambio**: la API exige `Borrado='N'`. **DECISIÓN FEP** (parece
  evidente).
- **Posts y archivos sin filtro**: el ASP no filtraba `SFEP_Microsites_Posts.Borrado/Visible` y la gestión
  inserta los posts de menú sin rellenar esas columnas (posible `NULL`): se respeta (sin filtro). Los
  archivos se borran con `DELETE`.
- **Varios posts por opción**: el ASP pintaba el primero (sin `ORDER BY`) y los archivos de todos. Se
  mantiene con `ORDER BY IdPost`.
- **Tipo**: `rtrim(Tipo)="NORMAL"` → texto + archivos; cualquier otro valor (`FORMULARIO`, o `NULL`) →
  formulario. Se mantiene (`normal` | `formulario`). Comentado: `Tipo="RNK"` → `Microsite_RnkU.asp`.
- **Micrositios tipo blog** (`SFEP_Microsites.Tipo=1`, posts con `IdMenu=0`): `Microsite.asp` nunca los
  pinta; no se exponen.
- **Menú de un nivel**: `SFEP_Microsites_Menus` no tiene jerarquía; el DTO es una lista plana por `Orden`
  (la gestión renumera `Orden` 1..n en cada alta/baja).
- `MicrositeNOVisible.asp` (vista previa de la gestión, sin filtro `Visible`) y `Paginas/Fap/microsite.asp`
  (micrositios de la Federación Aragonesa) no forman parte de este encargo.

## Problemas del original

1. **Inyección SQL**: `id` e `IdOpcion` se concatenaban en cinco consultas, sin comillas ni `limpiar()`.
   Ahora parámetros Dapper (`@Id`, `@IdOpcion`).
2. **Error 500** con `id` no numérico, inexistente o sin opciones visibles (`cint`, `rs_menus("IdMenu")`
   en EOF). Ahora 400/404, y un micrositio sin opciones devuelve `opciones: []`.
3. **XSS**: `Request("Id")` e `IdOpcion` se escribían sin codificar en `value` de los campos ocultos del
   formulario (XSS reflejado), y `Post` se pintaba tal cual. El front sanea el HTML (lista blanca) y todo
   texto de la API va por `textContent`.
4. **Enlaces web de adjuntos rotos**: la gestión guarda los adjuntos tipo web/YouTube/Facebook como URL
   completa (`Upload.Form("web1")`), pero el ASP anteponía siempre `Paginas/Microsites/`. La API respeta
   las URL http(s).
5. **Icono heredado**: `select case Icono` sin `case else`: un adjunto con icono `--` reutilizaba el icono
   del anterior. Ahora `tipo = null` y el front lo deduce de la extensión.
6. **Codificación mezclada**: la plantilla es Windows-1252 pero el contenido de la BD sale en UTF-8; en la
   web actual "El MIcrosite no est� visible a�n" (además, errata "MIcrosite"). Todo UTF-8 ahora.
7. **N+1 / consultas sobrantes**: 6 consultas por petición (una sin usar). Ahora una ida a la BD por
   endpoint (`QueryMultiple`).
8. Accesibilidad: iconos de adjuntos sin `alt`, menú sin indicación de página actual salvo negrita,
   tablas del contenido con cabeceras hechas con `<td>`.

## Saneado del HTML heredado (decisión técnica)

El `Post` es HTML de CKEditor guardado tal cual: documentos completos `<html><head><title><body>`,
`<span style>`/`<font>` anidados, `div style="display:none"` con marcas internas del editor
(`fiogf49gjkf0d`, `fjrigjwwe9r0…SFEP_Microsites_Posts:Post`), HTML mal cerrado (`<div style="dis<p>` en
Homologaciones › Pistas; `</span>` sin abrir en Introducción) y atributos de Gmail (`data-saferedirecturl`).

**La API lo devuelve sin tocar** (es el dato) y **el front lo sanea** en `micrositio.js`, sin dependencias:
`DOMParser` (no ejecuta scripts) + reconstrucción nodo a nodo con `document.createElement`; nunca `innerHTML`.

- **Etiquetas permitidas** (sin atributos salvo los indicados): `p`, `div`, `h3`, `h4`, `ul`, `ol[type]`,
  `li`, `a[href]`, `strong`, `em`, `b`, `i`, `u`, `sub`, `sup`, `br`, `hr`, `blockquote`, `table`,
  `caption`, `thead`, `tbody`, `tfoot`, `tr`, `th[scope]`, `td[colspan,rowspan]`,
  `img[src,alt,width,height]`, `iframe` (solo `youtube.com`/`youtube-nocookie.com/embed/…` y
  `calendar.google.com/calendar/embed`, con `title`, `loading="lazy"` y `sandbox`).
- **Renombradas**: `h1`/`h2` → `h3` y `h5`/`h6` → `h4` (el contenido cuelga del `h2` del apartado);
  `cite` → `em`; `address`/`center`/`pre` → `p`.
- **Eliminadas con su contenido**: `script`, `style`, `head`, `title`, `meta`, `link`, `object`, `embed`,
  `form` y controles, `svg`, `math`, `template`, `noscript`, `audio`/`video`…, y todo elemento oculto
  (`hidden`, `display:none`, `visibility:hidden`). Se quitan los textos que son marcas del editor.
- **Desenvueltas** (se queda el contenido): el resto (`span`, `font`, `html`, `body`, `section`…).
- **Atributos**: se descartan `style`, `class`, `align`, `on*`, `data-*`… Se pierden colores, tamaños,
  alineaciones y flotados del editor: la página usa la tipografía y los estilos de la web nueva
  (**DECISIÓN FEP** si algún contenido dependía del color/centrado).
- **Enlaces**: solo `http`, `https`, `mailto` o relativos; los relativos (`refs/...`, `Paginas/...`) se
  resuelven contra la web actual (`fep-legacy-base`); los de páginas ya migradas (incl. `Microsite.asp`)
  pasan a la página nueva (`attrsEnlace` de `comun.js`) y los de este mismo micrositio navegan sin
  recargar. `href="#"`/anclas y **`single.php`** (WordPress, 404) se convierten en texto (se informan en
  consola). Externos con `target="_blank" rel="noopener"` y aviso para lectores de pantalla.
- **Imágenes/iframes**: solo http(s); `http:` → `https:` (evita contenido mixto; los Flickr de Materiales
  y Pistas lo admiten). Imágenes `loading="lazy"`, ancho máximo 100 %.
- **Tablas** dentro de una región desplazable (`tabindex="0"`, sin scroll horizontal de la página); los
  `<td>` dentro de `<thead>` pasan a `<th scope="col">`.
- Se eliminan párrafos y elementos vacíos (`&nbsp;`) que el editor dejaba en abundancia.

Recomendación: cuando la gestión se migre, sanear también al guardar (servidor, p. ej. con una librería
de lista blanca) para que la BD deje de contener HTML arbitrario.

## Mejoras respecto al original

- Una sola página para todos los micrositios, con el índice lateral = menú del micrositio; navegación
  entre apartados sin recargar (`history.pushState`; atrás/adelante y enlaces directos funcionan; foco
  al título del apartado). Más de 12 apartados → el resto plegado ("Ver N apartados más"; Elecciones
  2024 tiene 72), abierto automáticamente si el activo está ahí.
- Adjuntos como lista de documentos con tipo ("Documento PDF", "Página web"…) y botón
  Descargar/Abrir; más de 8 → plegados. URLs codificadas (espacios, tildes, "º": comprobados `200`).
- `comun.js` traslada `Microsite.asp?Id=7&IdOpcion=19` → `micrositio.html?id=7&opcion=19` en el menú
  (`microsite.asp?id=7`), banners y contenidos de la BD (nuevo `QUERY_MIGRADA` en `paginaMigrada`).
- Cada apartado enlaza a su equivalente en la web actual; errores (sin id, privado, inexistente, fallo de
  red) con enlace a la web actual.
- Respuestas cacheadas 5 min (output cache por ruta).

## Correcciones de datos

No se modifica el contenido de la BD (lo gestiona la FEP). Correcciones de presentación:

| Original | Migrado | Motivo |
|---|---|---|
| Marcas `fiogf49gjkf0d` / `SFEP_Microsites_Posts:Post` (ocultas o visibles por HTML roto) | Eliminadas | Restos internos del editor |
| Enlaces web adjuntos con `Paginas/Microsites/` delante | URL tal cual | Enlaces rotos en el ASP |
| Icono de adjunto `--` = icono del anterior | Tipo deducido de la extensión | Error del `select case` |
| Imágenes `http://` (Flickr, `padelfederacion.es/Paginas/images/…`) | `https://` | Contenido mixto |
| Títulos de opción con espacios/tabuladores finales ("…Asamblea General\t") | Recortados | `rtrim` |

## Comprobación de enlaces (12/11/2026, `Invoke-WebRequest -Method Head`)

- `200`: `Paginas/Microsites/censo inicial.pdf`, `Paginas/Microsites/Acta nº 40 PROCLAMACIÓN PRESIDENTE
  PÁDEL_.pdf` (codificado), `Paginas/docs/REGLAMENTOJUEGO2010.pdf` y las 18 imágenes de la tabla de
  Homologaciones (incluidas las de `googleusercontent.com`).
- `404`: `/single.php` (carrusel de la barra lateral del micrositio; no se migra).

## Modo prototipo (mocks)

Instantánea de la web pública del 12/11/2026 (no salen de `Contenidos/`, por eso no tienen script
generador): `mock/micrositios-7.json` (Homologaciones, 8 apartados) con los 8 apartados
`micrositios-7-opciones-{30,24,19,26,20,25,23,31}.json`, y `mock/micrositios-24.json` (Elecciones 2024,
72 apartados) con `micrositios-24-opciones-315.json` y `-234.json` (con PDF adjuntos). El `html` es el de
la BD tal cual (sin sanear) para probar el saneado. `orden` = posición en el menú (la gestión renumera
1..n); `actualizado` = `null` (no visible en la web). Los demás apartados del 24 no tienen mock: en modo
prototipo muestran el error con enlace a la web actual.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: filtrar `SFEP_Microsites.Borrado='N'` (el ASP no lo hacía).
- **DECISIÓN FEP**: los micrositios privados (25, 26) siguen en la web actual (403 en la API). ¿Hay más
  micrositios que deban ser privados? Hoy la lista está fija en el código, como en el ASP; mejor una
  columna (ver modelo SQL).
- **DECISIÓN FEP**: el formulario "INSCRIPCIÓN AL SIMPOSIO" (tipo `FORMULARIO`) tiene precios y texto
  LOPD (Ley 15/1999, derogada) escritos en el código. ¿Sigue en uso? Si sí, migrarlo como formulario
  propio (con escritura en BD y subida de ficheros) en otra fase.
- **DECISIÓN FEP**: el saneado elimina colores, centrados y flotados del editor.
- Homologaciones › Empresas constructoras dice "El siguiente documento contiene el borrador del contrato
  para este reconocimeinto" (errata incluida) pero **no tiene adjunto**.
- Homologaciones › Preguntas frecuentes enlaza al Reglamento de Juego **de 2010** y tiene la errata
  "Reglemento". Tarifas: "Presupuesto IBVA" (¿IBV?) y "Más de 4 pistas hasta 8: por cada pista adicional
  1.500 €" (¿150 €?). No se han corregido (contenido de la BD).
- Imágenes de Homologaciones enlazadas desde `ci3.googleusercontent.com` (proxy de Gmail) y
  `lh4.googleusercontent.com`: pueden dejar de funcionar; conviene subirlas a `refs/images/`.
- El enlace "Cerrar Sesión" aparecía en todos los micrositios: se ha retirado.
- **SUPUESTO**: tipos de columnas (`IdMicrosite`, `IdMenu`, `IdPost`, `IdArchivo`, `Orden` enteros;
  `FechaUlt` datetime; `Tipo`/`Icono` char/varchar; `Post` texto largo).
- **SUPUESTO**: la web actual está en la raíz del dominio de `Media:BaseUrl` (para `Paginas/Microsites/` y
  `urlWebActual`).
- Añadir la redirección 301 `/Microsite.asp` → `/micrositio` (con traslado de `Id`/`IdOpcion`) a
  `redirecciones.csv` / `web.config` al publicar.

## Modelo SQL propuesto

El contenido ya está en BD; no hace falta ninguna tabla nueva. Para no fijar en código los micrositios
privados y acelerar las consultas:

```sql
ALTER TABLE SFEP_Microsites ADD Privado char(1) NOT NULL CONSTRAINT DF_SFEP_Microsites_Privado DEFAULT 'N';
UPDATE SFEP_Microsites SET Privado = 'S' WHERE IdMicrosite IN (25, 26);
CREATE INDEX IX_SFEP_Microsites_Menus_Micrositio ON SFEP_Microsites_Menus (IdMicrosite, Borrado, Visible, Orden);
CREATE INDEX IX_SFEP_Microsites_Posts_Menu ON SFEP_Microsites_Posts (IdMicrosite, IdMenu);
CREATE INDEX IX_SFEP_Microsites_Archivos_Post ON SFEP_Microsites_Archivos (IdPost);
```

Con `Privado`, el repositorio filtraría `m.Privado = 'N'` (o devolvería 403) sin cambiar el contrato.

## Ficheros

- API: `Contracts/Micrositios/MicrositioDto.cs` (`MicrositioDto`, `OpcionMicrositioDto`,
  `OpcionMicrositioContenidoDto`, `ArchivoMicrositioDto`, `ReglasMicrositios`),
  `Data/Repositories/Micrositios/MicrositiosRepository.cs` (Dapper), `Controllers/Micrositios/MicrositiosController.cs`,
  `Infrastructure/WebActual.cs` (raíz de la web actual a partir de `Media:BaseUrl`); registro en `Program.cs`;
  tests en `tests/FEP.Api.Tests/MicrositiosTests.cs` (400, 403 sin BD, 404 y 200 con repositorio falso,
  tipos y URLs de adjuntos).
- Web: `wwwroot/micrositio.html`, `assets/js/micrositio.js` (carga, navegación y saneado), estilos `.ms-*`
  en la sección "Páginas interiores" de `assets/css/fep.css` (y su bloque responsive).
- Mock: `mock/micrositios-7*.json`, `mock/micrositios-24*.json`; `mock/mocks.js` regenerado.
- Común: `assets/js/comun.js` (`PAGINAS_MIGRADAS['microsite']`, `QUERY_MIGRADA`, `MICROSITIOS_PRIVADOS`
  y `paginaMigrada` con traslado de parámetros).
- Mapa: `tools/generar-mapa-web.ps1` (filas de Homologaciones → Desarrollado; Asamblea/Censos → En proceso).
