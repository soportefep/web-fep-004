# Librerías de terceros en `X:\TICS\WEB`

Excluidas de `inventario.csv`: no son código propio y **no se migran**. Cada una se sustituye
por su equivalente en .NET o por el front nuevo (JS vanilla, sin dependencias pesadas).

| Ubicación | Ficheros | Qué es | Sustituto en la solución nueva |
|---|---:|---|---|
| `include\tiny_mce\` | 407 | Editor WYSIWYG TinyMCE 3.x (gestión) | Editor moderno en el back-office nuevo (fase 5), o Markdown |
| `js\ckeditor\`, `js\ckeditorFull\`, `js\Nckeditor\` | 805 + 252 + 528 | Tres copias/versiones de CKEditor 3/4 | Ídem: un único editor en el back-office |
| `js\summernote\` | 97 | Editor Summernote | Ídem |
| `gestion\nicEdit.js`, `gestion\nicEdit - copia.js` | 2 | Editor NicEdit | Ídem |
| `fpdf\` (+ `gestion\fpdf.asp`, usado por 59 páginas de gestión) | 22 | Port ASP de FPDF (generación de PDF: carnés, certificados, listados) | Librería PDF .NET (p. ej. QuestPDF) en un servicio `Documentos/Pdf` |
| `redsys\include\` (`brix_crypto-js-*`, `douglascrockford_JSON-js`, `dvim_*.asp`) | 173 | Kit no oficial de Redsys para ASP (crypto-js + adaptadores VBScript) | `System.Security.Cryptography` (3DES + HMAC-SHA256) en `PagosController` |
| `redsys\ejemplo*.asp`, `InotificacionSIS.wsdl` | 4 | Ejemplos del kit Redsys | Solo referencia |
| `include\mp3\` | 29 | Reproductor Flash (`.swf`) | Eliminar (Flash no existe) |
| `include\Instalaciones\` | 23 | jQuery 1.6 + Nivo Slider | CSS/JS propio del front |
| `include\jquery.js`, `include\plugins.js`, `include\ipopeng.htm` | 3 | jQuery antiguo, plugins y calendario popup | JS vanilla / `<input type="date">` |
| `js\` (jquery-1.6.2, cufon, Aller font, superfish, prettyPhoto, jcrop, easing, equalheights, easyTooltip, hover-image, FF-cash, html5, plugins) | 15 | Plantilla antigua de la web pública | CSS/JS propio (el diseño Figma sustituye la plantilla) |
| `refs\js\` (jquery-1.10.2, masonry, prettyPhoto, superfish, apprise, formToWizard, tumb, plugins) | 8 | Plantilla del área de torneos | Ídem |
| `refs\js\Graficos\` | 34 | Highcharts 3/4 (gráficas de estadísticas/rankings) | Gráficas SVG propias o una librería ligera (decidir en fase 2) |
| `refs\flexslider\` | 6 | FlexSlider | CSS/JS propio |
| `API\*\aspJSON1.17.asp`, `API\*\json2.asp`, `gestion\json2.asp`, `gestion\JSON_2.0.4.asp`, `include\JSON_204.asp`, `include\JSON_UTIL_011.asp` | 8 | Serializadores JSON para ASP (aspJSON, json2, JSON_2.0.4) | `System.Text.Json` |
| `include\MD5.asp`, `sha1.asp`, `sha1.txt` | 3 | Hash MD5/SHA1 en VBScript | `System.Security.Cryptography` (y nada de MD5/SHA1 para contraseñas) |

Notas:

- Hay **tres editores distintos** y tres copias de CKEditor: en el back-office nuevo se usa uno solo.
- La API antigua serializa JSON a mano con `Response.Write` en muchos endpoints; aspJSON/json2 solo
  se usan en parte de ellos.
- El código de las librerías no se ha revisado más allá de identificarlo.

## Servicios externos incrustados en el front nuevo

| Servicio | Página | Cómo se incrusta | A tener en cuenta |
|---|---|---|---|
| Google Calendar (calendario de reservas de Zoom) | `federaciones.html` (de `Federaciones.asp`) | `iframe` con `title`, `loading="lazy"`; URL de `https://calendar.google.com/calendar/embed` validada en API y front | CSP: `frame-src https://calendar.google.com`; Google puede fijar cookies propias (política de cookies) |
