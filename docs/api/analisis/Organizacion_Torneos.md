# Análisis: `Organizacion_Torneos.asp` → `organizacion-torneos.html` + `GET /api/v1/competicion/organizacion-torneos`

URL actual: `/Organizacion_Torneos` (enlace "Organizar un torneo" del menú Competición, acceso rápido de la portada y
banner de la convocatoria en `CalendarioFEP.asp`). Ruta nueva propuesta en `redirecciones.csv` (grupo
`licitacion-torneos`): `/organizacion-torneos` → `organizacion-torneos.html`. La página no recibe parámetros.
Pasado el plazo, el ASP redirigía a `Organizacion_FueraPlazo.asp`, que solo muestra un aviso: la página nueva cubre los
dos casos y `comun.js` lleva a ella los enlaces a ambas.

Includes: `conexionSQL.inc` (no se copia), `Menus.inc` y `PiePagina.inc` (ya migrados en `comun.js`),
`BarraLateralLicitacion.inc` (calendario de licitación por meses: migrado aquí, en la sección "Pruebas"),
`finconexion.inc`. Ficheros auxiliares que llamaba por AJAX: `cargarfechastorneo.asp` (fechas y fianza del torneo
elegido), `comboprovincias.asp` (`comboProvincias.asp`, provincias de la comunidad) y `comboclubesLicitacion.asp`
(clubes de la provincia). El formulario se envía a `Organizacion_P0.asp`, que guarda la solicitud y sigue con
`Organizacion_P1.asp` (autorización firmada), `P2` (oferta), `P4` (fianza: transferencia o TPV) y
`Organizacion_Final.asp` (resguardo por email). Codificación de todos: Windows-1252.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie | `Menus.inc`, `PiePagina.inc` (`activo=3`) | Ya migrado: `comun.js` (+ columna de patrocinadores) |
| Barra lateral "CALENDARIO LICITACIÓN" | `BarraLateralLicitacion.inc`: `sql_ano` (meses de `FechaFin` de 2027) y una consulta `sql_mes` por mes con `Licitable='S'`; pinta "Campeonato - (día ini-día fin)" | `torneos` (una consulta); el front agrupa por mes de fin en la sección "Pruebas que se pueden solicitar" |
| `sql_noticias` (`SFEP_Noticias`, `SELECT *`) | Se consultaba **pero no se usaba** | Eliminado |
| `sql_prov` (todas las provincias) | Se consultaba **pero no se usaba** (las provincias llegaban por AJAX) | Eliminado |
| `sql_num` (contador de pruebas) | Usaba la variable `ano`, que **nunca se asigna** (`Año=''`: siempre 0) y el resultado no se pintaba | Eliminado |
| "Archivos relevantes para la organización de Torneos 2027" | `sql_archivo` (`SFEP_Calendario_Archivo`, `SELECT *`): "Calendario Provisional 2027" → `ArchivosLicitacion`; "Condiciones Organización de Torneos" → `CalendarioLicitacion` (`Archivo` se leía sin usar); iconos `CalendarioLicitacion.jpg` y `DocumentacionLIc.png` sin `alt` | `documentos` (fichero de la BD + título de `Contenidos/`) con URL absoluta |
| Desplegable "Selecciona el torneo" | `sql_menores` (categorías 3, 4), `sql_abs` (1, 2, 5), `sql_vet` (6, 7), `sql_silla` (10): `SFEP_Calendario` con `Año='2027'`, `Federacion=0`, `Licitable='S'`, por `FechaIni`; en silla se añadía " / FechaFin" al nombre | `grupos` + `torneos[].grupo` (`ReglasOrganizacionTorneos.Grupos`) |
| Fechas y fianza del torneo elegido | `cargarfechastorneo.asp?id=` (AJAX a cada cambio): `FechaIni`, `FechaFin`, `Fianza` (0 → "A Negociar directamente con la FEP") | `torneos[].fechaInicio`, `fechaFin`, `fianza` (ya en la respuesta) |
| Comunidad autónoma | `sql_com`: `SFEP_Comunidades` con `IdComunidad<20`, por nombre | `comunidades` |
| Provincia | `comboProvincias.asp` (POST `id` = comunidad): `SFEP_Provincias` de la comunidad, por nombre | `comunidades[].provincias` (todas en la misma respuesta) |
| Club sede | `comboclubesLicitacion.asp` (POST `id` = provincia): `SFEP_Clubes` con `Baja='N'`, `Independiente='N'`, por nombre; si no hay, `club2` libre | `GET /api/v1/competicion/organizacion-torneos/clubes?provincia=` |
| Localidad, club2, responsable, organizador, teléfono, email | Campos de texto con `maxlength` 50/150/100/150/15/100 | Mismos nombres y límites |
| Protección de datos | Texto escrito a mano + `select lopd` NO/SI | `proteccionDatos` (`Contenidos/`) + casilla `lopd=S` |
| Botón "SIGUIENTE" | `javascript:validar()` → `POST Organizacion_P0.asp` con `accion=modificar` | Botón de envío; mismo destino (`envioUrl`) y mismos campos |
| Plazo | `if CDate(now()) > CDate("20/11/26 14:00:00") then response.redirect("Organizacion_FueraPlazo.asp")` | `abierta` / `fechaLimite` (`Contenidos/`, hora de Madrid) |
| Aviso de plazo cerrado | `Organizacion_FueraPlazo.asp` (texto escrito a mano) | `avisoCerrado` |

Columnas realmente usadas de `SFEP_Calendario`: `IdCalendario`, `Campeonato`, `Categoria` (para el grupo), `FechaIni`,
`FechaFin`, `Fianza`, y en el filtro `Año`, `Federacion`, `Licitable` (`Tipo` se leía sin usar). La página **no escribe**
en la BD: lo hace `Organizacion_P0.asp` (`INSERT INTO SFEP_Licitaciones`), que sigue en la web actual.

## El formulario y la web actual

La API es de solo lectura (contrato de `ARQUITECTURA.md`: sin autenticación ni escritura). Por eso la página nueva
sustituye la parte que se lee (convocatoria, documentos, pruebas y desplegables) y **envía el formulario, igual que el
ASP, a `Organizacion_P0.asp`** de la web actual, con los mismos nombres de campo (`torneo`, `comunidad`, `provincia`,
`localidad`, `club`, `club2`, `organizador`, `responsable`, `telefono`, `email`, `lopd`, `accion=modificar`). Desde ahí
la solicitud sigue en la web actual (autorización, oferta, fianza y resguardo). Detalles para que el envío funcione:

- `accept-charset="windows-1252"`: la web actual trabaja en iso-8859-1/Windows-1252; enviado en UTF-8, "Málaga" se
  guardaría como "MÃ¡laga".
- Un `<select>` deshabilitado no se envía: si la provincia no tiene clubes o la lista no carga, el front habilita el
  campo `club` con `0` antes de enviar, porque `Organizacion_P0.asp` solo usa `club2` cuando `club = "0"` (con `club`
  vacío buscaría `IdClub = ''` y guardaría "No designado").
- La validación del navegador replica `validar()` del ASP (sin las comprobaciones repetidas) y añade formato de
  teléfono y email. `Organizacion_P0.asp` no valida nada en el servidor.

## Lógica / visibilidad heredada

- **Temporada y plazo escritos a mano**: `Año='2027'` en seis consultas (cuatro del desplegable, `sql_ano` y `sql_mes`
  de la barra lateral) y la fecha `"20/11/26 14:00:00"` interpretada con `CDate` según el formato regional del servidor
  (`dd/mm/aa`). Pasan a `Contenidos/organizacion-torneos.json` (`temporada`, `fechaLimite`). El cierre se calcula en hora
  de Madrid (`ReglasOrganizacionTorneos.Abierta`), aunque el servidor esté en otra zona; el front lo vuelve a comprobar
  con el reloj del navegador (la respuesta se cachea 5 minutos y el mock dice "abierta"). La FEP prefiere que la
  temporada cambie sola (confirmado en P0021 y aplicado ya en el calendario FEP), pero aquí no se puede calcular: cada
  convocatoria tiene su propia fecha límite, que decide la FEP. Por eso se propone guardarlas en la BD (ver "Modelo SQL
  propuesto") para que la gestión las cambie sin tocar ficheros.
- **Plazo cerrado**: el ASP redirigía a `Organizacion_FueraPlazo.asp` (solo un aviso). Ahora la misma página muestra
  "Convocatoria cerrada" con el aviso y mantiene los documentos; la API devuelve `torneos` y `comunidades` vacíos.
- **Pruebas que no se pueden elegir**: la barra lateral listaba todas las pruebas licitables; el desplegable, solo las
  de las categorías 1–7 y 10. Si hubiera licitables de otra categoría, salen en el calendario con `grupo = null` y sin
  botón "Solicitar", como en el ASP. Hoy las 52 están en algún grupo.
- **Barra lateral por mes de fin**: el ASP agrupaba por `Month(FechaFin)` (las pruebas que empiezan el 30 de abril salen
  en mayo). Se mantiene, mostrando la fecha completa. Los meses sin pruebas licitables (el `sql_ano` no filtraba
  `Licitable`) ya no salen vacíos.
- **No se filtra `Visible`**: el ASP solo exigía `Licitable = 'S'`; se respeta (la convocatoria puede incluir pruebas
  aún no publicadas en el calendario).
- Funciones JS `MesAnterior`, `CargarEvento`, `MeGusta`, `busqueda`, `newPage` y `cambiar`: no se usan.
  `onChange="javascript:cargarprovincias()"` llamaba a una función que **no existe** (error de JS en cada cambio; las
  provincias las cargaba el manejador de jQuery).

## Problemas del original

1. **Inyección SQL** en los tres ficheros auxiliares: `cargarfechastorneo.asp` (`id` sin comillas tras `limpiar()`),
   `comboProvincias.asp` y `comboclubesLicitacion.asp` (`id` **sin limpiar** y sin comillas). Ahora todo va con
   parámetros Dapper. `Organizacion_P0.asp` (sigue en la web actual) también concatena `torneo` y `provincia` sin
   comillas en el `INSERT`.
2. **10 consultas propias por página + una por mes** de la barra lateral (+ las de `Menus.inc`) y una petición AJAX por
   cada cambio de torneo, comunidad o provincia. Ahora una ida a la BD (`QueryMultiple`) y una petición por provincia.
3. **Fianza vacía**: `cargarfechastorneo.asp` mostraba "Fianza del torneo: euros" (sin cifra) en tres pruebas de 2027
   (`IdCalendario` 5713, 5714 y 5715) y `Organizacion_P0.asp` concatena `Fianza` en el `INSERT` sin comprobarla: con
   `NULL` la sentencia queda mal formada y la solicitud de esas pruebas **falla con un error 500**. La página nueva
   muestra "a negociar directamente con la FEP"; el fallo al guardar depende de la web actual (ver pendientes).
4. **Pruebas duplicadas en el desplegable**: "*Circuito Nacional de Menores 4" (5680 y 5714) y "…Menores 7" (5699 y
   5715) aparecen dos veces con el mismo nombre y fechas (el mismo caso que P0054 del calendario FEP).
5. **Error 500 sin configuración**: `rs_archivo("Archivo")` se leía sin comprobar EOF; con `SFEP_Calendario_Archivo`
   vacía la página fallaba. Con columnas vacías, los iconos enlazaban a la carpeta `refs/docs/` (403). Ahora los
   documentos sin fichero no se muestran.
6. **XSS**: `Campeonato`, `Comunidad`, `Provincia` y `Club` se pintaban sin `Server.HTMLEncode`. El front usa
   `textContent`.
7. **Validación duplicada y con erratas**: `validar()` comprobaba dos veces provincia y localidad, una vez el club con
   `club.value == ""` (nunca vacío) y tenía mensajes como "Selecciona en la provincia en la que…". El servidor no valida
   nada (`lopd` ni se lee).
8. **Condición de carrera en `Organizacion_P0.asp`**: tras el `INSERT` busca la última licitación por nombre de club
   (`WHERE Club='…' ORDER BY Id DESC`) para seguir al paso 1; dos solicitudes simultáneas del mismo club podrían cruzarse.
   Queda anotado para cuando se migren los pasos con escritura.
9. **Accesibilidad/HTML**: textos sueltos como etiquetas (sin `label`), iconos de documento sin `alt`, botón
   `javascript:validar()`, `apprise()` como alerta, `<meta charset=iso-8859-1>` tras los scripts y `<script>` fuera de
   `<head>`.

## Mejoras respecto al original

- Estado de la convocatoria con el plazo exacto ("hasta el 20 de noviembre de 2026 a las 14:00, hora peninsular") y
  cierre automático en la misma página.
- Documentos como enlaces de descarga con tipo y descripción; URL `https://www.` (el ASP usaba `http://` sin `www`).
- Pasos del proceso explicados antes de empezar (el ASP no decía que después había que firmar una autorización, hacer
  la oferta y pagar la fianza).
- Desplegable con las fechas de todas las pruebas (no solo las de silla) y ficha de la prueba elegida con fechas y
  fianza sin peticiones adicionales.
- Calendario de pruebas licitables por mes con filtro por tipo y botón "Solicitar" que elige la prueba en el formulario.
- Formulario accesible: `label` en todos los campos, `fieldset` por bloques, resumen de errores con enlaces a cada campo
  y `aria-invalid`, casilla de aceptación en lugar del desplegable NO/SI, protección contra envíos dobles. Con una sola
  provincia (Madrid, Asturias…) se elige sola.
- Respuesta cacheada 5 min (output cache; `provincia` ya varía la caché de clubes).

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "Open de pádel en silla-Enero / 24/01/2027" (solo las de silla llevaban la fecha en el nombre) | "Open de pádel en silla-Enero · 22–24 ene" (todas las pruebas con sus fechas) | Presentación homogénea |
| " Circuito Nacional de Menores 1– …" (espacio inicial), "…Menores 7␉— …" (tabulador), "Master final  de pádel en silla" | Sin espacios ni tabuladores sobrantes | Espacios en `Campeonato` (la BD no se toca) |
| "Fianza del torneo: euros" (5713, 5714, 5715) | "Fianza: a negociar directamente con la FEP" | `Fianza` vacía en la BD; se trata como 0 |
| `Organizacion_FueraPlazo.asp`: "El proceso de licitación de Torneos para el año 2026 ha finalizado" | "El proceso de licitación de torneos para la temporada 2027 ha finalizado." | El aviso no se actualizó al abrir la convocatoria de 2027 |
| "Sino localizas el club o no aparece en el desplegable…" | "Elige el club en la lista o, si no lo encuentras, escribe su nombre." | Errata ("Sino") |
| "FEDERACION ESPAÑOLA DE PADEL ,C/ Francisco Silvela…" | "FEDERACION ESPAÑOLA DE PADEL, C/ Francisco Silvela…" | Puntuación (el resto de la cláusula, literal) |
| Mensajes de `validar()`: "Selecciona en la provincia en la que se disputaría el torneo", "Indica en el club…" | "Selecciona la provincia donde se disputaría la prueba", etc. | Erratas y comprobaciones repetidas |

## Comprobación de enlaces (16/11/2026)

- `https://www.padelfederacion.es/refs/docs/Calendario-2027-131026.pdf` → 200 `application/pdf` (calendario provisional).
- `https://www.padelfederacion.es/refs/docs/Bases-de-Licitacion-2027.pdf` → 200 `application/pdf` (condiciones).
- `http://padelfederacion.es/refs/docs/…` (enlace del ASP) → redirige a `https://padelfederacion.es/…` → 200.
- `https://www.padelfederacion.es/Organizacion_Torneos` → 200 (plazo abierto); `Organizacion_FueraPlazo.asp` → 200.
- `Organizacion_P0.asp` → 500 a una petición `HEAD` sin datos (esperado: `torneo` vacío rompe el SQL). **No se ha
  enviado el formulario** para no crear una licitación real.
- `cargarfechastorneo.asp` de las 52 pruebas, `comboProvincias.asp` de las 19 comunidades y `comboclubesLicitacion.asp`
  de las 52 provincias → 200 (son la base del mock).

## Modo prototipo (mocks)

`mock/competicion-organizacion-torneos.json` es una instantánea de la web pública del 16/11/2026: 52 pruebas licitables
de 2027 (fechas y fianza de `cargarfechastorneo.asp`), 19 comunidades con sus 52 provincias y los 2 documentos; la
parte escrita a mano sale de `Contenidos/organizacion-torneos.json`. `mock/competicion-organizacion-torneos-clubes.json`
tiene los 3.462 clubes de las 52 provincias (uno por línea); como el mock no filtra, `organizacion-torneos.js` se queda
con los de la provincia elegida. Los genera `tools/generar-mock-organizacion-torneos.ps1`: sin parámetros regenera la
parte de `Contenidos/` y conserva la instantánea; con `-DesdeWebPublica` la rehace (solo lectura; no funciona con el
plazo cerrado, porque la web actual redirige a `Organizacion_FueraPlazo.asp`). El mock dice `abierta: true`; pasado el
20/11/2026 a las 14:00 el front muestra la convocatoria cerrada por la fecha límite. En modo prototipo el formulario
también se envía a la web actual de verdad.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: la solicitud se sigue registrando en la web actual: el formulario nuevo envía los datos a
  `Organizacion_P0.asp` y los pasos 2 a 4 (autorización firmada, oferta y fianza con TPV) continúan allí. Migrar esos
  pasos exige escritura en BD, subida de ficheros y pago, previstos para la fase de área privada. Confirmar que se
  acepta esta transición mixta.
- **DECISIÓN FEP**: cláusula de protección de datos: es el texto antiguo (LOPD de 1999: derecho de "cancelación",
  cesión de datos a patrocinadores sin consentimiento expreso) con la dirección C/ Francisco Silvela, 36, mientras la
  Política de privacidad de la web indica C/ Saturnino Calleja nº 6. Se ha mantenido literal (mismo caso que P0273).
  Facilitar el texto RGPD vigente para `Contenidos/organizacion-torneos.json`.
- **DECISIÓN FEP**: cierre del plazo y banner del calendario: esta página se cierra sola el 20/11/2026 a las 14:00
  (`fechaLimite`), pero el aviso de la convocatoria del calendario FEP depende de `SFEP_Calendario_Archivo.HabitaSolicitud`
  y seguirá visible hasta que alguien lo cambie en la gestión, enlazando a una convocatoria cerrada. ¿Se unifican (por
  ejemplo, ocultar el aviso pasada la fecha límite)?
- **DECISIÓN FEP**: textos redactados para la página nueva (introducción, descripción de los documentos, pasos del
  proceso a partir de `Organizacion_P1/P2/P4/Final.asp` y aviso de convocatoria cerrada). Revisar y actualizar cada año
  en `Contenidos/organizacion-torneos.json` junto con la temporada y la fecha límite (mismo caso que P0062).
- **DECISIÓN FEP**: corregir en la gestión la fianza vacía de las pruebas 5713 ("Open de pádel en silla-Mar1"), 5714 y
  5715 (poner 0 si es a negociar): con `Fianza` vacía `Organizacion_P0.asp` falla al guardar la solicitud. 5714 y 5715
  son además los duplicados de "Circuito Nacional de Menores 4" y "7" (ya preguntado en P0054): si se borran, solo queda
  la 5713.
- **SUPUESTO**: tipos de columnas de `SFEP_Calendario` como en el calendario FEP (mismo supuesto que P0063), y además
  `Fianza` numérica (se lee con `TRY_CAST(... AS decimal(12, 2))`) y `Licitable` `char`; `SFEP_Comunidades.IdComunidad`,
  `SFEP_Provincias.IdProvincia` e `IdComunidad` enteros; `SFEP_Clubes` como en el buscador de clubes (mismo supuesto
  que P0138) con `Baja` e `Independiente` `char(1)`.
- **SUPUESTO**: `SFEP_Calendario_Archivo` tiene una sola fila (mismo supuesto que P0064) y sus columnas tienen los
  nombres cruzados respecto a su contenido: `ArchivosLicitacion` guarda el calendario provisional y
  `CalendarioLicitacion` las bases (comprobado en la web pública). Se respeta la asignación del ASP.
- **SUPUESTO**: la web actual decodifica los formularios en Windows-1252 (páginas iso-8859-1 y `CodePage` por defecto de
  IIS); el formulario nuevo se envía con `accept-charset="windows-1252"`. Comprobarlo con un envío de prueba controlado
  (y borrar después la licitación de `SFEP_Licitaciones`) antes de publicar.
- Redirecciones 301 pendientes: `/Organizacion_Torneos` y `/Organizacion_FueraPlazo.asp` → `/organizacion-torneos`
  (`redirecciones.csv`). Mientras tanto `comun.js` (`PAGINAS_MIGRADAS`) traslada los enlaces del menú, la portada y el
  calendario FEP.
- Siguen en la web actual: `Organizacion_P0.asp` … `P5`, `Organizacion_Final.asp`, `Organizacion_TPV*.asp`,
  `Organizacion_email.asp` y la licitación especial (`/Licitacion_Especial_Torneos`, `Organizacion_Torneos_Especial.asp`).
  `Organizacion_P1.asp` aún consulta el calendario lateral de 2018 (`Año='2018'`).
- El formulario envía datos desde otro dominio a `Organizacion_P0.asp`: funciona porque la web actual no tiene
  protección CSRF ni comprueba el origen; si se añade, habrá que permitir el dominio nuevo.

## Modelo SQL propuesto

Las pruebas, comunidades, provincias, clubes y ficheros ya están en BD. Lo escrito a mano (temporada, plazo, textos,
pasos y cláusula) se puede gestionar sin tocar ficheros ampliando la tabla de configuración de la convocatoria, la misma
que ya decide si el calendario muestra el aviso (`HabitaSolicitud`):

```sql
ALTER TABLE SFEP_Calendario_Archivo ADD
    LicitacionTemporada        int            NULL,  -- 2027
    LicitacionFechaLimite      datetime       NULL,  -- hora local de España: 2026-11-20 14:00
    LicitacionTitulo           nvarchar(150)  NULL,
    LicitacionIntroduccion     nvarchar(max)  NULL,  -- párrafos separados por línea en blanco
    LicitacionAvisoCerrado     nvarchar(300)  NULL,
    LicitacionProteccionDatos  nvarchar(max)  NULL;

CREATE TABLE SFEP_Licitacion_Pasos (
    IdPaso  int IDENTITY PRIMARY KEY,
    Orden   int           NOT NULL,
    Titulo  nvarchar(100) NOT NULL,
    Texto   nvarchar(500) NULL);
```

Con eso `ContenidoOrganizacionTorneosRepository` se sustituye por una consulta Dapper sin cambiar el contrato, y la
página y el aviso del calendario podrían usar la misma fecha límite. Índice recomendado: `SFEP_Calendario (Federacion,
[Año], Licitable) INCLUDE (IdCalendario, Campeonato, Categoria, FechaIni, FechaFin, Fianza)` y `SFEP_Clubes (Provincia,
Baja, Independiente) INCLUDE (IdClub, Club)` (no se ha podido ver el esquema real).

## Ficheros

- API: `Contracts/Competicion/OrganizacionTorneosDto.cs` (`OrganizacionTorneosDto`, `DocumentoOrganizacionTorneosDto`,
  `PasoOrganizacionTorneosDto`, `GrupoTorneosLicitablesDto`, `TorneoLicitableDto`, `ComunidadSolicitudDto`,
  `ProvinciaSolicitudDto`, `ClubSedeDto`, `DatosOrganizacionTorneosDto`, `ContenidoOrganizacionTorneosDto`,
  `DocumentoContenidoOrganizacionDto`), `Contracts/Competicion/ReglasOrganizacionTorneos.cs` (grupos por categoría,
  fianza, título, cierre en hora de Madrid), `Data/Repositories/Competicion/OrganizacionTorneosRepository.cs` (Dapper),
  `Data/Repositories/Competicion/ContenidoOrganizacionTorneosRepository.cs` (lee `Contenidos/organizacion-torneos.json`),
  `Controllers/Competicion/OrganizacionTorneosController.cs`; registro en `Program.cs`; tests en
  `tests/FEP.Api.Tests/OrganizacionTorneosTests.cs` (validación 400 de `provincia`, parámetros al repositorio con un
  repositorio falso, plazo abierto y cerrado con reloj fijo, documentos con URL codificada, `envioUrl`, JSON y reglas
  heredadas; no necesitan SQL Server).
- Contenido: `src/FEP.Api/Contenidos/organizacion-torneos.json`.
- Web: `wwwroot/organizacion-torneos.html`, `assets/js/organizacion-torneos.js`, estilos `.orgt-*` en la sección
  "Páginas interiores" de `assets/css/fep.css` (y su bloque responsive); reutiliza `.clubs-search`, `.clubs-form`,
  `.camp-check`, `.camp-month`, `.camp-item`, `.camp-tag--*`, `.calf-tag--silla`, `.calf-section`, `.doc-list`,
  `.org-more`, `.page-note`. Sin imágenes propias (los iconos del ASP se sustituyen por la lista de documentos).
- Mock: `mock/competicion-organizacion-torneos.json` y `mock/competicion-organizacion-torneos-clubes.json` (generados con
  `tools/generar-mock-organizacion-torneos.ps1`) + `mock/mocks.js`.
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`organizacion_torneos` y `organizacion_fueraplazo` →
  `organizacion-torneos.html`); acceso rápido "Organización de torneos" de `index.html` y botón de la convocatoria de
  `calendariofep.html`.
- Documentación: `docs/api/endpoints.md`, `tools/generar-mapa-web.ps1` (fila "Organización de Torneos" → Desarrollado y
  fila nueva "pasos 1 a 4 y pago" → Pendiente), `docs/migracion/mapa-migracion.md` (área `licitacion-torneos`).
