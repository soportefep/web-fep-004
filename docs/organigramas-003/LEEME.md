# Organigramas oficiales en imagen (prototipo 003)

Fuentes HTML de las 4 láminas que enlazan "Ver el organigrama oficial en imagen" en
`organos-de-gobierno.html` y `equipo-fep.html`. Estilo del prototipo 003 (Archivo condensada,
negro / rojo FEP / azul pista, esquinas rectas), 1920×1080.

| Fuente | Imagen publicada |
|---|---|
| `junta.html` | `src/FEP.Web/wwwroot/assets/img/fep/organos/organigrama-junta.jpg` |
| `ctap.html` | `src/FEP.Web/wwwroot/assets/img/fep/organos/ctap-fep.png` |
| `organigrama.html` | `src/FEP.Web/wwwroot/assets/img/fep/equipo/organigrama-fep.png` |
| `menores.html` | `src/FEP.Web/wwwroot/assets/img/fep/equipo/menores-fep.png` |

Los nombres y cargos salen de `mock/federacion-organos-gobierno.json` y `mock/federacion-equipo.json`.
Si cambian, hay que actualizar también el HTML de la lámina y volver a generarla.

Las imágenes originales de la FEP están en `docs/organigramas-originales/`.

## Volver a generar una lámina

Desde esta carpeta (necesita conexión para cargar la fuente Archivo de Google Fonts):

```powershell
& "C:\Program Files\Google\Chrome\Application\chrome.exe" --headless=new --disable-gpu --hide-scrollbars `
  --force-device-scale-factor=1 --window-size=1920,1080 --virtual-time-budget=6000 `
  --screenshot="$PWD\ctap.png" "file:///$PWD/ctap.html"
```

La Junta Directiva se publica en JPG (`organigrama-junta.jpg`): convertir el PNG antes de copiarlo.
