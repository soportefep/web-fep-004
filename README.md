# FEP: migración de ASP clásico a ASP.NET Core

Nueva web de la Federación Española de Pádel. Sustituye poco a poco el sitio en ASP clásico
(`X:\TICS\WEB`, que es **solo lectura** y no se copia a este repositorio).

| Proyecto | Qué es |
|---|---|
| `src/FEP.Api` | API REST (.NET 10). Es la única pieza que habla con SQL Server (Dapper, SQL parametrizado). |
| `src/FEP.Web` | Front-end estático (HTML/CSS/JS en `wwwroot`) servido por ASP.NET Core. |
| `tests/FEP.Api.Tests` | Tests de la API (xUnit + `WebApplicationFactory`). |
| `docs/` | Contrato de arquitectura, endpoints, análisis de páginas antiguas e inventario de la migración. |

Lectura recomendada: [`docs/ARQUITECTURA.md`](docs/ARQUITECTURA.md) (contrato),
[`docs/api/endpoints.md`](docs/api/endpoints.md) (endpoints disponibles) y
[`docs/migracion/plan-fases.md`](docs/migracion/plan-fases.md) (orden de migración).

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) (`dotnet --version` debe mostrar 10.x).
- Acceso de red a SQL Server con la base de datos `FEP` (solo para los endpoints que leen datos).

## Configurar la cadena de conexión (una vez por máquina)

La cadena **nunca** se escribe en `appsettings*.json` ni se sube al repositorio
(`appsettings.json` la deja vacía a propósito). En desarrollo se guarda con *user secrets*:

```powershell
cd src/FEP.Api
dotnet user-secrets set "ConnectionStrings:FEP" "Server=<servidor>;Database=FEP;User Id=<usuario>;Password=<clave>;TrustServerCertificate=True"
dotnet user-secrets list   # comprobar (no compartas la salida)
```

Usa un usuario de BD **de solo lectura** para la API pública, no la cuenta que usa el ASP antiguo.
Pide las credenciales al responsable de sistemas; no las copies de `include\conexionSQL.inc`.

En servidores se usa la variable de entorno equivalente (doble guion bajo = `:`):

```powershell
$env:ConnectionStrings__FEP = "Server=...;Database=FEP;..."
```

## Asistente virtual FEPi (chatbot)

FEPi es una burbuja de chat (abajo a la derecha) en todas las páginas: responde sobre reglamentos, circulares, estatutos y
rankings. La carga `comun.js` (`assets/js/asistente.js` + `assets/css/asistente.css`) y habla con
`POST /api/v1/asistente/chat`, que usa Claude (API de Anthropic) con cuatro herramientas: búsqueda en los documentos,
catálogo de documentos, clasificación de un ranking y estadísticas de un jugador (las dos últimas, con los mismos
repositorios de solo lectura que los endpoints públicos).

**Clave de la API de Anthropic** (nunca en `appsettings*.json`):

```powershell
cd src/FEP.Api
dotnet user-secrets set "Asistente:ApiKey" "<clave>"
```

En servidores, la variable de entorno `Asistente__ApiKey`. Sin clave, el chat se muestra igual pero responde que no
está disponible y ofrece el formulario de contacto. Los demás ajustes (`Asistente:Modelo`, `Esfuerzo`,
`PreguntasPorMinuto`, `PreguntasPorHora`, `TokensDiarios`, `UrlContacto`) tienen valores por defecto en
`Infrastructure/Asistente/AsistenteOptions.cs`.

**Documentos**: los PDF de `docs/info_fep/` se convierten en `src/FEP.Api/Contenidos/asistente/` (catálogo + fragmentos
de texto). Al añadir, cambiar o retirar un PDF:

```powershell
powershell -NoProfile -File tools\generar-corpus-asistente.ps1   # requiere pdftotext (viene con Git for Windows)
```

y reinicia la API. En `documentos.json` se pueden editar a mano `titulo`, `descripcion`, `anio`, `vigente` y `url`
(enlace público al PDF, para que el chat lo enlace como fuente); el script los conserva. Los PDF escaneados sin texto
quedan marcados con `"sinTexto": true` y no se consultan hasta pasarlos por OCR.

## Ejecutar

En dos terminales:

```powershell
dotnet run --project src/FEP.Api   # http://localhost:5080  (OpenAPI en /openapi/v1.json)
dotnet run --project src/FEP.Web   # http://localhost:5090
```

- La API acepta peticiones del front desde los orígenes de `Cors:OrigenesPermitidos`
  (`appsettings.Development.json` ya incluye `http://localhost:5090`).
- El front lee la URL de la API de `<meta name="fep-api-base">` en `wwwroot/index.html`
  (vacío = mismo origen). Para usar la API local pon `http://localhost:5080`.
- Si la API no responde, el front usa `wwwroot/mock/*.json`, así que `FEP.Web` funciona solo
  como prototipo sin base de datos.
- También se puede abrir `wwwroot/index.html` con doble clic (file://): como ahí el navegador
  bloquea `fetch()`, se usan los mocks embebidos en `wwwroot/mock/mocks.js`. **Si cambias un
  `.json` de mock, regenera ese fichero:**
  `powershell -NoProfile -File tools\generar-mocks-js.ps1`
- Antes de publicar en producción, quita la línea `<script defer src="mock/mocks.js">` de
  `index.html` (solo sirve para el prototipo).

## Tests

```powershell
dotnet test
```

Los tests de humo arrancan la API en memoria y no necesitan base de datos.

## Compilar toda la solución

```powershell
dotnet build FEP.sln
```

## Migración

- Inventario de los ~2.100 ficheros antiguos: `docs/migracion/inventario.csv`.
- Mapa origen → destino: `docs/migracion/mapa-migracion.md`.
- URLs públicas antiguas y su redirección: `docs/migracion/redirecciones.csv`.
- Contratos de la API antigua (app móvil, territoriales): `docs/migracion/api-antigua.md`.

### Añadir una página migrada al front

Ejemplo de referencia: `Organos_Gobierno.asp` → `wwwroot/organos-de-gobierno.html`
(análisis en `docs/api/analisis/Organos_Gobierno.md`).

1. Copia `organos-de-gobierno.html` y cambia solo el `<head>` y el `<main>`: barra superior, cabecera,
   menú y pie son iguales en todas las páginas.
2. Scripts, en este orden: `mock/mocks.js` → `assets/js/api.js` → `assets/js/comun.js` (cabecera,
   menú con (+), pie) → el script propio de la página, que usa las utilidades de `window.FEP`.
3. Añade la ruta antigua a `PAGINAS_MIGRADAS` en `assets/js/comun.js` para que los enlaces del menú y
   de la BD lleven a la página nueva, y cambia los enlaces fijos del HTML.
4. Si el contenido no está en BD (estaba escrito en el ASP), va a `src/FEP.Api/Contenidos/*.json` y se
   sirve con un endpoint propio; su mock se genera con un script de `tools/`.
5. Regenera `mocks.js` (`tools\generar-mocks-js.ps1`) y añade la redirección 301 de la URL antigua en
   `docs/migracion/redirecciones.csv`.
6. Patrocinadores: mantén el `<aside class="page-sponsors" id="patrocinadores-lateral">` como último
   hijo de `page-layout`. `comun.js` ajusta solo los logos por fila a la longitud del contenido
   (`.page-content`): contenido largo → 1 por fila; corto → 2 o 3. No fijes columnas en CSS. Si el
   contenido está en otro contenedor, pon `data-altura-ref="#id"` en el `<aside>`.

