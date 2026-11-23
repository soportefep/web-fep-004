# Análisis: `Organos_Gobierno.asp` → `organos-de-gobierno.html` + `GET /api/v1/federacion/organos-gobierno`

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (consultas `SFEP_Web_Menu`, `SFEP_Web_RS`, banners…) | Ya migrado: `comun.js` + `/api/v1/menu`, `/api/v1/redes-sociales` |
| `sql_noticias` (`SFEP_Noticias`) | Se consultaba **pero no se usaba** en la página | Eliminado |
| Junta Directiva | Tabla HTML escrita a mano + imagen `refs/images/ORGANIGRAMA JUNTA-V2.jpg` | `organos[junta-directiva]` |
| Asamblea General | 6 tablas HTML a mano (presidente, federaciones, clubes, jugadores, técnicos, jueces-árbitros) | `organos[asamblea].grupos` |
| Actas de la Asamblea | Tabla HTML a mano con 10 PDF en `refs/docs/` | `organos[asamblea].actas` |
| Comisión Delegada | 4 tablas HTML a mano | `organos[comision-delegada].grupos` |
| CTAP | Solo una imagen `refs/images/CTAP-FEP.png` | `organos[ctap]` (datos transcritos de la imagen + imagen) |

**No hay tablas en BD para este contenido**: todo estaba en el HTML. Se ha pasado a
`src/FEP.Api/Contenidos/organos-gobierno.json` (estructurado: órgano → grupos → miembros, y actas),
servido por `GET /api/v1/federacion/organos-gobierno`. La página nueva no lleva datos en el HTML.

## Visibilidad heredada (variable `a` sin declarar)

El ASP usa `<%if a=5 then%>` y `<%if a="" then%>` con una variable `a` que nunca se asigna (vale
`Empty`). Resultado en la web actual, que se ha respetado:

- `a=""` → **verdadero**: las actas de la Asamblea General **se ven**.
- `a=5` → **falso**: las pestañas Asamblea/Comisión y las **actas de la Comisión Delegada (22 documentos,
  2017–2022) no se ven**. No se han migrado al contenido público. **DECISIÓN FEP**: ¿se publican?
  Si es que sí, basta con añadirlas a `actas` de `comision-delegada` (están en `Organos_Gobierno.asp`,
  líneas ~835–945). Dos de ellas no tienen PDF (`href="#"`, "pendiente aprobación").

## Mejoras respecto al original

- Los datos que solo estaban **dentro de imágenes** (13 vocales de la Junta y composición del CTAP) se
  han transcrito como texto: accesibles para lectores de pantalla e indexables. La imagen original
  sigue disponible ("Ver el organigrama oficial en imagen").
- **Organigramas en HTML/CSS** (Junta Directiva y CTAP) en lugar de las capturas JPG/PNG rojas y
  naranjas: cabeza (Presidente/a) → ramas (vicepresidencias, secretaría general) → vocales, con
  conectores en CSS y los estilos del resto de la web; en anchos estrechos pasa a árbol vertical
  (container query). La jerarquía sale del campo `depende` de cada miembro (cargo del que cuelga, p. ej.
  `"Vicepresidente 2"`), transcrito de la imagen oficial; los miembros sin `depende` cuelgan
  directamente de la cabeza (vocales del CTAP).
- Emails como enlaces `mailto:`, webs de federaciones/clubes como enlaces externos con `rel="noopener"`.
- Actas ordenadas por fecha con tipo (Ordinaria/Extraordinaria) y enlace de descarga; URLs con espacios
  codificadas (`2023 Acta…pdf` → `2023%20Acta…pdf`). Comprobadas las 10: responden `200 application/pdf`.
- Eliminada la columna "CV" de la Junta (estaba siempre vacía) y la consulta de noticias no usada.
- HTML semántico (tablas con `caption`/`th scope`), índice lateral con la sección activa, móvil sin
  scroll horizontal.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "Federación Navarra de Páde" (Comisión Delegada) | "Federación Navarra de Pádel" | Errata |
| Enlace "padelcv.es" a `http://padelcv.com/` | Se muestra "padelcv.com" (el dominio real del enlace) | Texto e enlace no coincidían |
| Clubes de la Comisión Delegada con web `#` (CD Tres Olivos Montecarmelo, Club Pádel y Tenis la Capellanía) | Sin enlace | `#` no lleva a ningún sitio |
| "VOCAL RESONSABILIDAD SOCIAL COORPORATIVA RCC" (imagen) | "Vocal de Responsabilidad Social Corporativa (RSC)" | Erratas en la imagen |
| Junta: "Concha Velasco Martin" (imagen) / "Concha Velasco Mart&iacute;n" (tabla) | "Concha Velasco Martín" | Se usa la forma de la tabla |

**Pendiente de confirmar por la FEP**:
- "Acta de la Asamblea General Ordinaria del **31 de Enero** de 2020" enlaza a
  `Acta-Asamblea-Gral-ORD-Fep-**311020**.pdf` (¿31-10-2020?, que ya existe como acta aparte). Se ha
  mantenido el texto original.
- Los nombres de la Asamblea están en formato "APELLIDOS, NOMBRE" y mayúsculas; los de la Comisión
  Delegada, en "Nombre Apellidos". Se ha mantenido tal cual; conviene unificar.

## Modelo SQL propuesto (para cuando se quiera editar desde la gestión)

Hoy se edita el JSON. Para gestionarlo desde el back-office, basta con estas tablas y cambiar la
implementación de `IOrganosGobiernoRepository` por Dapper (el contrato de la API no cambia):

```sql
CREATE TABLE SFEP_Organos        (IdOrgano int IDENTITY PRIMARY KEY, Clave varchar(40) UNIQUE, Titulo nvarchar(150),
                                  Descripcion nvarchar(500) NULL, Imagen nvarchar(260) NULL, ImagenAlt nvarchar(200) NULL,
                                  Orden int, Visible char(1) DEFAULT 'S', Borrado char(1) DEFAULT 'N');
CREATE TABLE SFEP_Organos_Grupos (IdGrupo int IDENTITY PRIMARY KEY, IdOrgano int REFERENCES SFEP_Organos, Clave varchar(40),
                                  Titulo nvarchar(150), EtiquetaNombre nvarchar(50), EtiquetaDetalle nvarchar(50) NULL,
                                  Presentacion varchar(10) CHECK (Presentacion IN ('tarjetas','lista')), Orden int);
CREATE TABLE SFEP_Organos_Miembros (IdMiembro int IDENTITY PRIMARY KEY, IdGrupo int REFERENCES SFEP_Organos_Grupos,
                                  Nombre nvarchar(200), Cargo nvarchar(150) NULL, Detalle nvarchar(150) NULL,
                                  Email nvarchar(150) NULL, Web nvarchar(260) NULL, Orden int, Borrado char(1) DEFAULT 'N');
CREATE TABLE SFEP_Organos_Actas  (IdActa int IDENTITY PRIMARY KEY, IdOrgano int REFERENCES SFEP_Organos, Titulo nvarchar(250),
                                  Fecha date, Tipo varchar(20) NULL, Archivo nvarchar(260), Visible char(1) DEFAULT 'S');
```

## Ficheros

- API: `Contracts/Federacion/OrganosGobiernoDto.cs`, `Data/Repositories/Federacion/OrganosGobiernoRepository.cs`,
  `Controllers/Federacion/OrganosGobiernoController.cs`, `Contenidos/organos-gobierno.json`; tests en
  `tests/FEP.Api.Tests/OrganosGobiernoTests.cs`.
- Web: `wwwroot/organos-de-gobierno.html`, `assets/js/organos-gobierno.js`, estilos "Páginas interiores"
  en `assets/css/fep.css`, imágenes en `assets/img/fep/organos/`.
- Mock: `mock/federacion-organos-gobierno.json`, generado con `tools/generar-mock-organos-gobierno.ps1`
  (no editar a mano: se edita `Contenidos/organos-gobierno.json` y se vuelve a generar).
- Común: `assets/js/comun.js` (cabecera, menú, pie; extraído de `home.js`). `PAGINAS_MIGRADAS` en
  `comun.js` redirige los enlaces de la BD a las páginas ya migradas.
