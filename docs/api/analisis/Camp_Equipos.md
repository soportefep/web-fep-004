# Análisis: `Camp_Equipos.asp` → `campeonato-equipos.html` + `GET /api/v1/competicion/campeonatos-equipos/{id}`

URL actual: `/Camp_Equipos.asp?IdTorneo={id}` (ficha de un campeonato por equipos). Se llega desde el listado de
Campeonatos (antes vía `Torneos_Previo.asp`, ahora enlace directo: ver `Campeonatos.md`), desde banners y eventos
destacados de la portada y desde enlaces externos. `redirecciones.csv` no tiene fila para esta página; la ruta nueva
es `campeonato-equipos.html?id={IdPrueba}` (**SUPUESTO**: nombre elegido siguiendo `campeonatos.html`; ver
pendientes). La página nueva acepta también `?IdTorneo=` / `?idtorneo=` y `comun.js` traslada los enlaces antiguos.

Includes: `conexionSQL.inc` (no se copia), `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (ya migrados en
`comun.js`, + columna de patrocinadores), `finconexion.inc`. Codificación del `.asp`: Windows-1252.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, barra lateral, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (`activo=3`) | Ya migrado: `comun.js` |
| `sql_noticias` (`SELECT *` de `SFEP_Noticias` destacadas) | Se consultaba **y no se usaba** | Eliminado |
| `sql_adjuntos` y `sql_cuadros` (`SELECT *` de `SFEP_Torneos` repetido) | Se consultaban **y no se usaban** | Eliminados |
| Torneo | `sql_torneo`: `SELECT SFEP_Torneos.* WHERE IdPrueba=` + id (`limpiar()`) | Consulta con columnas explícitas (`SqlTorneo`) |
| Redirecciones | `Visible='N'` → `Campeonatos`; `MENORES`/`SEL_MEN` no equipos → `Prueba_CuadrosMenores.asp`; `VETERANOS`/`SEL_VET` no equipos → `Prueba_CuadrosVeteranos.asp` | `404` con `urlWebActual` (listado, o la ficha que corresponda según `Torneos_Previo.asp`) |
| Título `<h1>` y `<title>` | `Torneo` | `titulo` (espacios repetidos colapsados) → `h1` con punto final, miga y `<title>` |
| "Compartir en" Twitter / Facebook / Google+ | Enlaces a `padelfederacion.es/menores/Torneo.asp?IdTorneo=` (**otra página**) y a Google+ (cerrado) | Botón "Compartir" (Web Share API o copiar enlace de la página actual) |
| Nota de prensa | `NotaPrensa` si `VisibleNotaPrensa='S'` (HTML) | `notaPrensaHtml` → sección "Nota de prensa" (HTML saneado) |
| Marcadores on-line | `sql_arbitraje` (`SFEP_Torneos_Arbitraje`, `Borrado='N'`, `Visible='S'`, por `IdPartido`) → iframes de `ArbitrajeCtoFep.asp?Id={Aleatorio}` en filas de 2 | `marcadores` (`codigo` + `url`) → sección "En directo" |
| Streaming | 3 iframes de YouTube **escritos a mano** con `if IdTorneo=28133` | `Contenidos/campeonatos-equipos.json` → `videos` |
| Cuadros y horarios | `VerCuadros='S'` + `select case ModalidadTorneo` + `sql_cuadrosM/F/X` (`SFEP_Torneos_Desglose`, primera fila de cada género) + ~45 `if Ver…="S"` por género + un `sql_cambio`/`sql_texto` (`SFEP_Torneos_Cuadros_Nombres`) por cuadro con nombre personalizable | `cuadrosPublicados` + `cuadros` (género → categoría → cuadros con `titulo`, `nivel`, `cuadro`, `tipo` y `url` a `PrevioEquipos.asp`) |
| Aviso de licencia y LOPD | Texto fijo en recuadro rojo | Texto fijo en el HTML (`.cpe-alert`) |
| Notificaciones | `Notificaciones` (HTML) + `sql_noti` (`SFEP_Torneos_Adjuntos` por `IdArchivo DESC`: `Archivo`, `Link`, `Icono`) → `https://padelfederacion.es/Paginas/Torneos/{Año}/{Archivo}` | `notificacionesHtml` + `adjuntos` (`titulo`, `tipo`, `archivo`, `url` codificada) → lista de documentos |
| Listado de inscritos | `sql_inscritos` (`SFEP_Inscripciones` por `Genero`, `Borrado='N'`) si el total ≠ 0, columnas solo para los géneros visibles (`sql_cat`) → `Camp_Parejas_Inscritos.asp` | `inscritosParejas` → tabla (sección oculta si no hay) |
| Inscripción (plazo abierto) | `FechaInscripcion > now()`: `InfoInscripcion` en acordeón + "¿QUÉ DESEAS HACER?" con 3–5 enlaces según `Preinscripcion`, `FechaPreinscripcion`, `ClaseEquipos`, `Circuito='COPA1'` y 5 `IdTorneo` escritos a mano | `inscripcion` (`abierta`, `fechaLimite`, `por`, `informacionHtml`, `acciones`) |
| Equipos inscritos (plazo cerrado) | Aviso `FechaFijarPuntua > now()` + enlace `Camp_Equipos_Modificar.asp` | `inscripcion.equiposInscritosUrl` + `fechaLimitePuntuacion` |
| Información general | `FechaInicio`, `FechaFin` (`formatdatetime(…,1)`), categorías (`ABSOLUTO`: géneros de `sql_cat`; resto: `sql_catM/F/X` con `SFEP_CategoriasMenores`), cuota (`sql_cuota`, **solo la primera fila**), `Ciudad`, club (`sql_club`: `SELECT *` de `SFEP_Clubes` con `IdClub = '{Club}'`) + `Club2`, `InfoAdicional` y mapa de Google (`gMap`) con `latitud`/`longitud` del club | `fechaInicio`, `fechaFin`, `generos`, `cuotas`, `ciudad`, `sedes` (`nombre`, `web`, `latitud`, `longitud`) → tarjetas de datos + enlace al mapa |
| Información adicional | `InfoAdicional` si `VisibleCartel='S'` (además se pintaba **siempre** bajo la información general: salía dos veces) | `infoAdicionalHtml` → una sola sección |
| Cartel | `FotoCartel` si `VisibleCartel='S'` → `https://padelfederacion.es/Paginas/Torneos/{Año}/{FotoCartel}` | `cartel` + `cartelUrl` |
| Noticias relacionadas | `sql_notirel` (`SFEP_Torneos_Noticias` ⨝ `SFEP_Noticias`, `Site='FEP'`, `Visible='S'`, `Borrado='N'`, por `Fecha DESC`): foto, título, fecha, visitas, `DescriCorta` | `noticias` (sin visitas) → tarjetas |

La página **no escribe** en la BD. Las páginas a las que enlaza (cuadros `PrevioEquipos.asp` → `CuadroEquipos*.asp`,
`Camp_Equipos_Modificar.asp` → `…ModificarCOPA.asp`, `Camp_Equipos_InscPrevia.asp` → `…AltaClubAC.asp`,
`Camp_Equipos_Clubes.asp`, `Camp_Equipos_Nuevo.asp`, `Camp_Equipos_Copa_Excell.asp`, `Camp_Parejas_Inscritos.asp`,
`ArbitrajeCtoFep.asp`) siguen en la web actual (grupo `inscripciones-campeonatos`, fases 3–4).

## Parámetros (`Request`) y validación

| ASP | API nueva | Validación |
|---|---|---|
| `idtorneo` / `IdTorneo` (mezclados: `limpiar(request("idtorneo"))` en unas consultas y `request("IdTorneo")` **sin limpiar** en otras) | `id` en la ruta | Entero ≥ 1; si no, **400** `ValidationProblemDetails` (los no numéricos los rechaza el *model binding*) |

Respuestas: `200` con la ficha; `404` `ProblemDetails` con `urlWebActual` = listado de Campeonatos si no existe, está
borrado o `Visible='N'`; `404` con `urlWebActual` = ficha de la web actual (`Camp_Parejas.asp`, `Camp_Liga.asp`,
circuito de menores…) si existe pero no es por equipos.

## Lógica / visibilidad heredada

- **Plazo de inscripción**: abierto si `FechaInscripcion > now()`; vacía = cerrado. La API lo calcula con la hora local
  del servidor (`TimeProvider`) y el front lo vuelve a comprobar con el reloj del navegador (la respuesta se cachea 5 min).
  Con el plazo abierto se muestran la información y los pasos de inscripción; cerrado, el enlace a los equipos inscritos.
- **Pasos de la inscripción** (`ReglasCampeonatoEquipos.Acciones`), en el orden del ASP:
  - `Preinscripcion='S'`: "preinscripción del club" solo si `FechaPreinscripcion > now()` **y** `ClaseEquipos='CLUBES'`;
    "listado de clubes preinscritos" si `CLUBES` y el torneo **no** es 25071, 28676, 28677 ni 28854.
  - `Preinscripcion<>'S'`: "pago de los equipos del club" salvo en el 23069; "listado de clubes que han pagado".
  - `Circuito='COPA1'`: "enviar Excel con los posibles clasificados" + "crear equipo (ya clasificados)"; si no, "crear equipo".
  - Siempre: "modificar / eliminar / pagar un equipo". Las notas dicen "del club" o "de la federación" según `ClaseEquipos`.
  - Los `IdTorneo` escritos a mano pasan a `Contenidos/campeonatos-equipos.json` (`sinListadoClubes`, `sinPagoEquipos`).
- **Cuadros**: solo con `VerCuadros='S'` en el torneo y `ModalidadTorneo` en `ABSOLUTO`, `SEL_ABS`, `MENORES`, `SEL_MEN`,
  `VETERANOS`, `SEL_VET` (con otro valor el ASP no pintaba ninguno). Por género, la fila del desglose debe tener
  `VerCuadros='S'` y cada cuadro su columna `Ver…='S'`. Las tres listas (masculina, femenina, mixta) **no son iguales**
  y se han copiado tal cual en `ReglasCampeonatoEquipos.CuadrosMasculinos/Femeninos/Mixtos`:
  - Masculino: 1.ª–10.ª categoría, consolaciones 1.ª–5.ª, previa (nivel 21), pre-previa (31), liguillas 1–3 y grupos A–Z.
  - Femenino: 1.ª–6.ª, consolaciones 1.ª–5.ª, previa, pre-previa, liguillas 1–3 y grupos A–K.
  - Mixto: 1.ª–6.ª, "descenso" (consolación de 1.ª y 2.ª), "Consolación 3.ª Cat. grupo A/B" (consolaciones de niveles 3 y 4), previa, pre-previa, liguilla y grupos A–B.
  - "Primera categoría" pasa a "Cuadro final" si el desglose no tiene segunda categoría (`Cat2 = 0` o vacío).
- **Nombres personalizados** (`SFEP_Torneos_Cuadros_Nombres`): el ASP solo los buscaba en algunos cuadros y con claves
  a veces distintas del enlace; se respetan para no cambiar lo que hoy se ve: femenino grupos A y B con `Nivel=2` (el enlace
  es de nivel 1); mixto 1.ª y 2.ª con `Categoria='ABSOLUTO'` fija (aunque el torneo sea de `JUNIOR`). Ahora se leen todos
  de una vez y se resuelven en memoria sin distinguir mayúsculas (como la intercalación de SQL Server; el ASP usaba `'x'`
  y `'f'` en algunas consultas). El torneo 10666 tenía escrito a mano "5º y 6º Puesto" para la consolación femenina:
  pasa a `Contenidos/campeonatos-equipos.json` (`nombresCuadros`), con prioridad sobre la BD.
- **Primera fila por género** (`sql_cuadrosM/F/X` sin `ORDER BY`): si un género tuviera varias filas en el desglose, el ASP
  usaba una cualquiera. Ahora se pinta un grupo por fila con `VerCuadros='S'` (ordenadas por género y `Tipo`), con la
  categoría como etiqueta. Con una fila por género (lo habitual: cada categoría de menores es un torneo distinto) el
  resultado es el mismo. **SUPUESTO**.
- **Categorías de la información general**: en `ABSOLUTO` se listaban los géneros visibles; en el resto, las categorías
  de edad (`Tipo` presente en `SFEP_CategoriasMenores`, por `AnoInicial DESC`). En veteranos y selecciones no salía nada
  (sus `Tipo` no están en esa tabla: comprobado en 28854 y 29090). Ahora se muestra cada género y, si las hay, sus
  categorías ("Mixta: JUNIOR").
- **Listado de inscritos**: solo si el total de `SFEP_Inscripciones` (parejas) es ≠ 0. En los campeonatos por equipos
  consultados siempre era 0 (la tabla es de torneos por parejas); se mantiene por si algún torneo la usa.
- **Marcadores en directo**: el ASP pintaba siempre el bloque, con "Aún no hay ningún partido activo" también en
  campeonatos de años pasados. Ahora la sección "En directo" sale si hay marcadores o vídeos, o hasta el día siguiente
  al último día del campeonato.
- `Borrado` no se comprobaba en el torneo (solo `Visible`). Ahora un torneo con `Borrado='S'` responde 404. **DECISIÓN FEP**.
- Funciones JS `busqueda`, `MesAnterior`, `CargarEvento`, `MeGusta`, `cambiar`: no se usan. Google Maps cargado con
  `sensor=false` (parámetro retirado) y jQuery 1.10 desde Google: se eliminan.

## Problemas del original

1. **Inyección SQL**: `id` se concatenaba en ~20 consultas; `limpiar()` (lista negra) solo en las que usaban `id`, y
   `Request("IdTorneo")` **sin limpiar** en `sql_cuadrosM/F/X`, `sql_cambio` y `sql_texto`. `sql_cambio` concatenaba
   además `Tipo` del desglose entre comillas. Ahora `@Id` siempre como parámetro y las columnas `Ver…` salen de constantes.
2. **Error 500** con un id inexistente (comprobado: `Camp_Equipos.asp?IdTorneo=99999999` → 500), porque se leía
   `rs_TORNEO("visible")` sin comprobar EOF. Igual con `rs_cuota("Cuota")` si no hay desglose visible. Ahora 404 / sin cuota.
3. **N+1**: hasta ~30 consultas a `SFEP_Torneos_Cuadros_Nombres` (una por cuadro), más `sql_club`. Ahora: 1 consulta del
   torneo + 1 lote de 9 consultas.
4. **XSS**: `Torneo`, `Ciudad`, `Club`, `Club2`, `Link` de adjuntos, títulos de noticias y nombres de cuadro se pintaban
   sin codificar, y el nombre del club se metía en un `<script>` (`html: '<h3><%=club%></h3'`, además mal cerrado).
   El front usa `textContent`; el HTML de la gestión (nota de prensa, notificaciones, información) se sanea con lista
   blanca (DOMParser, sin `innerHTML`).
5. **Enlaces de cuadro con espacios**: la mitad de los enlaces no hacían `rtrim(Tipo)` → `Cat=ABSOLUTO   ` o
   `Cat=JUNIOR     `. Funcionaban porque SQL Server ignora los espacios finales; ahora la categoría va recortada y codificada.
6. **Web del club rota**: se anteponía siempre `https://`; con la web guardada con esquema salía `https://http://…`
   (28676: `https://http://padelbarcelonaprat.com/`). Ahora se normaliza (`CampeonatoEquiposRepository.WebClub`).
7. **Compartir**: los enlaces apuntaban a `padelfederacion.es/menores/Torneo.asp` (otra página) y a Google+ (cerrado).
8. **Icono de adjunto roto**: con `Icono` vacío se pedía `refs/images/-- .jpg` (404; 28854). Ahora el tipo se deduce de
   la extensión del fichero y, si no, del icono declarado.
9. **Información adicional duplicada** (bajo la información general y en su propia sección).
10. **Texto de cuadro mal cerrado**: en mixto 4.ª y 5.ª categoría el ASP escribía `</%%>` tras el nombre (HTML inválido).
11. HTML: `<meta charset=iso-8859-1>` tras los scripts, `<script>` fuera de `<head>`, `<h4>` sin `<h2>`/`<h3>`, tablas
    de maquetación, iframes sin `title`, imágenes de cuadro sin `alt` (el texto del cuadro iba fuera del enlace).

## Mejoras respecto al original

- Cabecera con el nombre, fechas ("Del viernes 13 al domingo 15 de noviembre de 2026"), sede, etiquetas (equipos,
  categoría, selecciones, silla de ruedas) y cifras (fechas, cuota, número de cuadros).
- Índice lateral solo con las secciones que tienen contenido (chips en móvil) y anclas (`#cuadros`, `#notificaciones`…).
- Cuadros como tarjetas enlazadas con el tipo (cuadro, consolación, liguilla, grupo, previa), agrupadas por género y
  categoría, en lugar de imágenes sin texto alternativo.
- Adjuntos como lista de documentos con tipo y acción (descargar / abrir), plegables a partir de 8.
- Pasos de la inscripción numerados, con su plazo propio cuando lo tienen (preinscripción).
- Sede con enlace a la web del club y al mapa (OpenStreetMap, sin cargar la API de Google Maps).
- Botón "Compartir" (Web Share API o copia del enlace).
- Respuesta cacheada 5 min; una sola ida y vuelta para el detalle.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| Textos fijos de cuadro con formatos mezclados ("2ª Categoría" / "Segunda Categoría", "Cuadro Consolación de 2ª Categoría", "GrupoA", "GRUPO A") | "Segunda categoría", "Consolación de segunda categoría", "Grupo A"… (mayúscula solo inicial, ordinales en letra) | Uniformidad; los nombres personalizados de la BD se muestran tal cual |
| Mixto 5.ª categoría por defecto: "4ª Categoría" | "Quinta categoría" | Errata (copiado del bloque de 4.ª) |
| Masculino liguilla de 3.ª: "15º-17ª Puesto" | "Puestos 15.º a 17.º" | Errata de género del ordinal |
| "Te recordamos que para poder alinear a un jugador/a es necesario que tenga la licencia renovada y que haya firmado las cláusulas de LOPD" | "Para alinear a un jugador o jugadora es necesario que tenga la licencia renovada y que haya firmado las cláusulas de protección de datos." | Lenguaje claro (LOPD → protección de datos). **DECISIÓN FEP** |
| Pasos de inscripción: "excell", "CREAR un NUEVO equipo", "Ver el listado de Clubes realizados el pago." | "Excel", "crear un equipo nuevo", "Ver el listado de clubes que han realizado el pago" | Ortografía y redacción |
| Streaming 28133: "STREAMING Semifinales Sábado Mañana" + URL con `?si=…` | "Semifinales, sábado por la mañana" + URL `/embed/` sin parámetro de seguimiento | Redacción; `si` es un identificador de seguimiento de YouTube |
| 10666: "5º y 6º Puesto" | "5.º y 6.º puesto" | Ordinales |
| Cuota: solo la mayor (`rs_cuota` primera fila) | Todas las cuotas distintas ("80 € / 60 €") | Con varias cuotas el ASP ocultaba las demás |
| Visitas de las noticias relacionadas | No se muestran | Dato interno sin valor para el usuario |
| `Ciudad` en minúsculas ("barcelona") | Tal cual | Dato de la BD: **DECISIÓN FEP** si se normaliza en la gestión |

## Comprobación de enlaces (13/11/2026)

- Fichas públicas descargadas: 28676, 28854, 28133, 30148, 29090 → 200. `IdTorneo=99999999` → **500**.
- Adjuntos y cartel (`/Paginas/Torneos/2026/…`, con espacios, tildes y paréntesis codificados): los 6 probados → 200
  (también por `padelfederacion.es` sin `www`; la API usa `www.padelfederacion.es`, la base de `Media:BaseUrl`).
- `PrevioEquipos.asp?…` → 302 a `CuadroEquipos16.asp?…` (funciona). `Camp_Equipos_Modificar.asp` → 302 a
  `Camp_Equipos_ModificarCOPA.asp`. `Camp_Equipos_InscPrevia.asp` → 302 a `Camp_Equipos_AltaClubAC.asp`.
  `Camp_Equipos_Clubes.asp`, `Camp_Equipos_Nuevo.asp`, `Camp_Parejas_Inscritos.asp` → 200.
- `ArbitrajeCtoFep.asp?Id=x` → 500 con un código inventado (no había partidos activos para probar uno real).
- `refs/images/-- .jpg` (icono de adjunto vacío) → **404**.
- Web del club 28676: `padelbarcelonaprat.com` **no resuelve** (DNS) ni por http ni por https: la sede tiene la web
  desactualizada en `SFEP_Clubes`.

## Modo prototipo (mocks)

`mock/competicion-campeonatos-equipos-{28133,28676,28854,30148}.json`: instantánea de la web pública del 13/11/2026
(ficha de cada torneo + fechas, categoría y título de `competicion-campeonatos.json`), con la misma forma que el DTO.
Los vídeos (28133) y los nombres de cuadro fijados a mano salen de `Contenidos/campeonatos-equipos.json` con
`tools/generar-mock-campeonatos-equipos.ps1`, que conserva el resto. Limitaciones:

- La inscripción de los cuatro está cerrada; la web no muestra `FechaInscripcion`, `ClaseEquipos`, `Preinscripcion`
  ni `Circuito`, así que `fechaLimite` es `null`, `acciones` está vacío y `por` es un **SUPUESTO** (federaciones en las
  copas de ligas autonómicas, clubes en el resto). El estado "inscripción abierta" no se puede ver en modo prototipo.
- Sin coordenadas de club (la web no pintaba mapa en ninguno), sin marcadores, nota de prensa ni noticias relacionadas.
- 28854: los géneros salen de los cuadros (la web no pinta la línea de categorías en veteranos).
- Los títulos de cuadro por defecto se han pasado a los textos nuevos; los personalizados ("Descenso 2ª Categoría" en
  30148) se conservan.
- Los demás torneos por equipos del listado enlazan a la página nueva, pero sin mock muestran el aviso con enlace a la
  web actual (con la API funcionan todos).

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: un torneo con `Borrado='S'` y `Visible='S'` se veía en la web actual; ahora responde 404.
- **DECISIÓN FEP**: ¿se mantienen las búsquedas de nombre "raras" (femenino grupos A/B con nivel 2, mixto 1.ª/2.ª con
  `ABSOLUTO` fijo)? Corregirlas cambiaría lo que hoy se ve en torneos que tengan nombres guardados con esas claves.
- **DECISIÓN FEP**: textos fijos de cuadro unificados y aviso de licencia reescrito (tabla de correcciones).
- **DECISIÓN FEP**: sección "En directo" oculta en campeonatos terminados sin marcadores ni vídeos.
- **DECISIÓN FEP**: los vídeos y las excepciones por torneo deberían gestionarse desde la gestión (modelo propuesto);
  mientras tanto se editan en `Contenidos/campeonatos-equipos.json`. Las excepciones 25071, 28676, 28677, 28854 y 23069
  son de campeonatos pasados o en curso: ¿siguen haciendo falta?
- **SUPUESTO**: ruta nueva `campeonato-equipos.html?id=` (no hay fila en `redirecciones.csv`; falta añadir la 301
  `/Camp_Equipos.asp?IdTorneo={id}` → `/campeonato-equipos.html?id={id}`).
- **SUPUESTO**: una fila de `SFEP_Torneos_Desglose` por género y categoría; si hay varias, ahora se pinta un grupo por fila.
- **SUPUESTO**: tipos de columnas (`IdPrueba`, `[Año]`, `TorneoPadre`, `IdArchivo`, `IdNoticia`, `Nivel` enteros; fechas
  `datetime`; `Ver…`, `Visible…`, `Preinscripcion`, `ClaseEquipos`, `Circuito`, `Genero`, `Tipo` `char`/`varchar`; `Cat2`
  numérico; `Cuota` numérica o texto numérico (`TRY_CONVERT`); `Latitud`/`Longitud` texto o número; `TipoNoticia`
  numérico; `SFEP_Torneos.Club` = `SFEP_Clubes.IdClub`, comparado como texto). Las columnas `Ver…` existen todas en
  `SFEP_Torneos_Desglose` (el ASP leía las 46 de la misma fila).
- **SUPUESTO**: `FechaInscripcion`, `FechaPreinscripcion` y `FechaFijarPuntua` en hora local de España.
- Actualizar en `SFEP_Clubes` la web de "PADEL BARCELONA-EL PRAT" (dominio que no resuelve).
- Las páginas enlazadas (cuadros, equipos, inscripción, marcadores) siguen en la web actual.

## Modelo SQL propuesto

Lo que estaba escrito a mano en el ASP (hoy en `Contenidos/campeonatos-equipos.json`), para gestionarlo desde la
gestión sin cambiar el contrato:

```sql
-- Retransmisiones por torneo (sustituye a los if IdTorneo=28133)
CREATE TABLE SFEP_Torneos_Videos (
    IdVideo     int IDENTITY(1,1) PRIMARY KEY,
    IdPrueba    int            NOT NULL,           -- SFEP_Torneos.IdPrueba
    Orden       int            NOT NULL DEFAULT 0,
    Titulo      nvarchar(150)  NOT NULL,
    UrlEmbed    nvarchar(300)  NOT NULL,           -- https://www.youtube.com/embed/…
    Visible     char(1)        NOT NULL DEFAULT 'S',
    Borrado     char(1)        NOT NULL DEFAULT 'N'
);
CREATE INDEX IX_SFEP_Torneos_Videos_IdPrueba ON SFEP_Torneos_Videos (IdPrueba, Orden);

-- Excepciones de la inscripción (sustituyen a los IdTorneo escritos a mano)
ALTER TABLE SFEP_Torneos ADD
    MostrarListadoClubes char(1) NOT NULL DEFAULT 'S',  -- 'N' en 25071, 28676, 28677, 28854
    MostrarPagoEquipos   char(1) NOT NULL DEFAULT 'S';  -- 'N' en 23069
```

El nombre "5.º y 6.º puesto" del 10666 se puede guardar como una fila más de `SFEP_Torneos_Cuadros_Nombres`
(`IdPrueba=10666`, `Nivel=1`, `Categoria=<Tipo>`, `Genero='F'`, `Cuadro='Consola'`) si el repositorio pasa a buscar
nombre también en las consolaciones. Índices recomendados para el detalle: `SFEP_Torneos_Desglose (IdPrueba, Genero)`,
`SFEP_Torneos_Cuadros_Nombres (IdPrueba)`, `SFEP_Torneos_Adjuntos (IdTorneo)`, `SFEP_Torneos_Arbitraje (IdPrueba)`,
`SFEP_Torneos_Noticias (IdTorneo)` (**SUPUESTO**: no se ha podido ver el esquema real).

## Ficheros

- API: `Contracts/Competicion/CampeonatoEquiposDto.cs` (DTO, sub-DTO, `ConsultaCampeonatoEquipos` y contenido escrito a
  mano), `Contracts/Competicion/ReglasCampeonatoEquipos.cs` (definición de cuadros por género, pasos de inscripción,
  normalizaciones), `Data/Repositories/Competicion/CampeonatoEquiposRepository.cs` (Dapper),
  `Data/Repositories/Competicion/ContenidoCampeonatosEquiposRepository.cs` (JSON), `Contenidos/campeonatos-equipos.json`,
  `Controllers/Competicion/CampeonatosEquiposController.cs`; registro en `Program.cs`; tests en
  `tests/FEP.Api.Tests/CampeonatosEquiposTests.cs` (validación 400, 404 con `urlWebActual`, hora local con repositorio
  falso, contenido de `Contenidos/` y reglas heredadas; no necesitan SQL Server).
- Web: `wwwroot/campeonato-equipos.html`, `assets/js/campeonato-equipos.js` (con saneado de HTML por lista blanca,
  copia reducida del de `micrositio.js`), estilos `.cpe-*` en la sección "Páginas interiores" de `assets/css/fep.css`
  (y su bloque responsive); reutiliza `.ms-rich`, `.ms-embed`, `.ms-table`, `.doc-list`, `.fedf-contact`, `.org-figure`,
  `.org-table`, `.camp-item__plazo`.
- Mock: `mock/competicion-campeonatos-equipos-{28133,28676,28854,30148}.json` + `tools/generar-mock-campeonatos-equipos.ps1`
  + `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS` y `QUERY_MIGRADA` (`camp_equipos`, `idtorneo` → `id`) y `paginaMigrada` exportada en
  `assets/js/comun.js`; `campeonatos.js` y `programa-impulso.js` enlazan ya a la ficha nueva.
