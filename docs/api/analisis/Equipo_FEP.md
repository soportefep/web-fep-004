# Análisis: `Equipo_FEP.asp` → `equipo-fep.html` + `GET /api/v1/federacion/equipo`

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (consultas `SFEP_Web_Menu`, `SFEP_Web_RS`, banners…) | Ya migrado: `comun.js` + `/api/v1/menu`, `/api/v1/redes-sociales` |
| `sql_noticias` (`SFEP_Noticias`) | Se consultaba **pero no se usaba** en la página | Eliminado |
| Título "equipo técnico y administrativo de la federación española de pádel" | HTML escrito a mano | `h1` "Equipo FEP." + entradilla |
| ORGANIGRAMA | Solo una imagen `refs/images/ORGANIGRAMA-FEP.png` (sin `alt`) | `areas[organigrama]`: imagen + datos transcritos (presidencia, área deportiva, equipo administrativo) |
| EQUIPO DE MENORES | Solo una imagen `refs/images/MENORES-FEP.png` (sin `alt`) | `areas[menores]`: imagen + datos transcritos (seleccionadores, equipo técnico) |

**No hay tablas en BD para este contenido** y, además, los nombres y puestos **solo existían dentro de
las imágenes**. Se han transcrito a `src/FEP.Api/Contenidos/equipo-fep.json` (área → grupos → miembros),
servido por `GET /api/v1/federacion/equipo`. Las imágenes originales se siguen mostrando (son lo que
hoy publica la FEP) y la API devuelve su ruta original (`imagen`) y la URL absoluta (`imagenUrl`).
Las dos imágenes de `X:\TICS\WEB\refs\images\` son idénticas (mismo tamaño en bytes) a las publicadas en
`https://www.padelfederacion.es/refs/images/` (ambas responden `200 image/png`).

## Lógica / visibilidad heredada

- No hay condiciones `<% if %>` propias de la página: todo lo que estaba en el HTML se ve.
- `activo=2` marca como activa en el menú la opción con `IdOpcion = 2` (heredado de la plantilla; la
  web nueva ya no usa ese mecanismo).
- `sql_noticias` se ejecuta en cada visita y no se pinta: consulta muerta.

## Mejoras respecto al original

- Los datos de las imágenes son ahora **texto**: accesibles para lectores de pantalla, indexables y
  editables sin rehacer la imagen. Las imágenes tienen `alt` (el original no lo tenía).
- Se elimina la consulta de noticias no usada.
- Índice lateral con contador de personas por área, organigrama como en Órganos de gobierno y
  nota con enlace a Órganos de gobierno (la Asamblea General y la Comisión Delegada aparecen en el
  organigrama pero se detallan allí).
- **Junta Directiva**: el organigrama la muestra como un recuadro sin nombres. La API devuelve sus
  18 miembros (la página usa Presidente y Secretaría General y enlaza al resto). No se copian: el grupo
  `junta-directiva` del JSON va vacío y lo rellena `EquipoFepRepository` (y el mock,
  `generar-mock-equipo-fep.ps1`) con la Junta de `Contenidos/organos-gobierno.json`. Una sola fuente:
  si cambia la Junta, se edita solo `organos-gobierno.json` y se regeneran los dos mocks. Así el área
  "Organigrama" pasa de 9 a 25 personas (Junta 18 + área deportiva 2 + equipo administrativo 5).
- **Organigramas en HTML/CSS** en lugar de las imágenes PNG rojas y naranjas (estilos `.ochart*`, los mismos
  que Órganos de gobierno). General: Asamblea General (+ Comisión Delegada, enlazadas a su página) →
  Presidente → Dirección Deportiva Federativa (seleccionador absolutos y enlace a la selección de menores)
  | Junta Directiva (recuadro con nº de miembros y enlace a su composición) | Secretaría General (equipo
  administrativo). Menores: Selecciones de menores → seleccionadores → equipo técnico. Presidente y
  Secretaría General salen de la Junta por su cargo; la imagen oficial sigue enlazada.
- Móvil sin scroll horizontal: el organigrama pasa a árbol vertical (container query).

## Correcciones de datos (revisar)

| Original (imagen) | Migrado | Motivo |
|---|---|---|
| "CONCHA VELASCO MARTIN" | "Concha Velasco Martín" | Tilde; misma forma que en Órganos de gobierno |
| "JOSÉ LUIS GUTIERREZ DEL ÁLAMO" (imagen de menores) | "José Luis Gutiérrez del Álamo" | Tilde, como en el organigrama general |
| Nombres en MAYÚSCULAS | Nombre propio con mayúsculas iniciales y tildes | Legibilidad; las tildes se toman de la imagen |
| Organigrama: "SELECCIONADOR MENORES" para Oriol Moyés y José Luis Gutiérrez | "Seleccionador femenino" / "Seleccionador masculino" (grupo "Seleccionadores de menores") | Se usa el detalle de la imagen de menores, más precisa |
| "EQUIPO TÉCNICO" aparece en las dos imágenes (mismas 5 personas) | Solo en el área "Equipo de menores" | En el organigrama cuelga de "Seleccionador menores"; se evita duplicar (**SUPUESTO**) |
| "Presidente" / "Secretaría General" / "Dirección Deportiva Federativa" | Igual | Se respetan los rótulos de la imagen |

Los textos de entradilla (`descripcion` de cada área y `page-hero__lead`) son nuevos: el original solo
tenía el título y las imágenes.

## Pendiente de confirmar por la FEP

- Resuelto: el presidente aparecía como "Javier Rodriguez Piris" (sin tilde) en
  `organos-gobierno.json`; se ha corregido a "Javier Rodríguez Piris", como en el organigrama.
- **SUPUESTO**: "Oriol Moyés **Munne**" (¿"Munné"?) y "Carmen **Fernández-Vegue**" (un solo apellido
  compuesto) se transcriben tal cual.
- **SUPUESTO**: el equipo técnico es el de menores (ver tabla). Si también trabaja con absolutos, basta
  con moverlo a `organigrama` en el JSON.
- Las imágenes dicen "PADEL" sin tilde en el título; si se rehacen, corregirlo.
- El organigrama no indica puestos del equipo administrativo ni del equipo técnico; si se quieren
  mostrar, añadir `cargo` en el JSON.
- ¿Se mantienen las imágenes una vez que los datos están en texto? Hoy se muestran ambas (es lo que
  publica la web actual). Si se actualiza el equipo, hay que cambiar **imagen y JSON** a la vez, o
  retirar las imágenes.

## Modelo SQL propuesto (para cuando se quiera editar desde la gestión)

Hoy se edita el JSON. Para gestionarlo desde el back-office, basta con estas tablas y cambiar la
implementación de `IEquipoFepRepository` por Dapper (el contrato de la API no cambia):

```sql
CREATE TABLE SFEP_Equipo_Areas    (IdArea int IDENTITY PRIMARY KEY, Clave varchar(40) UNIQUE, Titulo nvarchar(150),
                                   Descripcion nvarchar(500) NULL, Imagen nvarchar(260) NULL, ImagenAlt nvarchar(200) NULL,
                                   Orden int, Visible char(1) DEFAULT 'S', Borrado char(1) DEFAULT 'N');
CREATE TABLE SFEP_Equipo_Grupos   (IdGrupo int IDENTITY PRIMARY KEY, IdArea int REFERENCES SFEP_Equipo_Areas,
                                   Clave varchar(40), Titulo nvarchar(150), Orden int);
CREATE TABLE SFEP_Equipo_Miembros (IdMiembro int IDENTITY PRIMARY KEY, IdGrupo int REFERENCES SFEP_Equipo_Grupos,
                                   Nombre nvarchar(200), Cargo nvarchar(150) NULL, Orden int, Borrado char(1) DEFAULT 'N');
```

## Ficheros

- API: `Contracts/Federacion/EquipoFepDto.cs`, `Data/Repositories/Federacion/EquipoFepRepository.cs`,
  `Controllers/Federacion/EquipoFepController.cs`, `Contenidos/equipo-fep.json`; tests en
  `tests/FEP.Api.Tests/EquipoFepTests.cs`. Registro en `Program.cs`.
- Web: `wwwroot/equipo-fep.html`, `assets/js/equipo-fep.js`, estilo nuevo `.page-note` en la sección
  "Páginas interiores" de `assets/css/fep.css`, imágenes en `assets/img/fep/equipo/`
  (`organigrama-fep.png`, `menores-fep.png`).
- Mock: `mock/federacion-equipo.json`, generado con `tools/generar-mock-equipo-fep.ps1`
  (no editar a mano: se edita `Contenidos/equipo-fep.json` y se vuelve a generar).
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`equipo_fep` → `equipo-fep.html`), así el submenú
  "Federación → Equipo FEP" que llega de la BD apunta ya a la página nueva.
