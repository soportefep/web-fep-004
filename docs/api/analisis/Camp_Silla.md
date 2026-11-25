# Análisis: `Camp_Silla.asp` → `campeonato-silla.html` + `GET /api/v1/competicion/campeonatos-silla/{id}`

URL actual: `/Camp_Silla.asp?IdTorneo={id}` (ficha de un torneo de pádel en silla de ruedas por parejas). No se enlaza
directamente desde el listado: `Torneos_Previo.asp` manda los torneos por parejas a `Camp_Parejas.asp`, que redirige a
`Camp_Silla.asp` si `Circuito='SILLA'` (comprobado en `X:\TICS\WEB\Camp_Parejas.asp`). También llegan banners y eventos
destacados de la portada (`Camp_Silla.asp?IdTorneo=30180` y `31277`). `redirecciones.csv` no tiene fila para esta
página; la ruta nueva es `campeonato-silla.html?id={IdPrueba}`, que acepta también `?IdTorneo=` / `?idtorneo=`, y
`comun.js` traslada los enlaces antiguos.

`Camp_Silla.asp` es una copia de `Camp_Parejas.asp` adaptada a silla (licencias "PS", clasificación funcional, cuadro de
honor del ranking 15). Includes: `conexionSQL.inc` (no se copia), `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc`
(ya migrados en `comun.js`, + columna de patrocinadores), `finconexion.inc`. Codificación del `.asp`: Windows-1252.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, barra lateral, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (`activo=3`) | Ya migrado: `comun.js` |
| `sql_noticias` (`SELECT *` de `SFEP_Noticias` destacadas), `sql_adjuntos` y `sql_cuadros` (`SELECT *` de `SFEP_Torneos` repetido) | Se consultaban **y no se usaban** | Eliminados |
| `sql_edad` (`AnoMin`/`AnoMax` del desglose) | Solo para el desplegable de años del formulario | No se migra (el formulario sigue en la web actual) |
| Torneo | `sql_torneo`: `SELECT SFEP_Torneos.* WHERE IdPrueba=` + id (`limpiar()`) | Consulta con columnas explícitas (`SqlTorneo`) |
| Redirecciones | `IdTorneo=20255` → `Camp_Parejas_Copa.asp` (escrito a mano); `Circuito='INCLUSIVO'` → `Camp_Parejas_Inclusivo.asp`; `MENORES` → `PruebaEquipos_Cuadros.asp` (equipos) o `Prueba_CuadrosMenores.asp`; `VETERANOS` → `PruebaEquipos_Cuadros.asp` o `Camp_Parejas_Veteranos.asp` | `404` con `urlWebActual` = esa ficha (`ReglasCampeonatoSilla.RutaOtraFicha`) |
| Título `<h1>` y `<title>` | `Torneo` | `titulo` (espacios repetidos colapsados) → `h1` con punto final, miga y `<title>` |
| "Compartir en" Twitter / Facebook / Google+ | Enlaces por http a `Camp_Parejas.asp?Idtorneo=` y a Google+ (cerrado) | Botón "Compartir" (Web Share API o copiar enlace) |
| Bloque fijo del torneo 12578 | `if REQUEST("IDTORNEO")=12578`: "WEB ESPECÍFICA XXXIV CTO DE ESPAÑA PAREJAS", texto suelto "SSSS" e imagen enlazada a `campeonatodeespanadepadel.es` | **No se migra**: el 12578 es el "XXXIV Campeonato Absoluto de España" (no es de silla; solo salía entrando a mano con esa URL) y el dominio ya no resuelve |
| Marcadores on-line | `sql_arbitraje` (`SFEP_Torneos_Arbitraje`, `Borrado='N'`, `Visible='S'`, por `IdPartido`) → iframes de `ArbitrajeCtoFep.asp?Id={Aleatorio}` en filas de 2; "Aún no hay ningún partido activo" si no hay | `marcadores` (`codigo` + `url`) → sección "Marcadores en directo" (solo con el torneo en juego) |
| Nota de prensa | `NotaPrensa` si `VisibleNotaPrensa='S'` (HTML) | `notaPrensaHtml` (HTML saneado) |
| Cuadros y horarios | `VerCuadros='S'` + `select case ModalidadTorneo` (`ABSOLUTO`) + `sql_cuadrosM/F/X` (primera fila de cada género del desglose con `Vercuadros='S'`) + `if Ver…="S"` por cuadro; en mixto, un `sql_texto` (`SFEP_Torneos_Cuadros_Nombres`) por cuadro | `cuadrosPublicados` + `cuadros` (género → cuadros con `titulo`, `nivel`, `cuadro`, `tipo` y `url` a `Previo.asp`) |
| Cuadro de honor | `sql_honor`: `SFEP_RankingTorneos` ⨝ `SFEP_Fichas` (`LicenciaSilla = IdJugador`), `Ranking=15`, por `Puntos DESC`: `Ronda`, "PS"+`IdJugador`, `NOMBRECOMPLETO`, `Puntos` | `cuadroHonor` → tabla plegable con enlace a las estadísticas del jugador |
| Notificaciones | `Notificaciones` (HTML) + `sql_noti` (`SFEP_Torneos_Adjuntos` por `IdArchivo DESC`) → `http://padelfederacion.es/Paginas/Torneos/{Año}/{Archivo}` | `notificacionesHtml` + `adjuntos` (URL absoluta codificada) |
| Listado de inscritos | `sql_inscritos` (`SFEP_Inscripciones` por `Genero`, `Borrado='N'`) si el total ≠ 0, una columna por género visible (`sql_cat`) + columna "Coach" si `sql_coach` (inscripciones con `Coach<>0`) no está vacío, con el número de `sql_Numcoach` (entrenadores con licencia `T` tramitada en la temporada) → `Camp_Parejas_Inscritos.asp` | `inscritosParejas` + `entrenadores` → tabla |
| Formulario de inscripción | `FechaInscripcion > now()`: `InfoInscripcion`, fecha tope, `infFormulario` (condiciones), "Cómo realizar la inscripción" (texto fijo), jugador/a 1 y 2 (licencia "PS" con AJAX a `datonombreSilla.asp`, `datoape1Silla.asp`, `datoape2Silla.asp`; clasificación funcional 1–4; día/mes/año de nacimiento; DNI; móvil; email), observaciones (`ObsInscripcion`, `NumCaracteresObs`), entrenador (`datotecnico.asp`) → POST a `Camp_Silla_intro.asp` (**escribe en la BD**). Jugador/a 2 solo si `InscripcionSolo='N'` | `inscripcion` (`abierta`, `fechaLimite`, `individual`, `informacionHtml`, `condicionesHtml`, `formularioUrl`): plazo, condiciones, pasos y botón al formulario de la web actual |
| Información general | `FechaInicio`, `FechaFin` (`formatdatetime(…,1)`), categorías (`ABSOLUTO`: géneros de `sql_cat`; resto: `sql_catM/F/X`), cuota (`sql_cuota`, **solo la primera fila**), `Ciudad`, club (`sql_club`: `SELECT *` de `SFEP_Clubes` con `IdClub = '{Club}'`; "--" si no hay) + `Club2` | `fechaInicio`, `fechaFin`, `generos`, `cuotas`, `ciudad`, `sedes` → tarjetas de datos |
| Mapa y política de privacidad | Solo si el club tenía `latitud`: mapa de Google (`gMap`) y, dentro del mismo `if`, el texto completo de la política de privacidad | Enlace "Ver en el mapa" (OpenStreetMap) si hay coordenadas; la política, enlazada desde la inscripción |
| Información adicional | `InfoAdicional` si `VisibleCartel='S'` | `infoAdicionalHtml` (solo con `VisibleCartel='S'`, como el ASP) |
| Cartel | `FotoCartel` si `VisibleCartel='S'` | `cartel` + `cartelUrl` |
| Noticias relacionadas | `sql_notirel` (`SFEP_Torneos_Noticias` ⨝ `SFEP_Noticias`, `Site='FEP'`, `Visible='S'`, `Borrado='N'`, por `Fecha DESC`) | `noticias` (sin visitas) → tarjetas |

La página nueva **no escribe** en la BD. El formulario (`Camp_Silla_intro.asp`, que además envía correos) y sus
cargadores AJAX, los cuadros (`Previo.asp` → `Cuadro16.asp`…), los listados (`Camp_Parejas_Inscritos.asp`), los
marcadores (`ArbitrajeCtoFep.asp`) y las estadísticas (`Silla-Ruedas-Est`) siguen en la web actual (grupo
`inscripciones-campeonatos`, fases 3–4).

## Parámetros (`Request`) y validación

| ASP | API nueva | Validación |
|---|---|---|
| `idtorneo` / `IdTorneo` (`limpiar(request("idtorneo"))` en unas consultas y `request("idtorneo")` **sin limpiar** en las de nombres de cuadro y en los enlaces) | `id` en la ruta | Entero ≥ 1; si no, **400** `ValidationProblemDetails` (los no numéricos los rechaza el *model binding*) |

Respuestas: `200` con la ficha; `404` `ProblemDetails` con `urlWebActual` = listado de Campeonatos si no existe, está
borrado o `Visible='N'`; `404` con `urlWebActual` = la ficha que corresponde si existe pero no es de silla de ruedas por
parejas (redirecciones del ASP; los de silla por equipos van a `Camp_Equipos.asp`, ya migrada; los que no son de silla,
a la ficha de `Torneos_Previo.asp`).

## Lógica / visibilidad heredada

- **Qué es un torneo de silla**: `Circuito='SILLA'` (la condición de `Camp_Parejas.asp`), por parejas, sin `MENORES` ni
  `VETERANOS` (que el ASP redirigía) y distinto de 20255. Un `SEL_ABS` de silla se muestra aquí (el ASP lo pintaba sin cuadros).
- **Plazo de inscripción**: abierto si `FechaInscripcion > now()`; vacía = cerrado (hora local, mismo supuesto que P0112).
  La API lo calcula con `TimeProvider` y el front lo vuelve a comprobar con el reloj del navegador (la respuesta se cachea
  5 min). Con el plazo cerrado el ASP no mostraba nada de la inscripción: igual ahora.
- **Inscripción individual o por parejas**: el ASP pedía jugador/a 2 solo con `InscripcionSolo='N'`; con cualquier otro
  valor (también vacío) era individual. Se respeta en `individual`.
- **Cuadros**: solo con `VerCuadros='S'` en el torneo y `ModalidadTorneo='ABSOLUTO'`. Por género, la fila del desglose debe
  tener `VerCuadros='S'` y cada cuadro su columna `Ver…='S'`. Las tres listas no son iguales y se han copiado en
  `ReglasCampeonatoSilla.CuadrosMasculinos/Femeninos/Mixtos`:
  - Masculino: 1.ª (o "Cuadro final" si `Cat2 = 0`), consolación, 2.ª, 3.ª, previa (nivel 21), pre-previa (31), 4.ª–10.ª.
  - Femenino: lo mismo hasta 5.ª, grupos A y B, y 6.ª.
  - Mixto: cuadro final, 2.ª, consolación del cuadro final, consolación de 2.ª (7.º-8.º), 3.ª, consolación de 3.ª
    (13.º-16.º), previa, pre-previa, 4.ª, 5.ª y 6.ª.
  - Enlaces con `Cat=ABSOLUTO` fijo, salvo la consolación masculina y femenina, que llevaban el `Tipo` del desglose.
  - **Nombres personalizados** (`SFEP_Torneos_Cuadros_Nombres`): solo los buscaba el bloque mixto, siempre con
    `Categoria='ABSOLUTO'`. Ahora se leen todos de una vez y se resuelven en memoria (sin N+1). En mixto, la primera
    categoría se llamaba siempre "Cuadro Final" si no tenía nombre, aunque hubiera `Cat2` (el `if Cat2` solo cambiaba la imagen).
  - Primera fila por género: si hubiera varias, ahora se pinta un grupo por fila (mismo supuesto que P0110).
- **Cuadro de honor**: todas las filas de `SFEP_RankingTorneos` con `Ranking=15` del torneo cuyo `IdJugador` exista como
  `LicenciaSilla` en `SFEP_Fichas` (el `INNER JOIN` deja fuera a quien no tenga ficha), por puntos. Las parejas salen en
  dos filas seguidas con el mismo puesto y puntos: el front atenúa el puesto repetido.
- **Listado de inscritos**: solo si el total de parejas es ≠ 0. La columna de entrenadores salía si había alguna
  inscripción con `Coach<>0` (también borradas), aunque el número contara solo licencias `T` tramitadas en la temporada:
  en el 28412 se veía "0 Coach". Ahora la fila de entrenadores sale solo si el número es mayor que 0.
- **Marcadores en directo**: el ASP pintaba siempre el bloque, también en torneos terminados, y los marcadores que se
  quedaban visibles daban error (28658, junio de 2026: `ArbitrajeCtoFep.asp?Id=2865883902` → 500). Ahora la sección sale
  solo hasta el día siguiente al último día del torneo, con "Aún no hay ningún partido activo" si no hay ninguno.
- **Información adicional**: en `Camp_Silla.asp` (a diferencia de `Camp_Equipos.asp`) solo salía con `VisibleCartel='S'`; se respeta.
- `Borrado` no se comprobaba en el torneo; ahora un torneo con `Borrado='S'` responde 404 (mismo criterio que P0104).
- `Visible` **tampoco** se comprobaba (`Camp_Equipos.asp` y el listado sí): ahora `Visible='N'` responde 404.
- Funciones JS `busqueda`, `MesAnterior`, `CargarEvento`, `MeGusta`, `cambiar`, jQuery 1.10, Google Maps con
  `sensor=false` y `custom.js`: no se usan en la página nueva.

## Problemas del original

1. **Inyección SQL**: `id` se concatenaba en ~25 consultas; `limpiar()` (lista negra) solo en las que usaban `id`, y
   `Request("idtorneo")` **sin limpiar** en las 9 `sql_texto` de los nombres de cuadro y en los enlaces del HTML
   (`Previo.asp?IdTorneo=<%=request("IdTorneo")%>`, `<input name="idtorneo" value=…>`: XSS reflejado). `sql_Numcoach`
   concatenaba además `Año` entre comillas y `sql_club` el `Club`. Ahora `@Id`, `@AnioLicencia` y `@Ranking` como
   parámetros y las columnas `Ver…` salen de constantes.
2. **Error 500** con un id inexistente (comprobado: `Camp_Silla.asp?IdTorneo=99999999` → 500), porque se leía
   `rs_TORNEO("Circuito")` sin comprobar EOF. Igual con `rs_cuota("Cuota")` y `rs_edad("AnoMin")` si no hay desglose.
   Ahora 404 / sin cuota.
3. **Sin control de tipo**: cualquier id se pintaba como torneo de silla (p. ej. `Camp_Silla.asp?IdTorneo=28676`, por
   equipos, se pinta como torneo de silla). Ahora 404 con la ficha que corresponde.
4. **N+1**: hasta 9 consultas a `SFEP_Torneos_Cuadros_Nombres` (una por cuadro mixto), más `sql_club`. Ahora: 1
   consulta del torneo + 1 lote de 11 consultas.
5. **XSS**: `Torneo`, `Ciudad`, `Club`, `Club2`, `Link` de adjuntos, títulos de noticias, nombres de jugadores y de
   cuadro se pintaban sin codificar; el nombre del club se metía en un `<script>` (`html: '<h3><%=club%></h3'`, mal
   cerrado). El front usa `textContent`; el HTML de la gestión (nota de prensa, notificaciones, condiciones,
   información) se sanea con lista blanca (DOMParser, sin `innerHTML`).
6. **Título del grupo mixto**: el bloque mixto se titulaba "Femeninos" (copiado del femenino). Ahora "Mixto".
7. **Pre-previa mixta sin texto propio**: pintaba el nombre del cuadro anterior (variable `texto` reutilizada).
8. **Web del club rota**: sin web se enlazaba `http://` (29030: "CD.ADAPTA-T TU"); con la web guardada con esquema
   salía `http://http://…`. Se normaliza con `CampeonatoEquiposRepository.WebClub`.
9. **"Club: --"**: si `Club` era 0 o no estaba en `SFEP_Clubes` se pintaba "--" (y "-- y POLIDEPORTIVO…" con `Club2`).
10. **Política de privacidad condicionada al mapa**: el texto legal del formulario estaba dentro del `if` del mapa del
    club; en los torneos sin coordenadas (todos los consultados) no se mostraba.
11. **Compartir**: enlaces por http a `Camp_Parejas.asp` (que redirige) y a Google+ (cerrado).
12. HTML: `<meta charset=iso-8859-1>` tras los scripts, `<script>` fuera de `<head>`, `<div align="prl-span-10…">`,
    `<h4>` sin `<h2>`/`<h3>`, tablas de maquetación, iframes sin `title`, imágenes de cuadro sin `alt` (el texto del
    cuadro iba fuera del enlace), validación del formulario con `alert()` y erratas ("nacimeinto", "Comunidad
    Autonóma", "realizar las inscripción").

## Mejoras respecto al original

- Cabecera con el nombre, fechas ("Del viernes 27 al domingo 29 de noviembre de 2026"), sede, etiquetas (parejas, silla de
  ruedas, categoría) y cifras (fechas, parejas inscritas, número de cuadros); botón "Inscribirse" con el plazo abierto.
- Índice lateral solo con las secciones que tienen contenido (chips en móvil) y anclas (`#cuadros`, `#cuadro-honor`…).
- Cuadros como tarjetas enlazadas con su tipo (cuadro, consolación, grupo, previa) en lugar de imágenes sin texto alternativo.
- Cuadro de honor con enlace a las estadísticas de cada jugador/a, parejas agrupadas visualmente y plegado a partir de 10 filas.
- Inscripción resumida: plazo, tipo (individual o por parejas), condiciones, pasos del formulario y botón; la política
  de privacidad, siempre enlazada.
- Sede con enlace a la web del club y al mapa (OpenStreetMap, sin cargar la API de Google Maps).
- Respuesta cacheada 5 min; una sola ida y vuelta para el detalle.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| Título del grupo mixto: "Femeninos" | "Mixto" | Errata (bloque copiado del femenino) |
| Textos fijos de cuadro: "Cuadro Final", "Consolación Cuadro Final", "7º-8º Puesto", "13º-16º Puesto", "2ª Categoría", "4ª Categoria", "Cuadro de Pre-Previa" | "Cuadro final", "Consolación del cuadro final", "Puestos 7.º y 8.º", "Puestos 13.º a 16.º", "Segunda categoría", "Cuarta categoría", "Cuadro de pre-previa" | Uniformidad con Camp_Equipos (mismo criterio que P0106); los nombres personalizados de la BD ("9º al 12º Puesto") se muestran tal cual |
| Pre-previa mixta con el nombre del cuadro anterior | "Cuadro de pre-previa" | Errata del ASP (variable reutilizada) |
| "Club: --" y "-- y POLIDEPORTIVO MUNICIPAL CIUDAD JARDÍN" | Solo la sede conocida | Club sin ficha en `SFEP_Clubes` |
| "0 Coach" (28412) | Sin fila de entrenadores | **DECISIÓN FEP**: el ASP mostraba la columna si había alguna inscripción con entrenador, aunque ninguno tuviera la licencia de técnico tramitada |
| Cuota: solo la mayor | Todas las cuotas distintas | Mismo criterio que P0100 |
| Visitas de las noticias relacionadas | No se muestran | Mismo criterio que P0101 |
| Nombres con espacios finales ("JAVIER NAVARRO FERNANDEZ ") | Recortados | Limpieza |
| "Cómo realizar la inscripción": "arrastra el resto de datos… Comunidad Autonóma… poder realizar las inscripción" | Tres pasos (licencia de silla, datos para verificar, entrenador opcional) con enlaces a Contacto y a "Buscar mi número de licencia" | **DECISIÓN FEP**: texto reescrito en lenguaje claro; el formulario de la web actual mantiene el original |
| Texto completo de la política de privacidad (solo con mapa) | Enlace a `Politica_Privacidad` bajo el botón del formulario | **DECISIÓN FEP**: el texto legal acompaña al formulario, que sigue en la web actual |

## Comprobación de enlaces (16/11/2026)

- Fichas públicas descargadas: 31277, 30180, 29030, 28658, 28412 → 200. `IdTorneo=99999999` → **500**.
  `Camp_Silla.asp?IdTorneo=28676` (por equipos) → 200: se pinta como si fuera un torneo de silla (sin control de tipo).
- `Previo.asp?IdTorneo=28412&Genero=X&Nivel=2&Cat=ABSOLUTO&Cuadro=Final` → 302 a `Cuadro16.asp?…` → 200.
- `Camp_Parejas_Inscritos.asp?tipo=X&id=31277` y `?tipo=C&id=29030` → 200. `Silla-Ruedas-Est?id=PS1008` → 200.
- `ArbitrajeCtoFep.asp?Id=2865883902` (marcador aún visible del 28658, junio de 2026) → **500**.
- `http://padelfederacion.es/Contacto` y `/Licencias_Buscar` → 301 a https → 200. `Politica_Privacidad` → 200.
- `refs/Noticias/wEBcTOeSPAIN.jpg` (bloque del 12578) → 200, pero `www.campeonatodeespanadepadel.es` **no resuelve**.
- `http://www.padelariumgava.com` (sede del 28412) → 200 (https).

## Modo prototipo (mocks)

`mock/competicion-campeonatos-silla-{28412,28658,29030,30180,31277}.json`: instantánea de la web pública del 16/11/2026
(ficha de cada torneo + fechas de `competicion-campeonatos.json`), con la misma forma que el DTO. No hay contenido de
`Contenidos/` que regenerar, así que no hay script `generar-mock-*`. Limitaciones:

- 31277 tiene la inscripción abierta hasta el 20/11/2026 12:00 (después, el front la oculta solo); los demás, cerrada.
  La web no muestra `InfoInscripcion` en ninguno ni `infFormulario` en los cerrados.
- Sin coordenadas de club (la web no pintaba mapa en ninguno), sin nota de prensa, notificaciones, cartel ni noticias.
- 28658 conserva el marcador que la web aún muestra; como el torneo terminó, el front no lo pinta.
- Los títulos de cuadro por defecto se han pasado a los textos nuevos; los personalizados (28412) se conservan.
- Los demás torneos de silla del listado enlazan a la página nueva, pero sin mock muestran el aviso con enlace a la web
  actual (con la API funcionan todos).

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: los torneos con `Visible='N'` se veían en `Camp_Silla.asp` (no lo comprobaba); ahora responden 404,
  como en la ficha por equipos y en el listado.
- **DECISIÓN FEP**: la sección de marcadores en directo solo se muestra mientras dura el torneo (hasta el día siguiente
  al último día); los marcadores que se quedan visibles en torneos terminados daban error 500. ¿Se deberían ocultar
  también en la gestión (`SFEP_Torneos_Arbitraje.Visible='N'`) al cerrar el torneo?
- **DECISIÓN FEP**: los puestos del cuadro de honor se muestran tal cual se escriben en la gestión (`Ronda`), con
  formatos distintos entre torneos ("Tercero" / "3º Clasificado", "Sexta", "Septimo", "11avo"). ¿Se unifican en la
  gestión o se calcula el puesto a partir de los puntos?
- **DECISIÓN FEP**: la cuota de todos los opens de silla consultados es "0 euros". Se muestra "0 €": ¿la inscripción es
  gratuita o la cuota no se informa en la gestión (y habría que ocultarla)?
- **SUPUESTO**: ruta nueva `campeonato-silla.html?id=` (no hay fila en `redirecciones.csv`; falta añadir la 301
  `/Camp_Silla.asp?IdTorneo={id}` → `/campeonato-silla.html?id={id}`).
- **SUPUESTO**: tipos de las columnas nuevas respecto a Camp_Equipos: `SFEP_RankingTorneos.IdJugador` y
  `SFEP_Fichas.LicenciaSilla` numéricos y comparables; `Puntos` numérico (`TRY_CONVERT`); `Ronda` texto;
  `SFEP_Licencia.[Año]` texto o número (el ASP lo comparaba con `'2026'`); `InscripcionSolo` `char(1)`; `infFormulario` HTML.
  El resto, como en Camp_Equipos (mismo supuesto que P0111).
- **SUPUESTO**: en el cuadro de honor las dos personas de una pareja tienen el mismo `Ronda` y los mismos puntos y salen
  seguidas (así en los cuatro torneos consultados); el front solo atenúa el puesto repetido, no agrupa los datos.
- Las páginas enlazadas (formulario y `Camp_Silla_intro.asp`, cuadros, listados de inscritos, marcadores, estadísticas)
  siguen en la web actual.
- `calendario-fep.js` y `programa-impulso.js` siguen enlazando los torneos de silla a `Camp_Parejas.asp` (que redirige a
  la ficha antigua); `campeonatos.js` ya enlaza a `campeonato-silla.html`.

## Modelo SQL propuesto

No hay contenido escrito a mano que conservar (el bloque del 12578 no era de silla; la excepción 20255 es una regla de
`ReglasCampeonatoSilla`). Para no depender de textos libres en el cuadro de honor, sin cambiar el contrato:

```sql
-- Puesto numérico en la clasificación del torneo (el texto Ronda se mantiene para lo que ya existe)
ALTER TABLE SFEP_RankingTorneos ADD Puesto int NULL;   -- 1 = campeones, 2 = finalistas…
```

Con `Puesto` informado, la API podría generar el texto ("Campeones", "3.º puesto"…) y ordenar por él en lugar de por
puntos. Índices recomendados para el detalle (además de los de Camp_Equipos): `SFEP_RankingTorneos (IdPrueba, Ranking)`,
`SFEP_Inscripciones (IdPrueba, Borrado)`, `SFEP_Licencia (IdLicencia, Tipo)` (**SUPUESTO**: no se ha podido ver el esquema real).

## Ficheros

- API: `Contracts/Competicion/CampeonatoSillaDto.cs` (DTO, `PuestoHonorDto`, `EntrenadoresInscritosDto`,
  `InscripcionSillaDto`, `ConsultaCampeonatoSilla`; reutiliza los sub-DTO de `CampeonatoEquiposDto.cs`),
  `Contracts/Competicion/ReglasCampeonatoSilla.cs` (redirecciones, cuadros por género, inscripción individual),
  `Data/Repositories/Competicion/CampeonatoSillaRepository.cs` (Dapper; reutiliza `ReglasCampeonatoEquipos` y
  `CampeonatoEquiposRepository.WebClub/Espacios`), `Controllers/Competicion/CampeonatosSillaController.cs`; registro en
  `Program.cs`; tests en `tests/FEP.Api.Tests/CampeonatosSillaTests.cs` (validación 400, 404 con `urlWebActual`, hora
  local con repositorio falso, redirecciones y reglas de cuadros; no necesitan SQL Server).
- Web: `wwwroot/campeonato-silla.html`, `assets/js/campeonato-silla.js` (saneado de HTML por lista blanca, copia del de
  `campeonato-equipos.js`), estilos `.cps-*` en la sección "Páginas interiores" de `assets/css/fep.css` (y su bloque
  responsive); reutiliza `.cpe-*`, `.ms-rich`, `.doc-list`, `.fedf-contact`, `.org-table`, `.org-figure`, `.camp-item__plazo`.
- Mock: `mock/competicion-campeonatos-silla-{28412,28658,29030,30180,31277}.json` + `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS` y `QUERY_MIGRADA` (`camp_silla`, `idtorneo` → `id`) en `assets/js/comun.js`;
  `campeonatos.js` enlaza los torneos de silla por parejas a la ficha nueva.
