# Análisis: `Ley_De_Transparencia.asp` → `ley-de-transparencia.html` + `GET /api/v1/federacion/transparencia`

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (consultas `SFEP_Web_Menu`, `SFEP_Web_RS`, banners…) | Ya migrado: `comun.js` + `/api/v1/menu`, `/api/v1/redes-sociales` |
| `sql_noticias` (`SFEP_Noticias`) | Se consultaba **pero no se usaba** | Eliminado |
| `sql_monitor`, `sql_foco`, `sql_juez` (`SFEP_Formacion_Calendario`, `FechaInscripcion > '" & now() & "'`) | Tres consultas copiadas de la portada; **no se usaban** (y comparaban la fecha como texto) | Eliminadas (las convocatorias ya están en `/api/v1/formacion/convocatorias`) |
| Acordeón jQuery `jAccordion` con 22 desplegables | HTML escrito a mano | `categorias[].apartados[]` (22 apartados, agrupados en 4 categorías) |
| Funciones, Normativa aplicable | Texto con viñetas `.-` y `<br>` | `parrafos` + `lista` + `actualizacion` ("enero de 2026") |
| Estructura organizativa | Tabla: Presidente, Asamblea, Comisión Delegada (filas sin contenido) + equipo administrativo (6) y técnico (2) con funciones | `lista` + `enlaces` (Órganos de gobierno, Equipo FEP) + `personas` con `grupo` y `funciones` |
| Contratos con entidades públicas, Convenios, Presupuestos, Cuentas anuales, Código de Buen Gobierno, Protocolo acoso, Contratación, Conciliación, Igualdad | Tablas con título + icono `refs/images/pdf.jpg` enlazando a `refs/docs/*.pdf` (39 enlaces, 38 ficheros distintos) | `documentos` (`archivo` original + `url` absoluta codificada) |
| Subvenciones 2025, Ingresos propios | Texto con importes ("248.256,18 €", "894.729 euros") | `importes` (`decimal`, euros) |
| Retribuciones altos cargos | Texto: nombre, cargo, funciones, retribución | `personas` con `retribucionAnualBruta` y `nota` |
| Concurso de acreedores, 4 indicadores "pendientes de actualización", Envío al CSD | Texto | `parrafos` / `lista`, con `pendiente: true` donde la FEP lo declara |
| "Normativa de ayudas…" (bloque `h5` final, fuera del acordeón) | Duplicado del desplegable del mismo nombre: "En estos momentos no hay ningún archivo para poder consultar" | Fusionado en el apartado `normativa-ayudas` |

**No hay tablas en BD para este contenido**: todo estaba en el HTML. Se ha pasado a
`src/FEP.Api/Contenidos/transparencia.json`, servido por `GET /api/v1/federacion/transparencia`.
Se ha comparado con la web pública (12/11/2026): mismos 22 apartados y mismos 39 enlaces a PDF.

## Lógica / visibilidad heredada

- No hay condiciones `<% if %>`: todo el contenido se veía (dentro del acordeón, plegado).
- **Todos los desplegables menos los dos primeros usan `id="acc3"`** (20 ids repetidos): HTML inválido y
  anclas inservibles. En la página nueva cada apartado tiene ancla propia (`#presupuestos`,
  `#cuentas-anuales`…); el test `Devuelve_las_cuatro_categorias…` comprueba que las claves no se repiten.
- Las 4 consultas de la cabecera (`noticias`, `monitor`, `foco`, `juez`) se ejecutaban en cada visita sin
  pintarse; las de formación concatenaban `now()` como texto (dependen del formato regional).
- `activo=2` marca como activa la opción de menú `IdOpcion = 2` (Federación). Lo resuelve `comun.js`.

## Mejoras respecto al original

- Agrupación en 4 categorías (institucional; contratos y convenios; económica; buen gobierno e igualdad)
  con índice lateral y un índice de apartados por categoría. Se respeta el orden original salvo
  "Envío de documentación financiera al CSD", que pasa de buen gobierno a información económica.
- Todo visible sin acordeón JavaScript (accesible, indexable, imprimible). Listas de más de 5
  documentos plegadas en "Ver N documentos más" (`<details>`).
- Documentos como enlaces de descarga con título, ejercicio/periodo y tipo (PDF) en lugar de un icono
  sin texto alternativo. URLs con espacios codificadas (`Presupuesto 2023.pdf` → `Presupuesto%202023.pdf`,
  `HEAD-FEP-2025-26_ 071026.pdf` → `…_%20071026.pdf`…). **Comprobados los 38 PDF: todos responden
  `200 application/pdf`** (HEAD, 12/11/2026). La carpeta `refs/docs/` no está en la copia
  `X:\TICS\WEB\refs` (solo `css, email, flexslider, fonts, images, js, Torneos`), así que los PDF se
  enlazan a la web pública.
- Importes como números (`decimal`) formateados en el front con `Intl.NumberFormat('es-ES')`, en tabla
  con `caption`/`th scope`.
- Enlaces nuevos a Órganos de gobierno y Equipo FEP desde "Estructura organizativa" (en el original esas
  filas no tenían contenido).
- Apartados "pendientes de actualización" marcados con una etiqueta, y contador en la cabecera.
- Se elimina el bloque duplicado "Normativa de ayudas…" del final.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| Títulos en MAYÚSCULAS ("CONVENIOS SUSCRITOS", "concurso de acreedores…") | Mayúscula inicial ("Convenios suscritos", "Concurso de acreedores…") | Uniformidad |
| "Indicador **a** Autonomía Financiera" | "Indicador de autonomía financiera" | Errata |
| "RETRIBUCIONES ALTOS CARGOS" | "Retribuciones de altos cargos" | Redacción |
| "CONTRATO DE MODIFICACION N°1 AL CONTRATO DE **PATROCINO** ENTRE BABOLAT VS S.A.S Y LA FEDERACION **ESPANOLA** DE PADEL" | "Contrato de modificación nº 1 al contrato de patrocinio entre Babolat VS S.A.S. y la Federación Española de Pádel" | Erratas y tildes |
| "´TITLE SPONSOR´" | «title sponsor» | Comillas |
| "Contrato BCD Agencia **Oficia** de viajes FEP" | "Contrato BCD, agencia oficial de viajes FEP" | Errata |
| "Contrato **BVMPERS** BRANDS CLOTHES" | "Contrato BVMPRS Brands Clothes" | La marca es BVMPRS (barra de patrocinadores; fichero `BUMPERS_…`) (**SUPUESTO**) |
| "Contrato TACTICBO" | "Contrato TacticBo" | Nombre comercial (barra de patrocinadores) |
| "CCAA 2024 y auditoría", "CCAA 2023 y Auditoría"… | "Cuentas anuales 2024 y auditoría"… | Se desarrolla la abreviatura |
| "Ajuste Auditoria 2019", "Informe Auditoria…" | "Ajuste auditoría 2019", "Informe de auditoría…" | Tildes |
| "Subvención Padel Adaptado: 22.430,00€" | "Subvención Pádel Adaptado" 22430.00 | Tilde; importe como número |
| "Ayuda Años Anteriores Subv CSD Deportistas" | "Ayuda años anteriores subvención CSD deportistas" | Abreviatura |
| "894.729  euros Presupuesto para 2024" | Importe 894729.00, concepto "Ingresos propios. Presupuesto para 2024" | Importe como número |
| "La FEP **NO** se encuentra…" | "La FEP no se encuentra…" | Mayúsculas |
| "· CCAA 2023 enviadas al CSD en Julio 2024" | "Cuentas anuales 2023 enviadas al CSD en julio de 2024." | Abreviatura y mes en minúscula |
| "Javier **Casadesus**- Director Técnico" | "Javier Casadesús" · "Director Técnico" | Tilde (como en el organigrama de Equipo FEP) |
| "JUAN JOSE **GUTIERRREZ**" | "Juan José Gutiérrez" | Errata (triple r) y tildes |
| "PLAN CONCILIACIÓN Y CORRESPONSABILIDAD" | "Plan de conciliación y corresponsabilidad" | Redacción |
| Texto en negrita (`<strong>`) dentro de párrafos | Texto plano | La API no devuelve HTML (evita XSS); se pierde el resaltado de algunas fechas y normas |

Datos añadidos a partir del **nombre del fichero** (no estaban en el texto): `periodo` de varios contratos
(Babolat 2022-2023, Bullpadel 2024-2027, Iberdrola 2025-2027, BVMPRS 2026-2027, SIUX/JOMA/TacticBo 2026,
Conciliación 2025, Código ético 2015, Manual de contratación 2021) y las descripciones "Nota aclaratoria
sobre la prórroga del presupuesto" (Presupuesto 2025), "Aprobado en la Asamblea General del 16 de
diciembre de 2023" (Presupuesto 2024), "Anexo III: Código de Ética Deportiva (15 de junio de 2015)" y
"Manual de procedimientos de contratación y compras" (**SUPUESTO**: revisar).

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: "Contrato de modificación nº 1…" y "Contrato de patrocinio de Babolat como title
  sponsor…" enlazan **al mismo PDF** (`BABOLAT-CIRCUITO-MENORES-FEP 22-23-061026.pdf`). Falta uno de los dos
  documentos. Se han mantenido ambas entradas, como en la web actual.
- **DECISIÓN FEP**: "Contrato de patrocinio publicitario HEAD pelota oficial FEP **27-28**" enlaza a
  `HEAD-Webmail_ 071026.pdf` (nombre de fichero sospechoso). ¿Es el documento correcto y el periodo es 2027-2028?
- **DECISIÓN FEP**: "Ingresos propios" sigue en **2024** (894.729 €) aunque ya hay presupuesto 2026; "Envío
  de documentación financiera al CSD" solo cita las CCAA **2023** aunque ya están publicadas las de 2024;
  "Subvenciones" es de 2025. ¿Se actualizan?
- **DECISIÓN FEP**: los nombres del equipo no coinciden con el organigrama de Equipo FEP
  (`Equipo_FEP.asp`): "Vanessa Magro" / "Vanesa Magro Herráiz"; "**Cristina San Vicente** – Formación" /
  "**Cristina San Valentín Blanco**"; "Carmen Fernández Vegue" / "Carmen Fernández-Vegue"; "Juan José
  Gutiérrez – Seleccionador Nacional Absoluta" / "Juanjo Gutiérrez Vicario – Seleccionador absolutos";
  "Director Técnico" / "Dirección Deportiva Federativa". Se ha respetado lo que publica cada página.
  Además, las funciones de Carmen Fernández y Cristina San Vicente son casi idénticas (¿copia y pega?) y
  el equipo técnico de esta página no incluye a los seleccionadores de menores.
- **DECISIÓN FEP**: el presidente figura como "Javier Rodríguez Piris" (aquí y en Equipo FEP) y como
  "Javier Rodriguez Piris" en Órganos de gobierno.
- "Contrato suscrito con el CSD" enlaza a `Contrato_Prestamo_Con_CSD.pdf` (de 2018): ¿se aclara que es un
  contrato de préstamo y su vigencia?
- "Procedimiento de contratación y compras" muestra el documento como "Protocolo" pero el fichero es el
  *Manual de procedimientos de contratación y compras 2021*: se mantiene "Protocolo" como título y se
  añade la descripción.
- Cinco apartados están declarados **pendientes de actualización** (autonomía financiera, indicador de
  endeudamiento, gastos relevantes, endeudamiento, normativa de ayudas 2026).
- `actualizado` = 2026-11-06 (última modificación del `.asp`); "Funciones" y "Normativa aplicable"
  declaran "Información actualizada: enero de 2026".

## Modelo SQL propuesto (para cuando se quiera editar desde la gestión)

Hoy se edita el JSON. Para gestionarlo desde el back-office (sobre todo, subir PDF sin tocar código),
basta con estas tablas y cambiar la implementación de `ITransparenciaRepository` por Dapper (el contrato
de la API no cambia):

```sql
CREATE TABLE SFEP_Transparencia_Categorias (IdCategoria int IDENTITY PRIMARY KEY, Clave varchar(40) UNIQUE,
                                    Titulo nvarchar(150), Descripcion nvarchar(500) NULL, Orden int);
CREATE TABLE SFEP_Transparencia_Apartados  (IdApartado int IDENTITY PRIMARY KEY, IdCategoria int REFERENCES SFEP_Transparencia_Categorias,
                                    Clave varchar(60) UNIQUE, Titulo nvarchar(250), Pendiente char(1) DEFAULT 'N',
                                    Actualizacion nvarchar(60) NULL, Orden int, Visible char(1) DEFAULT 'S', Borrado char(1) DEFAULT 'N');
-- Párrafos y puntos de lista (Tipo = 'P' párrafo | 'L' lista)
CREATE TABLE SFEP_Transparencia_Textos     (IdTexto int IDENTITY PRIMARY KEY, IdApartado int REFERENCES SFEP_Transparencia_Apartados,
                                    Tipo char(1) CHECK (Tipo IN ('P','L')), Texto nvarchar(2000), Orden int);
CREATE TABLE SFEP_Transparencia_Enlaces    (IdEnlace int IDENTITY PRIMARY KEY, IdApartado int REFERENCES SFEP_Transparencia_Apartados,
                                    Texto nvarchar(200), Url nvarchar(400), Orden int);
CREATE TABLE SFEP_Transparencia_Personas   (IdPersona int IDENTITY PRIMARY KEY, IdApartado int REFERENCES SFEP_Transparencia_Apartados,
                                    Nombre nvarchar(200), Cargo nvarchar(200) NULL, Grupo nvarchar(100) NULL,
                                    Funciones nvarchar(max) NULL, -- una función por línea
                                    RetribucionAnualBruta decimal(12,2) NULL, Nota nvarchar(250) NULL, Orden int, Borrado char(1) DEFAULT 'N');
CREATE TABLE SFEP_Transparencia_Importes   (IdImporte int IDENTITY PRIMARY KEY, IdApartado int REFERENCES SFEP_Transparencia_Apartados,
                                    Concepto nvarchar(250), Importe decimal(14,2), Orden int);
CREATE TABLE SFEP_Transparencia_Documentos (IdDocumento int IDENTITY PRIMARY KEY, IdApartado int REFERENCES SFEP_Transparencia_Apartados,
                                    Titulo nvarchar(400), Descripcion nvarchar(400) NULL, Periodo varchar(20) NULL,
                                    Archivo nvarchar(260), FechaPublicacion date NULL, Orden int,
                                    Visible char(1) DEFAULT 'S', Borrado char(1) DEFAULT 'N');
```

## Ficheros

- API: `Contracts/Federacion/TransparenciaDto.cs`, `Data/Repositories/Federacion/TransparenciaRepository.cs`,
  `Controllers/Federacion/TransparenciaController.cs`, `Contenidos/transparencia.json`; tests en
  `tests/FEP.Api.Tests/TransparenciaTests.cs`. Registro en `Program.cs`.
- Web: `wwwroot/ley-de-transparencia.html`, `assets/js/transparencia.js`, estilos `.transp-*`,
  `.person-card__funcs` y `.person-card__money` en la sección "Páginas interiores" de `assets/css/fep.css`.
- Mock: `mock/federacion-transparencia.json`, generado con `tools/generar-mock-transparencia.ps1`
  (no editar a mano: se edita `Contenidos/transparencia.json` y se vuelve a generar).
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`ley_de_transparencia` y `transparencia` →
  `ley-de-transparencia.html`; `/Transparencia`, que llega de la BD en el menú de la portada, da **404** en
  la web actual). Enlaces "Transparencia" de la barra superior, panel móvil y bloque "Cerca de la pista"
  de `index.html`, `organos-de-gobierno.html` y `equipo-fep.html` apuntan ya a la página nueva.
