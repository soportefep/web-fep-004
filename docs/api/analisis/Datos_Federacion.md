# Análisis: `Datos_Federacion.asp` → `federacion.html` + `GET /api/v1/federacion/federaciones-autonomicas/{id}`

URL actual: `/Federacion/{id}/{nombre}` (regla `Federaciones/Federacion` de `web.config`:
`^Federacion/([0-9]+)/([_0-9a-z-]+)` → `Datos_Federacion.asp?id={R:1}&Fed={R:2}`; el `Fed` no se usa) y
`Datos_Federacion.asp?Id={id}` (enlace de `Federaciones.asp`). Ruta nueva propuesta en `redirecciones.csv`:
`/federacion` → `federacion.html?id={id}` (0 = Federación Española, con el total nacional).

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` (+ columna de patrocinadores) |
| `sql_noticias` (`SFEP_Noticias`) | Se consultaba **pero no se usaba** | Eliminado |
| Enlace "<< Volver a todas las Federaciones" | HTML a mano (`href="Federaciones"`) | Miga de pan "Federaciones autonómicas" + bloque "Otras federaciones" al final |
| Bandera | `refs/images/{id}.jpg` (no venía de la BD) | `imagen` / `imagenUrl` (el front usa la del listado si existe: Canarias `18b.jpg`) |
| Nombre, presidente/a, dirección, CP, localidad, teléfonos, email, web | `sql_fed`: `SELECT SFEP_Federaciones.* … WHERE IdFederacion=<id>` | `nombre`, `presidente`, `direccion`, `codigoPostal`, `localidad`, `telefonos[]`, `email`, `web` (columnas explícitas) |
| Recuadro azul "Los datos facilitados están actualizados online…" | HTML a mano | Entradilla de la sección "Licencias {año}" (mismo mensaje, redactado) |
| Nº de licencias jugador/a | `sql_numLicJug`: `COUNT(SFEP_Licencia.Federacion)` de `SFEP_Licencia ⨝ SFEP_Fichas (LicenciaJugador)`, `Año=year(now()) AND Tipo='J' AND Tramitado='S'` (+ `Federacion=<id>` si `id<>0`) | `licencias.jugadores` |
| "Puesto Federacion Autonómicas" | `sql_puesto`: mismo recuento `GROUP BY Federacion ORDER BY COUNT DESC`, recorrido entero en VBScript hasta encontrar la federación | `licencias.puesto` (`RANK()` en SQL) + `federacionesEnRanking` |
| Nº de licencias técnico nacional | `sql_numLicTec`: `Tipo='T'`; con `id<>0` por `SFEP_Licencia.Federacion`, con `id=0` por `SFEP_Fichas.Comunidad = SFEP_Federaciones.Comunidad` | `licencias.tecnicos` |
| Nº de licencias juez-árbitro nacional | `sql_numLicArb`: igual que técnicos con `Tipo='A'` y `SFEP_Fichas.ArbitroHabilitadoFEP='S'` | `licencias.arbitros` |
| Nº de clubes federados | `sql_clubes`: `SFEP_Federaciones ⨝ SFEP_Clubes (Comunidad) ⨝ SFEP_Clubes_Licencia`, `Año=year(now()) AND Renovado='S'` (+ `IdFederacion=<id>`) | `licencias.clubes` (`EXISTS`, cada club una vez) |
| Gráfico Highcharts "Evolución Licencias Jugador/a últimos años" | `sql_evolucion` (`SFEP_Web_Historial`: Año, Licencia) volcado en 14 variables `lic1…lic14` con un `select case` de 2012 a 2025 escrito a mano, más `lic15` = jugadores del año en curso con la etiqueta **"2026" fija** | `licencias.evolucion[]` (2012 → año en curso, calculado) |
| Gráfico "Distribución por Género de las licencias" | `sql_genero`: `COUNT(Genero) GROUP BY Genero`; solo pinta `M` y `F` | `licencias.genero` (+ `sinDato`) |
| Gráfico "Distribución por Edad de las licencias" | `sql_edades`: **una fila por licencia** con `Year(FechaNac)`, contada en VBScript en 4 tramos (≤18, 19–23, 24–39, >39) | `licencias.edades` (+ `sinDato`), agregado en SQL |

Columnas de `SFEP_Federaciones` realmente pintadas: `Federacion`, `Presidente`, `Direccion`, `CP`, `Localidad`,
`Telefono1`, `Telefono2`, `Email`, `Web`. La página **no escribe** en la BD.

## Parámetros (`Request`) y validación

| ASP | API nueva | Validación |
|---|---|---|
| `id` (`limpiar(request("id"))`, sin conversión a número) | `id` en la ruta (`/federaciones-autonomicas/{id}`) | entero 0–99 → si no, **400** `ValidationProblemDetails`; si no existe, **404** `ProblemDetails` con `urlWebActual` (listado de la web actual) |
| `Fed` (nombre en la URL limpia) | — | Se ignoraba; se ignora |
| `year(now())` | año de `TimeProvider` (hora local del servidor) | — |

En el front, `federacion.html?id=6` (también `?Id=6`, como el ASP). `comun.js` traslada a la página nueva los
enlaces de la BD a `Datos_Federacion.asp?Id=…` y a `/Federacion/{id}/{nombre}`.

## Lógica / visibilidad heredada

- **`id = 0` es la FEP con el total nacional**: sin filtro de federación en jugadores, género y edades, y con el
  cruce por `Comunidad` en técnicos, árbitros y clubes. No muestra puesto (`if id<>0`). Se mantiene (`nacional`).
- **Sin licencias no hay puesto**: si la federación no tiene licencias de jugador/a en el año, no aparece en el
  `GROUP BY` y el ASP pintaba "Puesto Federacion Autonómicas: **º**" (vacío; hoy La Rioja). Ahora `puesto = null`
  y no se muestra.
- **Web solo si hay dato** (`if rs_fed("Web")<>""`): se mantiene (La Rioja y la FEP no tienen).
- **Años del gráfico escritos a mano**: el `select case` iba de 2012 a 2025 y la última barra decía "2026"
  aunque su valor era el del año en curso. En 2027 la barra "2026" mostraría los datos de 2027 y el 2026 cerrado
  desaparecería hasta que alguien editase el ASP. Ahora la serie se calcula: 2012 → año anterior desde
  `SFEP_Web_Historial` (`null` si falta un año) y el año en curso al final, marcado `enCurso`.
- `activo=2`: opción de menú activa (plantilla). Funciones JS `MesAnterior`, `CargarEvento`, `MeGusta`,
  `busqueda`: no se usan. Se cargaban **dos jQuery** (1.10.2 y 1.8.2) más Highcharts y su módulo de exportación.

## Problemas del original

1. **Inyección SQL**: `id` se concatenaba **sin comillas** en nueve consultas; `limpiar()` (lista negra) no lo
   impide. Ahora `@Id` y `@Anio` son parámetros Dapper y el id se valida (0–99) antes de tocar la BD.
2. **`SELECT SFEP_Federaciones.*`**: traía a la página todas las columnas, entre ellas `Org_id`/`Secret_Key` de
   la API antigua (no se pintaban, pero viajaban al servidor web en cada visita). Ahora columnas explícitas.
3. **Error 500** con un id inexistente (`rs_fed("Federacion")` sobre un recordset vacío) o no numérico. Ahora 404 / 400.
4. **Rendimiento**: `sql_edades` traía una fila por licencia (≈120.000 en la FEP) para contarlas en VBScript y
   `sql_puesto` recorría todas las federaciones; nueve idas a la BD. Ahora 2 idas (ficha + un lote) con todo
   agregado en SQL Server (`COUNT(CASE …)`, `RANK()`), y caché de 5 minutos.
5. **Clubes de la FEP duplicados**: con `id=0`, el `INNER JOIN SFEP_Federaciones ON Comunidad` multiplica cada club
   por el número de filas de `SFEP_Federaciones` con su comunidad. La web actual dice **1.518** clubes; la suma de
   las 19 federaciones es **1.274** (el mismo total que el buscador de clubes). La diferencia (244) es exactamente
   Andalucía (187) + Aragón (57), lo que indica que esas dos comunidades tienen dos filas en `SFEP_Federaciones`
   (**SUPUESTO** deducido de las cifras públicas; no se ha podido consultar la BD). Ahora `EXISTS`.
6. **Técnicos y árbitros nacionales**: con `id=0` cuentan por la comunidad de la ficha (no por la federación que
   expide la licencia) y con el mismo `INNER JOIN` que duplica: la web da 1.533 técnicos y 311 árbitros frente a
   1.257 y 250 sumando las federaciones. Se mantiene el criterio por comunidad del ASP pero con `EXISTS` (sin
   duplicados), así que **las cifras nacionales bajarán** respecto a la web actual (ver pendientes).
7. **JavaScript roto con datos vacíos**: sin licencias el ASP escribía `['Masculino', ],` en el script y
   Highcharts dibujaba un gráfico vacío sin explicación; sin dato de un año, `['2015', ],`. Ahora la API devuelve
   ceros o `null` y el front lo explica ("No hay licencias de jugador/a tramitadas en 2026", "s. d.").
8. **Edades y género incompletos**: las fichas sin fecha de nacimiento (o con otro género) no entraban en ningún
   tramo y los porcentajes no cuadraban con el total sin avisar. Ahora se cuentan en `sinDato` (hoy son 0).
9. **XSS**: todos los campos se pintaban sin `Server.HTMLEncode`. El front usa `textContent`.
10. **Accesibilidad**: los gráficos eran `<div>` de Highcharts sin alternativa textual, la bandera sin `alt`
    significativo y el título de la página en mayúsculas de la BD.

## Mejoras respecto al original

- Cabecera con bandera, nombre oficial (del listado de federaciones) y resumen: licencias, puesto y clubes.
- Contacto en tarjetas: teléfonos como enlaces `tel:`, email `mailto:` y web externa en pestaña nueva; teléfonos
  repetidos fuera (Galicia tenía el mismo número en los dos campos).
- Cifras de licencias en tarjetas; la de clubes enlaza al buscador ya migrado filtrado por la federación
  (`clubes.html?federacion={id}`).
- Gráficos sin librería (adiós a jQuery ×2 y Highcharts): columnas de evolución con la cifra encima de cada barra y
  el año en curso en rojo; tabla desplegable con los datos y la variación interanual (`caption`, `th scope`);
  barras de género y edad con cifra y porcentaje. El lector de pantalla lee una frase por año.
- Índice lateral (Contacto · Licencias · Evolución · Género y edad), enlaces a las demás federaciones y a la ficha
  en la web actual, y fecha de consulta de las cifras.
- Móvil sin scroll horizontal: las 15 columnas caben con las cifras en vertical; tarjetas en una columna.
- `federaciones.html` enlaza ya a esta página ("Ficha") en lugar de a la web actual.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| Web de Baleares y Canarias: `https://https://www.fpadelib.es/` | `https://www.fpadelib.es/`, `https://canariaspadel.es/` | En BD la web ya trae esquema y el ASP anteponía otro (`Texto.EnlaceExterno`) |
| Web de la Comunitat Valenciana `https://www.padelcv.com` (BD) | En pantalla, `https://padelcv.com` (la del listado revisado) | `www.padelcv.com` falla con certificado TLS no válido; sin `www` responde 200. La API devuelve lo que hay en BD |
| Nombre de la BD en mayúsculas ("FEDERACION ANDALUZA DE PADEL") | Título con el nombre oficial del listado ("Federación Andaluza de Pádel") | Legibilidad; la API devuelve el de la BD (con espacios dobles colapsados: "FEDERACION DE PADEL  CASTILLA-LA MANCHA") |
| "Teléfonos: 954270884 - " (guion colgando) / "Teléfonos: - " (Asturias) | Solo los teléfonos que hay; sin bloque si no hay ninguno | Campos vacíos |
| Galicia: "981 231 537 - 981231537" | "981 231 537" | Mismo número repetido |
| "Puesto Federacion Autonómicas" | "Puesto 4.º de 18 federaciones autonómicas" | Tilde y base del puesto |
| Clubes de la FEP: 1.518 | 1.274 | Duplicados del `JOIN` (problema 5) |
| Barra "2026" fija | Año en curso calculado | Problema de los años escritos a mano |

## Comprobación de enlaces (12/11/2026)

- Las 20 fichas públicas (`Datos_Federacion.asp?Id=…`) responden 200, igual que `/Federacion/6/andalucia`.
- Banderas `refs/images/{id}.jpg`: las 20 responden 200. Canarias tiene dos: `18.jpg` (bandera civil, la que usa
  la ficha) y `18b.jpg` (con escudo, la que usa el listado); el front muestra la del listado.
- Webs de la BD: todas 200 salvo `https://www.padelcv.com` (error TLS; ver tabla). Las demás se comprobaron en la
  migración de `Federaciones.asp`.

## Modo prototipo (mocks)

`mock/federacion-federaciones-autonomicas-{id}.json` (20 ficheros: las 19 autonómicas y la FEP) son una
instantánea de la web pública del 12/11/2026 pasada a la forma de `FichaFederacionDto` con las mismas
normalizaciones que el repositorio (espacios, teléfonos, web). Salvo: los clubes de la FEP van ya corregidos
(1.274) y sus técnicos/árbitros son los de la web (1.533/311), porque la cifra corregida solo se puede calcular
contra la BD. `imagenUrl` apunta a las banderas locales de `assets/img/fep/federaciones/`. No salen de
`Contenidos/`, por eso no tienen script generador en `tools/`.

Datos observados: Cataluña es la 1.ª (30.844 licencias); La Rioja tiene 0 licencias de jugador/a en 2026 (8 en 2025,
221 en 2024), sin presidente/a ni web; Melilla, 0 clubes; Canarias y Ceuta, 1.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: técnicos y árbitros nacionales. El ASP los contaba por la comunidad de la ficha y con duplicados
  (1.533 / 311 en la web actual). Se ha mantenido el criterio por comunidad sin duplicados (bajarán). ¿Debe el total
  nacional ser simplemente todas las licencias `T`/`A` tramitadas del año (incluidas las expedidas por la FEP)?
- **DECISIÓN FEP**: `SFEP_Federaciones` tiene dos filas con la comunidad de Andalucía y dos con la de Aragón
  (deducido de la diferencia de clubes). ¿Son delegaciones u otras entidades? ¿Deben contar en el total nacional?
- **DECISIÓN FEP**: el puesto se calcula solo entre federaciones autonómicas (`Federacion` 1–99) y los empates
  comparten puesto; el ASP ordenaba todos los valores de `Federacion` y desempataba al azar. Hoy da el mismo
  resultado (18 federaciones con licencias, puestos 1–18).
- **DECISIÓN FEP**: corregir en la BD la web de la Comunitat Valenciana (`padelcv.com` sin `www`), el CP de
  Cantabria (`319012`, 6 cifras; probablemente `39012`) y el CP vacío de Cataluña; completar presidente/a y web de
  La Rioja. Se muestran tal cual están en la BD.
- **DECISIÓN FEP**: ¿qué bandera de Canarias es la oficial para la web, `18.jpg` o `18b.jpg`?
- **SUPUESTO**: `SFEP_Web_Historial` tiene (como mucho) una fila por federación y año, y la FEP la rellena al cerrar
  cada año; si falta un año, la barra sale como "sin dato". Antes había que editar el ASP cada año.
- **SUPUESTO**: tipos de columnas: `IdFederacion`, `[Año]`, `Federacion`, `Licencia` enteros; `Tipo`, `Tramitado`,
  `Genero`, `Renovado`, `ArbitroHabilitadoFEP` `char(1)`; `FechaNac` fecha; `CP` y teléfonos texto (se leen con
  `CAST` por si alguno fuera numérico).
- **SUPUESTO**: se mantiene `COUNT(SFEP_Licencia.Federacion)` como total de jugadores (ignora licencias sin
  federación, como el ASP); género y edades cuentan todas las filas.
- **SUPUESTO**: id válido 0–99 (las federaciones de la web son 0–22; los buscadores ya excluían `IdFederacion >= 100`).
- Las cifras se cachean 5 minutos (output cache): "actualizado online" pasa a "con hasta 5 minutos de retraso".
- `Datos_Federacion2.asp` (versión antigua sin enlaces entrantes) está marcada como DESCARTAR en el inventario.

## Modelo SQL propuesto

No aplica: todo sale de tablas existentes. Para el rendimiento conviene un índice en
`SFEP_Licencia ([Año], Tipo, Tramitado) INCLUDE (Federacion, IdFicha)` si no existe (**SUPUESTO**, revisar el plan
de ejecución en producción).

## Ficheros

- API: `Contracts/Federacion/FichaFederacionDto.cs` (`FichaFederacionDto`, `LicenciasFederacionDto`,
  `EvolucionLicenciasDto`, `GeneroLicenciasDto`, `EdadesLicenciasDto`, `ReglasFichaFederacion`),
  `Data/Repositories/Federacion/FichaFederacionRepository.cs` (Dapper),
  `Controllers/Federacion/FichaFederacionController.cs` (comparte prefijo de ruta con el listado de
  `FederacionesAutonomicasController`); registro en `Program.cs`; tests en
  `tests/FEP.Api.Tests/FichaFederacionTests.cs` (400, 404 y año con repositorio falso, serie de evolución,
  teléfonos y espacios: no necesitan SQL Server).
- Web: `wwwroot/federacion.html`, `assets/js/federacion.js`, estilos `.fedf-*` en la sección "Páginas interiores"
  de `assets/css/fep.css` (y su bloque responsive).
- Mock: `mock/federacion-federaciones-autonomicas-{0,1,2,3,6,7,8,9,10,11,12,13,14,15,16,17,18,19,21,22}.json` y
  `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS`/`QUERY_MIGRADA` en `assets/js/comun.js` (`datos_federacion` → `federacion.html?id=`, y la
  URL limpia `/Federacion/{id}/{nombre}`); enlace "Ficha" de `assets/js/federaciones.js`.
