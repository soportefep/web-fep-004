# Migración FEP: ASP clásico → ASP.NET Core

Documento de contrato compartido por los agentes de `.claude/agents/`. Cualquier
cambio de estructura se hace aquí primero. Las páginas nuevas a migrar se dejan en
`pages_to_dev/` y las procesa `asp-pagina-migrador` (API + front + mock + análisis).

## Situación actual (origen)

- Código fuente original: `X:\TICS\WEB` (solo lectura: **nunca se modifica**).
  - ~570 `.asp` en la raíz (web pública), ~1.170 en `gestion\` (sistema de gestión),
    `include\` (conexión, menús, barras, pie, generadores de cuadros, rankings…),
    `API\` (API JSON previa en ASP clásico: `Desarrollo\` y `Produccion\`), `redsys\` (pagos), `fpdf\`.
- Cada página abre una conexión ADO directa (`include/conexionSQL.inc`) a SQL Server (BD `FEP`)
  y concatena SQL con valores de usuario (riesgo de inyección; la función `limpiar()` de
  `Menus.inc` es una lista negra, no una protección).
- Codificación **mixta**: ~1.560 ficheros en Windows-1252 y ~235 en UTF-8 (casi toda `API\`).
  Detectarla por fichero (BOM o decodificación UTF-8 estricta sin errores → UTF-8; si no, 1252):
  `[Text.Encoding]::GetEncoding(1252).GetString([IO.File]::ReadAllBytes($ruta))`.
- Las credenciales de BD están en claro en `conexionSQL.inc`. **No se copian jamás** a ningún
  fichero nuevo, informe ni mensaje.

## Arquitectura destino

```
FEP.sln
src/
  FEP.Api/              ASP.NET Core Web API (.NET 10 LTS) – única puerta a SQL Server
    Program.cs
    appsettings.json            ConnectionStrings:FEP = "" (se rellena con user-secrets / variables de entorno)
    Controllers/<Area>/<Recurso>Controller.cs
    Contracts/<Area>/*.cs       DTOs de respuesta/petición (records)
    Data/                       Acceso a datos con Dapper + Microsoft.Data.SqlClient
      IDbConnectionFactory.cs
      Repositories/<Area>/<Recurso>Repository.cs
    Infrastructure/             (CORS, ProblemDetails, caché, versionado)
  FEP.Web/              Front-end nuevo (HTML/CSS/JS en wwwroot, servido por ASP.NET Core)
    wwwroot/
      index.html
      assets/css/  assets/js/  assets/img/
      mock/                     JSON de ejemplo con la misma forma que la API (modo prototipo)
tests/
  FEP.Api.Tests/
docs/
  ARQUITECTURA.md               (este fichero)
  api/endpoints.md              inventario vivo de endpoints (lo mantiene el agente API)
  api/analisis/<pagina>.md      análisis por fichero .asp
  migracion/                    inventario y mapa de ficheros (lo mantiene el agente de reestructuración)
  figma/                        renders del diseño Figma "FEP UI_workflow" + textos
```

### Convenciones de la API

- Rutas REST en español, minúsculas, plural, prefijo versionado: `/api/v1/<recurso>`.
  Ej.: `/api/v1/noticias/destacadas`, `/api/v1/formacion/convocatorias?categoria=juez`.
- Siempre SQL **parametrizado** (Dapper). Nada de concatenar entrada del usuario.
- Fechas: comparar con parámetros `DateTime` (`@Ahora`), no con `'" & now() & "'` (el original
  depende del formato regional del servidor).
- Filtros fijos del ASP (`Borrado='N'`, `Visible='S'`, `Site='FEP'`, `Federacion=0`, `Portal='FEP'`)
  se mantienen en el repositorio; los que varían entre páginas se exponen como parámetros de query
  con el valor por defecto de la web FEP.
- DTOs en camelCase JSON, sin exponer columnas internas (`Borrado`, `Visible`…).
- Rutas de imagen: la API devuelve la ruta relativa original (`foto`, `imagen`) **y** una URL
  absoluta calculada con la opción `Media:BaseUrl` (`https://www.padelfederacion.es/refs/...`).
- Solo lectura pública sin autenticación; todo lo de área privada/gestión irá con JWT + roles
  (Jugador, Árbitro, Administración con 2FA) según `docs/figma/flow.jpg`. Para la home no hace falta.
- Errores: `ProblemDetails`. Respuestas cacheables (`[ResponseCache]`/output cache) para contenido público.

### Convenciones del front

- Sigue **exactamente** el diseño Figma (`docs/figma/home1-3.jpg`, `figma-textos.json`).
- HTML semántico + CSS propio con variables (sin framework pesado). JS vanilla (módulos ES).
- Consume la API vía `assets/js/api.js` con `API_BASE` configurable; si la API no responde, usa
  `wwwroot/mock/*.json` (mismo esquema) para que el prototipo funcione sin backend.
- Español, `lang="es"`, UTF-8 (adiós a iso-8859-1), accesible (contraste AA, foco visible, alt).
