# Análisis: `Impulso.asp` → `programa-impulso.html` + `GET /api/v1/competicion/programa-impulso`

URL actual: `/Programa_Impulso` (regla `Impulso` de `web.config`: `^Programa_Impulso` → `Impulso.asp`; opción de
menú "Programa IMPULSO" bajo Competición, `SFEP_Web_Menu` id 32, enlace `Programa_Impulso`). Ruta nueva propuesta en
`redirecciones.csv`: `/programa-impulso` → `programa-impulso.html`. El formulario hacía `POST` a `Programa_Impulso`
con `ano`; la página nueva acepta `?anio=2023` y el nombre antiguo `?ano=2023`, y `comun.js` traslada los enlaces
antiguos (`Programa_Impulso` e `Impulso.asp`).

Al pulsar un torneo **sin** `LinkCuadros`, el ASP enviaba otro formulario `POST` a `Torneos_Previo.asp` (con
`idtorneo`); esas reglas ya están en la API (`ReglasCampeonatos.RutaFicha`, ver `Campeonatos.md`) y se reutilizan.

Includes: `conexionSQL.inc` (no se copia), `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (ya migrados en
`comun.js` + columna de patrocinadores), `finconexion.inc`; todos disponibles en `X:\TICS\WEB\include\`.
Codificación del `.asp`: Windows-1252. En el mapa de la web la fila decía `Programa_Impulso.asp`: el fichero real es
`Impulso.asp` (corregido).

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, barra lateral, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (`activo=3`) | Ya migrado: `comun.js` (+ columna de patrocinadores) |
| `sql_noticias` (`SFEP_Noticias`, `SELECT *`) | Se consultaba **pero no se usaba** | Eliminado |
| Título "programa impulso" | HTML a mano (`h5`) | `h1` "Programa Impulso." + entradilla |
| Imagen `refs/Noticias/Impulso2017.jpg` (logotipo del programa) | HTML a mano, `alt="Cto. España Empresas"` | `imagen`/`imagenUrl`/`imagenAlt` en `Contenidos/programa-impulso.json`; copia local `assets/img/fep/programa-impulso/impulso-2017.jpg` para el mock |
| Tabla "Información Circuito Impulso 2026" / "Calendario Tentativo Circuito Impulso 2026" con dos botones-imagen `pdfdescargar.jpg` | HTML a mano: `refs/docs/Programa-Impulso-2026 15-01-26.pdf`, `refs/docs/CALENDARIO-IMPULSO-2026.pdf` | `documentos` (título, descripción, temporada, fecha, tipo, ruta original y URL codificada) |
| — (nuevo) | El texto del programa solo estaba dentro del PDF | `introduccion`: 3 párrafos resumidos del PDF de bases 2026 (**DECISIÓN FEP**: validar) |
| "Filtros del listado de Campeonatos (N)" + desplegable "Año del Torneo" | `sql_anotorneos`: `SFEP_Torneos.Año` con `IdFederacion=0`, `Borrado='N'`, `Visible='S'`, `ProgramaImpulso='S'`, `GROUP BY Año HAVING Año>1900 ORDER BY Año DESC`; por defecto el primero | `anios` (y `anio` = el más reciente si no se pide); contador en `role="status"` y en la cabecera |
| Listado de torneos | `sql_torneos` (ver abajo) `ORDER BY FechaInicio DESC` | `items` (`CampeonatoDto`, el mismo formato que `/competicion/campeonatos`) |
| Nombre del torneo (enlace) | `Torneo`; si `LinkCuadros` → `https:\\{LinkCuadros}` en ventana nueva; si no, `javascript:irCamp(IdPrueba)` → `POST Torneos_Previo.asp` | `titulo` + `enlace` (URL absoluta) + `nuevaVentana` |
| Fechas "6 JUL - 12 JUL" | `day()` + mes abreviado (dos `select case` de 12 ramas) | `fechaInicio`, `fechaFin` (`yyyy-MM-dd`); el front usa `bloqueFecha` de `comun.js` |
| "Ciudad - Club" | `Ciudad` + `sql_club` (**una consulta por torneo** a `SFEP_Clubes`) + `Club2`; "Sin definir" si no hay | `ciudad` + `clubes` (`OUTER APPLY`, sin N+1; vacío si no hay) |
| "Fecha Tope de Inscripción" | `FechaInscripcion`/`FechaPreInscripcion` comparadas con `now()` | `inscripcion` (`tipo` + `fechaLimite`) solo si el plazo sigue abierto |
| Colores por `ModalidadTorneo`/`TipoTorneo` (`estiloMod`, `estiloTipo`) | Se calculaban **pero no se pintaban** | `tipo`/`categoria` llegan en el DTO; la página no muestra etiquetas (como hoy) |

`sql_torneos` (columnas): `IdPrueba`, `IdTorneo` (no se usaba), `Torneo`, `FechaInicio`, `PPT` (no se usaba),
`ModalidadTorneo`, `FechaFin`, `Ciudad`, `FechaInscripcion`, `FechaPreInscripcion`, `TipoTorneo`, `Club`, `Club2`,
`LinkCuadros`. La consulta nueva añade `TorneoPadre`, `Liga`, `Empresas` (las usaba `Torneos_Previo.asp` para elegir
la ficha) y `Circuito` (silla de ruedas, como en Campeonatos). Filtros:

```sql
WHERE IdFederacion = 0 AND Año = '<ano>' AND Borrado = 'N' AND Visible = 'S' AND ProgramaImpulso = 'S'
      [AND tipoTorneo = '<modalidad>'] [AND ModalidadTorneo = '<tipo>']
ORDER BY FechaInicio DESC
```

La página **no escribe** en la BD.

## Parámetros (`Request`) y validación

| ASP | API nueva | Validación |
|---|---|---|
| `ano` (`""`/`--` → el año más reciente) | `anio` (opcional) | entero 1901–2100; si no, **400** `ValidationProblemDetails` (los no numéricos los rechaza el *model binding*) |
| `modalidad`, `tipo` | — | Se leían de `Request` y filtraban el SQL, pero **ningún formulario los enviaba** (solo había el desplegable de año): eliminados |
| `listado` | — | Se leía y no se usaba: eliminado |
| `idtorneo` (a `Torneos_Previo.asp`) | — | El enlace a la ficha lo calcula la API |

Una temporada válida sin torneos devuelve `items: []`; la página, si la temporada no está en `anios` (enlace
antiguo), vuelve a la más reciente.

## Lógica / visibilidad heredada

- **Año por defecto**: `rs_anotorneos("Año")` sin comprobar EOF: si no hubiera ningún torneo del programa, el ASP
  daría error. Ahora `anio = null` y la página dice que no hay torneos publicados.
- **Distinto de Campeonatos**: aquí no se filtran `PPT`, `Empresas` ni `TorneoLigadoFEP`, y no hay excepción del
  CEM 2021. Se mantiene igual. (Campeonatos excluye `ProgramaImpulso='S'`, así que un torneo sale en una página o en
  la otra, nunca en las dos.)
- **Plazo de inscripción**: el ASP pintaba "Fecha Tope de Inscripción" con la fecha de **preinscripción** si esta
  seguía abierta y era distinta de la inscripción (sin mirar si era por equipos), y con la de inscripción en otro
  caso. Se aplica la regla común de `ReglasCampeonatos.Plazo` (preinscripción solo en equipos; etiqueta correcta
  "Preinscripción abierta"). En la instantánea no hay ningún plazo abierto. **DECISIÓN FEP** menor.
- `activo=3`: opción de menú activa (plantilla). Funciones JS `MesAnterior`, `CargarEvento`, `MeGusta`, `busqueda`:
  no se usan. `<span class="solicitaclave2">` vacío en cada torneo: restos de plantilla.
- La tabla de documentos está fijada a mano en 2026: cada temporada hay que editar `Contenidos/programa-impulso.json`
  (como antes el `.asp`).

## Problemas del original

1. **Inyección SQL**: `ano`, `modalidad` y `tipo` se concatenaban entre comillas tras `limpiar()` (lista negra);
   `Torneos_Previo.asp` concatenaba `IdTorneo` sin comillas. Ahora `@Anio` con Dapper y la ficha se calcula en la API.
2. **N+1**: una consulta a `SFEP_Clubes` por torneo, concatenando el código del club. Ahora `OUTER APPLY`.
3. **Doble recorrido** del recordset (contar y pintar) y `SELECT *` de noticias sin usar.
4. **XSS**: `Torneo`, `Ciudad`, `Club`, `Club2` sin `Server.HTMLEncode`. El front usa `textContent`.
5. **Enlaces con barras invertidas** (`https:\\www.fmpadel.com/…`): no es una URL válida (los navegadores la
   corrigen). Ahora `https://…` normalizada (`ReglasCampeonatos.EnlaceCuadros`).
6. **`alt` copiado de otra página**: la imagen del programa tenía `alt="Cto. España Empresas"`. Corregido.
7. **Botones de descarga como imagen** (`pdfdescargar.jpg`, sin `alt`): el nombre del documento era una celda en
   negrita separada del enlace. Ahora el título es el enlace y hay botón "Descargar" con `aria-label`.
8. **Título "Filtros del listado de Campeonatos"** en una página que lista torneos del Programa Impulso: ahora
   "Torneos del programa 2026".
9. **Texto basura en el origen**: no tiene (a diferencia de Campeonatos), pero el mensaje sin torneos decía
   "No Hay torneos visibles en el año seleccionado"; ahora "No hay torneos del Programa Impulso publicados en esta
   temporada".
10. HTML: `<meta charset=iso-8859-1>` tras los scripts, `<script>` fuera de `<head>`, sin `<main>`, formulario que
    solo funcionaba con JavaScript (`onChange="cambiar()"`), `<table>` de maquetación para los PDF.

## Mejoras respecto al original

- Presentación del programa (3 párrafos) para que la página se entienda sin abrir el PDF.
- Documentos como lista de descarga con tipo, temporada, fecha de la versión y descripción.
- Enlaces directos a los cuadros o a la ficha de cada torneo (sin `javascript:irCamp` ni `Torneos_Previo.asp`); los
  externos se abren en pestaña nueva con aviso para lectores de pantalla.
- Índice lateral (El programa · Bases y calendario · Torneos) con contadores; temporada en la URL.
- Cifras en la cabecera (torneos de la temporada, temporadas, documentos).
- Respuesta cacheada 5 min (output cache varía por `anio`); contenido + torneos en una sola petición.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| `alt="Cto. España Empresas"` | "Logotipo del Programa Impulso de la Federación Española de Pádel" | `alt` copiado de otra página |
| "Calendario Tentativo Circuito Impulso 2026" | "Calendario tentativo Circuito Impulso 2026" | Mayúsculas de estilo |
| "Sin definir" (sin club) | Solo la ciudad | No aporta información; el original lo mostraba |
| `https:\\…` | `https://…` | URL mal formada |
| "FIP SILVER ALCALÁ DE GUADAIRA " (espacio final), "CD  LA CALZADA", "	MALAGA" (tabulador) | Recortados / espacios simples al pintar | Datos de la BD con espacios sobrantes |
| Mayúsculas de `Ciudad` ("santander", "Malaga", "Tarragona") | Tal cual | Dato de la BD: **DECISIÓN FEP** si se normaliza en la gestión |
| — | Fecha 15/01/2026 del PDF de bases | Tomada del nombre del fichero (`…2026 15-01-26.pdf`) |

## Comprobación de enlaces (13/11/2026)

- `https://www.padelfederacion.es/refs/docs/Programa-Impulso-2026%2015-01-26.pdf` → 200 (138 KB).
- `https://www.padelfederacion.es/refs/docs/CALENDARIO-IMPULSO-2026.pdf` → 200 (98 KB).
- `https://www.padelfederacion.es/refs/Noticias/Impulso2017.jpg` → 200 (960×382). No está en `X:\TICS\WEB\refs\`
  (allí no hay carpeta `Noticias`): copiada desde la web pública.
- `https://www.padelfederacion.es/Paginas/fapN/Prueba_Cuadros.asp?IdTorneo=28731` (FIP Silver Alcalá 2026) → 200.
  El mapa de la web la marcaba como "página de pruebas publicada"; es el `LinkCuadros` del torneo en la BD.
- `https://padelcv.com/competicion/torneos/fip-bronze-castellon-automocion-cano-kia/` → 200.
- `Torneos_Previo.asp?IdTorneo=11657` (Impulso Santander 2017, único sin `LinkCuadros`) → 302 a `Camp_Parejas.asp?IdTorneo=11657`.
- Los enlaces de temporadas antiguas (fmpadel.com, fcpadel.cat, padelcv.com, federacionpadelmurcia.com y páginas de
  federaciones en `padelfederacion.es/Paginas/…`) no se han comprobado uno a uno: son datos históricos de la BD.

## Modo prototipo (mocks)

`mock/competicion-programa-impulso.json`:
- Presentación, imagen y documentos: generados desde `Contenidos/programa-impulso.json` con
  `tools/generar-mock-programa-impulso.ps1` (la imagen apunta a la copia local).
- Torneos: instantánea de la web pública del 13/11/2026 de las **8 temporadas** (2026, 2023, 2022, 2021, 2020, 2019,
  2018, 2017; **26 torneos**). El script los conserva al regenerar. Como el mock no puede filtrar,
  `programa-impulso.js` filtra siempre por temporada en el navegador.

Limitaciones del mock: la web no muestra el `IdPrueba` de los torneos con `LinkCuadros` (`id: 0`; solo el de Santander
2017, `11657`, es real); `tipo`/`categoria` (`parejas`/`absoluto`) son **SUPUESTO** (el ASP no los pintaba);
`fechaInicio`/`fechaFin` reconstruidas con día y mes de la web y el año de la temporada; ningún plazo abierto.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: validar los 3 párrafos de presentación (resumen del PDF de bases 2026: programa desde 2014,
  pruebas FIP Bronze–Platinum del CUPRA FIP Tour, 8 pruebas, 10 ayudas de 250 € a junior y sub-21). Si se prefiere
  la página como estaba, basta con dejar `introduccion: []`.
- **DECISIÓN FEP**: el calendario tentativo 2026 tiene 8 pruebas, pero en la BD solo 2 torneos de 2026 están marcados
  con `ProgramaImpulso='S'` (Castellón y Alcalá de Guadaira). ¿Faltan por marcar Collbató, El Prat, Melilla, Astorga,
  A Coruña y Alicante?
- **DECISIÓN FEP**: el FIP Silver Alcalá 2026 enlaza a `Paginas/fapN/Prueba_Cuadros.asp` (página de la FAP con nombre
  de "prueba"). Responde 200; se mantiene. ¿Es el enlace definitivo?
- **DECISIÓN FEP**: no hay temporadas 2024 y 2025 en el desplegable (no hay torneos marcados): ¿no hubo programa o
  falta marcarlos?
- **DECISIÓN FEP**: plazo de inscripción con la regla común (ver "Lógica heredada").
- **SUPUESTO**: tipos de columnas iguales que en `Campeonatos.md` (`ProgramaImpulso` `char(1)` 'S'/'N').
- **SUPUESTO**: `FechaInscripcion`/`FechaPreInscripcion` en hora local de España.
- Cada temporada hay que actualizar los PDF en `Contenidos/programa-impulso.json` y volver a generar el mock.

## Modelo SQL propuesto

Los torneos ya están en BD. Para los documentos y la presentación (hoy en el JSON), cuando se quiera editar desde la
gestión basta con esto y cambiar `ContenidoProgramaImpulsoRepository` por Dapper (el contrato no cambia):

```sql
CREATE TABLE SFEP_Web_Programas (IdPrograma int IDENTITY PRIMARY KEY, Clave varchar(40) UNIQUE, Titulo nvarchar(150),
                                 Introduccion nvarchar(max) NULL,   -- párrafos separados por línea en blanco
                                 Imagen nvarchar(260) NULL, ImagenAlt nvarchar(200) NULL, FechaUlt datetime);
CREATE TABLE SFEP_Web_Programas_Documentos (IdDocumento int IDENTITY PRIMARY KEY, IdPrograma int REFERENCES SFEP_Web_Programas,
                                 Clave varchar(40), Titulo nvarchar(250), Descripcion nvarchar(500) NULL, Temporada int NULL,
                                 Fecha date NULL, Tipo varchar(10) NULL, Archivo nvarchar(260), Orden int,
                                 Visible char(1) DEFAULT 'S', Borrado char(1) DEFAULT 'N');
```

Índice recomendado para los torneos: `SFEP_Torneos (IdFederacion, ProgramaImpulso, [Año]) INCLUDE (Borrado, Visible,
FechaInicio)` (**SUPUESTO**: no se ha podido ver el esquema real).

## Ficheros

- API: `Contracts/Competicion/ProgramaImpulsoDto.cs` (`ProgramaImpulsoDto`, `ContenidoProgramaImpulsoDto`,
  `DocumentoProgramaImpulsoDto`, `TorneosProgramaImpulsoDto`; los torneos reutilizan `CampeonatoDto`),
  `Data/Repositories/Competicion/ContenidoProgramaImpulsoRepository.cs` (JSON, singleton),
  `Data/Repositories/Competicion/TorneosProgramaImpulsoRepository.cs` (Dapper; reglas de `ReglasCampeonatos`),
  `Controllers/Competicion/ProgramaImpulsoController.cs`, `Contenidos/programa-impulso.json`; registro en `Program.cs`
  (`anio` ya estaba en las claves de output cache). Tests en `tests/FEP.Api.Tests/ProgramaImpulsoTests.cs`
  (validación 400, contenido y URLs codificadas, año y hora que llegan al repositorio falso, forma del JSON).
- Web: `wwwroot/programa-impulso.html`, `assets/js/programa-impulso.js`, estilos `.imp-*` en la sección "Páginas
  interiores" de `assets/css/fep.css` (y su bloque responsive); reutiliza `.doc-list`/`.doc`, `.clubs-results`,
  `.clubs-form`, `.camp-item`, `.transp-text`, `.page-note`. Imagen en `assets/img/fep/programa-impulso/impulso-2017.jpg`.
- Mock: `mock/competicion-programa-impulso.json` (`tools/generar-mock-programa-impulso.ps1`) + `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS` y `QUERY_MIGRADA` en `assets/js/comun.js` (`programa_impulso` e `impulso` →
  `programa-impulso.html`, con `ano` → `anio`).
