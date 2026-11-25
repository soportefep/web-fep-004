# Plan de migración por fases

Estrategia: **sustitución progresiva** (strangler fig). El ASP y la solución nueva conviven
contra la misma base de datos `FEP`; cada fase pasa un bloque de URLs al sistema nuevo y deja el
resto en el ASP. Nada se apaga hasta que su sustituto está en producción y las URLs antiguas
redirigen (301) o siguen respondiendo (API Legacy).

Propuesta de enrutado durante la convivencia (no está en `ARQUITECTURA.md`; decidir antes de la
fase 1 en producción): IIS con ARR/URL Rewrite (o YARP delante) envía a `FEP.Web`/`FEP.Api` las
rutas ya migradas y el resto al sitio ASP.

## Fase 0 · Cimientos y seguridad (antes de publicar nada)

- Solución `FEP.sln` (API, Web, tests) ✔ · CI con `dotnet build` + `dotnet test`.
- **Rotar todas las credenciales** que aparecen en el código antiguo (BD, SMTP, clave del TPV
  Redsys, `Secret_Key` de federaciones, token de Mobilendo): están en claro en ~200 ficheros y en
  documentos `.docx`/`.html`. Ver "Riesgos".
- Usuario de BD **de solo lectura** para la API pública; otro con permisos mínimos para escrituras.
- Comprobar en el IIS actual que `/include/*.inc` y `/refs/**/*.asp` no se sirven ni ejecutan.
- Decidir almacenamiento de `refs\` (777 MB de imágenes y documentos subidos): mantener la ruta
  pública `/refs/...` (la API ya genera URLs con `Media:BaseUrl`).

## Fase 1 · Home y layout común (en curso)

Origen: `index.asp` + `Menus.inc`, `BarraLateral.inc`, `PiePagina.inc`, `conexionSQL.inc`.
Destino: `/api/v1/portada` y endpoints de menú, banners, avisos, patrocinadores, fotos,
noticias destacadas y convocatorias (**ya creados**, ver `docs/api/endpoints.md`) + `index.html`.

- Dependencias: ninguna (solo lectura).
- Salida: componentes de cabecera, barra lateral y pie reutilizables para todas las páginas.
- Redirecciones: `/Home`, `/index.asp` → `/`.

## Fase 2 · Contenido público de solo lectura

Orden (de más a menos tráfico/visibilidad, y de menor a mayor complejidad):

1. Noticias: `News.asp`, `Noticia.asp`, `Noticias.asp`, `MyP_Noticias.asp`.
2. Institucional: `Bienvenida`, `Organos_Gobierno`, `Equipo_FEP`, `Federaciones`,
   `Datos_Federacion`, `Clubes`/`Club`, `Contacto` (el envío del formulario es escritura: fase 4),
   `Politica_Privacidad`, `Politica_Cookies`, `Aviso_Legal`, `Ley_De_Transparencia`,
   `Padel-Adaptado`, `MujerYPadel` + `MyP_*`, `Hoteles_Concertados`, `Mapa_Web`.
3. Documentos: `Documentos_*` (13).
4. Calendario y campeonatos (lectura): `CalendarioFEP`, `CalendarioFT`, `Calendario_*_FFAA`,
   `Campeonatos`.
5. Rankings públicos: `Rankings`, `Ranking_Absoluto/Menores/Veteranos/Silla_Ruedas/Equipos_Absolutos`
   + includes de categoría (`ABS_F`…`VET55_X`) → un solo endpoint parametrizado.
6. Formación (informativo): `Formacion_Calendario`, `Formacion_Acciones`, `Formacion_Detalle`,
   `Formacion_BolsaTrabajo`, `Formacion_Jornadas`.
7. Maestros (`combo*.asp`, `dato*.asp` públicos) → `/api/v1/maestros/*`.

Al cerrar cada bloque: revisar sus puntos abiertos (ver "Revisión de supuestos y decisiones") y
activar sus **301** de `redirecciones.csv` (167 URLs públicas con regla en `web.config`, más los
`.asp` directos con su query string, p. ej. `Noticia.asp?id=` → `/noticias/{id}`).

### Revisión de supuestos y decisiones

Cada migración deja en su análisis los puntos que no se pudieron confirmar desde el ASP. Se acumulan
en un registro único, `docs/migracion/registro-puntos.csv` (id estable `P0001`…), y se revisan en el
Excel `docs/api/supuestos-decisiones-fep.xlsx`. `tools/registro-puntos.ps1` importa las respuestas del
Excel, añade los puntos nuevos y regenera el Excel; el agente `asp-pagina-migrador` lo ejecuta al
terminar cada página y consulta las respuestas antes de empezar la siguiente.

| Tipo | Revisa | Cuándo |
|---|---|---|
| Decisión FEP | FEP | Al cerrar el bloque, antes de sus 301 (mientras la API va con mocks, cambiarla es barato) |
| Corrección de datos | FEP | Al cerrar el bloque; lo que sea "corregir en la gestión" puede hacerse ya en el ASP |
| Supuesto | Equipo técnico | Al conectar la API a la BD real, página a página (consulta + tests) |
| A tener en cuenta | Equipo técnico | Como tareas del bloque (redirecciones, enlaces, páginas que siguen en la web actual) |
| Decisión técnica | — | Informativo |

Estados: Pendiente · Confirmado · Rechazado (la respuesta dice qué cambiar) · Aplicado (cambio hecho
en el código) · No aplica · Informativo.

## Fase 3 · Competición pública + API Legacy de lectura

- Torneos (detalle, inscritos, cuadros, horarios, resultados, ligas): `Cuadro*.asp`,
  `CuadroEquipos*`, `CuadroPrevia*`, `Liguilla*`, `Previo*`, `EquiposEnfrentamientos`,
  `Equipos-Alineaciones-*` y sus copias de `refs\Torneos\` → `TorneosController`/`CuadrosController`
  con salida JSON y XLSX (sustituye las variantes `…Excell`).
- **Fachada Legacy** (solo endpoints de lectura de `API\Produccion`, ver `api-antigua.md`) con las
  rutas exactas, el sobre `{result}` y los códigos actuales. Tests de contrato contra respuestas
  grabadas del sistema actual.
- Revisar a mano las especificaciones PDF de `X:\TICS` (v0.11, v0.13, v1).
- Dependencia: los repositorios de cuadros/rankings de lectura (no el motor de generación).

## Fase 4 · Identidad y área privada (escrituras)

Orden: identidad → ficha → licencias → inscripciones → pagos.

1. **Login de federados** con JWT (rol Jugador). Las contraseñas actuales están en claro
   (`SFEP_Fichas.ClaveMod`): migrarlas a hash en el primer inicio de sesión y forzar cambio.
   Origen: `Licencias_TuLicencia*`, `Licencias_SolicitaClave*`, `Portal_Federado`,
   `Acceso_Estamento_*`, `Aceptar_Clausulas`; API Legacy `login`, `registro`, `recordarPassword`.
2. Ficha y licencias: `Licencias_*` (alta, renovación jugador/técnico/árbitro, extranjeros,
   cambio de federación), API Legacy `Modificar_Ficha`, `Nueva_licencia`, `Renovacion_licencia`.
3. Inscripciones: `Camp_Parejas*`, `Camp_Equipos*`, `Camp_Liga*`, `Camp_Silla*`, `Empr_*`,
   `Copa_AbonoFed*`; API Legacy `Inscripcion`, `Eliminar_Inscripcion`, `Mod_Insc`. Conservar las
   45 validaciones (códigos 010–045) como reglas de dominio con tests.
4. **Pagos Redsys** (38 ficheros `*_TPV*`, `*_Pago`, `*_Return`, `Cierre_TPV_PM_ok`,
   `PagoInscripcion-PM`, API `ResultadoTPV`): un único `PagosController` con notificación
   servidor-a-servidor firmada (HMAC-SHA256), idempotente y con registro de cada notificación.
   Las URLs de notificación están configuradas en el panel de Redsys: mantener las antiguas
   (proxy a la nueva) hasta cambiarlas allí.
5. Certificados (`Certificado*`), formación con pago (`Formacion_TPV`, `Formacion_Jornada_*`,
   `Form_Evento*`, exámenes `Acceso-Examenes`, `Datos-Examen`, `Examen-Subir`), solicitudes de
   arbitraje y prácticas, licitación de torneos (`Organizacion_*`).
6. Arbitraje y marcador en directo (`Arbitraje*`, `Marcador*`, `Cargar*`, streaming): rol Árbitro;
   valorar SignalR para el tiempo real.

## Fase 5 · Back-office de gestión (`gestion\`, 1.180 ficheros)

Requisito previo: **identidad de administración** con roles y 2FA (ASP.NET Core Identity) que
sustituya la cookie `ControlAccesoSFEP` (ver riesgos) y los permisos de `SFEP_Usuarios_Permisos`.

Orden propuesto (por dependencia y uso):

1. Configuración, usuarios y permisos (`Configuracion*`, `Usuarios*`, `ControlAcceso`, `Index`).
2. Torneos (`Torneos*`, 286 ficheros) y **motor de cuadros** (`Cuadro*`, `GenerarCuadros*.inc`,
   `PrevioGenerar*.inc`, `Liguilla*`): migrar el motor como servicio puro con tests de regresión
   que comparen contra cuadros generados por el ASP con los mismos datos.
3. Resultados, horarios, sanciones; rankings (`Ranking*`, `calcula*`) como job programado.
4. Fichas, licencias, carnés y certificados en PDF (`Ficha*`, `Carnet*`, `Certificado*`, FPDF).
5. Pagos y remesas (`Pagos*`), comunicaciones y mailing (`Comunicaciones*`, `Newsletter`).
6. Contenidos (CMS: noticias, banners, multimedia, microsites, patrocinadores, hoteles, eventos).
7. Formación, informes, clubes/federaciones.
8. Integraciones (`API_PM_*` PadelManager, Mobilendo, FCP, Sunpad) como `HttpClient` tipados.

Los 286 ficheros de gestión marcados **REVISAR** deben validarse con usuarios de la FEP antes de
estimar: muchos pueden ser versiones o páginas sin uso.

## Fase 6 · Retirada

- Censo electoral (`Censo_*`, 54 ficheros): solo se usa en periodo electoral; migrar cuando toque
  el siguiente proceso o sustituir por una solución externa.
- Webservices de terceros (`Webservice/*`) y fachada Legacy: retirar ruta a ruta según el
  registro de uso por cliente.
- Apagar el sitio ASP; dejar las reglas 301 de forma permanente.

## Dependencias clave

```
Fase 0 ──► Fase 1 (home/layout) ──► Fase 2 (público lectura) ──► Fase 3 (competición + Legacy lectura)
                                                    │
                                                    └──► Fase 4 (identidad ► ficha ► licencias ► inscripciones ► pagos)
                                                                          │
                                                                          └──► Fase 5 (identidad admin ► torneos/cuadros ► resto)
```

- Cuadros públicos (fase 3) dependen solo de **leer** `SFEP_Torneos_*`; el motor de generación
  (fase 5) puede seguir en el ASP mientras tanto porque escribe en las mismas tablas.
- Inscripciones (fase 4) dependen de identidad y licencias; pagos dependen de inscripciones.
- La API Legacy de escritura depende de la fase 4.

## Riesgos

| # | Riesgo | Evidencia | Mitigación |
|---|---|---|---|
| 1 | **Credenciales en claro** en el código | Cadena de conexión con usuario/contraseña en `include\conexionSQL.inc`, `conexionDesarrollo.inc`, `conexionSQLDemo.inc` y en 28 páginas más que abren su propia conexión (scripts de gestión y páginas `*_TPV_Pago`/`*_Return`); contraseña SMTP en 96 ficheros; clave de comercio Redsys en 34; `Secret_Key`/tokens de API en 48 (incluido `Documentacion_API.html`, publicado en la raíz del sitio, y los `imagen.txt` de `API\`); token de Mobilendo y `Secret_Key` en el `.docx` de la API | Rotar todo en fase 0. Nada de credenciales en el repo (`user-secrets`/variables de entorno). Lista completa: notas `contiene_credenciales=si` en `inventario.csv` |
| 2 | **Sesión de gestión falsificable** | `gestion\` autentica con la cookie `ControlAccesoSFEP` = id de usuario, sin firmar (923 páginas la leen; `ControlAcceso.asp` enlazado desde 792); permisos con ids de usuario fijos en el código | Prioridad máxima en el ASP actual (fuera de este proyecto). En lo nuevo: Identity + 2FA |
| 3 | **Contraseñas de federados en claro** | `SFEP_Fichas.ClaveMod` comparada en el SQL del login | Hash (PBKDF2/Argon2) en el primer login; forzar cambio |
| 4 | **Inyección SQL** generalizada | SQL concatenado en todas las páginas; `limpiar()` y el `requestFiltering` de `web.config` son listas negras | Dapper parametrizado (contrato); no portar SQL tal cual |
| 5 | **Pagos Redsys** | 38 ficheros con TPV; las URLs de notificación abren conexión propia y escriben en BD; sin idempotencia visible | Un solo `PagosController`; pruebas en entorno de test de Redsys; cambio coordinado de URLs en el panel |
| 6 | **Código que no está en la copia** | 20 reglas `Webservice/*` (PadelManager, Sunpad, app), 6 `Paginas/*`, 6 `Bootstrap_3.3.7-Versions/*` (pantallas de orden de juego), `Reserva-ZOOM.html`, y los includes `conexionSQLSunpad.inc` y `cLibOAuth.asp` (usados por 3 ficheros cada uno) | Localizar en el servidor antes de la fase 3 |
| 7 | **Clientes externos de la API antigua** | App PadelCV, territoriales, FCP, PadelManager | Fachada Legacy con rutas y formato exactos; registro de uso por `Org_id` |
| 8 | **Codificación mixta** | 1.564 ficheros en Windows-1252, 235 en UTF-8 (casi toda `API\`), 375 ASCII | Leer cada fichero con su codificación al analizarlo; la salida nueva es siempre UTF-8 |
| 9 | **Ficheros subidos en `refs\`** | 777 MB; `refs\images\` y `refs\Torneos\` contienen `.asp`/`.inc` ejecutables | Servir `refs` como estático sin ejecución; migrar a almacenamiento con las mismas rutas |
| 10 | **Datos personales en el árbol web** | `gestion\` contiene PDF de títulos de personas, fotos `.heic`, `LicenciasExcell.xls`; el `.docx` de la API trae DNI/teléfonos/emails de ejemplo reales | No copiar al repo; revisar con el DPD (RGPD) y retirarlos del servidor |
| 11 | **URLs públicas** | 167 reglas de URL limpia (`Padel-Adaptado`, `Politica_Privacidad`, `Noticias`, censos…) + enlaces directos a `.asp` con parámetros | `redirecciones.csv` → 301 por bloque migrado; `UseHttpsRedirection` + HSTS sustituye la regla `https` |
| 12 | **Motor de cuadros y rankings** | `GenerarCuadrosABS.inc` 185 KB, `Torneos_Cuadros.asp` 135 KB, 25 includes del motor en "revisar" | Migrar al final, con tests de regresión de resultados |
| 13 | **Fechas poco fiables / ficheros muertos** | 1.387 ficheros con la fecha de copia; 369 en "revisar" | Validar con la FEP antes de estimar; usar logs de IIS (si existen) para ver qué URLs se usan |
| 14 | **Dev y prod mezclados** | `API\Desarrollo` y `conexionDesarrollo.inc` en el mismo sitio público | No publicar `API/Desarrollo/*` en lo nuevo (35 reglas) |
