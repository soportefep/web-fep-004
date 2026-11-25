# Análisis: `CalendarioFEP.asp` → `calendariofep.html` + `GET /api/v1/competicion/calendario`

URL actual: `/CalendarioFEP` (regla `^CalendarioFEP` de `web.config` → `CalendarioFEP.asp`; opción de menú
"Calendario FEP" bajo Competición, `SFEP_Web_Menu.Link = 'CalendarioFEP'`). Ruta nueva propuesta en
`redirecciones.csv`: `/calendariofep` → `calendariofep.html`. El formulario hacía `POST` a `CalendarioFEP` con `ano`;
`Request("ano")` también lo lee de la query (`/CalendarioFEP?ano=2027`, comprobado). La página nueva acepta `?ano=`
y `?anio=`, y `comun.js` traslada los enlaces antiguos (también los de la BD, como la opción del menú).

Includes: `conexionSQL.inc` (no se copia), `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (ya migrados en
`comun.js`), `finconexion.inc`. Codificación del `.asp`: Windows-1252. `CalendarioFT.asp` (calendario de las
federaciones territoriales) es casi el mismo fichero con `fed` elegido en un desplegable: **se migra aparte**; el
repositorio ya recibe la federación como parámetro (`ReglasCalendario.IdFederacionFep`) para poder reutilizarlo.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, barra lateral, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (`activo=8`) | Ya migrado: `comun.js` (+ columna de patrocinadores) |
| `sql_noticias` (`SFEP_Noticias`, `SELECT *`) | Se consultaba **pero no se usaba** | Eliminado |
| `sql_fed` (`SFEP_Federaciones` 1–21) | Se consultaba **pero no se usaba** (es el desplegable de `CalendarioFT.asp`) | Eliminado |
| `sql_archivo` (`SFEP_Calendario_Archivo`, `SELECT *`, tabla de una fila) | Solo se usaba `HabitaSolicitud`; `Archivo`, `ArchivosLicitacion` y `CalendarioLicitacion` se leían para variables que se sobrescribían o no se pintaban | `licitacion` (solo `HabitaSolicitud`) |
| Banner "Organización Torneos 2027" | `<img src="refs/images/Torneos2027.jpg">` **sin `alt`**, enlazado a `Organizacion_Torneos`, si `HabitaSolicitud = 'S'` (hoy sí) | `licitacion` (texto en `Contenidos/calendario-fep.json`; si se muestra lo sigue decidiendo la BD) |
| Título "calendario oficial federación española de padel" + "Filtro y Datos del Calendario (N)" | HTML a mano + `sql_num` recorrido entero solo para contar (**sin** filtrar `Visible`) | `h1` "Calendario oficial FEP." + contador real en `role="status"` |
| Desplegable "Año del Torneo" | `sql_ano`: `SFEP_Calendario.Año` con `Federacion=0`, `visible='S'`, `año>2014`, `GROUP BY` `ORDER BY Año DESC`; por defecto **`ano=2026` escrito a mano** (el código que elegía el más reciente, comentado) | `temporadas[].anio`; `anio` por defecto = la del año en curso (`ReglasCalendario.TemporadaPorDefecto`) |
| "Fecha de Actualización" + "Descarga del Calendario" | `sql_calen`: `SFEP_Calendario_Documentos` con `Fed=0`, `Ano='<ano>'`, `Tipodoc='Calendario'`, `Borrado='N'` → `FechaAct` (`formatdatetime(…,1)`) y `http://padelfederacion.es/refs/docs/` + `Archivo` | `temporadas[]` (`actualizado`, `archivo`, `url`) de **todas** las temporadas en una consulta |
| Tabla por meses (ENERO … DICIEMBRE + "SIN FECHA ASIGNADA") | 13 consultas iguales (`sql_ene`…`sql_dic` y `FechaINi IS NULL`): `SFEP_Calendario ⨝ SFEP_Calendario_Categorias` con `Año='<ano>'`, `month(FechaINi)=N`, `Federacion=<fed>`, `VIsible='S'`, `ORDER BY FechaIni` | `items` (una consulta, por `FechaIni` con los `NULL` al final); el front agrupa por mes |
| Columnas Inicio / Fin | `day()` + mes abreviado de `FechaIni`/`FechaFin` (dos `select case` de 12 ramas por mes: 26 bloques) | `fechaInicio`, `fechaFin` (`yyyy-MM-dd`); el front usa `bloqueFecha` |
| Prueba / Categoría / Sede | `Campeonato`, `TextoCategoria`, `Sede` | `titulo`, `categoria`, `sede` |
| Enlace de cada celda | Si `IdPrueba <> 0` y `Link <> '#'`: `javascript:irCamp(IdPrueba)` → `POST Campeonatos_Previo.asp` con `idtorneo` | `idTorneo` + `enlace` (URL absoluta a la ficha) + `nuevaVentana` |

Columnas de `SFEP_Calendario` realmente usadas: `Año`, `Federacion`, `Visible`, `FechaIni`, `FechaFin`, `Campeonato`,
`Categoria` (para el `JOIN`), `Sede`, `IdPrueba`, `Link` (solo para comparar con `#`). De `SFEP_Calendario_Categorias`
solo se pintaba `TextoCategoria` (`Categoria` no). La página **no escribe** en la BD.

## Parámetros (`Request`) y validación

| ASP | API nueva | Validación |
|---|---|---|
| `ano` (`""` → 2026 fijo) | `anio` (opcional; sin él, la temporada del año en curso o la más reciente anterior) | entero 1901–2100; si no, **400** `ValidationProblemDetails` |
| `fed` (`""` → 0) | — | El endpoint es solo del calendario FEP (`Federacion = 0`). `CalendarioFEP?fed=N` mostraba el calendario de otra federación con los años de la FEP y sin PDF: ese caso es `CalendarioFT.asp` (ya migrado: `comun.js` lleva esos enlaces a `calendarioft.html?federacion=N`; ver `CalendarioFT.md`) |
| `idtorneo` (a `Campeonatos_Previo.asp`) | — | El enlace a la ficha lo calcula la API |

Los no numéricos (`anio=abc`) los rechaza el *model binding* de `[ApiController]` con el mismo 400. Una temporada
válida sin pruebas devuelve `items: []`; la página, si la temporada no está en `temporadas` (enlace antiguo),
vuelve a la de por defecto.

## Lógica / visibilidad heredada

- **Temporada por defecto escrita a mano** (`ano=2026`): la FEP lo cambiaba en el código cada año para no abrir la
  temporada siguiente, que se publica "en licitación" meses antes (hoy ya existe 2027). Decidido por la FEP
  (13/11/2026): **cambio automático el 1 de enero de cada año**. La API calcula el año en curso en hora de Madrid
  (`ReglasCalendario.AnioEnCurso`, a las 00:00 del 1 de enero aunque el servidor esté en otra zona horaria) y abre esa
  temporada si está publicada; si no, la más reciente anterior; si solo hay futuras, la más próxima. Hoy da 2026.
- **`<%if a="" then%>`**: la variable `a` no se asigna nunca (mismo patrón que en `Organos_Gobierno.asp`), así que la
  descarga del PDF se mostraba siempre que `fed = 0`. Se mantiene (siempre visible si hay fichero).
- **Banner de la convocatoria**: solo con `HabitaSolicitud = 'S'`. Se mantiene con la BD como interruptor; la imagen
  y el texto pasan a `Contenidos/` (había que editar el ASP cada año para cambiar `Torneos2027.jpg`).
- **Enlace de las pruebas**: el ASP solo enlazaba si `IdPrueba <> 0` y `Link <> '#'` (con `Link` vacío o `NULL`, sí;
  `ReglasCalendario.Enlazable`). Se mantiene, y además se exige que el torneo exista en `SFEP_Torneos` sin borrar.
- **Agrupación por mes de inicio sin año**: `month(FechaIni) = N` agrupaba por mes aunque la prueba empezara en otro
  año que la temporada. Ahora se ordena por fecha completa y el front agrupa por mes y año ("Diciembre de 2026").
- **Fecha comodín 1 ene – 31 dic**: la gestión la usa para las pruebas sin fecha ("En Licitación", "Por Designar",
  "sin adjudicar", "PRUEBA CANCELADA"); el ASP las pintaba en ENERO como "1 Ene – 31 Dic". Ahora `fechaPorConfirmar`
  y el front las agrupa al final, junto a las que no tienen fecha, como "Fecha por confirmar". **DECISIÓN FEP**.
- `activo=8`: opción de menú activa (plantilla). Funciones JS `MesAnterior`, `CargarEvento`, `MeGusta`,
  `busqueda`: no se usan. Fila vacía tras NOVIEMBRE siempre visible (el `end if` está antes de la fila separadora).
  Anchos de columna distintos en enero (15/15/35/15/20 %) que en el resto (9/7/41/13/30 %).

## Problemas del original

1. **Enlaces rotos en todas las pruebas con torneo**: `irCamp()` enviaba un `POST` a `Campeonatos_Previo.asp`, que
   **no existe** en `X:\TICS\WEB`; la regla `^Campeonatos` de `web.config` lo reescribe a `Campeonatos.asp`, que ignora
   `idtorneo` y muestra el listado general de campeonatos (comprobado el 13/11/2026: `POST Campeonatos_Previo.asp` →
   200 "Campeonatos | FEP"). Lo previsto era, sin duda, `Torneos_Previo.asp` (el que usa `Campeonatos.asp`): la API
   enlaza directamente a la ficha con `ReglasCampeonatos.RutaFicha` (o a `LinkCuadros`, como el listado de campeonatos).
2. **Inyección SQL**: `ano` (entre comillas, tras `limpiar()`) y `fed` (**sin comillas**) se concatenaban en 15
   consultas. Ahora todo va con parámetros Dapper (`@Anio`, `@Federacion`, `@AnioDesde`).
3. **19 consultas por página** (más las de `Menus.inc`): noticias y federaciones sin usar, `sql_num` solo para contar,
   13 iguales por mes y una `SELECT *`. Ahora dos idas a la BD (`QueryMultiple` con temporadas, documentos y
   configuración; y las pruebas con su torneo en `OUTER APPLY`).
4. **Contador "(N)" erróneo**: `sql_num` no filtraba `Visible`, así que contaba pruebas ocultas (2026: dice 45 y se ven
   43; 2023: 28/26; 2021: 54/53; 2020: 23/10). Ahora el total es el de las pruebas mostradas.
5. **Error 500 sin documento**: `rs_calen("Archivo")` y `rs_calen("Fechaact")` se leían sin comprobar EOF; una
   temporada sin fila en `SFEP_Calendario_Documentos` (o `SFEP_Calendario_Archivo` vacía) rompía la página. Ahora
   `actualizado`/`archivo` llegan `null` y la página lo indica.
6. **Enlace a la carpeta `refs/docs/`**: con `Archivo` vacío (2021, 2022, 2023 y 2024) el icono de descarga enlazaba a
   `http://padelfederacion.es/refs/docs/` (**403**). Ahora no hay enlace y se indica "No hay calendario en PDF
   publicado". También se pasa de `http://` sin `www` a `https://www.…` (`Media:BaseUrl`), con espacios codificados.
7. **XSS**: `Campeonato`, `TextoCategoria` y `Sede` se pintaban sin `Server.HTMLEncode`. El front usa `textContent`.
8. **Sin enlaces navegables**: `javascript:irCamp(id)` no se puede abrir en otra pestaña ni funciona sin JS.
9. **Accesibilidad/HTML**: banner sin `alt`, tabla de maquetación sin `th`/`caption`, cabeceras de mes como celdas,
   `<meta charset=iso-8859-1>` tras los scripts, `<script>` fuera de `<head>`, formulario que solo funcionaba con
   JavaScript (`onChange="cambiar()"`).

## Mejoras respecto al original

- Pruebas agrupadas por mes ("Febrero de 2026" con contador) en tarjetas con fecha grande, etiqueta de categoría
  (colores de Campeonatos; silla de ruedas con color propio) y sede; las que tienen torneo enlazan a su ficha.
- Filtros nuevos en el navegador: **categoría** (las de la temporada), **prueba o sede** (sin distinguir mayúsculas
  ni tildes) y **"solo pendientes de celebrar"**; todos en la URL para enlazar y compartir.
- PDF de la temporada sobre la lista ("Actualizado el …") y sección **"Calendarios en PDF"** con los de todas las
  temporadas (los 4 más recientes visibles y el resto plegados).
- Aviso de la convocatoria como bloque accesible (título, texto y botón); el banner se conserva como imagen
  decorativa (`alt=""`: repite el título).
- Índice lateral con la convocatoria, los meses de la temporada y los PDF (chips en móvil); cifras en la cabecera.
- Respuesta cacheada 5 min (output cache por `anio`); una sola temporada por petición, sin paginar (25–55 pruebas).

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "Filtro y Datos del Calendario (45)" (2026) | 43 pruebas | El contador incluía pruebas no visibles |
| "1 Ene – 31 Dic" en ENERO | "Fecha por confirmar", al final | Fecha comodín de la gestión para pruebas sin fecha (**DECISIÓN FEP**) |
| `http://padelfederacion.es/refs/docs/` (sin fichero) | Sin enlace + aviso | Enlace a la carpeta: 403 |
| "SILLA RUEDAS" | "Silla de ruedas" (solo el texto visible; la API devuelve el valor de la BD) | Presentación |
| Mayúsculas/minúsculas de `Sede` ("REGAL PADEL CLUB (ZARAGOZA)", "Club de Pádel Arruzafa - Córdoba") | "Regal Padel Club (Zaragoza)"; las que ya mezclan mayúsculas y minúsculas, tal cual | Decidido por la FEP (13/11/2026): sede en formato "Nombre Propio" (`Texto.NombrePropio`) |

Registros de la gestión que parecen mal introducidos. La web los publica tal como están guardados y la migración no
los toca: si son errores, hay que corregirlos en la gestión (backoffice) de la FEP, no en la web. **DECISIÓN FEP**:
para cada uno, indicar si se corrige en la gestión o si es correcto así.

- **Pruebas repetidas (temporada 2027)**: el "Circuito Nacional de Menores 4" (21–23 de mayo) y el "Circuito Nacional
  de Menores 7" (29–31 de octubre) aparecen **dos veces cada uno** en el calendario; la segunda copia del 7 solo se
  diferencia por un espacio de más. En la web salen duplicados. Lo normal sería borrar una de las dos copias.
- **Número romano imposible (2027)**: "VIX Cto. España Equipos 1ª, 2ª y 3ª cat." — "VIX" no es un número válido.
  Probablemente es "IX" (9.º) o "XIV" (14.º).
- **Categoría distinta a la de sus pruebas hermanas (2026)**: el "Circuito Nacional de Veteranos 2" está en la
  categoría "Veteranos FFAA", pero el 1 y el 3 están en "Veteranos". Y el "XXIII Cto. España Selecciones Autonómicas
  Menores" está en "Menores", cuando por ser de selecciones autonómicas encajaría en "Menores FFAA". Afecta al filtro
  por categoría y al color de la etiqueta.
- **Campeonato Absoluto confuso (2026)**: hay un "Campeonato de España Absoluto" todavía "En licitación" y sin fecha,
  y además dos "Campeonato de España Absoluto por Parejas" (11–13 de diciembre en el Club G24, y 19–21 de junio "Sin
  designar"), los dos en la categoría "Silla de ruedas". Hay que aclarar si esos dos son el Absoluto normal mal
  clasificado como silla de ruedas, o si son el de silla de ruedas y les falta esa palabra en el título.

## Comprobación de enlaces (13/11/2026)

- PDF de 2015–2020 y 2025–2027 → 200 `application/pdf` (por `https://www.padelfederacion.es/refs/docs/…`).
- 2021–2024: sin fichero (el ASP enlazaba a `refs/docs/` → 403).
- `https://www.padelfederacion.es/Organizacion_Torneos` → 200; `refs/images/Torneos2027.jpg` → 200.
- `Torneos_Previo.asp?IdTorneo=` de los 50 torneos enlazados en las 13 temporadas → 302 a su ficha (`Camp_Parejas.asp`,
  `Camp_Equipos.asp` o `http://circuitomenores.padelfederacion.es/Campeonato?IdTorneo=`). Muestras: `Camp_Equipos.asp?
  IdTorneo=28927` → 200; `Camp_Parejas.asp?IdTorneo=28544` → 302 a `Camp_Parejas_Veteranos.asp` → 200; circuito de
  menores `IdTorneo=21806` → 200 (solo http).
- `Campeonatos_Previo.asp` (destino del ASP) → 200, pero es el listado general de `Campeonatos.asp` (ver problema 1).

## Modo prototipo (mocks)

`mock/competicion-calendario.json` es una instantánea de la web pública (13/11/2026) con las **13 temporadas**
(2015–2027) y **434 pruebas**, el documento y la fecha de actualización de cada temporada y la convocatoria (abierta
hoy). Lo genera `tools/generar-mock-calendario-fep.ps1` (solo lectura sobre la web pública; la convocatoria sale de
`Contenidos/calendario-fep.json` con la imagen local `assets/img/fep/calendario-fep/torneos-2027.jpg`; los enlaces a
fichas se reutilizan del mock de campeonatos o se resuelven con `Torneos_Previo.asp`). Como el mock no filtra,
`calendario-fep.js` se queda siempre con las pruebas de la temporada mostrada.

Limitaciones del mock: las fechas se reconstruyen con día y mes de la web y el año de la temporada (si el fin cae en
un mes anterior al inicio, al año siguiente); `idTorneo` es el `IdPrueba` de `irCamp()`; el orden dentro del mismo día
es alfabético (como el `ORDER BY` de la API, el ASP no lo fijaba). Recuento por temporada: 2027: 54 · 2026: 43 ·
2025: 27 · 2024: 27 · 2023: 26 · 2022: 51 · 2021: 53 · 2020: 10 · 2019: 26 · 2018: 25 · 2017: 26 · 2016: 24 · 2015: 42.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: las pruebas con fecha comodín 1 ene – 31 dic se muestran como "Fecha por confirmar" al final, no
  en enero. ¿Es correcta la interpretación? Lo limpio sería dejar `FechaIni`/`FechaFin` a `NULL` en la gestión.
- **DECISIÓN FEP**: al pulsar una prueba del calendario, la web actual debería abrir la página de ese torneo (cuadros,
  inscritos, resultados), pero por un error del código antiguo abre siempre el listado general de "Campeonatos" y el
  usuario tiene que buscar el torneo a mano. La web nueva abre directamente la página del torneo (la misma a la que se
  llega desde "Campeonatos"). Confirmar que es lo que se quiere.
- **DECISIÓN FEP**: corregir en la gestión los registros que parecen mal introducidos (pruebas repetidas de 2027,
  "VIX", categorías de 2026 y el Campeonato Absoluto; explicados en "Correcciones de datos"). La web no los cambia.
- **DECISIÓN FEP**: texto del aviso de la convocatoria (redactado a partir de `Organizacion_Torneos.asp`: "Calendario
  Provisional 2027", "Condiciones Organización de Torneos" y el formulario). Revisar y actualizar cada año en
  `Contenidos/calendario-fep.json` (con la imagen del año).
- **SUPUESTO**: tipos de columnas: `SFEP_Calendario.Año` y `SFEP_Calendario_Documentos.Ano` enteros o texto numérico
  (se leen con `TRY_CAST(... AS int)`; el filtro `c.[Año] = @Anio` usa un parámetro entero); `Federacion`, `Fed`,
  `IdPrueba`, `Categoria`/`IdCategoria` enteros; `FechaIni`, `FechaFin`, `FechaAct` `datetime`; `HabitaSolicitud`,
  `Visible`, `Borrado`, `Tipodoc`, `Link` `char`/`varchar`.
- **SUPUESTO**: `SFEP_Calendario_Archivo` tiene una sola fila (el ASP leía la primera sin `ORDER BY`; se mantiene
  `TOP (1)`). Si una temporada tuviera varios documento "Calendario", se usa el de `FechaAct` más reciente (el ASP
  cogía el primero que devolviera la BD).
- **SUPUESTO**: el enlace exige que el torneo exista en `SFEP_Torneos` con `Borrado = 'N'` (no se filtra `Visible`, como
  `Torneos_Previo.asp`).
- La ficha de los torneos (`Camp_Parejas.asp`, `Camp_Parejas_Veteranos.asp`…), el circuito de menores (solo http) y la
  convocatoria (`Organizacion_Torneos.asp`, grupo `licitacion-torneos`) siguen en la web actual; `Camp_Equipos.asp` ya
  está migrada (`campeonato-equipos.html`, vía `paginaMigrada`).

## Modelo SQL propuesto

Las pruebas y documentos ya están en BD. Solo el aviso de la convocatoria se ha sacado a `Contenidos/`; para
gestionarlo sin tocar ficheros bastaría con ampliar la tabla de configuración que ya decide si se muestra:

```sql
ALTER TABLE SFEP_Calendario_Archivo ADD
    LicitacionTitulo   nvarchar(150) NULL,   -- "Organización de torneos 2027"
    LicitacionTexto    nvarchar(600) NULL,
    LicitacionImagen   nvarchar(200) NULL,   -- ruta bajo refs/: images/Torneos2027.jpg
    LicitacionImagenAlt nvarchar(150) NULL,
    LicitacionEnlace   nvarchar(200) NULL;   -- Organizacion_Torneos
-- Temporada por defecto configurable (opcional, ver DECISIÓN FEP):
ALTER TABLE SFEP_Calendario_Archivo ADD TemporadaPublicada int NULL;
```

Con eso `ContenidoCalendarioRepository` se sustituye por una consulta Dapper sin cambiar el contrato. Índice
recomendado para la consulta de pruebas: `SFEP_Calendario (Federacion, [Año], Visible) INCLUDE (FechaIni, FechaFin,
Campeonato, Categoria, Sede, IdPrueba, Link)` (**SUPUESTO**: no se ha podido ver el esquema real).

## Ficheros

- API: `Contracts/Competicion/CalendarioCompeticionDto.cs` (`CalendarioCompeticionDto`, `TemporadaCalendarioDto`,
  `LicitacionTorneosDto`, `PruebaCalendarioCompeticionDto`, `DatosCalendarioCompeticionDto`, `ContenidoCalendarioDto`),
  `Contracts/Competicion/ReglasCalendario.cs` (temporada por defecto, fecha comodín, cuándo se enlaza),
  `Data/Repositories/Competicion/CalendarioCompeticionRepository.cs` (Dapper),
  `Data/Repositories/Competicion/ContenidoCalendarioRepository.cs` (lee `Contenidos/calendario-fep.json`),
  `Controllers/Competicion/CalendarioController.cs`; registro en `Program.cs`; reutiliza `ReglasCampeonatos`
  (`RutaFicha`, `EnlaceCuadros`, rango de `anio`); tests en `tests/FEP.Api.Tests/CalendarioTests.cs` (validación 400,
  parámetros al repositorio con un repositorio falso, convocatoria desde `Contenidos/`, JSON y reglas heredadas; no
  necesitan SQL Server).
- Contenido: `src/FEP.Api/Contenidos/calendario-fep.json`.
- Web: `wwwroot/calendariofep.html`, `assets/js/calendario-fep.js`, estilos `.calf-*` en la sección "Páginas
  interiores" de `assets/css/fep.css` (y sus bloques responsive); reutiliza `.clubs-search`, `.clubs-form`,
  `.camp-form`, `.clubs-results`, `.camp-month`, `.camp-item`, `.camp-tag--*`, `.doc-list`, `.org-more`;
  imagen `assets/img/fep/calendario-fep/torneos-2027.jpg` (copia de `refs/images/Torneos2027.jpg`).
- Mock: `mock/competicion-calendario.json` (generado con `tools/generar-mock-calendario-fep.ps1`) + `mock/mocks.js`.
- Común: `PAGINAS_MIGRADAS` y `QUERY_MIGRADA` en `assets/js/comun.js` (`calendariofep` → `calendariofep.html`, con
  `ano`; los enlaces con `fed` distinto de 0 van a `calendarioft.html` desde la migración de `CalendarioFT.asp`);
  acceso rápido "Calendario" de `index.html`.
- Documentación: `docs/api/endpoints.md`, `tools/generar-mapa-web.ps1` (fila "Calendario FEP" → Desarrollado),
  `docs/migracion/mapa-migracion.md` (área `competicion-cuadros`).
