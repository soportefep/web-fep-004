# Análisis: `Calendario_Menores_FFAA.asp` → `calendario-menores-ffaa.html` + `GET /api/v1/competicion/calendarios-autonomicos/menores`

URL actual: `/Calendario_Menores_FFAA` (menú Competición → "Calendario Menores FFAA"). Ruta nueva propuesta en
`docs/migracion/redirecciones.csv`: `/calendario-menores-ffaa`.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, barra lateral, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` (+ columna de patrocinadores de las páginas interiores) |
| `sql_noticias` (`SFEP_Noticias`) | Se consultaba **pero no se usaba** | Eliminado |
| `sql_ano` (`SFEP_Calendario`, años con `Federacion=0`) y parámetro `ano` | Se calculaba el año **pero no se pintaba** | Eliminado (la temporada va en el contenido) |
| `sql_archivo` (`SFEP_Calendario_Archivo`: `Archivo`, `ArchivosLicitacion`, `CalendarioLicitacion`) | Se montaban 3 enlaces a `refs/docs/` **que no se pintaban** | Eliminado |
| `sql_num` (`SFEP_Calendario` ⨝ `SFEP_Calendario_Categorias`, parámetros `ano` y `fed`) | Contaba pruebas en un bucle **sin usar el resultado** | Eliminado |
| `sql_fed` (`SFEP_Federaciones` 1–21) | Se consultaba **pero no se usaba** | Eliminado |
| Título "calendario MENORES federaciones territoriales 2026" | `<h5>` escrito a mano | `titulo` + `temporada` |
| Texto del RTG (hasta 6 pruebas, puntos 3.10.1/3.10.2), los 3 momentos de incorporación (3.10.9), qué compone el ranking y condiciones de participación | HTML escrito a mano, en cursiva y entre comillas | `introduccion`, `actualizacionesRanking`, `participacion` |
| Leyenda "* En Verde pruebas incluidas actualmente en el ranking de Menores" | `<span class="verde">` | `leyenda` + marca `enRanking` por prueba |
| Pruebas de 17 federaciones (75 pruebas) | Una tabla HTML escrita a mano, una fila por federación con `<p>`/`<li>`/`<ol>` mezclados; "en verde" = clase `creativ-shortcode-alertbox-colour-theme` | `federaciones[].pruebas[]` estructuradas (fechas, nombre, sede, campeonato autonómico, en el ranking, fases) |

**El contenido no sale de la BD**: aunque la página abría cinco consultas (y el mapa de la web la clasificaba como
"Calendario BD"), todo lo que se ve estaba escrito a mano en el HTML. Se ha pasado a
`src/FEP.Api/Contenidos/calendarios-autonomicos/menores.json`, servido por
`GET /api/v1/competicion/calendarios-autonomicos/menores`. La página nueva no lleva datos en el HTML.

### Diseño común con Veteranos

`Calendario_Veteranos_FFAA.asp` tiene exactamente la misma estructura (presentación del RTG + una tabla por
federación con las pruebas "en verde"). Todo lo nuevo es común a las dos categorías:

- API: un solo recurso `/api/v1/competicion/calendarios-autonomicos/{categoria}` (`menores` | `veteranos`), un
  repositorio que lee `Contenidos/calendarios-autonomicos/{categoria}.json` y un DTO genérico
  (`actualizacionesRanking` es opcional: Veteranos no tiene "momentos"). Si falta el JSON de una
  categoría, la API responde **404** con `urlWebActual` = su página en la web actual. Veteranos ya está migrado
  (ver `Calendario_Veteranos_FFAA.md`).
- Front: `assets/js/calendario-autonomico.js` lee la categoría de `data-categoria` en `#cal-contenido`; para
  Veteranos basta con copiar `calendario-menores-ffaa.html` cambiando textos, `data-categoria="veteranos"` y el
  enlace de error. Estilos `cal-*` comunes en `fep.css`.
- Mock: `tools/generar-mock-calendarios-autonomicos.ps1` procesa todos los ficheros de la carpeta.
- Federaciones sin pruebas (en Veteranos hay varias con `<blockquote>&nbsp;</blockquote>`): `pruebas: []`; el
  front muestra "Esta federación no ha incluido pruebas…".

## Lógica heredada y problemas del original

- **Inyección SQL**: `sql_num` concatenaba `ano` (entre comillas) y `fed` (**sin comillas**) leídos de
  `Request(...)` y filtrados solo con `limpiar()` (lista negra). Además `sql_archivo` leía la primera fila sin
  comprobar `EOF` (error 500 si la tabla estaba vacía). Todo ello para datos que no se pintaban: desaparece.
- Seis consultas por visita (cinco propias + `Menus.inc`) sin ningún dato de BD en la página.
- Tabla de 4 columnas (16/35/12/29 %) que solo usaba `colspan="4"`, con filas vacías de separación y listas
  anidadas inválidas (`<ol><blockquote><ol><blockquote><ol><ol><li>`); en móvil no se adaptaba.
- La única forma de saber si una prueba puntuaba era el **color de fondo** (verde): no accesible.

## Mejoras respecto al original

- Pruebas estructuradas: fechas de inicio y fin reales (`fechaInicio`/`fechaFin`), nombre, sede, marca
  `campeonatoAutonomico` (la participación en esas pruebas puede estar restringida, como avisa el propio texto) y
  `enRanking` como etiqueta de texto ("En el ranking"), no como color.
- Dos vistas: **por federación** (la de la web actual, con ancla por federación y enlace a su ficha
  `federacion.html?id=`) y **por fecha** (todas las pruebas por mes, `?vista=fecha`), con índice lateral que se
  adapta a la vista.
- Resumen en la cabecera (federaciones, pruebas, en el ranking, campeonatos autonómicos y, si las hay, por disputar).
- Los 3 momentos de incorporación al ranking como pasos con su fecha (8 y 28 de octubre de 2026) y el máximo de
  torneos; la condición de participación destacada.
- Sin consultas a la BD; contenido cacheable (output cache 5 min).

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| `<title>` "Calendario Menors FFAA" | "Calendario de menores de las federaciones territoriales" | Errata |
| "3 momentos en **el** que se introducen" | "3 momentos en **los** que se introducen" | Concordancia |
| "Ranquing de Menores FEP" | "Ranking de Menores FEP" | Errata (el resto de la página dice "ranking") |
| Párrafos en cursiva y entre comillas `"…"` | Texto sin comillas | Las comillas no indicaban ninguna cita identificable |
| Orden: RTG → 3 momentos → qué compone el ranking → participación | RTG → qué compone el ranking → 3 momentos → participación | Agrupa la explicación del ranking antes del calendario de actualizaciones |
| Vasca: "Del 31 al 6 de septiembre" | "Del 31 de agosto al 6 de septiembre" (31-08 → 06-09) | Fecha imposible tal cual (**SUPUESTO**) |
| Murcia: "Del 16 al 18de Octubre" | "Del 16 al 18 de octubre" | Errata |
| Castilla-La Mancha: "2-.-Del 13 al 14 de junio" | Prueba 2, "Del 13 al 14 de junio" | Errata de numeración |
| Cataluña: "setiembre", "categories alevín y cadete" | "septiembre", "Categorías alevín y cadete" | Catalanismos en texto en castellano |
| Fechas "Del 13 al 15  febrero", "13 al 15 de febrero", "Del 04 al 07"… | "Del 13 al 15 de febrero", "Del 4 al 7 de junio"… | Formato homogéneo (el texto original se conserva en el sentido, no en la forma) |
| Cabeceras "federación Andaluza de Pádel", "Federación Catalana de Pádel", "…Comunidad Valenciana", "…de las Illes Balears", "…Castilla la Mancha" | Nombres oficiales de `Contenidos/federaciones-autonomicas.json` (Federació Catalana de Pàdel, Comunitat Valenciana, les Illes Balears, Castilla-La Mancha) | Coherencia con la página de federaciones ya migrada |
| Murcia: sedes entre asteriscos "* VILLAESCUSA SPORT SAN JAVIER*" | Sin asteriscos | Los `*` eran decoración |
| Baleares: espacios `&nbsp;` repetidos antes de la sede | Separados en `nombre` / `sede` | Maquetación |

Los nombres de pruebas y sedes se mantienen como los escribió cada federación (mayúsculas incluidas: "PUNTUABLE
MENORES FEP", "TORNEO BARCELÓ"…). La separación de cada línea en `nombre` y `sede` es interpretación de la
migración (**SUPUESTO**), por ejemplo "CP Liceo Sport. Club Propadel Indoor (Almería)" queda entera como sede.

**Pendiente de confirmar por la FEP**:
- **DECISIÓN FEP — pruebas "en verde"**: el `.asp` recibido (04/11/2026) marca en verde **todas** las pruebas salvo
  la de Navarra (que lleva la clase `creativ-shortcode-button-colour-theme`, fondo naranja). La web pública
  consultada el 13/11/2026 solo tiene en verde 43 pruebas (las disputadas hasta julio) y la de Navarra sin color. Se
  ha migrado el estado del `.asp` (más reciente: incluye las incorporadas en la actualización del 28 de octubre):
  74 `enRanking = true` y Navarra `false`. ¿Es correcto? ¿Qué significaba el fondo naranja de Navarra?
- **SUPUESTO**: el Campionat de Catalunya de menores se modela como **una** prueba (n.º 5) con **dos fases**
  (2–11 de octubre: benjamín, infantil y júnior en Club Pàdel Cambrils; 16–25 de octubre: alevín y cadete en
  Pàdel Park Llavaneres), como en el original; sus fechas generales (2–25 de octubre) son derivadas.
- **SUPUESTO**: pruebas de fines de semana alternos (Cataluña: "13, 14, 15, 20, 21 y 22 de febrero") tienen
  `fechaInicio`/`fechaFin` del primer al último día; el texto `fechas` conserva los días exactos y se muestra.
- **SUPUESTO**: `campeonatoAutonomico` se ha marcado en las pruebas cuyo nombre indica campeonato
  autonómico/regional (15: una por federación, salvo Extremadura y Melilla). La FEP debe confirmarlo, porque el
  texto de participación depende de ello.
- **SUPUESTO**: Galicia, prueba 4 "Sede por confirmar" se ha dejado como texto de sede; Asturias y Cantabria no
  indican sede ni (Asturias) nombre: la tarjeta muestra "Prueba puntuable".
- **DECISIÓN FEP**: la página se actualiza a mano cada temporada (el `<h5>` lleva el año fijo "2026"). Con este
  diseño se edita `menores.json` (o, en el futuro, las tablas propuestas abajo) y se regenera el mock.
- El texto de los momentos 2 y 3 habla en primera persona de la FEP ("haremos", "abriremos"): se ha mantenido.

## Modelo SQL propuesto (para cuando se quiera editar desde la gestión)

Hoy se edita el JSON. Las tablas `SFEP_Calendario*` existentes no sirven tal cual (no tienen nombre de prueba, sede
ni marca de ranking, y la página nunca las pintó). Para gestionarlo desde el back-office, basta con estas tablas y
cambiar la implementación de `ICalendariosAutonomicosRepository` por Dapper (el contrato no cambia):

```sql
CREATE TABLE SFEP_CalendarioFFAA        (IdCalendario int IDENTITY PRIMARY KEY, Categoria varchar(20) CHECK (Categoria IN ('menores','veteranos')),
                                         Temporada int, Titulo nvarchar(200), Ranking nvarchar(100) NULL, MaximoPruebas int NULL,
                                         Introduccion nvarchar(max) NULL /* párrafos separados por línea en blanco */,
                                         TextoActualizaciones nvarchar(500) NULL, Participacion nvarchar(1000) NULL, Leyenda nvarchar(300) NULL,
                                         Actualizado date NULL, Visible char(1) DEFAULT 'S', UNIQUE (Categoria, Temporada));
CREATE TABLE SFEP_CalendarioFFAA_Momentos (IdMomento int IDENTITY PRIMARY KEY, IdCalendario int REFERENCES SFEP_CalendarioFFAA,
                                         Orden int, Titulo nvarchar(150), Descripcion nvarchar(1000) NULL, MaximoTorneos int NULL, Fecha date NULL);
CREATE TABLE SFEP_CalendarioFFAA_Pruebas (IdPrueba int IDENTITY PRIMARY KEY, IdCalendario int REFERENCES SFEP_CalendarioFFAA,
                                         IdFederacion int REFERENCES SFEP_Federaciones(IdFederacion), Orden int,
                                         Fechas nvarchar(150), FechaInicio date NULL, FechaFin date NULL,
                                         Nombre nvarchar(250) NULL, Sede nvarchar(250) NULL,
                                         CampeonatoAutonomico char(1) DEFAULT 'N', EnRanking char(1) DEFAULT 'N', Borrado char(1) DEFAULT 'N');
CREATE TABLE SFEP_CalendarioFFAA_Fases  (IdFase int IDENTITY PRIMARY KEY, IdPrueba int REFERENCES SFEP_CalendarioFFAA_Pruebas,
                                         Orden int, Fechas nvarchar(150), FechaInicio date NULL, FechaFin date NULL,
                                         Descripcion nvarchar(250) NULL, Sede nvarchar(250) NULL);
```

El orden de las federaciones, su clave (ancla), territorio y nombre oficial saldrían de `SFEP_Federaciones` (o del
contenido de `federaciones-autonomicas.json` mientras siga en JSON).

## Verificación

- No hay PDF ni imágenes propias en la página (los enlaces de `SFEP_Calendario_Archivo` no se pintaban).
- Enlaces a la web actual comprobados con `HEAD` (13/11/2026): `/Calendario_Menores_FFAA`,
  `/Calendario_Veteranos_FFAA` y `/Rankings.asp` → 200.
- El texto de la web pública coincide con el del `.asp`; solo difieren las clases "en verde" (ver DECISIÓN FEP).

## Ficheros

- API: `Contracts/Competicion/CalendarioAutonomicoDto.cs` (DTOs + `ReglasCalendariosAutonomicos`),
  `Data/Repositories/Competicion/CalendariosAutonomicosRepository.cs`,
  `Controllers/Competicion/CalendariosAutonomicosController.cs`, `Contenidos/calendarios-autonomicos/menores.json`;
  registro en `Program.cs`; tests en `tests/FEP.Api.Tests/CalendariosAutonomicosTests.cs`.
- Web: `wwwroot/calendario-menores-ffaa.html`, `assets/js/calendario-autonomico.js` (común a menores y veteranos),
  estilos `cal-*` en la sección "Páginas interiores" de `assets/css/fep.css`; `PAGINAS_MIGRADAS` en
  `assets/js/comun.js` (`calendario_menores_ffaa`).
- Mock: `mock/competicion-calendarios-autonomicos-menores.json`, generado con
  `tools/generar-mock-calendarios-autonomicos.ps1` (no editar a mano: se edita el JSON de `Contenidos/` y se vuelve
  a generar).
