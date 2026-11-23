# Camp_Parejas.asp / Camp_Parejas_Veteranos.asp → campeonato-parejas.html

Ficha de un campeonato por parejas (absoluto y veteranos). Origen: `X:\TICS\WEB\Camp_Parejas.asp`, que redirige según
el torneo: `Circuito='SILLA'` → `Camp_Silla.asp` (ya migrada, `campeonato-silla.html`), veteranos →
`Camp_Parejas_Veteranos.asp` (cuadros por género y categoría +35…+60), Copa/Inclusivo → sus propias páginas (no
migradas). Lee las mismas tablas que `Camp_Silla.asp` (ver `Camp_Silla.md`): `SFEP_Torneos`, `SFEP_Torneos_Desglose`,
`SFEP_Torneos_Adjuntos`, `SFEP_Torneos_Noticias` + `SFEP_Noticias`, `SFEP_Inscripciones`, `SFEP_Torneos_Arbitraje`,
`SFEP_Clubes`.

## Web

- `wwwroot/campeonato-parejas.html` (`<body data-modalidad="parejas">`) reutiliza `assets/js/campeonato-silla.js`: la
  modalidad cambia el endpoint, los textos (antetítulo, etiquetas, pasos de inscripción) y el enlace a la web actual.
  Las categorías de veteranos de la gestión (`Cat35`) se muestran como `+35`.
- `comun.js` (`paginaMigrada`): `Camp_Parejas.asp?IdTorneo=` y `Camp_Parejas_Veteranos.asp?IdTorneo=` →
  `campeonato-parejas.html?id=`; también `Camp_Equipos.asp?IdTorneo=` → `campeonato-equipos.html?id=` (antes no se
  enlazaba). Los de silla siguen yendo a `campeonato-silla.html` (`campeonatos.js` los resuelve antes).
- Sin API ni mock, la ficha muestra el aviso con enlace a la misma ficha en la web actual.

## API (pendiente)

`GET /api/v1/competicion/campeonatos-parejas/{id}` con la misma forma que `CampeonatoSillaDto` (sin cuadro de honor de
silla). Aún no existe: la web funciona con los mocks.

## Mocks: instantánea de la web pública

`tools/generar-mock-web-publica.ps1 -Torneos 28714,30120,…` descarga la web pública (no la BD) y genera:

- `mock/competicion-campeonatos-parejas-{id}.json` (ficha),
- `mock/competicion-cuadros-{id}.json` (índice) y `mock/competicion-cuadros-{id}-{G}-{Cat}-{Nivel}-{Cuadro}.json`
  (cada cuadro), convirtiendo la tabla de `Cuadro4…Cuadro128` / `CuadroPrevia*` / `CuadroPrePrevia*` (rejilla con
  `rowspan`: huecos `tablalineaabajo*`, `div.resultado`, `div.horario`, cabezas de serie en la columna C.S.).

Validado: el cuadro +50 masculino del 28714 que genera es idéntico al mock hecho a mano anteriormente.

Instantánea del 17/11/2026: 28714 (21 cuadros), 30120 (1), 28544 y 30197 (sin cuadros publicados), 23263 (absoluto:
final, previa y pre-previa de ambos géneros). Limitaciones (no se ven en la web pública): puntos de los cabezas de
serie, licencias, fecha/lugar estructurados del horario (va como texto en `obs`), liguillas (se omiten).
