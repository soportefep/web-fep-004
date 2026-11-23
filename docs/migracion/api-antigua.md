# API antigua (ASP clásico): contratos que hay que respetar

Fuentes analizadas (solo lectura):

- `X:\TICS\WEB\API\Produccion\*.asp` (91 ficheros propios, 79 rutas publicadas en `web.config`).
- `X:\TICS\WEB\API\Desarrollo\*.asp` (42): copia de desarrollo con 35 rutas propias. **No se migra.**
- `X:\TICS\WEB\API\Documentación Técnica - API PadelCV - FEP (1).docx` (v1.3, 10/03/2020): texto
  extraído de `word/document.xml`. Las copias de `API\Produccion\` y `API\Desarrollo\` son
  versiones anteriores del mismo documento (más cortas).
- `X:\TICS\Especificaciones API FEP v0.11 / v0.13 / v1.pdf`: **no se han podido leer** (no hay
  extractor de PDF en este entorno). Hay que revisarlas a mano antes de la fase 3.
- `X:\TICS\WEB\Documentacion_API.html` (raíz pública del sitio): no analizado en detalle; contiene
  un secreto de API (ver riesgos).

> Este documento **no reproduce** credenciales, tokens ni los datos personales de ejemplo que
> aparecen en el .docx (DNI, emails, teléfonos reales).

## Clientes conocidos

| Cliente | Evidencia | Usa |
|---|---|---|
| App móvil PadelCV (proveedor Mobilendo) | .docx: tabla "EndPoint API padelcv.com ↔ EndPoint API FEP" | usuarios, ficha, clubes, torneos, inscripciones, cuadros, documentos, tarifas |
| Mobilendo (notificaciones push) | .docx §4: la FEP **llama** a `POST {padelcv}/api/v1/notification` | saliente: avisar a licencias de una competición |
| Federaciones territoriales | `Org_id`/`Secret_Key` guardados por federación en `SFEP_Federaciones` | rankings, ligas, clubes, licencias |
| FCP (Fed. Catalana) | `FCP_RnkUnico.asp`, `FCP_CargarLicencias.asp`, gestion `7x-Json*FCP.asp` | ranking único, licencias |
| PadelManager ("PM"), Sunpad, app "APP_*" | 20 reglas `Webservice/*` en `web.config` | **el código no está en `X:\TICS\WEB`** (ver riesgos) |

## Convenciones del contrato antiguo

- **Autenticación**: parámetros `Org_id` y `Secret_Key` en query string o formulario; se validan
  con `SELECT ... FROM SFEP_Federaciones WHERE Org_id='..' AND Secret_Key='..'` (concatenado).
  Cada cliente queda así ligado a una federación, que filtra los datos.
- **Métodos**: el ASP lee todo con `Request("x")`, así que acepta GET y POST indistintamente.
- **Respuesta**: siempre HTTP 200 con cuerpo JSON y un código propio en `result` (algunos usan
  `resultado`):
  `100` OK · `001` error de autenticación · `002` datos de entrada incorrectos · `003` licencia/
  password incorrectos (o tarifas no disponibles) · `005` licencia/fecha incorrectas · `006` usuario
  ya existe · `007` licencia no existe · `008` club no existe · `009` torneo no existe ·
  `010`–`045` validaciones de inscripción · `046` cuadro no generado · `047` cuadro no visible ·
  `500` error interno.
- **Formato**: JSON escrito a mano con `Response.Write` (frágil: comillas sin escapar, comas
  finales). Fechas `dd/mm/yyyy` como cadena; números decimales a veces con coma (`"1,87"`).
  Nombres de campo con mayúsculas mixtas e incluso con espacios (`" Pareja"`, `"Idclub "`).
- **Codificación**: casi todos los ficheros de `API\` están en **UTF-8** (no en Windows-1252 como
  el resto del sitio).
- **Contraseñas**: el login compara `SFEP_Fichas.ClaveMod` en claro dentro del SQL.

## Endpoints de producción y destino propuesto

La fachada `Controllers/Legacy/` mantendrá **las rutas exactas** (`/API/Produccion/<Nombre>`) y
el sobre `{result: ...}` mientras haya clientes; internamente llamará a los mismos servicios que
los endpoints `/api/v1/*`. Columna "E": escribe en BD.

| Ruta antigua (`/API/Produccion/…`) | Fichero | E | Documentada (.docx) | Destino v1 propuesto |
|---|---|:-:|:-:|---|
| `usuarioRegistrado` | usuarioRegistrado.asp | | sí | `GET /api/v1/auth/existe?dni&fechaNacimiento` |
| `registro` | registro.asp | sí | sí | `POST /api/v1/auth/registro` |
| `login` | login.asp | | sí | `POST /api/v1/auth/login` (JWT) |
| `recordarPassword`, `envioRecordatorio` | recordarPassword.asp, envioRecordatorio.asp | sí | sí | `POST /api/v1/auth/recordar` (2 pasos) |
| `provincias`, `comunidades`, `federaciones`, `paises` | *.asp | | sí | `GET /api/v1/maestros/{provincias,comunidades,federaciones,paises}` |
| `Datos_Ficha` | Ficha.asp | sí | sí | `GET /api/v1/jugadores/{licencia}` |
| `Modificar_Ficha`, `Modificar_Jugador` | Modificar_Ficha.asp, Modificar_Jugador.asp | sí | sí | `PUT /api/v1/jugadores/{licencia}` |
| `Modificar_Imagen` | GuardarFoto.asp | sí | sí | `PUT /api/v1/jugadores/{licencia}/foto` |
| `Info_Jugador`, `Nuevo_FEP`, `API_NumFEP_Cat`, `Historico` | Info_Jugador.asp, NuevoFEP.asp, API_NumFEP_Cat.asp, Historico.asp | | no | `GET /api/v1/jugadores/...` (revisar uno a uno) |
| `Jugadores` | Jugadores.asp | | sí | `GET /api/v1/jugadores?genero&provincias&...&page&limit` |
| `Clubes_Fed`, `Clubes_Listado`, `Clubes_Localidades` | *.asp | | parcial | `GET /api/v1/clubes?federacion&busqueda&page&limit` |
| `Clubes_Cercanos` | Clubes_Cercanos.asp | | sí | `GET /api/v1/clubes/cercanos?lat&lon&radioKm` |
| `Datos_Club` | club.asp | | sí | `GET /api/v1/clubes/{id}` |
| `Club_Modificar` | Club_Modificar.asp | sí | no | `PUT /api/v1/clubes/{id}` (JWT) |
| `Cambiar_Club_Autonomico` | Cambio_ClubAutonomico.asp | sí | no | `PUT /api/v1/jugadores/{licencia}/club` |
| `Calendario`, `Listado_Torneos` | Calendario.asp, Torneos.asp | | no | `GET /api/v1/torneos?desde&hasta&federacion` |
| `Datos_Torneo` | Datos_Torneo.asp | | sí | `GET /api/v1/torneos/{id}` |
| `Categorias_Torneo`, `Cat_Visibles` | *.asp | | no | `GET /api/v1/torneos/{id}/categorias` · `/cuadros` |
| `Cuadro`, `Enfrentamientos` | Cuadro.asp, Partidos.asp | | sí / no | `GET /api/v1/torneos/{id}/cuadros/{nivel}/{genero}/{categoria}/{fase}` |
| `CampeonatosJugador`, `TorneosFinalizadosJugador`, `ProximosPartidos` | TorneosJugador.asp, … | | sí | `GET /api/v1/jugadores/{licencia}/torneos?estado=` · `/proximos-partidos` |
| `Inscritos`, `Listado_Insc`, `Listado_Eliminados`, `Coach_Autorizados` | *.asp | | parcial | `GET /api/v1/torneos/{id}/inscritos?categoria&genero` |
| `Inscripcion` | Inscripciones.asp | sí | sí | `POST /api/v1/inscripciones` (mantener los códigos 010–045 como `errors` de ProblemDetails) |
| `Eliminar_Inscripcion` | Eliminar_Inscripcion.asp | sí | sí | `DELETE /api/v1/inscripciones/{codigo}` |
| `Mod_Insc`, `Modificar-Inscr` | Mod_Insc.asp | sí | no | `PATCH /api/v1/inscripciones/{codigo}` |
| `ResultadoTPV` | ResultadoTPV.asp | sí | no | `PagosController` (ver plan, riesgo pagos) |
| `Tarifas`, `Precios_Licencias` | Tarifas.asp, Precios_Licencias.asp | | sí | `GET /api/v1/licencias/tarifas?federacion&anio` |
| `Nueva_licencia`, `Renovacion_licencia` | *.asp | sí | no | `POST /api/v1/licencias` · `POST /api/v1/licencias/{id}/renovacion` |
| `Documentos` | Documentos.asp | | sí | `GET /api/v1/documentos?federacion=` |
| `CategoriasRanking`, `RankingFed`, `RankingFinal`, `RankingTorneo`, `RankingHistorial`, `Ranking_FEP`, `FCP_Rnk_FEP` | *.asp | (`RankingHistorial` escribe tabla auxiliar) | no (solo en notas del .docx) | `GET /api/v1/rankings?federacion` · `/rankings/{n}` · `/rankings/{n}/torneos/{id}` · `/rankings/{n}/jugadores/{licencia}/historial` |
| `Ligas_*` (13 rutas) y `Equipos_Listado` | Ligas_*.asp | `Ligas_ModificarEquipo`, `Ligas_ActualizarActa` | no | `/api/v1/ligas/{id}/{calendario,clasificacion,equipos,eliminatorias,actas,documentos}` |
| `Arbitro`, `Pruebas_Arbitraje`, `Solicitud_Arbitraje` | *.asp | `Solicitud_Arbitraje` | no | `/api/v1/arbitraje/...` (JWT rol Árbitro) |
| `SeguirMail` | SeguirEmail.asp | sí | no | píxel de seguimiento de mailing → `ComunicacionesController` (revisar RGPD) |
| `API_Whatsapp` | API_Whatsapp.asp | sí | no | revisar con negocio |

Sin regla en `web.config` (no publicados con URL limpia, revisar si se llaman por `.asp`):
`FCP_CargarLicencias`, `HorariosAct`, `Resultados`, `Subir-Archivo`, `WPT`, `GuardarFoto2`,
`Inscripciones2`, `registro2`. Pruebas a descartar: `API_MB_Probar`, `InscripPrueba`,
`PruebaInsc`, `ModEjemplo`.

## Integración saliente (Mobilendo)

`POST https://{dev.|api.}padelcv.com/api/v1/notification` con cabeceras `Api-Org` y `Api-Token`
y cuerpo `{notificationId, Competicion{Id, Nombre, DescripcionCuadro}, Licencias[]}`. En .NET:
`HttpClient` tipado en `Integrations/Mobilendo`, con org y token en configuración segura
(**el token actual está escrito en el .docx y en ficheros ASP: hay que rotarlo**).

## Recomendaciones para la fachada Legacy

1. Ningún cambio de forma en las respuestas Legacy: probar con respuestas grabadas del sistema
   actual (tests de contrato con JSON de ejemplo anonimizados).
2. Aceptar `Org_id`/`Secret_Key` solo en la fachada, nunca en `/api/v1` (allí: cabecera
   `X-Api-Key` + JWT de usuario). Guardar los secretos con hash y rotarlos al publicar la fachada.
3. Corregir en la fachada los defectos de JSON (escapado) sin cambiar nombres de campo.
4. Registrar uso por `Org_id` y endpoint para saber cuándo se puede retirar cada ruta.
