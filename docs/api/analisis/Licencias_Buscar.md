# Análisis: `Licencias_Buscar.asp` → `buscar-licencia.html` + `GET /api/v1/licencias`

Por petición del usuario (19/11/2026), la página nueva **une "Buscar licencia" y "Consulta tu licencia"** (menú
Licencias: `SFEP_Web_Menu` id 42 `Licencias_Buscar` e id 41 "Tu licencia / Modificación" → `Consulta_Tu_Licencia`,
reescrita a `Licencias_TuLicencia.asp`). Cada función es una sección del índice lateral: `#buscar` (con `#resultados`) y
`#consulta`. Ver el apartado "Consulta tu licencia (`Licencias_TuLicencia.asp`)".

URL actual: `/Licencias_Buscar` (menú "Licencias › Buscar Licencia", `SFEP_Web_Menu` id 42; también el enlace "Buscar mi
número de licencia" de las fichas de silla de ruedas). El formulario hacía `POST` a la propia página con `accion=alta`,
`tipo`, `fed`, `dianac`, `mesnac`, `anonac`, `jugador` y `busqueda` (el texto con los espacios cambiados por `-`).
**No hay fila en `docs/migracion/redirecciones.csv`**: se propone `/buscar-licencia` → `buscar-licencia.html`.

Fichero en Windows-1252, idéntico al de `X:\TICS\WEB\Licencias_Buscar.asp` (hay además un `Licencias_Buscar2.asp` de 2015
que no enlaza nadie y no se migra). Includes: `conexionSQL.inc` (conexión; no se copia nada), `Menus.inc` (menú, redes y
`limpiar()`), `BarraLateral.inc`, `PiePagina.inc`, `finconexion.inc` (comunes, ya migrados en `comun.js`). Todos
disponibles en `X:\TICS\WEB\include\`.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie, barra lateral | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` + columna de patrocinadores |
| `sql_noticias` (`SFEP_Noticias` destacadas) | Se consultaba **pero no se usaba** | Eliminado |
| Título "busca tu licencia" y recuadro azul ("En este formulario puedes conseguir el número de tu licencia…") | HTML escrito a mano | `h1` "Busca y consulta tu licencia." + entradilla (mismo texto y una frase sobre la consulta) |
| Desplegable "Tipo Licencia" (JUGADOR, TÉCNICO, JUEZ-ÁRBITRO, SILLA RUEDAS) | HTML a mano (`J`, `T`, `A`, `S`) | `tipo` (`jugador`, `tecnico`, `arbitro`, `silla-ruedas`; la API acepta también las letras) |
| Desplegable "Federación" | `sql_federaciones`: `SFEP_Federaciones` (`Federacion`, `IdFederacion`) `WHERE IdFederacion<>0 AND IdFederacion<100 ORDER BY Federacion` (19 federaciones en la web pública) | Reutiliza `GET /api/v1/federacion/federaciones-autonomicas` (nombre oficial, sin la FEP, por nombre) |
| Día, mes y año de nacimiento | HTML a mano (años de `year(now())` a 1930) | Tres desplegables (años hasta 1920) → `fechaNacimiento` `aaaa-mm-dd` |
| "Nombre o apellido" | `jugador` → `busqueda` (espacios → `-`), `ucase`, partido por `-` | `busqueda` (términos por espacios o guiones, máx. 6) |
| Validación (`comprobar()`) | Federación obligatoria en `J`; fecha o texto; si no hay texto, día y mes (el año no) | Misma validación en el navegador y en la API (fecha completa y válida) |
| Listado "Listado de registros encontrados en la BBDD FEP - (N)" | `sql_jugador`: 12 variantes (4 tipos × "solo nombre", "solo fecha", "nombre y fecha"); contaba recorriendo el recordset | `GET /api/v1/licencias` → `total` + `items` (máx. 100) |
| Tabla Num Licencia / Primer Apellido / Segundo Apellido / Nombre | `lic`, `Apellido1`, `Apellido2`, `Nombre`; en `J` enlace a `Estadisticas.asp?Id={lic}`; en `S`, `"PS" & lic` | `items[]` (`licencia`, `apellido1`, `apellido2`, `nombre`, `estadisticasUrl`); el enlace va a `estadisticas.html?licencia=` (migrada) |
| Mensaje rojo "Lo sentimos pero en nuestra base de datos no se encuentra ninguna licencia…" + "REINTENTAR" | HTML a mano | Aviso "No hemos encontrado ninguna licencia con esos datos" con consejos; el formulario sigue visible |
| "Se está realizando la búsqueda… Espere unos segundos" | HTML + `Inicio()` | Estado `role="status"` y skeleton |

Consultas por tipo (todas con `NombreCompleto LIKE '%término%'` por cada término y/o `FechaNac = 'd/m/aaaa'`, y
`ORDER BY Apellido1, Apellido2`):

| Tipo | Tablas y filtros fijos | Columna `lic` |
|---|---|---|
| `J` jugador/a | `SFEP_Fichas` ⨝ `JugadorUltimoAno` (`LicenciaJugador`) ⨝ `SFEP_Licencia` (`IdLicencia`, `Año = Ano`, `Tipo`) · `SFEP_Licencia.Federacion = fed` | `LicenciaJugador` |
| `T` técnico/a | `SFEP_Fichas` · `LicenciaTecnico IS NOT NULL` (sin temporada ni federación) | `LicenciaTecnico` |
| `A` juez-árbitro | `SFEP_Fichas` · `LicenciaArbitro IS NOT NULL` (sin temporada ni federación) | `LicenciaArbitro` |
| `S` silla de ruedas | `SFEP_Fichas` ⨝ `SillaUltimoAno` ⨝ `SFEP_Licencia` · `Federacion = fed` (uniones distintas en cada variante, ver Problemas) | `LicenciaSilla` |

Columnas realmente pintadas: licencia, `Apellido1`, `Apellido2`, `Nombre`. El ASP leía además `FechaNac`, `Genero`,
`NombreCompleto` y `SFEP_Licencia.Federacion`, que **no se pintaban** y no se publican. La página **no escribe** en la BD.

## Parámetros y validación

| ASP | API nueva | Validación |
|---|---|---|
| `tipo` (`J`/`T`/`A`/`S`) | `tipo` (def. `jugador`) | `jugador`\|`tecnico`\|`arbitro`\|`silla-ruedas` o `J`/`T`/`A`/`S`; otro → **400** |
| `fed` (0 = "--") | `federacion` | Jugador/a: obligatoria, 1–22. Silla: opcional, 1–22. Técnico/a y juez-árbitro: no admitida (400) |
| `jugador` / `busqueda` | `busqueda` | 2–100 caracteres, máx. 6 términos (espacios o guiones) |
| `dianac`, `mesnac`, `anonac` | `fechaNacimiento` | `aaaa-mm-dd`, fecha real, desde 1900 hasta hoy (hora de Madrid) |
| `accion=alta` | — | Hace falta `busqueda`, `fechaNacimiento` o ambos (400 si no) |

Respuesta sin caché (`Cache-Control: no-store`; sin output cache) y con límite de búsquedas por IP (30 por minuto y 300
por hora; `429` con `Retry-After`).

## Lógica / visibilidad heredada

- **Técnicos/as y jueces-árbitros sin temporada**: se buscaba cualquier ficha con número de licencia de técnico o árbitro,
  esté o no en vigor, y sin federación (el desplegable se ocultaba). Se mantiene.
- **Jugador/a y silla de ruedas, solo la última temporada**: las vistas `JugadorUltimoAno` y `SillaUltimoAno` dan la última
  licencia de cada persona; la federación es la que la expidió esa temporada. Se mantiene.
- `cambiatipo()` no trataba `S`: al pasar de técnico a silla de ruedas la federación seguía oculta y se enviaba `fed=0`
  (`Federacion = 0`: sin resultados). En la página nueva la federación se muestra para silla de ruedas y es opcional.
- El aviso rojo decía "licencia de **jugador/a**" para cualquier tipo. Ahora es neutro.
- Funciones `busqueda()`, `MesAnterior`, `CargarEvento`, `MeGusta`, `cambiar()` y `onlyNumbersDano` no se usaban.
- `activo=4`: opción de menú activa (plantilla).
- El texto se convertía a mayúsculas (`ucase`) y se partía por `-`; `limpiar()` (lista negra de `Menus.inc`) quitaba antes
  `--`, `/`, `+`, `,`, `=`, " AND ", " OR "… y cambiaba `'` por `´`, así que un apellido con apóstrofo (D'Angelo) no se
  encontraba nunca (comprobado: 0 resultados).

## Problemas del original

1. **Inyección SQL**: `fed` se concatenaba sin comillas (`Federacion=" & fed`) y el texto y la fecha entre comillas;
   `limpiar()` es una lista negra. `fed=abc` da **error 500** en la web actual. Ahora todo va con parámetros Dapper
   (`@Federacion`, `@T0…@T5`, `@FechaNacimiento`) y los comodines `%`, `_`, `[` se escapan.
2. **Fecha comparada como texto** (`FechaNac='15/3/1980'`): depende del idioma del servidor y, con una fecha imposible
   (día o mes a 0 con un año elegido, que `comprobar()` dejaba pasar si había texto), da **error 500** (comprobado con
   `0/0/1975`). Ahora fecha completa validada y parámetro `DateTime`.
3. **Silla de ruedas roto**: la variante "solo nombre" unía `SFEP_Fichas.LicenciaJugador = SillaUltimoAno.LicenciaSilla`
   (otra numeración; en la web pública buscar "PEREZ SOUTO", número 1 del ranking de silla, da 0 resultados con las 20
   federaciones) y "nombre y fecha" usaba `SillaUltimoAno.LicenciaJugador` (**error 500**, comprobado). Las tres usan
   ahora la unión de la variante "solo fecha" (`LicenciaSilla` en los tres lados).
4. **Sin límite de resultados**: buscar una letra listaba miles de personas con su número de licencia, recorriendo el
   recordset dos veces. Ahora `COUNT(*)` + `TOP (100)` y un aviso para afinar la búsqueda (DECISIÓN FEP abajo).
5. **Búsqueda sensible a tildes**: "GARCÍA" no encuentra "GARCIA" (comprobado). Ahora `COLLATE Latin1_General_CI_AI`.
6. **XSS**: licencia, apellidos y nombre se pintaban sin `Server.HTMLEncode`. El front usa `textContent`.
7. HTML: opción "--" duplicada en el mes ("--" y "-- Mes --", ambas con valor 0), `<meta charset>` iso-8859-1 al final del
   `<head>`, tabla sin `caption` ni `th scope`, enlaces `href="#"` vacíos, jQuery 1.10.2 de un CDN.

## Protección de datos

- La API publica **solo lo que pintaba el ASP**: número de licencia, apellidos y nombre. No devuelve fecha de nacimiento,
  género, federación, DNI, email ni teléfono. La fecha de nacimiento es un criterio: la respuesta solo dice
  `conFechaNacimiento: true/false`.
- La fecha de nacimiento viaja en la URL de la API (`GET`): la respuesta lleva `Cache-Control: no-store` y no pasa por la
  caché de salida. La página no la guarda en su propia URL ni en el historial.
- Límite de 100 resultados y de búsquedas por IP para que no se pueda recorrer la base de datos con un programa (el ASP
  no tenía ninguna de las dos cosas).

## Mejoras respecto al original

- SQL parametrizado en una ida a la BD (recuento + primeras 100), sin errores 500 con datos no válidos (400 con el campo).
- Búsqueda sin distinguir mayúsculas ni tildes; los apóstrofos y los guiones del texto funcionan.
- Silla de ruedas funciona en las tres variantes y con federación opcional.
- Formulario y resultados en la misma página (sin "REINTENTAR"): se puede corregir y volver a buscar; validación con
  mensajes junto al campo (`role="alert"`, `aria-invalid`) en lugar de ventanas `apprise`.
- El número de licencia de jugador/a enlaza a `estadisticas.html?licencia=` (migrada) en lugar de `Estadisticas.asp`.
- Apellidos y nombre en tipo título solo en el front ("Garcia de Vinuesa"), como en estadísticas y rankings; la API los
  devuelve como están en la BD (recortados: la BD tiene espacios al final, p. ej. "BELETA ").
- `comun.js` lleva `/Licencias_Buscar` (menú, BD, fichas de silla) a `buscar-licencia.html`; `?tipo=` preselecciona el tipo.
- Tabla accesible (`caption`, `th scope`) que en móvil pasa a fichas con etiquetas, sin scroll horizontal.

## Consulta tu licencia (`Licencias_TuLicencia.asp`)

URL actual `/Consulta_Tu_Licencia` (regla de `web.config` → `Licencias_TuLicencia.asp`, Windows-1252, leído en
`X:\TICS\WEB`, solo lectura; mismos includes que `Licencias_Buscar.asp`). Es un **acceso con número de licencia y clave**
al área privada del federado/a:

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| "MODIFICA tu licencia" + recuadro explicativo (clave única por licencia; consultar la ficha, cambiar la clave, el envío de comunicaciones y el club de competición nacional; descargar el carné; pedir la clave la primera vez en `Licencias_SolicitaClave.asp`; enlace al buscador) | HTML a mano | Sección `#consulta` de `buscar-licencia.html` (mismo texto, estilo de frase) |
| Formulario licencia (máx. 6 cifras; "en silla, solo el número") + clave → `POST` a `Consulta_Tu_Licencia` | `sql_ident`: `SFEP_Fichas` por `LicenciaJugador`/`LicenciaTecnico`/`LicenciaArbitro`/`LicenciaSilla` = licencia **y** `ClaveMod` = clave; si coincide, cookie `SFEP_TuLicencia` = `IdFicha`, `UPDATE SFEP_Fichas SET Pass = <aleatorio>` y redirección a `Licencias_TuLicenciaOK.asp`; si no, a `Licencias_TuLicenciaError.asp` | **No se migra**: botón "Acceder con mi licencia y clave" a `/Consulta_Tu_Licencia` de la web actual (no se ha inventado un login) |
| "Solicita tu clave / No recuerdo la clave" | Enlace a `Licencias_SolicitaClave.asp` | Mismo enlace (web actual) |
| Aviso amarillo "acceder desde un dispositivo de confianza" y texto de cookies | HTML a mano | Nota bajo el botón; el texto de cookies no se migra (el acceso sigue en la web actual) |
| `sql_noticias` | No se usaba | Eliminado |

`Licencias_TuLicenciaOK.asp` (la ficha privada: datos personales, licencias, club nacional, carné, cambio de clave) y
`Licencias_TuLicenciaError.asp`, `…CerrarSesion.asp`, `…Act.asp` siguen en la web actual; se migrarán con "Mi Portal" (área
privada con JWT, fase 4). Riesgos observados que lo justifican, sin copiar nada sensible:

- **Inyección SQL en el login**: la licencia se concatena sin comillas en `sql_ident` y la clave entre comillas (solo
  `limpiar()`).
- **Claves en claro** en `SFEP_Fichas.ClaveMod` (ya señalado en el mapa: "Contraseñas en claro en BD").
- **Sesión falsificable**: `Licencias_TuLicenciaOK.asp` solo comprueba que la cookie `SFEP_TuLicencia` no esté vacía y la
  usa como `IdFicha` (concatenada en el SQL, también en un `UPDATE SFEP_Licencia`): cambiando la cookie se vería la ficha
  de otra persona. Por eso no se enlaza a la ficha privada directamente ni se replica el mecanismo.

En `comun.js`, `Consulta_Tu_Licencia` y `Licencias_TuLicencia(.asp)` llevan a `buscar-licencia.html#consulta`, y el menú
Licencias tiene una sola entrada, "Buscar / consulta tu licencia" (también en `mock/menu.json`: se quita la id 41 y la 42
pasa a orden 1).

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "busca tu licencia" (título en minúsculas) y "MODIFICA tu licencia" (`Licencias_TuLicencia.asp`) | "Busca y consulta tu licencia." (h1 de la página unida) y "Consulta y modifica tu licencia" (sección) | Estilo de las páginas interiores y unión de las dos páginas |
| "JUEZ-ÁRBITRO", "SILLA RUEDAS", "TÉCNICO", "JUGADOR" | "Juez-árbitro", "Pádel en silla de ruedas", "Técnico/a", "Jugador/a" | Estilo de frase y lenguaje inclusivo, como el resto de la web nueva |
| "…ninguna licencia de jugador/a que corresponda…" (para cualquier tipo) | "No hemos encontrado ninguna licencia con esos datos." | El texto no valía para técnicos, árbitros ni silla |
| "Selecciona a que Federación corresponde tu licencia." | "Selecciona la federación que expidió tu licencia." | Errata ("a que" sin tilde) |
| Nombres de federación en mayúsculas de la BD ("FEDERACION MADRILEÑA DE PADEL") | Nombre oficial de `Contenidos/federaciones-autonomicas.json` ("Federación Madrileña de Pádel") | Mismo origen que el resto de páginas migradas |
| Años de nacimiento desde 1930 | Desde 1920 (la API admite desde 1900) | Federados/as de más de 96 años no podían buscar por fecha |

## Comprobación de enlaces y de la web actual (19/11/2026)

- `https://www.padelfederacion.es/Licencias_Buscar` (GET y POST), `Portal-del-Federado`, `Registro_Nacional_Arbitros`,
  `Registro_Nacional_Tecnicos`, `Estadisticas?id=12762` y `?id=3889`: 200.
- POST `tipo=J&fed=1&busqueda=MATADOR-GARCIA`: 1 resultado (12762). `tipo=J&fed=2&busqueda=SANMARTI-BELETA`: 2 resultados.
- POST `tipo=S`, "PEREZ SOUTO", sin fecha, con cada una de las 20 federaciones (y `0`): 0 resultados. Con nombre y fecha:
  **500**. `tipo=J&fed=abc`: **500**. `tipo=J` con `anonac=1975` y día y mes a 0: **500**. "D'ANGELO" y "MATADOR GARCÍA": 0.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: búsqueda solo por fecha de nacimiento. El ASP permitía, sin identificarse, listar a todas las personas
  con licencia nacidas un día concreto (de una federación en jugador/a; de toda España en técnico/a y juez-árbitro), lo que
  revela indirectamente su fecha de nacimiento. Se ha mantenido (es la forma de encontrar la licencia propia sin
  recordar cómo está escrito el nombre) con el límite de 100 resultados y de búsquedas por IP. ¿Se mantiene, se exige
  también el nombre o se pasa al área privada (Mi Portal)? Relacionado con P0453 (búsqueda por DNI en estadísticas).
- **DECISIÓN FEP**: límite de 100 resultados por búsqueda (el ASP los listaba todos) y de 30 búsquedas por minuto y 300 por
  hora por IP (`ReglasLicencias.ResultadosMaximos`, `LimiteBusquedaLicencias`). ¿Son adecuados?
- **DECISIÓN FEP**: la fecha de nacimiento viaja en la URL de la API (`GET`, sin caché). Si se prefiere que no quede en los
  registros del servidor, se puede pasar a `POST` sin cambiar los datos de la respuesta.
- **DECISIÓN FEP**: licencias de técnico/a y de juez-árbitro sin temporada: el ASP (y la API) listan cualquier ficha con
  número de licencia de técnico o árbitro, aunque no la haya renovado. ¿Se limita a las licencias en vigor (como en
  jugador/a), para que no parezca que una licencia caducada está vigente?
- **DECISIÓN FEP**: silla de ruedas con la federación opcional (sin ella, se busca en todas). En el ASP el desplegable se
  mostraba pero no era obligatorio, y con "--" buscaba `Federacion = 0`. ¿Las licencias de silla las expide la FEP
  (`Federacion = 0`) o la federación autonómica?
- **DECISIÓN FEP**: las fichas marcadas como fallecido (`SFEP_Fichas.Fallecido = 'S'`) salen en el buscador, como en el
  ASP (las estadísticas sí las excluyen). ¿Se excluyen?
- **DECISIÓN FEP**: apellidos y nombre en tipo título solo en el front, como en estadísticas y rankings; mismo criterio
  pendiente que P0123.
- **SUPUESTO**: `JugadorUltimoAno` (`LicenciaJugador`, `Ano`, `Tipo`) y `SillaUltimoAno` (`LicenciaSilla`, `Ano`, `Tipo`)
  son vistas o tablas con una fila por licencia y su última temporada, comparables con `SFEP_Licencia.IdLicencia`, `[Año]`
  y `Tipo`, como las usaba el ASP; `SFEP_Licencia.Federacion` es el `IdFederacion` (entero) que expidió la licencia.
- **SUPUESTO**: `SFEP_Fichas.FechaNac` es `datetime` (en la web actual una fecha imposible da error 500 de conversión); se
  compara con `TRY_CONVERT(date, FechaNac, 103)`, que admitiría también texto dd/mm/aaaa (mismo supuesto que P0461).
  `NombreCompleto` existe y está relleno (mismo supuesto que P0315).
- **SUPUESTO**: `LicenciaTecnico`, `LicenciaArbitro` y `LicenciaSilla` son numéricas o texto corto (se leen con
  `CAST(... AS varchar(20))`); una licencia de técnico o árbitro vacía pero no nula saldría sin número y se descarta.
- **SUPUESTO**: la intercalación de `SFEP_Fichas.NombreCompleto` distingue acentos (como en el buscador de clubes); el
  `COLLATE Latin1_General_CI_AI` no cambia nada si ya no los distinguiera.
- **SUPUESTO**: el mock (`mock/licencias.json`) solo trae dos licencias de jugador/a reales de la web pública (12762 y
  3889, ya publicadas en los rankings y en los mocks de estadísticas); se ha omitido a otra persona que devolvía la misma
  búsqueda. En modo prototipo no se puede filtrar por federación ni por fecha.
- **DECISIÓN FEP**: unir "Buscar licencia" y "Tu licencia / Modificación" en una sola página y una sola entrada del menú
  Licencias con el texto "Buscar / consulta tu licencia" (antes "Buscar Licencia" y "Tu licencia / Modificación").
  ¿Es correcto el texto?
- **DECISIÓN FEP**: el acceso con número de licencia y clave (`Licencias_TuLicencia.asp`) no se migra a la API pública: la
  sección "Consulta tu licencia" explica el servicio y enlaza a la web actual hasta que exista "Mi Portal" (área privada
  con identificación segura). El login actual concatena la licencia en el SQL, guarda las claves en claro y la ficha
  privada se abre con una cookie que solo contiene el `IdFicha` (falsificable). ¿Se prioriza migrar Mi Portal o, mientras
  tanto, corregir esos tres fallos en la web actual?
- Activar el 301 `/Licencias_Buscar` → `/buscar-licencia` (añadir la fila a `redirecciones.csv`). `/Consulta_Tu_Licencia`
  debe seguir en la web actual hasta que se migre el área privada (no redirigir). El enlace "Buscar mi
  número de licencia" de `campeonato-silla.js` sigue apuntando a la web actual (no pasa por `paginaMigrada`).
- `Licencias_Buscar2.asp` (2015, sin enlaces): no se migra; se puede retirar.

## Modelo SQL propuesto

No hace falta contenido nuevo: todo sale de la BD. Índices que aprovechan las consultas de la API (si no existen):

```sql
CREATE INDEX IX_Fichas_Apellidos ON SFEP_Fichas (Apellido1, Apellido2, Nombre)
    INCLUDE (NombreCompleto, FechaNac, LicenciaJugador, LicenciaTecnico, LicenciaArbitro, LicenciaSilla);
CREATE INDEX IX_Licencia_Federacion ON SFEP_Licencia (Federacion, Tipo, [Año]) INCLUDE (IdLicencia);
```

Si la FEP decide limitar técnicos/as y jueces-árbitros a las licencias en vigor, bastaría con vistas `TecnicoUltimoAno` y
`ArbitroUltimoAno` como las de jugador/a y silla, sin cambiar el contrato.

## Ficheros

- API: `Contracts/Licencias/BusquedaLicencias.cs` (`TipoLicencia`, criterios y `ReglasLicencias`),
  `Contracts/Licencias/LicenciasBusquedaDto.cs`, `Data/Repositories/Licencias/LicenciasRepository.cs` (Dapper),
  `Controllers/Licencias/LicenciasController.cs`, `Infrastructure/LimiteBusquedaLicencias.cs` (límite por IP); registro
  en `Program.cs`; tests en `tests/FEP.Api.Tests/LicenciasTests.cs` (validación, criterios, sin caché y 429 con un
  repositorio falso: no necesitan SQL Server).
- Web: `wwwroot/buscar-licencia.html`, `assets/js/buscar-licencia.js`, estilos `.lic-*` en la sección "Páginas
  interiores" de `assets/css/fep.css` (y su bloque responsive).
- Mock: `mock/licencias.json` (dos licencias de la web pública del 19/11/2026; no sale de `Contenidos/`, por eso no tiene
  script generador) y `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`licencias_buscar` → `buscar-licencia.html`;
  `consulta_tu_licencia` y `licencias_tulicencia` → `buscar-licencia.html#consulta`) y entrada única del menú Licencias en
  `MENU_FEP` y en `mock/menu.json`.
- Documentación: `docs/api/endpoints.md`, fila de `tools/generar-mapa-web.ps1` (Desarrollado).
