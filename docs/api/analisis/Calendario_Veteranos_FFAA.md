# Análisis: `Calendario_Veteranos_FFAA.asp` → `calendario-veteranos-ffaa.html` + `GET /api/v1/competicion/calendarios-autonomicos/veteranos`

URL actual: `/Calendario_Veteranos_FFAA` (menú Competición → "Calendario Veteranos FFAA"). Ruta nueva añadida a
`docs/migracion/redirecciones.csv` (no tenía fila): `/calendario-veteranos-ffaa`.

Página gemela de `Calendario_Menores_FFAA.asp`: se reutiliza **toda** la infraestructura de aquella migración
(ver `docs/api/analisis/Calendario_Menores_FFAA.md`): mismo endpoint, DTO, repositorio, controlador,
`calendario-autonomico.js`, estilos `cal-*` y generador de mocks. Lo propio de Veteranos es solo el contenido
(`Contenidos/calendarios-autonomicos/veteranos.json`) y la página HTML.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, barra lateral, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` | Ya migrado: `comun.js` (+ columna de patrocinadores de las páginas interiores) |
| `sql_noticias` (`SFEP_Noticias`) | Se consultaba **pero no se usaba** | Eliminado |
| `sql_ano` (`SFEP_Calendario`, años con `Federacion=0`) y parámetro `ano` | Se calculaba el año **pero no se pintaba** | Eliminado (la temporada va en el contenido) |
| `sql_archivo` (`SFEP_Calendario_Archivo`) | 3 enlaces a `refs/docs/` **que no se pintaban** | Eliminado |
| `sql_num` (`SFEP_Calendario` ⨝ `SFEP_Calendario_Categorias`, parámetros `ano` y `fed`) | Contaba pruebas **sin usar el resultado** | Eliminado |
| `sql_fed` (`SFEP_Federaciones` 1–21) | Se consultaba **pero no se usaba** | Eliminado |
| Título "calendario veteranos federaciones territoriales 2026" | `<h5>` escrito a mano | `titulo` + `temporada` |
| Texto del RTG (hasta 4 pruebas, puntos 3.9.1.1/3.9.1.3) y condiciones de participación | HTML escrito a mano | `introduccion`, `participacion` (`actualizacionesRanking = null`: Veteranos no fija "momentos") |
| Leyenda "* En Verde pruebas incluidas actualmente en el ranking de Veteranos" | `<span class="verde">` | `leyenda` + marca `enRanking` por prueba |
| Pruebas de 17 federaciones (19 pruebas; 8 federaciones vacías) | Tabla HTML escrita a mano, una fila por federación con `<p>`/`<ol>`/`<li>`/`<blockquote>`; "en verde" = clase `creativ-shortcode-alertbox-colour-theme` | `federaciones[].pruebas[]` estructuradas; vacías → `pruebas: []` |
| Una `s` suelta tras `</table>` | Resto de edición | Eliminada |

**El contenido no sale de la BD** (igual que en Menores): las cinco consultas propias no se pintaban.

Resumen del contenido migrado: 17 federaciones (en el orden del original), 19 pruebas, 11 `enRanking` (las 12 marcas
verdes del HTML: en Aragón la prueba 2 estaba partida en dos `span`), 9 `campeonatoAutonomico`. Federaciones sin
pruebas (`<blockquote>&nbsp;</blockquote>` o listas vacías): Cantabria, Castilla-La Mancha, Región de Murcia,
Baleares, Principado de Asturias, Galicia, Melilla y País Vasco. El front muestra para ellas "Esta federación no ha
incluido pruebas en el calendario de la temporada." y el resumen de cabecera cuenta solo las 9 federaciones con pruebas.

## Lógica heredada y problemas del original

- **Inyección SQL**: `sql_num` concatenaba `ano` y `fed` (sin comillas) de `Request(...)`, filtrados solo con
  `limpiar()`. `sql_archivo` leía la primera fila sin comprobar `EOF`. Todo para datos que no se pintaban: desaparece.
- Seis consultas por visita (cinco propias + `Menus.inc`) sin ningún dato de BD en la página.
- HTML inválido (`<ol><blockquote><ol><ol><li>`), numeración mezclada (unas pruebas numeradas a mano, otras con
  `<li>`, la primera de Aragón sin número) y líneas partidas con `<br>` en mitad de los nombres.
- La única forma de saber si una prueba puntuaba era el color de fondo verde: no accesible.
- El `<head>` declaraba `iso-8859-1` y la fecha de la cabecera era la del servidor (ya cubierto por `comun.js`).

## Mejoras respecto al original

Las mismas que en Menores (vistas por federación y por fecha, índice lateral, resumen de temporada, etiquetas de
texto "En el ranking" / "Campeonato autonómico", fechas reales `fechaInicio`/`fechaFin`, contenido cacheable sin BD).
Además, la nota "Más competición" de ambas páginas enlaza ahora entre sí (Menores ↔ Veteranos) dentro de la web nueva.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| `<title>` "Calendario Veteranos FFAA" | "Calendario de veteranos de las federaciones territoriales" | Coherencia con Menores |
| "punto 3.9.1.1. del RTG", "3.9.1.3. del RTG" | "punto 3.9.1.1 del RTG", "3.9.1.3 del RTG" | Punto final duplicado |
| Canarias: "CAMPEONATO DE CANARIAS DE VETERNAOS 2026." | "CAMPEONATO DE CANARIAS DE VETERANOS 2026" | Errata |
| Andalucía: "CAMPEONATO DE ANDALUCIA DE VETERANOS" | "CAMPEONATO DE ANDALUCÍA DE VETERANOS" | Tilde |
| Aragón: "De 13 AL 19 de abril" | "Del 13 al 19 de abril" | Formato homogéneo |
| Aragón: "VETERANOS + 35 + 45 + 55" | "VETERANOS +35 +45 +55" | Espaciado de categorías de edad |
| Navarra: "Del 11 al 13 diciembre" | "Del 11 al 13 de diciembre" | Falta "de" |
| Navarra: "(upna)" | "(UPNA)" | Sigla de la Universidad Pública de Navarra |
| Extremadura: "28 al 29 de noviembre" | "Del 28 al 29 de noviembre" | Formato homogéneo |
| Cabeceras "federación Andaluza de Pádel", "Federación Catalana de Pádel", "…Comunidad Valenciana", "…de las Illes Balears", "…Castilla la Mancha" | Nombres oficiales de `Contenidos/federaciones-autonomicas.json` (igual que en `menores.json`) | Coherencia con la página de federaciones |
| `2.000€ en premios` (Canarias) | "2.000 € en premios" en el texto de la sede | El DTO no tiene campo de observaciones (**SUPUESTO**, ver abajo) |
| `s` suelta tras la tabla | Eliminada | Resto de edición |

Los nombres de pruebas y sedes se mantienen como los escribió cada federación (mayúsculas incluidas). La separación
de cada línea en `nombre` y `sede` es interpretación de la migración (**SUPUESTO**): p. ej. Aragón "REGAL PADEL CLUB
CIRCUITO DE VETERANOS 2" → sede "REGAL PADEL CLUB" + nombre "CIRCUITO DE VETERANOS 2"; Castilla y León "VALLADOLID
PUNTUABLE VETERANOS FEP. CAMPEONATO CASTILLA Y LEÓN" → sede "VALLADOLID"; Extremadura "BADAJOZ." → sede.

**Pendiente de confirmar por la FEP**:
- **DECISIÓN FEP — pruebas "en verde"**: el `.asp` recibido (07/10/2026) y la web pública consultada el 13/11/2026
  coinciden exactamente (12 marcas verdes). Se ha migrado ese estado: 11 pruebas `enRanking`. Las no verdes son las
  de otoño (Aragón 3, Cataluña, Castilla y León 2, C. Valenciana, Extremadura 2, Madrid 4, Navarra 1 y 2): algunas ya
  se han disputado (Madrid 4, C. Valenciana, Cataluña) y siguen sin verde. ¿Hay que actualizar el estado? El contenido
  tiene `actualizado = 2026-10-07` (fecha del `.asp`).
- **DECISIÓN FEP**: significado de "VET 2000 35*" y "VET 1600 30*" (Andalucía). Parecen categoría/puntos del torneo
  y el asterisco una nota que no aparece en la página; se mantienen tal cual dentro del nombre.
- **SUPUESTO**: `campeonatoAutonomico` marcado en las 9 pruebas cuyo nombre indica campeonato autonómico/regional
  (Andalucía 1, Aragón 2, Canarias 1, Cataluña 1, Castilla y León 2, C. Valenciana 1, Extremadura 2, Madrid 4,
  Navarra 1 "Campeonato Navarro por Parejas"). El "Máster Navarro" no se ha marcado.
- **SUPUESTO**: Comunidad Valenciana "23 de octubre" se ha modelado como prueba de un día (2026-10-23).
- **SUPUESTO**: "2.000 € en premios" (Canarias) se ha dejado en el texto de la sede para no perder el dato sin
  ampliar el contrato común. Si la FEP quiere publicar premios u observaciones, se añadiría un campo opcional
  `observaciones` a `PruebaCalendarioDto` (compatible con Menores).
- **SUPUESTO**: Madrid "Sede sin determinar" (pruebas 3 y 4) se deja como texto de sede, como "Sede por confirmar" en Menores.
- **DECISIÓN FEP**: Veteranos no fija momentos de incorporación al ranking (Menores sí, punto 3.10.9): no se muestra
  ese bloque. Si el RTG los tiene para veteranos, basta con rellenar `actualizacionesRanking` en el JSON.
- **DECISIÓN FEP**: la página se actualiza a mano cada temporada; ahora se edita `veteranos.json` y se regenera el mock.

## Modelo SQL propuesto

El mismo de `Calendario_Menores_FFAA.md` (`SFEP_CalendarioFFAA` con `Categoria = 'veteranos'`,
`SFEP_CalendarioFFAA_Momentos` vacía para veteranos, `SFEP_CalendarioFFAA_Pruebas`, `SFEP_CalendarioFFAA_Fases`). Si
se acepta el campo de observaciones, añadir `Observaciones nvarchar(300) NULL` a `SFEP_CalendarioFFAA_Pruebas`.

## Verificación

- El bloque de contenido del `.asp` y el de la web pública (13/11/2026) son idénticos (comparación normalizando espacios).
- No hay PDF ni imágenes propias en la página.
- Enlaces a la web actual comprobados con `HEAD` (13/11/2026): `/Calendario_Veteranos_FFAA` y `/Rankings.asp` → 200.
- IDs usados por `calendario-autonomico.js` presentes en el HTML; assets locales existentes (salvo `accesibilidad.html`
  del pie común, que falta en todas las páginas migradas).
- Mocks regenerados y validados con `ConvertFrom-Json`.
- `dotnet` no está instalado en este equipo: no se ha compilado ni ejecutado `dotnet test`.

## Ficheros

- API: `Contenidos/calendarios-autonomicos/veteranos.json` (nuevo). Reutilizados sin cambios de código:
  `Contracts/Competicion/CalendarioAutonomicoDto.cs`, `Data/Repositories/Competicion/CalendariosAutonomicosRepository.cs`,
  `Controllers/Competicion/CalendariosAutonomicosController.cs` (solo comentarios actualizados); comentario en `Program.cs`.
- Tests: `tests/FEP.Api.Tests/CalendariosAutonomicosTests.cs` (tests de veteranos; el 404 se prueba ahora con un
  repositorio vacío porque las dos categorías tienen contenido).
- Web: `wwwroot/calendario-veteranos-ffaa.html` (copia de la de Menores con `data-categoria="veteranos"`); enlace a
  Veteranos en `calendario-menores-ffaa.html`; `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`calendario_veteranos_ffaa`).
- Mock: `mock/competicion-calendarios-autonomicos-veteranos.json` (generado con
  `tools/generar-mock-calendarios-autonomicos.ps1`) y `mock/mocks.js`.
