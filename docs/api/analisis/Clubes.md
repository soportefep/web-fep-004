# Análisis: `Clubes.asp` → `clubes.html` + `GET /api/v1/clubes` y `GET /api/v1/clubes/filtros`

URL actual: `/Clubes` (regla `^Clubes` de `web.config`). Ruta nueva propuesta en `redirecciones.csv`:
`/clubes` → `clubes.html`. El formulario hacía `POST` a `Clubes` con `fed`, `provincia`, `tipo` y
`textobuscado`; la página nueva acepta `?fed=` y `?textobuscado=` además de los parámetros nuevos, para
no romper enlaces antiguos con query string.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` (+ columna de patrocinadores) |
| `sql_noticias` (`SFEP_Noticias`) | Se consultaba **pero no se usaba** | Eliminado |
| Título "CLUBES FEDERADOS" y recuadro de presentación | HTML a mano | `h1` "Clubes federados." + entradilla (mismo texto) |
| Desplegable Federación | `sql_fed`: `SFEP_Federaciones` (IdFederacion, Abreviatura) `WHERE IdFederacion<>0 AND IdFederacion<100 ORDER BY Abreviatura` | `GET /api/v1/clubes/filtros` → `federaciones` |
| Desplegable Provincia | `sql_prov`: con federación, `SFEP_Federaciones ⨝ SFEP_Provincias ON Comunidad = IdComunidad WHERE IdFederacion=<fed>`; sin ella, `SFEP_Provincias WHERE IdProvincia<>0`, por `Provincia` | `GET /api/v1/clubes/filtros` → `provincias` + `federaciones[].provincias` (el front acota el desplegable sin otra llamada) |
| Contador "Número de clubes encontrados" | Recorría el recordset entero para contar | `total` (`COUNT(*)`) |
| Tabla Club / Provincia / Localidad | `sql_clubes` (6 variantes): `SFEP_Clubes ⨝ SFEP_Clubes_Licencia ⨝ SFEP_Federaciones (Comunidad) ⨝ SFEP_Provincias`, `Año = year(now()) AND Renovado='S'` + filtro de federación **o** provincia + `Club LIKE '%término%'` por cada término · `ORDER BY Provincia, Localidad, Club` | `GET /api/v1/clubes` (paginado) |
| Enlace de cada celda | `Club.asp?id=<IdClub>` | Enlace a la ficha de la **web actual** (no migrada en este encargo) |

Columnas realmente pintadas: `Club`, `Provincia`, `Localidad` e `IdClub` (en el enlace). La página **no
escribe** en la BD.

## Parámetros (`Request`) y validación

| ASP | API nueva | Validación |
|---|---|---|
| `fed` (0 = "----") | `federacion` (opcional) | entero 1–99; otro valor → **400** `ValidationProblemDetails` |
| `provincia` (0 = "----") | `provincia` (opcional) | entero 1–999 → si no, 400 |
| `textobuscado` (espacios → `-`, partido por `-`) | `busqueda` (opcional) | máx. 100 caracteres y 8 términos (separados por espacios o guiones) → si no, 400 |
| `tipo` (`federacion`/`provincia`/`texto`: qué control cambió) | — | Se resuelve en el front: al cambiar de federación se vacía la provincia |
| — (no paginaba) | `pagina` (def. 1, máx. 1000) · `limite` (1–100, def. 50) | 400 fuera de rango |

Los no numéricos (`federacion=abc`) los rechaza el *model binding* de `[ApiController]` con el mismo 400.

## Lógica / visibilidad heredada

- **Sin filtros no se veía ningún club**: con `fed=0` y `provincia=0` la consulta filtraba
  `IdFederacion = 0` (la propia FEP, sin clubes). Las ramas "sin filtros" y "con federación" eran el mismo
  SQL copiado (error evidente). Al entrar en `/Clubes` la web actual dice "Número de clubes encontrados: 0".
  **Cambio**: la API sin filtros devuelve todos los clubes con licencia (paginados) y la página nueva los
  muestra al entrar (1.274 el 12/11/2026). **DECISIÓN FEP** si se prefiere volver a la tabla vacía.
- **La provincia mandaba sobre la federación**: si llegaban las dos, el ASP ignoraba la federación. Ahora
  se combinan (AND). En la práctica es igual, porque el desplegable solo ofrece provincias de la federación
  elegida y al cambiar de federación se vacía la provincia (como hacía `tipo=federacion`).
- **Búsqueda por texto sin federación**: el `INNER JOIN` con `SFEP_Federaciones` (sin filtrar por Id)
  exigía que la comunidad del club tuviera alguna federación; se mantiene con `EXISTS`.
- `activo=2`: opción de menú activa (plantilla). Funciones JS `MesAnterior`, `CargarEvento`, `MeGusta`: no se usan.
- La búsqueda por texto se lanzaba en `onBlur` del campo; ahora al enviar o tras una pausa al escribir.

## Problemas del original

1. **Inyección SQL**: `fed` y `provincia` se concatenaban **sin comillas** (`IdFederacion=" & federacion`):
   `limpiar()` (lista negra) no lo impide. El texto se concatenaba con `Replace("'", "''")`, pero `limpiar()`
   ya había cambiado `'` por `´`, así que buscar "L'Hospitalet" no encontraba nada. Ahora todo va con
   parámetros Dapper (`@Federacion`, `@Provincia`, `@T0…@T7`) y los comodines `%`, `_`, `[` del texto se escapan.
2. **Error 500** con valores no numéricos (`clng(federacion)` en el `<select>`). Ahora 400.
3. **Duplicados posibles**: los `INNER JOIN` con `SFEP_Clubes_Licencia` (varias filas del mismo club y año) y
   con `SFEP_Federaciones` (varias federaciones con la misma `Comunidad`) podían repetir clubes. Ahora `EXISTS`.
4. **Doble recorrido y sin paginar**: contaba recorriendo el recordset y lo volvía a recorrer para pintar
   (Cataluña: 379 filas en una sola tabla). Ahora `COUNT(*)` + `OFFSET/FETCH`, 50 por página.
5. **XSS**: `Club`, `Provincia` y `Localidad` se pintaban sin `Server.HTMLEncode`. El front usa `textContent`.
6. **Búsqueda sensible a tildes** (según la intercalación de la BD): ahora `COLLATE Latin1_General_CI_AI`
   (**SUPUESTO**: la BD distingue acentos; si ya fuera `_AI`, el `COLLATE` no cambia nada).
7. **`year(now())`**: se mantiene el año en curso (`TimeProvider`, hora local del servidor). Consecuencia
   heredada: a principios de año, hasta que los clubes renuevan, el listado sale casi vacío.
8. HTML: `<td>` sin cerrar en el mensaje de "no hay clubes"; tabla sin `caption` ni `th scope`.

## Mejoras respecto al original

- Clubes visibles al entrar, paginación accesible (Anterior/Siguiente, "Página X de Y"), contador en una
  región `role="status"` que anuncia los resultados.
- Criterios en la URL (`clubes.html?federacion=1&busqueda=club&pagina=2`): se pueden enlazar y compartir.
  `federaciones.html` enlaza a los clubes de cada federación.
- Índice lateral con acceso directo a cada federación (chips en móvil).
- Búsqueda sin distinguir mayúsculas ni tildes; los apóstrofos funcionan.
- Federaciones y provincias se muestran como nombre propio ("Com.Valenciana", "A Coruña") aunque la BD
  las guarde en mayúsculas; la API las devuelve tal cual.
- Respuestas cacheadas 5 min (output cache varía por `federacion`, `provincia`, `busqueda`, `pagina`, `limite`).
- Móvil: formulario en una columna y cada club en una fila apilada, sin scroll horizontal.

## Modo prototipo (mocks)

`mock/clubes.json` es una instantánea de la web pública del 12/11/2026 (los 1.274 clubes con licencia de
las 19 federaciones, rastreados con el propio buscador) y `mock/clubes-filtros.json` sus desplegables.
Como el mock no puede filtrar, trae todos los clubes en una sola "página" (`limite` = `total` = 1.274, sin
criterios). `clubes.js` compara los criterios que devuelve la respuesta con los pedidos: si no coinciden
(es el mock), filtra y pagina en el navegador. Con la API real no se filtra nada en el cliente.

Datos observados (útiles para pruebas): Melilla 0 clubes, Ceuta 1, Canarias 1, La Rioja 2; Cataluña 379.
Hay `IdClub` con ceros a la izquierda (`0104015`), por eso la API devuelve `id` como texto.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: mostrar todos los clubes al entrar (hoy la web enseña 0 por el error descrito).
- **DECISIÓN FEP**: el buscador público no filtra `SFEP_Clubes.Baja` ni `Borrado` (solo licencia renovada),
  ni excluye los clubes "NO ELEGIR" que sí excluye la API antigua `Clubes_Fed`. Se ha respetado el filtro
  del ASP. ¿Debe un club de baja con licencia renovada seguir saliendo?
- **SUPUESTO**: tipos de columnas (`IdFederacion`, `IdProvincia`, `SFEP_Clubes.Provincia`, `Comunidad`,
  `IdComunidad`, `[Año]` enteros; `Renovado` `char(1)`; `IdClub` texto, se lee con `CAST(... AS varchar(20))`).
- **SUPUESTO**: la intercalación de `SFEP_Clubes.Club` distingue acentos (ver problema 6).
- Canarias y Ceuta solo tienen 1 club y Melilla ninguno: ¿gestionan sus clubes fuera de este sistema? Si es
  así, convendría un aviso en la página.
- La ficha del club (`Club.asp`) ya tiene endpoint: `GET /api/v1/clubes/{id}` (`ClubDetalleDto`), que usa la app móvil; la web
  sigue enlazando a `Club.asp` hasta que se haga su página.
  **DECISIÓN FEP (RGPD)**: la ficha publica, como la web actual, nombre completo, número de licencia, género y
  categoría de cada jugador/a del club. ¿Se mantiene esa publicación o se reduce (p. ej. solo el número de licencias)?
- La ficha de la federación (`Datos_Federacion.asp`) ya está
  migrada a `federacion.html?id=` (enlaza a este buscador filtrado por su federación).

## Ficheros

- API: `Contracts/Clubes/ClubesDto.cs` (`ClubesPaginaDto`, `ClubResumenDto`, `ClubesFiltrosDto`…),
  `Contracts/Clubes/BusquedaClubes.cs` (criterios, límites, troceo del texto y escape de `LIKE`),
  `Data/Repositories/Clubes/ClubesRepository.cs` (Dapper), `Controllers/Clubes/ClubesController.cs`;
  registro en `Program.cs`; claves de output cache en `Infrastructure/ServiceCollectionExtensions.cs`;
  tests en `tests/FEP.Api.Tests/ClubesTests.cs` (validación 400 y criterios normalizados con un repositorio
  falso: no necesitan SQL Server).
- Web: `wwwroot/clubes.html`, `assets/js/clubes.js`, estilos `.clubs-*` en la sección "Páginas interiores"
  de `assets/css/fep.css` (y su bloque responsive).
- Mock: `mock/clubes.json`, `mock/clubes-filtros.json` (instantánea de la web pública; no salen de
  `Contenidos/`, por eso no tienen script generador).
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`clubes` → `clubes.html`).
