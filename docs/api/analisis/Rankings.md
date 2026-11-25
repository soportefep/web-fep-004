# Análisis: `Rankings.asp` → `rankings.html` + `GET /api/v1/competicion/rankings`

URL actual: `/Rankings` (regla `^Rankings` de `web.config` → `Rankings.asp`; es también el enlace "Rankings" del
menú de la BD). Ruta nueva propuesta en `redirecciones.csv`: `/rankings` → `rankings.html`.

## Qué hacía la página ASP

| Bloque | Origen en el ASP | Destino |
|---|---|---|
| Cabecera, menú, redes, pie | `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc` (consultas `SFEP_Web_Menu`, `SFEP_Web_RS`, banners…) | Ya migrado: `comun.js` + `/api/v1/menu`, `/api/v1/redes-sociales`, `/api/v1/patrocinadores` (columna derecha) |
| `sql_noticias` (`SFEP_Noticias`, destacadas) | Se consultaba **pero no se usaba** en la página | Eliminado |
| Título "Rankings" (`h5`) | HTML escrito a mano | `h1` "Rankings." + entradilla |
| Rejilla de rankings (3 por fila) | `sql_galeria`: `SELECT SFEP_Rankings_Web.* … WHERE Visible='S' ORDER BY Orden`. Pinta `Link` (enlace, `target="_parent"`), `Foto` (imagen `refs/images/{Foto}`), `Texto` (título y `alt`) | `GET /api/v1/competicion/rankings` → `RankingWebDto[]` (`clave`, `titulo`, `enlace`, `enlaceUrl`, `nuevaVentana`, `imagen`, `imagenUrl`, `orden`) |

Contenido de la tabla en la web pública (13/11/2026), en este orden:

| Texto | Link | Foto |
|---|---|---|
| Ranking Único FEP | `Ranking_Absoluto` | `RnkAbs.jpg` |
| Ranking Menores | `Ranking_Menores` | `RnkMenores.jpg` |
| Ranking Equipos Asbolutos | `Ranking_Equipos_Absolutos` | `RnkEquiposAbs.jpg` |
| Ranking Silla Ruedas | `Ranking_Silla_Ruedas` | `RnkSilla.jpg` |
| Ranking Veteranos | `Ranking_Veteranos` | `RnkVet.jpg` |

`SFEP_Rankings_Web` **solo la usa `Rankings.asp`** (búsqueda en todo `X:\TICS\WEB`): no hay página de gestión
que la edite; se mantiene directamente en la BD.

## Lógica / visibilidad heredada

- Único filtro: `Visible = 'S'` (se mantiene). No filtra `Borrado` (no se sabe si la columna existe: no se añade).
- El recordset se recorría dos veces: la primera solo para contar (`numregistros`), valor que **no se usaba**.
- `activo=58` marca como activa la opción de menú `IdOpcion = 58` (plantilla; la web nueva no lo usa).
- Las funciones JS `MesAnterior`, `CargarEvento`, `MeGusta`, `cambiar` y `busqueda` del `<head>` no se usan.
- El `clearfix` cada 3 tarjetas solo llegaba hasta la 18 (`i=3 or … or i=18`); irrelevante con 5.
- Sin parámetros de `Request(...)` y sin escritura en BD: **no hay inyección SQL** en esta página.
- `Texto` y `Link` se escribían sin codificar (`<%=rs_galeria("Texto")%>` también dentro de `alt="…"`): XSS
  almacenado si alguien guardara HTML en la tabla. En la web nueva todo va por `textContent`/atributos y
  `urlSegura` (solo http/https).

## Mejoras respecto al original

- SQL con columnas explícitas (`Texto`, `Link`, `Foto`, `Orden`) y una sola pasada; desempate por `Texto` para
  que el orden sea estable si dos filas tienen el mismo `Orden`.
- Título de la pestaña: el ASP decía **"Archivos Multimedia | FEP"** (copiado de otra plantilla) → "Rankings ·
  Federación Española de Pádel", con `meta description` propia.
- Las imágenes (228×169, fondo negro con el texto "Ranking Único Absoluto"…) solo repiten el título: ahora son
  decorativas (`alt=""`) y el título va como texto (`h3`) en la tarjeta. Toda la tarjeta es un único enlace con
  foco visible; índice lateral con un enlace por ranking (anclas `#ranking-{clave}`).
- La API devuelve la ruta original de la imagen (`images/RnkAbs.jpg`) y su URL absoluta (`Media:BaseUrl`), y el
  enlace original (`Ranking_Absoluto`) y su URL absoluta en la web actual.
- `clave` estable (`absoluto`, `menores`, `equipos-absolutos`, `silla-ruedas`, `veteranos`) derivada del enlace,
  para anclas y para elegir la página nueva de cada ranking.
- Móvil: dos tarjetas por fila sin scroll horizontal; columna de patrocinadores como en el resto de páginas
  interiores; nota con enlaces al calendario y a los campeonatos (ya migrados).
- Enlaces a "Rankings" de todas las páginas migradas (pie, accesos rápidos y panel de ranking de la portada,
  notas de campeonatos/calendarios) apuntan ya a `rankings.html`; `PAGINAS_MIGRADAS` traslada el enlace
  "Rankings" del menú de la BD.

## Enlaces a cada ranking (páginas aún no migradas)

Las cinco páginas de destino (`Ranking_Absoluto.asp`, `Ranking_Menores.asp`, `Ranking_Equipos_Absolutos.asp`,
`Ranking_Silla_Ruedas.asp`, `Ranking_Veteranos.asp`) se migran por separado. `rankings.js` decide el destino:

1. si la página ya está en `PAGINAS_MIGRADAS` de `comun.js`, la página nueva (así, al migrarlas, basta con
   añadirlas allí);
2. si es un `Ranking_*` de la web actual, la página nueva **previsible** según la convención del proyecto
   (`Calendario_Menores_FFAA` → `calendario-menores-ffaa.html`): `ranking-absoluto.html`, `ranking-menores.html`,
   `ranking-equipos-absolutos.html`, `ranking-silla-ruedas.html`, `ranking-veteranos.html`
   (**SUPUESTO**: hasta que existan, esos enlaces dan 404 en el prototipo);
3. cualquier otro enlace, tal cual (los de otro dominio, en pestaña nueva).

Los enlaces del panel "Tu lugar en el juego" de la portada (`index.html`) a `Ranking_*.asp` **no se han tocado**:
siguen en la web actual hasta que se migren esas páginas.

Actualización: `Ranking_Absoluto.asp` ya está migrada (`ranking-absoluto.html`, ver `Ranking_Absoluto.md`): está en
`PAGINAS_MIGRADAS` y el enlace "Absoluto" de la portada apunta a la página nueva.

## Correcciones de datos (revisar)

| Original | Migrado | Motivo |
|---|---|---|
| "Ranking Equipos **Asbolutos**" (`Texto` en BD) | "Ranking Equipos Absolutos" | Errata. Se corrige al servir (`ReglasRankings.Titulo`); conviene corregirla también en la BD |
| `<title>Archivos Multimedia \| FEP</title>` | "Rankings · Federación Española de Pádel" | Título heredado de otra página |
| `alt` de cada imagen = `Texto` (con la errata) | `alt=""` (decorativa) + título en texto | La imagen solo repite el título |

## Comprobación de enlaces (13/11/2026)

- Imágenes `https://www.padelfederacion.es/refs/images/{RnkAbs,RnkMenores,RnkEquiposAbs,RnkSilla,RnkVet}.jpg`:
  200, mismo tamaño que las de `X:\TICS\WEB\refs\images\`. Copias locales para el mock en
  `assets/img/fep/rankings/` (`rnkabs.jpg`, `rnkmenores.jpg`, `rnkequiposabs.jpg`, `rnksilla.jpg`, `rnkvet.jpg`).
- `https://www.padelfederacion.es/Ranking_Absoluto`, `/Ranking_Menores`, `/Ranking_Equipos_Absolutos`,
  `/Ranking_Silla_Ruedas`, `/Ranking_Veteranos`: 200.

## Pendiente de confirmar por la FEP

- **DECISIÓN FEP**: corregir en la BD `Texto` = "Ranking Equipos Asbolutos" (la API ya lo sirve corregido).
- **DECISIÓN FEP**: ¿"Ranking Silla Ruedas" → "Ranking Silla **de** Ruedas"? Se ha dejado tal cual.
- **DECISIÓN FEP**: ¿se mantienen las imágenes? Solo repiten el título en texto sobre negro y la de absoluto
  dice "Ranking Único Absoluto" mientras el texto dice "Ranking Único FEP". Se pueden retirar sin perder nada.
- **SUPUESTO**: nombres de las páginas nuevas de cada ranking (`ranking-{clave}.html`, ver arriba). Si al migrarlas
  se eligen otros, basta con añadirlas a `PAGINAS_MIGRADAS` (tiene prioridad sobre la regla previsible).
- **SUPUESTO**: tipos de columna (`Texto`, `Link`, `Foto` texto; `Orden` entero). Los valores de `Orden` del mock
  (1–5) son supuestos: la web pública solo muestra el orden resultante.
- **SUPUESTO**: la ruta del endpoint, `/api/v1/competicion/rankings` (área competición, como el calendario y los
  campeonatos). El mapa proponía `/api/v1/rankings?modalidad&categoria&genero` para las clasificaciones; para no
  mezclar ambas cosas se sugiere `/api/v1/competicion/rankings/{clave}?categoria=&genero=` al migrar `Ranking_*.asp`.
- La página no enlaza a "Estadísticas" (`/Estadisticas`), aunque el enlace de la portada dice "Ver rankings y
  estadísticas" (texto del Figma). ¿Añadir una tarjeta en `SFEP_Rankings_Web` cuando se migre?

## Modelo SQL

La tabla ya existe; no hace falta modelo nuevo. Columnas usadas: `Texto`, `Link`, `Foto`, `Orden`, `Visible`.
Si se quiere gestionar desde el back-office, bastaría con un mantenimiento sencillo de esa tabla y, opcionalmente:

```sql
ALTER TABLE SFEP_Rankings_Web ADD
    Descripcion nvarchar(300) NULL,          -- entradilla de la tarjeta (hoy no existe)
    ImagenAlt   nvarchar(200) NULL;          -- solo si la imagen deja de ser decorativa
```

## Ficheros

- API: `Contracts/Competicion/RankingWebDto.cs`, `Contracts/Competicion/ReglasRankings.cs`,
  `Data/Repositories/Competicion/RankingsWebRepository.cs`, `Controllers/Competicion/RankingsController.cs`;
  registro en `Program.cs`; tests en `tests/FEP.Api.Tests/RankingsTests.cs`.
- Web: `wwwroot/rankings.html`, `assets/js/rankings.js`, estilos `.rnk-*` (y `.sk--rnk`) en la sección "Páginas
  interiores" de `assets/css/fep.css` (y su bloque responsive), imágenes en `assets/img/fep/rankings/`.
- Mock: `mock/competicion-rankings.json` (instantánea de la web pública; `imagenUrl` a las copias locales) y
  `mock/mocks.js` regenerado.
- Común: `PAGINAS_MIGRADAS` en `assets/js/comun.js` (`rankings` → `rankings.html`); enlaces a `Rankings.asp` en
  `index.html` y en las demás páginas migradas cambiados a `rankings.html`.
