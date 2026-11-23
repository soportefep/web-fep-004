# Mapa de migración: `X:\TICS\WEB` → `FEP.Api` + `FEP.Web`

Lo mantiene el agente `asp-reestructurador`. Inventario fila a fila en
[`inventario.csv`](inventario.csv); librerías de terceros en [`terceros.md`](terceros.md);
contratos de la API antigua en [`api-antigua.md`](api-antigua.md); URLs públicas en
[`redirecciones.csv`](redirecciones.csv); orden de trabajo en [`plan-fases.md`](plan-fases.md).

## Cómo leer `inventario.csv`

Una fila por fichero propio (`.asp`, `.inc`, `.js` y restos `.txt/.bak/.tmp/.html/.config`),
2.097 filas. Separador `,`; las listas dentro de una celda van separadas por `;`.

| Columna | Contenido |
|---|---|
| `ruta` | relativa a `X:\TICS\WEB` |
| `area` | área funcional (tabla de abajo) |
| `tipo` | extensión |
| `tamano`, `modificado` | bytes y fecha. **Ojo:** 1.387 ficheros (casi todo `gestion\` e `include\`) tienen `2026-11-10`, que es la fecha de la copia, no la del último cambio real |
| `includes` | ficheros incluidos con `<!--#include -->` |
| `tablas` | tablas `SFEP_*` citadas (en mayúsculas) |
| `escribe_bd` | `si` si hay `INSERT INTO`, `UPDATE … SET`, `DELETE FROM` o `.AddNew` |
| `usa_request` | `si` si lee `Request(…)`, `.Form`, `.QueryString`, `.Cookies` o `.BinaryRead` |
| `destino` | `API: … \| Web: …`, `REVISAR \| …` o `DESCARTAR` |
| `prioridad` | 1 = home y layout · 2 = público de lectura · 3 = competición pública y API Legacy · 4 = área privada y pagos · 5 = gestión · 9 = revisar antes de decidir · vacío = descartar |
| `notas` | `url_publica=` (regla de `web.config`), `duplicado_exacto_de=`, `contiene_credenciales=si(tipo)`, `usa=` (sesion, email, subida, pdf, tpv, json, excel), `codificacion=utf8` (si no es Windows-1252), `entrantes=` (nº de ficheros que lo enlazan o incluyen), motivo de descarte |

Criterios de **descarte**: copias (`- copia`, `- Bueno`, `Bueno220425`), fechas en el nombre
(`300915`, `23-05-14`, `070525`), pruebas (`EmailPrueba`, `InscripPrueba`, `ModEjemplo`…),
backups (`.txt`, `.bak`, `.aspok`, `.tmp`), `API\Desarrollo\` (copia de producción),
scripts numerados de mantenimiento de `gestion\` (`0-…`, `1-…`, `75-Recalcular…`), utilidades
y conexiones alternativas, y páginas de la raíz **sin enlaces entrantes, sin URL pública y sin
cambios desde antes de 2022**. "Revisar" = sin enlaces entrantes pero con cambios recientes
(o con la fecha de copia), o versiones `…2`/`…b` de otra página: pueden llamarse desde JS,
desde la BD (menús) o desde fuera, así que alguien de la FEP debe confirmarlo.

Limitación conocida: los enlaces entrantes se cuentan por **nombre** de fichero, así que
`index.asp` (raíz) y `gestion\Index.asp` comparten contador.

## Cifras por área

M = migrar · R = revisar · D = descartar · E = escriben en BD.

| Área | Total | M | R | D | E | API destino | Web destino |
|---|---:|---:|---:|---:|---:|---|---|
| publica-portada | 2 | 1 | 0 | 1 | 0 | `/api/v1/portada` (+ menu, banners, avisos, noticias/destacadas, formacion/convocatorias…) **ya existe** | `index.html` (diseño Figma) |
| include-layout | 18 | 15 | 2 | 1 | 0 | `/api/v1/menu`, `/redes-sociales`, `/banners`, `/avisos`, `/patrocinadores`, `/multimedia/fotos/ultimas` **ya existen** | componentes de cabecera, barra lateral y pie |
| include-conexion-bd | 4 | 2 | 0 | 2 | 0 | `Data/IDbConnectionFactory` **ya existe** | — |
| publica-noticias | 4 | 4 | 0 | 0 | 1 | `NoticiasController`: `GET /api/v1/noticias?etiqueta&page`, `/noticias/{id}` | `noticias.html`, `noticia.html` |
| publica-documentos | 13 | 12 | 1 | 0 | 1 | `DocumentosController`: `GET /api/v1/documentos?tipo=estatutos\|memorias\|reglamentos\|normativas\|…` | `documentos.html` |
| publica-institucional | 42 | 35 | 2 | 5 | 2 | `ContenidosController` (`/paginas/{slug}`), `FederacionesController`, `ClubesController` | páginas institucionales |
| publica-ajax-auxiliares (`combo*`, `dato*`) | 74 | 70 | 0 | 4 | 0 | `MaestrosController` `/api/v1/maestros/*` y validaciones en los POST | `<select>`/autocompletar del front |
| rankings | 21 | 8 | 7 | 6 | 7 | `RankingsController` (`/api/v1/competicion/rankings` **ya existe**: índice Rankings.asp → `rankings.html`) y `ClasificacionesRankingController` (`/api/v1/competicion/rankings/{clave}?genero=&categoria=&licencia=&busqueda=&limite=` y `/{clave}/normativa` **ya existen** para `absoluto`: Ranking_Absoluto.asp + CargarRnkAbs.asp + CargarRnkAbsNombre.asp → `ranking-absoluto.html`; para `menores` (sin normativa): Ranking_Menores.asp + CargarRnkMen.asp → `ranking-menores.html`, **migrada**; y para `silla-ruedas` (sin normativa; `TipoRanking` 15, lista única mixta con licencias de silla "PS…"): Ranking_Silla_Ruedas.asp → `ranking-silla-ruedas.html`, **migrada**; y para `veteranos` (sin normativa; categorías +35…+60 × género, `TipoRanking` 10035–11060): Ranking_Veteranos.asp + CargarRnkVet.asp → `ranking-veteranos.html`, **migrada**; otro ranking de jugadores solo necesitaría declarar sus `TipoRanking` en `ReglasClasificaciones`) y `RankingEquiposController` (`/api/v1/competicion/rankings/equipos-absolutos?anio=` **ya existe**: Ranking_Equipos_Absolutos.asp → `ranking-equipos-absolutos.html`, recurso propio porque sus puestos son equipos de club por temporada) y `EstadisticasJugadorController` (`/api/v1/competicion/jugadores/{licencia}/estadisticas` **ya existe**: Estadisticas.asp + CargarPruebas.asp → `estadisticas.html`, **migrada**; búsqueda solo por licencia, la de DNI sigue en la web actual) | `rankings.html`, `ranking-absoluto.html`, `ranking-menores.html`, `ranking-silla-ruedas.html`, `ranking-veteranos.html`, `ranking-equipos-absolutos.html` (JS `ranking-equipos.js`) (JS común `ranking.js`), `estadisticas.html` (JS `estadisticas.js`) |
| competicion-cuadros | 100 | 75 | 13 | 12 | 10 | `TorneosController`, `CuadrosController` (`/api/v1/competicion/cuadros/…` **ya existe**: Previo.asp, PrevioEquipos.asp, Cuadro*.asp, Liguilla*.asp, EquiposEnfrentamientos.asp → `cuadro.html`), `CalendarioController` (`/api/v1/competicion/calendario` **ya existe**: CalendarioFEP.asp → `calendariofep.html`; `/api/v1/competicion/calendario/federaciones[/{id}]` **ya existe**: CalendarioFT.asp → `calendarioft.html`) | `competicion/*.html` |
| formacion | 45 | 37 | 6 | 2 | 14 | `FormacionController` (`/formacion/convocatorias` ya existe) | `formacion/*.html` |
| api-antigua | 134 | 78 | 2 | 54 | 40 | `Controllers/Legacy/` + servicios v1 (ver `api-antigua.md`) | — |
| inscripciones-campeonatos (`Camp_*`, `Empr_*`) | 136 | 118 | 4 | 14 | 69 | `InscripcionesController` + `PagosController` (JWT) | `area-privada/inscripciones/*` |
| area-federado-licencias | 52 | 43 | 2 | 7 | 28 | `LicenciasController`, `AuthController` (JWT rol Jugador) | `area-privada/licencias/*` |
| certificados-pago | 7 | 6 | 1 | 0 | 5 | `CertificadosController` + `PagosController` | `area-privada/certificados.html` |
| arbitraje-marcador | 35 | 25 | 5 | 5 | 11 | `ArbitrajeController`, `MarcadorController` (JWT rol Árbitro; tiempo real → SignalR) | `area-privada/arbitro/*`, pantalla marcador |
| licitacion-torneos | 19 | 14 | 2 | 3 | 7 | `OrganizacionTorneosController` (`/api/v1/competicion/organizacion-torneos` y `/organizacion-torneos/clubes?provincia=` **ya existen**, solo lectura: Organizacion_Torneos.asp + Organizacion_FueraPlazo.asp + cargarfechastorneo.asp + comboProvincias.asp + comboclubesLicitacion.asp → `organizacion-torneos.html`, **migrada**; el formulario sigue enviándose a Organizacion_P0.asp) y `LicitacionesController` (JWT) para los pasos con escritura (P0–P5, TPV) | `organizacion-torneos.html`, `area-privada/licitacion/*` |
| censo-electoral | 54 | 50 | 3 | 1 | 33 | `CensoController` (solo en periodo electoral) | `censo/*` |
| pagos-redsys (`redsys\`) | 3 | 0 | 0 | 3 | 0 | `PagosController` (firma HMAC-SHA256 propia) | `pago-ok.html`, `pago-ko.html` |
| front-js-propio (`js\`, `refs\` propios) | 21 | 21 | 0 | 0 | 5 | — | reescribir en `assets/js` (módulos ES) |
| utilidades-pruebas | 19 | 0 | 0 | 19 | 3 | — | — |
| include-motor-cuadros | 57 | 25 | 25 | 7 | 34 | `Domain/Cuadros` (servicio) + `Admin/CuadrosController` | — |
| include-rankings | 51 | 43 | 8 | 0 | 13 | `Domain/Rankings` (cálculo) + job de recálculo | — |
| include-fichas | 3 | 3 | 0 | 0 | 2 | `FichasRepository` | — |
| include-otros / include-utilidades | 3 | 2 | 0 | 1 | 0 | `System.Text.Json` | — |
| gestion-competicion | 515 | 343 | 167 | 5 | 284 | `Admin/TorneosController`, `Admin/CuadrosController`, `Admin/ResultadosController`, `Admin/HorariosController` | back-office `gestion/competicion/*` |
| gestion-licencias-fichas | 139 | 78 | 58 | 3 | 65 | `Admin/FichasController`, `Admin/LicenciasController`, `Admin/CarnesController` (PDF) | `gestion/fichas/*` |
| gestion-contenidos | 112 | 103 | 8 | 1 | 56 | `Admin/ContenidosController` (noticias, banners, multimedia, microsites, patrocinadores, hoteles, eventos) | `gestion/contenidos/*` |
| gestion-ajax-auxiliares | 79 | 69 | 10 | 0 | 3 | `Admin/MaestrosController` | componentes de formulario |
| gestion-configuracion-sistema | 57 | 50 | 4 | 3 | 36 | ASP.NET Core Identity (usuarios, roles, 2FA) + `Admin/ConfiguracionController` | `gestion/configuracion/*` |
| gestion-comunicaciones | 43 | 37 | 4 | 2 | 26 | `Admin/ComunicacionesController` + cola de email | `gestion/comunicaciones/*` |
| gestion-pagos | 35 | 27 | 6 | 2 | 24 | `Admin/PagosController` (remesas, justificantes) | `gestion/pagos/*` |
| gestion-formacion | 31 | 31 | 0 | 0 | 19 | `Admin/FormacionController` | `gestion/formacion/*` |
| gestion-rankings | 25 | 20 | 4 | 1 | 16 | `Admin/RankingsController` + job | `gestion/rankings/*` |
| gestion-informes | 24 | 22 | 2 | 0 | 6 | `Admin/InformesController` (CSV/XLSX) | `gestion/informes/*` |
| gestion-clubes-federaciones | 22 | 21 | 1 | 0 | 17 | `Admin/ClubesController`, `Admin/FederacionesController` | `gestion/clubes/*` |
| gestion-integraciones (`API_PM_*`, `API_Post`) | 22 | 2 | 17 | 3 | 1 | `Integrations/PadelManager`, `Integrations/Mobilendo` (HttpClient tipado) | — |
| gestion-scripts-mantenimiento (`0-…`…`79-…`) | 48 | 0 | 0 | 48 | 43 | — (si alguno es recurrente → job programado) | — |
| gestion-otros | 28 | 11 | 5 | 12 | 12 | revisar | revisar |
| **Total** | **2.097** | **1.501** | **369** | **227** | | | |

Por carpeta: raíz 576 (458 / 39 / 79) · `gestion\` 1.180 (814 / 286 / 80) · `include\` 136
(90 / 35 / 11) · `API\Produccion` 91 + `API\FCP_RnkUnico.asp` (78 / 2 / 12) · `API\Desarrollo`
42 (todo descartar) · `refs\` 58 (51 / 7 / 0) · `js\` 10 · `redsys\` 3 (descartar).
Además 77 ficheros de terceros fuera del inventario y ~1.900 imágenes/documentos subidos en
`refs\` (777 MB) que no son código.

## Includes → componentes del front

| Include ASP | Usado por | Qué hace | Destino |
|---|---:|---|---|
| `Menus.inc` | 438 páginas | Lee avisos, banners, menú (`SFEP_Web_Menu`), redes; pinta la cabecera; define `limpiar()` | Componente **cabecera + menú** (`assets/js` + `/api/v1/menu`, `/redes-sociales`). `limpiar()` desaparece: SQL parametrizado |
| `BarraLateral.inc` | 246 | Avisos, patrocinadores, banners de interés | Componente **barra lateral** (`/avisos`, `/patrocinadores`, `/banners?tipo=interes`) |
| `BarraLateralCenso*.inc` (5), `BarraLateralequipos`, `…Licitacion`, `…Microsite*`, `…TV`, `…COVID`, `BarraMujerYDeporte`, `BarraAdaptado`, `BarraPatrocinadores` | 5–17 c/u | Variantes de la barra lateral por sección | **Un solo componente** con variante por sección (no 14 ficheros) |
| `PiePagina.inc` | 366 | Pie, últimas fotos, enlaces | Componente **pie** (`/multimedia/fotos/ultimas`, `/banners?tipo=interes`) |
| `MenuIdioma.inc` | 0 | Selector de idioma | No lo incluye ninguna página: descartar tras confirmar |
| `conexionSQL.inc` / `finconexion.inc` | 1.838 / 853 | Abrir/cerrar ADO con credenciales en claro | `IDbConnectionFactory` + user-secrets |

## Includes → lógica de servicio (no front)

| Include(s) | Qué hace | Destino |
|---|---|---|
| `GenerarCuadros*.inc` (ABS, ESP, PPT, TYC, Equipos, Consolacion, Segunda), `PrevioGenerar*.inc`, `GenerarLiguilla*.inc`, `GenerarEquiposLiguilla*.inc`, `LiguillaMenores.inc`, `LiguillaCuadroMenores.inc`, `TYCReorganizar.inc`, `ResumenMenores-*.inc`, `Torneos_Liga_NuevaEliminatoria.asp`, `Cuadro16.asp` | Generación de cuadros, previas, liguillas, consolaciones; escriben en `SFEP_Torneos_*` | `Domain/Cuadros`: servicio puro (sin SQL) con tests unitarios exhaustivos + repositorio. **Migrar con tests de regresión** que comparen contra cuadros generados por el ASP |
| `Cat1-*.inc`, `Cat2-*.inc`, `Cat3-*.inc` (22) | Plantillas por formato de cuadro; 14 están vacías (14 bytes) y ninguna aparece incluida en otro fichero | Probablemente muertas; si se confirma algún formato vivo, pasa a configuración del motor (tabla o JSON), no a código |
| `RankingUnico*.inc`, `RankingAverage*.inc`, `RankingPromedio*.inc`, `RankingNormal.inc` | Cálculo de rankings (único, promedio, average) por modalidad | `Domain/Rankings` + job de recálculo |
| `ABS_*`, `ALE_*`, `BEN_*`, `CAD_*`, `INF_*`, `JUN_*`, `PBEN_*`, `VET35…55_*` (`_F`/`_M`/`_X`) (37) | Pestañas de ranking por categoría y género, cada una con su SQL casi idéntico | **Un único** endpoint `GET /api/v1/rankings?modalidad=&categoria=&genero=` |
| `DatosFicha.inc`, `LimpioFicha.inc`, `Fichas.asp` | Datos y normalización de la ficha de jugador | `FichasRepository` / validadores |

## Páginas públicas con URL limpia

`web.config` tiene 318 reglas: 167 URLs públicas de la web, 79 de `API/Produccion`, 35 de
`API/Desarrollo`, 20 de `Webservice/`, 12 de `Paginas/…` y `Bootstrap_3.3.7-Versions/…`
(pantallas de orden de juego), 4 de `Gestion/Carnet*` y 1 redirección a HTTPS. Todas están en
`redirecciones.csv` con la acción propuesta (301, mantener ruta Legacy, no publicar o localizar).

## Duplicados notables

- `API\Desarrollo\*` ≈ `API\Produccion\*` (misma API, otra conexión): se migra solo Producción.
- `Cuadro*.asp`, `CuadroEquipos*.asp`, `CuadroPrevia*.asp`, `Liguilla*.asp`, `Previo*.asp` existen
  en la raíz **y** en `refs\Torneos\` (+ variantes `…Excell`). El destino es un único endpoint de
  cuadro: **ya existe** `/api/v1/competicion/cuadros/{idTorneo}/{genero}/{categoria}/{nivel}/{cuadro}` (+ lista de
  cuadros del torneo y `…/enfrentamientos/{avance}/{posicion}`; ver `docs/api/analisis/Previo.md`). La exportación
  `xlsx` (`…Excell`) no se ha migrado.
- `combo*.asp` / `dato*.asp` existen en raíz y en `gestion\` con el mismo nombre.
- `refs\images\` contiene `.asp`/`.inc` (`Noticia.asp`, `Clubes_Modificar.asp`,
  `cargarinfocurso.asp`, `BarraLateral.inc`): código dentro de la carpeta de subidas (ver riesgos).
- La columna `notas` marca `duplicado_exacto_de=` cuando el contenido es idéntico byte a byte.
