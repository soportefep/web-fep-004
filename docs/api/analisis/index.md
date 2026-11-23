# Análisis: `index.asp` (home pública)

Origen (solo lectura): `X:\TICS\WEB\index.asp` y sus includes `include/conexionSQL.inc` (apertura de
conexión, no se copia), `include/Menus.inc`, `include/BarraLateral.inc`, `include/PiePagina.inc`,
`include/finconexion.inc` (cierre de conexión).

Todas las consultas son de **lectura**; ninguna usa valores de `Request(...)`.

## Inventario de accesos a datos → endpoint

| # | Variable ASP (fichero) | Tabla(s) | Columnas pintadas | Filtros fijos / orden | Endpoint nuevo |
|---|---|---|---|---|---|
| 1 | `sql_menu0` (Menus.inc) | `SFEP_Web_Menu` | IdOpcion, Opcion, Link, Ventana, Icono | `Site='FEP' AND Nivel=1` · `Orden` | `GET /api/v1/menu` |
| 2 | `sql_menu1` (index.asp, ×2 por opción de nivel 1) | `SFEP_Web_Menu` | Opcion, Link, Ventana, Icono | `Site='FEP' AND Nivel=2 AND Origen=<IdOpcion>` · `Orden` | `GET /api/v1/menu` (anidado en `hijos`) |
| 3 | `sql_RS` (Menus.inc) | `SFEP_Web_RS` | URLRED, Icono, Red | sin filtros · `Orden` | `GET /api/v1/redes-sociales` |
| 4 | `sql_noticias` (index.asp) | `SFEP_Noticias` | foto, Link, Ventana, Titulo | `Visible='S' AND Borrado='N' AND Destacada='S' AND Site='FEP' AND Orden<>0` · `Orden`; se pintan 6 | `GET /api/v1/noticias/destacadas?limite=6` |
| 5 | `sql_evento` (Menus.inc → index.asp) | `SFEP_Banners` | Imagen, Link, Ventana, Banner, Texto | `Borrado='N' AND Visible='S' AND Portal='FEP' AND RTrim(Tipo)='Evento'` · `orden` | `GET /api/v1/eventos/destacados` (= `banners?tipo=evento`) |
| 6 | `sql_monitor` (index.asp) | `SFEP_Formacion_Calendario` | Titulo, FechaIni, FechaFin, Lugar, Localidad, FechaInscripcion | `AbiertaInscripcion='S' AND FechaInscripcion>now() AND Borrado='N' AND Federacion=0 AND Categoria IN (ParcialN1, Monitor, EntrenadorN2, EntrenadorN3, FC)` · `FechaInscripcion` | `GET /api/v1/formacion/convocatorias?categoria=monitor` |
| 7 | `sql_foco` (index.asp) | `SFEP_Formacion_Calendario` | ídem | ídem con `Categoria='Foco'` | `GET /api/v1/formacion/convocatorias?categoria=foco` |
| 8 | `sql_juez` (index.asp) | `SFEP_Formacion_Calendario` | ídem | ídem con `Categoria='Juez'` | `GET /api/v1/formacion/convocatorias?categoria=juez` |
| 9 | `sql_avisos` (Menus.inc → BarraLateral.inc) | `SFEP_Avisos` | Fecha, Link, ventana, aviso | `Visible='S' AND Borrado='N' AND Portal IN ('FEP','AMB')` · `Fecha DESC` | `GET /api/v1/avisos` |
| 10 | `sql_destacados` (Menus.inc → BarraLateral.inc) | `SFEP_Banners` | Banner, Imagen, Link (Imagen2 se lee pero no se usa) | como #5 con `Tipo='Destacado'` | `GET /api/v1/banners?tipo=destacado` |
| 11 | `sql_patro` (Menus.inc → BarraLateral.inc) | `SFEP_Patrocinadores` | Logo, Link | `Borrado='N' AND Ver='S' AND Federacion=0` · `orden` | `GET /api/v1/patrocinadores` |
| 12 | `sql_banners` (Menus.inc → PiePagina.inc) | `SFEP_Banners` | Link, imagen, banner, ventana | como #5 con `Tipo='Interes'` | `GET /api/v1/banners?tipo=interes` |
| 13 | `sql_multimedia` (PiePagina.inc) | `SFEP_Galeria_Multimedia_Archivos` ⨝ `SFEP_Galeria_Multimedia` | IdGaleria, Archivo, Subtitulo, FechaEvento (año para la ruta) | `Tipo='FOT' AND Fed=0` · `FechaMod DESC`, `TOP 3` | `GET /api/v1/multimedia/fotos/ultimas?limite=3` |
| — | todas las anteriores | — | — | — | `GET /api/v1/portada` (agregado, una llamada) |

Rutas de imagen que componía el ASP (la API devuelve el nombre original y la URL absoluta con `Media:BaseUrl` = `https://www.padelfederacion.es/refs/`):

| Recurso | Ruta en el ASP |
|---|---|
| Noticia destacada | `refs/Noticias/{foto}` |
| Banner evento | `refs/images/Banners/{Imagen}` |
| Banner destacado | `https://www.padelfederacion.es/refs/images/{Imagen}` |
| Banner interés | `refs/images/banners/{imagen}` |
| Patrocinador | `https://www.padelfederacion.es/refs/images/banners/{Logo}` y enlace `https://{Link}` |
| Foto | `https://www.padelfederacion.es/refs/Multimedia/{year(FechaEvento)}/{Archivo}`, enlace `Documentos_Multimedia_Previo.asp?id={IdGaleria}` |

## Contenido estático (no viene de BD; queda en el front)

- Cabecera: teléfono `+34 91 510 34 00` y fecha actual (`formatdatetime(now(),1)`).
- Tarjetas de formación: imágenes fijas `refs/IMAGES/Monitor.jpg`, `Foco.jpg`, `Arbitro.jpg` y enlace fijo `Formacion_Inscripciones.asp`.
- Banners publicitarios codificados en `index.asp` (Málaga Premier Padel, Premier Padel Tour Finals,
  Cupra Raval, SAE Deportes) y bloque "Productos homologados" (`BarraPatro*.jpg`).
- Bloque "Pádel adaptado" de la barra lateral (`BannerPadelAdaptado.jpg` → `Padel-Adaptado`).
- Pie: "Colabora con nosotros" (CELAD), sello GFM, Extranet, canal de denuncias y enlaces legales.

## Problemas detectados en el original

1. **N+1 consultas en el menú**: `sql_menu1` se ejecuta una vez por cada opción de nivel 1 **y dos veces**
   (menú de escritorio y menú off-canvas) → 2·N + 1 consultas por visita. *Solución*: una sola consulta
   `Nivel IN (1,2)` y anidado en memoria.
2. **Fechas comparadas como texto**: `FechaInscripcion > '" & now() & "'` depende del formato regional del
   servidor y de `SET DATEFORMAT`; puede excluir o incluir cursos erróneamente. *Solución*: parámetro
   `@Ahora` de tipo `DateTime`.
3. **Bucle sin control de EOF**: `do while not i>6` sobre `rs_noticias` sin comprobar `rs.eof`; con menos
   de 6 destacadas la página entera rompe (error ADO EOF/BOF). *Solución*: `TOP (@Limite)`.
4. **Consultas duplicadas**: tres consultas de formación que solo cambian la categoría y tres de banners que
   solo cambian el tipo → un endpoint parametrizado por recurso. Filtros repetidos sin efecto
   (`Borrado='N'` y `Portal='FEP'` dos veces en `sql_evento`; `Federacion=0` dos veces en `sql_patro`).
5. **XSS / HTML sin codificar**: ningún valor de BD se pasa por `Server.HTMLEncode` (títulos, `Banner`,
   `Texto`, `aviso`, `Subtitulo`, atributos `href`/`target`/`class`). El front nuevo debe pintar con
   `textContent` y validar esquemas de URL (`http`, `https`, rutas relativas) antes de usar `enlace`.
6. **`SELECT *`** en casi todas las consultas: la API selecciona solo las columnas que se pintan.
7. **`RTrim([Tipo])='...'`** impide usar índices; en SQL Server la igualdad ya ignora espacios finales, se usa `Tipo = @Tipo`.
8. **`limpiar()`** (Menus.inc) es una lista negra contra inyección SQL, no una protección; aquí no aplica
   (la home no recibe parámetros), y la API usa siempre parámetros Dapper.
9. **Consultas que no se usan en todas las páginas**: `Menus.inc` lanza 6 consultas en cada página que lo
   incluye, las necesite o no. Con la API, cada pantalla pide solo lo que usa (o `/portada` en la home).
10. **Ruta de foto dependiente de `year(FechaEvento)`**: si `FechaEvento` es NULL el ASP falla; la API devuelve `imagenUrl = null`.
11. **Codificación**: `PiePagina.inc` contiene texto ya corrompido ("Enlaces de interï¿½s", "Polï¿½tica")
    — es contenido estático; el front nuevo usa UTF-8.
12. **Recordsets** nunca se cierran explícitamente (solo la conexión en `finconexion.inc`); con Dapper +
    `await using` la conexión vuelve al pool al terminar cada consulta.
13. Meta `no-cache` en toda la página: la API sirve el contenido público con caché de 5 min (output cache
    en servidor + `Cache-Control: public, max-age=300`).

## Decisiones

- **Recursos, no páginas**: 9 endpoints por recurso + 1 agregado `/portada` (llamadas en paralelo).
- **Eventos destacados** = banners de tipo `Evento` (mismo DTO `BannerDto`). El diseño Figma muestra fecha,
  lugar y etiqueta por evento, pero esos datos **no existen** como columnas en `SFEP_Banners`; hoy van dentro
  de `Texto`. En los mocks `texto` lleva "Lugar · Sede · fechas".
- **Formación**: `categoria` opcional; sin él se devuelven los tres grupos (lo que pinta la portada), cada
  convocatoria con su grupo (`categoria`) y el valor original (`tipoCurso`). Valores no válidos → 400 ProblemDetails.
- **Banners**: `tipo` obligatorio (`interes|destacado|evento`); otro valor → 400 ProblemDetails. La carpeta de la
  imagen depende del tipo, como en el ASP.
- **Hora de referencia**: `TimeProvider.GetLocalNow()` (hora local del servidor, como `now()`).
- **Textos**: se recortan espacios (columnas `char`); en los títulos de noticia, dos acentos graves seguidos (como guarda el backoffice las comillas) se convierten en `"`, igual que el `replace` del ASP.
- **Patrocinadores**: se antepone `https://` solo si `Link` no trae esquema.
- **Ids**: solo se exponen los identificadores confirmados por el ASP (`IdOpcion`, `IdGaleria`).
- Lo que no se expone: `Borrado`, `Visible`, `Site`, `Portal`, `Federacion`, `Nivel`, `Origen`, `Imagen2`.

## SUPUESTOS (no confirmables desde el ASP)

- Tipos: `IdOpcion`, `Nivel`, `Origen`, `Orden`/`orden`, `Federacion`, `Fed`, `IdGaleria` → enteros.
- `FechaIni`, `FechaFin`, `FechaInscripcion`, `SFEP_Avisos.Fecha`, `FechaMod`, `FechaEvento` → `datetime`
  (el ASP les aplica `formatdatetime`/`year`). Si alguna fuera `varchar`, el mapeo Dapper fallará y habrá que convertir en SQL.
- `Opcion`, `Ventana`, `Icono`, `Tipo` → `char` con relleno (el ASP hacía `rtrim`); resto de textos `varchar`/`nvarchar`.
- `Ventana` contiene destinos HTML (`_self`, `_blank`…).
- `SFEP_Banners.Texto` puede contener HTML heredado (el ASP lo pintaba sin codificar).
- `SFEP_Web_RS` no tiene columna de baja/visibilidad (el ASP no filtra nada).
- El servidor de la API corre en hora peninsular española.
