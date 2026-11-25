# Análisis: `Federaciones.asp` → `federaciones.html` + `GET /api/v1/federacion/federaciones-autonomicas`

URL actual: `/Federaciones` (regla `^Federaciones` de `web.config`, que también atiende
`/Federaciones_Territoriales`, el enlace que guarda el menú de la BD). Ruta nueva propuesta en
`redirecciones.csv`: `/federaciones` → `federaciones.html`.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (consultas `SFEP_Web_Menu`, `SFEP_Web_RS`, banners…) | Ya migrado: `comun.js` + `/api/v1/menu`, `/api/v1/redes-sociales`, `/api/v1/patrocinadores` (columna derecha) |
| `sql_noticias` (`SFEP_Noticias`) | Se consultaba **pero no se usaba** en la página | Eliminado |
| Título "federaciones y delegaciones AUTONÓMICAS" y recuadro amarillo de presentación | HTML escrito a mano | `h1` "Federaciones autonómicas." + `introduccion` |
| 19 federaciones autonómicas + Federación Española | HTML escrito a mano: bandera `refs/images/{Id}.jpg` (Canarias `18b.jpg`), "Federación" + territorio y enlace a `Datos_Federacion.asp?Id={Id}` | `federaciones[]` (`id`, `territorio`, `nombre`, `nacional`, `imagen`, `imagenUrl`, `web`) |
| "Reserva Herramienta ZOOM" | HTML a mano: texto, imagen `refs/images/zoom.jpg` (809×135), email `zoom@padelfederacion.es` en texto plano | `reservaZoom` (`titulo`, `parrafos`, `email`, `imagen`, `imagenUrl`, `imagenAlt`) |
| Google Calendar de reservas | `<iframe>` de `calendar.google.com/calendar/embed?…` (800×600 fijos, sin `title`) | `reservaZoom.calendario` (`titulo`, `url`, `zonaHoraria`) |

**La página no consultaba la BD** (el mapa de la web la describía como "Listado BD"): todo estaba en el
HTML. Se ha pasado a `src/FEP.Api/Contenidos/federaciones-autonomicas.json`, servido por
`GET /api/v1/federacion/federaciones-autonomicas`. El `id` de cada federación es su
`SFEP_Federaciones.IdFederacion` (el mismo que usaba el enlace a la ficha), así que sirve también como
filtro `federacion` de `GET /api/v1/clubes` (página de clubes).

Los nombres oficiales y las webs **no estaban en esta página**: se han tomado de la ficha pública de cada
federación (`Datos_Federacion.asp?Id=…`, que pinta `SFEP_Federaciones.Federacion` y `Web`), rastreada el
12/11/2026. Son una copia: cuando se migre la ficha, conviene leerlos de `SFEP_Federaciones` (ver modelo).

## Lógica / visibilidad heredada

- No hay condiciones `<% if %>` propias de la página: todo lo que estaba en el HTML se ve.
- `activo=1` marca como activa la opción de menú `IdOpcion = 1` (la portada): heredado de la plantilla.
- Las funciones JS `MesAnterior`, `CargarEvento`, `MeGusta` y `busqueda` del `<head>` no se usan en la página.
- `sql_noticias` se ejecuta en cada visita y no se pinta: consulta muerta.

## Google Calendar incrustado

Se mantiene, con estas condiciones:

- `iframe` con `title` accesible ("Calendario de reservas de Zoom de la FEP (Google Calendar)"),
  `loading="lazy"` (no se carga hasta que se acerca a la pantalla) y `referrerpolicy`.
- Ancho fluido (100 %), 600 px de alto en escritorio y 480 px en móvil; en móvil se añade `mode=AGENDA`
  a la URL (la vista de mes de Google no cabe en 360 px).
- Enlace alternativo "Abrir el calendario en Google Calendar" (nueva pestaña) bajo el `iframe`.
- Solo se incrusta una URL de `https://calendar.google.com/calendar/embed`: la API descarta cualquier otra
  (`FederacionesAutonomicasRepository`) y el front lo vuelve a comprobar (`federaciones.js`).
- Comprobado: la URL de inserción responde 200. Es un servicio de terceros (Google): si se publica una
  política de cookies/CSP, hay que permitir `frame-src https://calendar.google.com` (ver
  `docs/migracion/terceros.md`).

## Mejoras respecto al original

- HTML válido: el ASP repetía `id="federaciones"` 20 veces y no daba `alt` a ninguna imagen.
- Cada federación muestra su **nombre oficial**, su **web** (enlace externo) y dos enlaces: **Clubes**
  (`clubes.html?federacion={id}`, el buscador ya migrado, filtrado por esa federación) y **Ficha** (la
  ficha `federacion.html?id={id}`, migrada desde `Datos_Federacion.asp`: ver `Datos_Federacion.md`).
- La Federación Española va en su propia sección, separada de las autonómicas.
- El email de Zoom pasa de texto plano a botón `mailto:` con asunto "Reserva de Zoom".
- Las banderas son decorativas (`alt=""`): el nombre del territorio va al lado como texto.
- Índice lateral, columna de patrocinadores y móvil sin scroll horizontal (el `iframe` de 800 px fijos
  del original desbordaba en móvil).
- Nota con enlace al buscador de clubes.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "Federaci**ò**n Baleares" (`&ograve;`) | "Baleares" (territorio) · "Federación de Pádel de les Illes Balears" | Errata de acento |
| "Federación Castilla La Mancha" | "Castilla-La Mancha" | Nombre oficial de la comunidad |
| "…que la deseen **utitilizar**…" | "utilizar" | Errata |
| "Federación" + territorio en todas las tarjetas ("Federación Andalucía") | Territorio + nombre oficial ("Andalucía" · "Federación Andaluza de Pádel") | Los nombres oficiales salen de la ficha |
| Nombres oficiales en la BD en MAYÚSCULAS sin tildes ("FEDERACION ANDALUZA DE PADEL") | "Federación Andaluza de Pádel"; Cataluña en catalán: "Federació Catalana de Pàdel" | Legibilidad (la BD guarda "FEDERACIO CATALANA DE PADEL") |
| Web de Baleares y Canarias en la ficha: `https://https://…` | `https://www.fpadelib.es/`, `https://canariaspadel.es/` | En BD la web ya trae esquema y la ficha antepone otro |
| Web de la Comunitat Valenciana `www.padelcv.com` | `https://padelcv.com` | `www.padelcv.com` falla con certificado TLS no válido; sin `www` responde 200 |

## Comprobación de enlaces (12/11/2026)

- Imágenes: `refs/images/{Id}.jpg`, `18b.jpg` y `zoom.jpg` responden 200 y son idénticas (mismo tamaño) a
  las de `X:\TICS\WEB\refs\images\`. Copias locales para el mock en `assets/img/fep/federaciones/`
  (`bandera-{id}.jpg`, `zoom.jpg`).
- Webs de federaciones: 200 todas salvo `https://www.fcpadel.cat` (308, redirección permanente; se
  mantiene) y la de la Comunitat Valenciana (ver tabla). La Rioja no tiene web en la ficha.
- Calendario de Google: 200.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: ¿sigue vigente la reserva de Zoom compartida y su calendario? Se ha migrado tal cual.
  La imagen `zoom.jpg` es publicidad de Zoom ("#1 HD Video and Web Conferencing Service"), en inglés; se
  puede retirar sin perder información.
- **SUPUESTO**: nombre oficial de La Rioja. La ficha dice "FEDERACION DE LA RIOJA" (sin "de Pádel") y no
  tiene web: se ha dejado "Federación de La Rioja".
- **SUPUESTO**: la Federación Española (Id 0) se mantiene en la página (el original la listaba al final
  como una tarjeta más); ahora va en sección aparte.
- El título original decía "federaciones y **delegaciones** autonómicas", pero no hay ninguna delegación
  en la lista: se ha titulado "Federaciones autonómicas". Confirmar si falta alguna delegación.
- Las webs y nombres oficiales son una copia de `SFEP_Federaciones` a 12/11/2026: si cambian en la BD, hay
  que actualizar el JSON (o pasar al modelo SQL de abajo).

## Modelo SQL propuesto (para cuando se quiera editar desde la gestión)

`SFEP_Federaciones` ya existe y tiene `IdFederacion`, `Federacion` (nombre oficial), `Abreviatura`,
`Comunidad`, `Web`, `Email`, `Presidente`, dirección y teléfonos (usados por `Datos_Federacion.asp`), además
de `Org_id`/`Secret_Key` de la API antigua (**nunca** deben exponerse). Basta con añadir lo que solo estaba
en el HTML y una tabla para el bloque de Zoom; después se sustituye `IFederacionesAutonomicasRepository`
por una implementación Dapper (el contrato no cambia):

```sql
ALTER TABLE SFEP_Federaciones ADD
    Territorio  nvarchar(80)  NULL,          -- "Andalucía", "Comunidad de Madrid"…
    NombreWeb   nvarchar(150) NULL,          -- nombre oficial con tildes (Federacion está en mayúsculas)
    Bandera     nvarchar(260) NULL,          -- ruta bajo refs/ (images/6.jpg)
    OrdenWeb    int           NULL,
    VisibleWeb  char(1)       NOT NULL DEFAULT 'S';

CREATE TABLE SFEP_Web_Bloques (IdBloque int IDENTITY PRIMARY KEY, Clave varchar(40) UNIQUE,  -- 'reserva-zoom'
                               Titulo nvarchar(150), Texto nvarchar(max), Email nvarchar(150) NULL,
                               Imagen nvarchar(260) NULL, ImagenAlt nvarchar(200) NULL,
                               CalendarioUrl nvarchar(1000) NULL, CalendarioTitulo nvarchar(200) NULL,
                               Visible char(1) DEFAULT 'S');
```

Consulta del listado: `SELECT IdFederacion, Territorio, NombreWeb, Federacion, Bandera, Web FROM SFEP_Federaciones
WHERE IdFederacion < 100 AND VisibleWeb = 'S' ORDER BY OrdenWeb` (la `Web` en BD unas veces trae esquema y
otras no: se normaliza con `Texto.EnlaceExterno`).

## Ficheros

- API: `Contracts/Federacion/FederacionesAutonomicasDto.cs`,
  `Data/Repositories/Federacion/FederacionesAutonomicasRepository.cs`,
  `Controllers/Federacion/FederacionesAutonomicasController.cs`, `Contenidos/federaciones-autonomicas.json`;
  registro en `Program.cs`; tests en `tests/FEP.Api.Tests/FederacionesAutonomicasTests.cs`.
- Web: `wwwroot/federaciones.html`, `assets/js/federaciones.js`, estilos `.fed-*` en la sección "Páginas
  interiores" de `assets/css/fep.css` (y su bloque responsive), imágenes en `assets/img/fep/federaciones/`.
- Mock: `mock/federacion-federaciones-autonomicas.json`, generado con
  `tools/generar-mock-federaciones-autonomicas.ps1` (no editar a mano: se edita
  `Contenidos/federaciones-autonomicas.json` y se vuelve a generar).
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`federaciones`, `federaciones_territoriales` →
  `federaciones.html`); enlace "Federaciones territoriales" del pie de `index.html`,
  `organos-de-gobierno.html`, `equipo-fep.html` y `ley-de-transparencia.html`.
