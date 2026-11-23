# Análisis: `Ranking_Veteranos.asp` → `ranking-veteranos.html` + `GET /api/v1/competicion/rankings/veteranos`

URL actual: `/Ranking_Veteranos` (enlace "Ranking Veteranos" de `SFEP_Rankings_Web`, desde `rankings.html`, y enlace
"Veteranos · Por grupo de edad" de la portada; responde igual con y sin `.asp`). Ruta nueva: `/ranking-veteranos` →
`ranking-veteranos.html` (no estaba en `redirecciones.csv`; se ha añadido con la convención del proyecto y el nombre que
ya preveían `rankings.js` y `Rankings.md`).

Como `Ranking_Menores.asp`, la página es un "cascarón": la clasificación la cargaba por AJAX (`$(div).load(...)`) desde
`CargarRnkVet.asp` (copia de `CargarRnkMen.asp` con otros `TipoRanking`), que **no está en `pages_to_dev/`** y se ha
leído en `X:\TICS\WEB\` (solo lectura, Windows-1252). Se reutiliza **íntegramente** el diseño de los rankings ya
migrados (`ReglasClasificaciones`, `ClasificacionesRankingRepository`, `ClasificacionesRankingController`,
`ClasificacionRankingDto`, `ranking.js` y estilos `.rkc-*`): basta con declarar los 12 `TipoRanking` de veteranos. No
hay endpoint, DTO, repositorio ni CSS nuevos. Cambios genéricos pequeños, sin efecto en las páginas ya migradas:
nombre "+35" para las categorías numéricas, `NULLIF` de los datos vacíos de `SFEP_Ranking_Tipos` y un mensaje para una
lista declarada sin puestos.

Includes: `conexionSQL.inc` (conexión; no se copia nada), `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc`,
`finconexion.inc` (comunes, ya migrados en `comun.js`). El `.asp` de `pages_to_dev/` es idéntico al de `X:\TICS\WEB\`.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie, barra lateral | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` + `/api/v1/menu`, `/redes-sociales`, `/patrocinadores` (columna derecha) |
| `sql_noticias` (`SFEP_Noticias` destacadas) | Se consultaba **pero no se usaba** | Eliminado |
| Título "RANKING VETERANOS CIRCUITO FEP" (`h5`) | HTML escrito a mano | `h1` "Ranking de Veteranos." + entradilla + fecha de actualización de la lista elegida |
| Aviso del "Ciclo clasificatorio 2 del Circuito de **Menores**… ranking a 12/02/2026… PAREJAS MIXTAS" + tabla de 10 PDF de menores "Ranking a 04/03/26" | HTML dentro de `<% if a=5 then %>` (copiado de `Ranking_Menores.asp`) | **No se migra**: `a` nunca se asigna, así que **nunca se mostraba** (comprobado en la web pública); además es contenido de menores |
| "Se está actualizando el ranking de Menores… Normativa: 1- … máximo de 2 torneos autonómicos" | HTML dentro de `<% if a=5 then %>` | **No se migra** (nunca se mostraba; de menores) |
| Desplegables "Selecciona un género" (`----`/MASCULINO/FEMENINO) y "Selecciona una categoría" (`----`/CAT +35/+40/+45/+50/+55/+60) + "Indica un número de licencia" | HTML + JS (`rnkmen`, `rnkmen1`, `rnkmen2`) con dos `hidden` (`gen`, valor inicial `X`; `cat`, valor inicial `0`) | Formulario: selector segmentado de categoría de edad (6) y de género (2) + un campo "Nombre y apellidos o número de licencia" |
| Recuadro azul "El ranking único tiene un total de  jugadores y están sumando a este ranking  pruebas. Fecha de Actualización… Descarga del Ranking completo [PDF]" | `SFEP_Ranking_Tipos` (`Jugadores`, `Pruebas`, `FechaAct`, `ArchivoPDF`) con `IdFederacion=0 AND TipoRanking={tipo de la 1.ª fila}` | `ListaRankingDto.jugadores`, `pruebas`, `actualizado`, `documento`/`documentoUrl` (para **cada** lista). Hoy en veteranos `Jugadores`, `Pruebas` y `ArchivoPDF` están **vacíos**: solo se muestra la fecha |
| Tabla Categoría · Puesto · Jugador · Total Torneos · Total Puntos · Torneos Rnk · Puntos Rnk · Evolución | `CargarRnkVet.asp?cat=&genero=`: `SELECT … FROM SFEP_RankingFinal INNER JOIN SFEP_Fichas ON NumLicencia = LicenciaJugador WHERE IdFederacion=0 AND TipoRanking={rnk} ORDER BY Posicion` (**sin TOP**: hasta 145 filas). `rnk` = 10000 + edad (M) u 11000 + edad (F): 10035/11035 … 10060/11060 | `GET /api/v1/competicion/rankings/veteranos?categoria=&genero=&limite=` → `ClasificacionRankingDto` (una `ListaRankingDto` por categoría × género). La categoría va en el título de cada lista ("Ranking +45 femenino") en lugar de repetirse en cada fila |
| Búsqueda por licencia | En la página llamaba a **`CargarRnkMen.asp?id=`** (el de menores: busca en `TipoRanking BETWEEN 3999 AND 8999`). `CargarRnkVet.asp?id=` (nunca llamado desde la página) buscaba en `TipoRanking BETWEEN 10035 AND 11060` con `SFEP_Ranking_Tipos.Visible='S'` | El mismo endpoint con `licencia=` (sin género ni categoría: las 12 listas declaradas); una lista por categoría con resultados |
| Enlace de cada celda | `Estadisticas.asp?id={licencia}` | `PuestoRankingDto.estadisticasUrl` = `https://www.padelfederacion.es/Estadisticas?id={licencia}` (web actual hasta que se migre) |
| Nota "*Evolución: Numero de posiciones que sube o baja…" | HTML escrito a mano | Leyenda bajo la tabla |
| `linko` (`BENM01012026.pdf`, `ALEM…`, `JUNF…`) | Variable de `CargarRnkVet.asp` **nunca usada** (nombres de PDF de **menores** copiados) | Eliminada |

No hay normativa de cálculo: la página no la tenía. `/api/v1/competicion/rankings/veteranos/normativa` responde 404 con
`urlWebActual` y `ranking-veteranos.html` no tiene la sección `#calculo` (`ranking.js` solo la pide si existe).

## Lógica / visibilidad heredada

- **Cargadores cruzados (fallo grave del original)**: la página copiaba el JS de menores y solo cambió la función de la
  categoría. Resultado, comprobado en la web pública el 13/11/2026:
  - Elegir **género** llamaba a `CargarRnkMen.asp?genero=M&cat={cat}`: con la categoría sin elegir (`cat=0`) o con una
    de veteranos (`CAT45`), `rnk` queda vacío → **error 500** (la tabla no cambiaba).
  - Elegir **categoría** llamaba a `CargarRnkVet.asp?cat=CAT45&genero={gen}`: funciona solo si antes se eligió género.
    Si no, `gen` vale `X` y `CargarRnkVet.asp` asigna los `TipoRanking` **mixtos de menores** (8002, 7002, 6002, 5002,
    4002, copiados de `CargarRnkMen.asp`): +35 → 500 (8002 vacío); +45, +50, +55, +60 → **el ranking mixto de menores**
    (1.154–1.780 filas, datos de 2015) presentado como "CAT45", "CAT60"… en la página de veteranos.
  - La **licencia** llamaba a `CargarRnkMen.asp?id=` (menores): con una licencia de veteranos daba **500**
    (`?id=12762`).
  La API solo sirve los 12 `TipoRanking` de veteranos; género `X` y categorías de menores dan 400.
- **+60 femenino (11060)**: sin filas en `SFEP_RankingFinal` → `CargarRnkVet.asp` daba **500** (`rs_datos` sin
  asignar). Se declara igualmente (DECISIÓN FEP): la API devuelve la lista vacía y la web muestra "Todavía no hay
  jugadores ni jugadoras clasificados en este ranking"; aparecerá sola cuando se calcule.
- **Contenido oculto**: los dos bloques `<% if a=5 then %>` (de menores) nunca se pintaban (`a` sin asignar). Mismo
  patrón que la variable `a` de Órganos de Gobierno y de Ranking_Menores.
- **Datos vacíos en `SFEP_Ranking_Tipos`**: en las 11 listas con filas `Jugadores`, `Pruebas` y `ArchivoPDF` llegan
  vacíos ("un total de  jugadores… sumando  pruebas") y el botón PDF enlazaba a `refs/docs/` (**403**). La API los
  devuelve como `null` (`NULLIF`: si vinieran como cadena vacía `TRY_CONVERT` los habría convertido en 0) y la web no
  los muestra. `FechaAct` sí está: 07/10/2026 en todas salvo **+55 femenino: 01/07/2026**.
- **Fila oculta "CAT" con puesto 0**: `CargarRnkVet.asp?id=` (sin uso en la página) devuelve, además de la categoría
  del jugador, otra fila con categoría "CAT", puesto 0 y sin evolución (p. ej. licencias 6799 y 2401): es otro
  `TipoRanking` del rango 10035–11060 que no corresponde a ningún desplegable. **No se publica** (la API solo consulta
  los tipos declarados). SUPUESTO: es un tipo auxiliar o de trabajo.
- "----" en cualquiera de los desplegables: `TipoRanking=` vacío → 500. La API admite solo categorías y géneros
  declarados (400 en otro caso) y el front siempre pide una categoría y un género.
- La búsqueda por licencia de `CargarRnkVet.asp` exigía `SFEP_Ranking_Tipos.Visible='S'`; la de categoría no. La API no
  filtra por `Visible` (SUPUESTO: los 12 tipos están visibles; los 11 con filas se ven hoy en la web pública).
- `</td` sin cerrar en la última celda de cada fila (HTML inválido) e icono de evolución sin `alt`. Irrelevante.
- Las funciones `rnkabs*`, `MesAnterior`, `CargarEvento`, `MeGusta` y `busqueda` del `<head>` no se usaban.

## Seguridad

- **Inyección SQL**: `Numlicencia=" & id` sin comillas ni validación en `CargarRnkVet.asp` (y en `CargarRnkMen.asp`,
  al que llamaba la página). Ahora todo va parametrizado (`@Licencia` int, `@Tipos`, `@T0…`) con validación previa;
  `categoria` y `genero` se comprueban contra la lista blanca de `ReglasClasificaciones` (no llegan al SQL).
- **XSS**: nombres y cifras se escribían sin codificar. En la web nueva todo va por `textContent`/atributos y `urlSegura`.
- Se publicaba por accidente el ranking mixto de **menores** (nombres y licencias de menores de edad) en la página de
  veteranos. Ya no.

## Mejoras respecto al original

- Una consulta parametrizada por petición, columnas explícitas, `ROW_NUMBER` para limitar por lista.
- Al entrar se ve el ranking +35 masculino (antes una tabla vacía con "Selecciona un género y una categoría…" y un
  formulario que fallaba según el orden en que se usara).
- Categoría y género como selectores segmentados accesibles (`fieldset`/`legend`, foco visible); criterios en la URL
  (`?categoria=45&genero=F`, `?licencia=`, `?busqueda=`), enlazables.
- **Búsqueda por licencia que funciona** y **búsqueda por nombre** (nueva) en todas las categorías, sin distinguir
  acentos; los resultados se agrupan por categoría (un mismo jugador puede estar en varias: p. ej. BEGOÑA HIDALGO
  MENDIA en +35 y +55).
- Tabla accesible (`caption`, `th scope`) y en móvil una ficha por puesto, sin scroll horizontal.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "RANKING VETERANOS CIRCUITO FEP" | "Ranking de Veteranos." | Título en estilo de frase, como el resto; "Circuito FEP" en la entradilla |
| "CAT +35" … "CAT +60" (desplegable) y "CAT35" (columna Categoría) | "+35" … "+60"; título de la lista "Ranking +35 masculino" | Nombre habitual de las categorías de veteranos; la clave de la API es la edad (`35`) |
| "El ranking único tiene un total de  jugadores y están sumando a este ranking  pruebas" + PDF a `refs/docs/` | No se muestran jugadores, pruebas ni PDF; sí la fecha de actualización | Datos vacíos en la BD; enlace roto (403); además el texto se copió del absoluto (no es el "ranking único") |
| "*Evolución: Numero de posiciones…" | "Número de posiciones…" | Tilde |
| Columna "Categoría" repetida en cada fila | Categoría en el título de cada lista | Una lista = una categoría y un género |
| "MARIA  GARRIDO MUÑOZ", "MIGUEL ANGEL JIMENEZ NUÑEZ " | Sin espacios dobles ni de relleno | Como en el resto de rankings (`NombreJugador`) |
| `<title>Ranking Veteranos \| FEP</title>` | "Ranking de Veteranos · Federación Española de Pádel" | Coherencia con el `h1`; `meta description` propia |

No se han cambiado cifras ni puestos. Algunos nombres de la ficha traen un punto como segundo apellido ("MELISSA ADRIANA
CAPRA ."): se muestran tal cual (dato de `SFEP_Fichas`).

## Comprobación de enlaces (13/11/2026)

- `https://www.padelfederacion.es/Ranking_Veteranos` y `/Ranking_Veteranos.asp`: 200 (sin los avisos ocultos).
- `CargarRnkVet.asp?cat=CAT{35…60}&genero=M|F`: 200 en 11 listas (instantánea usada para el mock: +35 M 14 filas,
  +35 F 66, +40 M 87, +40 F 66, +45 M 69, +45 F 79, +50 M 145, +50 F 29, +55 M 89, +55 F 24, +60 M 77);
  `cat=CAT60&genero=F`: **500** (sin filas). `genero=X`: 500 en +35; 200 con el mixto de menores en +45…+60.
  `genero=0` y `cat=0`: **500**. `?id=1`: **500**. `?id=12762`: 200.
- `CargarRnkMen.asp?id=12762` (la búsqueda de la página): **500**. `CargarRnkMen.asp?genero=M&cat=0` (primer género
  elegido): **500**.
- PDF del ranking completo: `ArchivoPDF` vacío → `refs/docs/`: **403**. No hay PDF de veteranos.
- `Estadisticas?id=12762`, `Estadisticas.asp?id=2401`: 200.
- No hay imágenes propias de la página.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: `SFEP_Ranking_Tipos` de veteranos sin `Jugadores`, `Pruebas` ni `ArchivoPDF`. ¿Se rellenan (y se
  publica el PDF completo, como en menores)? La web los mostrará automáticamente.
- **DECISIÓN FEP**: +55 femenino actualizado el 01/07/2026 y el resto el 07/10/2026. ¿Es correcto o falta recalcularlo?
- **DECISIÓN FEP**: +60 femenino (11060) no tiene clasificación. Se publica la pestaña con "Todavía no hay jugadores ni
  jugadoras clasificados". ¿Se mantiene o se oculta hasta que haya datos?
- **DECISIÓN FEP**: al entrar se muestra +35 masculino (primer valor de cada desplegable del ASP), que es la lista más
  corta (14 puestos). ¿Otra por defecto?
- **DECISIÓN FEP**: se añade búsqueda por nombre (no existía) y la búsqueda por licencia ahora funciona (en el ASP
  consultaba el ranking de menores).
- **DECISIÓN FEP**: la tabla publica el número de licencia (en el ASP iba en el enlace, no como columna; en el absoluto
  ya se publicaba). Basta con quitar la columna en `ranking.js` si no se quiere.
- **DECISIÓN FEP**: la web muestra hasta 500 puestos por lista (100 al entrar, ampliables); antes se pintaban todos
  (máx. 145 hoy, así que no hay diferencia práctica).
- **SUPUESTO**: la fila "CAT" con puesto 0 que devuelve la búsqueda por licencia de `CargarRnkVet.asp` es un
  `TipoRanking` auxiliar que no debe publicarse.
- **SUPUESTO**: los 12 `TipoRanking` de veteranos tienen `Visible='S'` en `SFEP_Ranking_Tipos` (la API no filtra por
  `Visible`, igual que la consulta por categoría del ASP).
- **SUPUESTO**: tipos de columna y unicidad de `SFEP_Fichas.LicenciaJugador`, como en `Ranking_Absoluto.md`.
  `Jugadores`/`Pruebas` vacíos pueden ser `NULL` o cadena vacía: ambos llegan como `null` (un 0 también).
- **SUPUESTO**: ruta nueva `/ranking-veteranos` (no había fila en `redirecciones.csv`).

## Modelo SQL propuesto

No aplica: la clasificación ya está en BD y la página no tiene texto informativo propio. Si se quisiera declarar los
rankings en BD en lugar de en `ReglasClasificaciones.Rankings`, ver la propuesta de `Ranking_Absoluto.md`
(`ALTER TABLE SFEP_Ranking_Tipos ADD ClaveWeb varchar(40) NULL, Genero char(1) NULL;` — `Categoria` y `Visible` ya
existen; en veteranos `Categoria` vale "CAT35"…).

## Ficheros

- API: `Contracts/Competicion/ReglasClasificaciones.cs` (ranking `veteranos` con sus 12 `TipoRanking`; categorías
  numéricas → "+35"), `Data/Repositories/Competicion/ClasificacionesRankingRepository.cs` (`NULLIF` en los datos de
  `SFEP_Ranking_Tipos`; comentarios), comentarios en `Controllers/Competicion/ClasificacionesRankingController.cs` y
  `Program.cs`; tests en `tests/FEP.Api.Tests/ClasificacionesRankingTests.cs` (tipos, categorías, nombres "+35",
  validación, búsqueda por licencia y 404 de la normativa).
- Web: `wwwroot/ranking-veteranos.html`, `assets/js/ranking.js` (mensaje para una lista sin puestos; comentarios). Sin
  CSS nuevo (`.rkc-*`) ni imágenes propias.
- Mock: `mock/competicion-rankings-veteranos.json` (instantánea de la web pública del 13/11/2026: top 100 de cada una de
  las 12 listas sin filtrar, en el orden de la API; `coincidencias` = filas reales; +60 femenino vacía; jugadores,
  pruebas y PDF `null`) y `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`ranking_veteranos`), enlace "Veteranos · Por grupo de edad" de
  `index.html`, comentario de `rankings.js` (las tarjetas de `rankings.html` ya usan `PAGINAS_MIGRADAS`).
- Documentación: `docs/api/endpoints.md`, `docs/migracion/redirecciones.csv` (fila nueva),
  `docs/migracion/mapa-migracion.md`, fila de `tools/generar-mapa-web.ps1` (Desarrollado).
