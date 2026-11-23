# Genera src/FEP.Web/wwwroot/mock/federacion-federaciones-autonomicas.json a partir del contenido de la
# API (src/FEP.Api/Contenidos/federaciones-autonomicas.json), con la misma forma que devuelve
# GET /api/v1/federacion/federaciones-autonomicas:
#   - banderas e imagen de Zoom -> copias locales en assets/img/fep/federaciones/ (prototipo sin conexion)
#   - webs -> se antepone https:// si faltara el esquema (como Texto.EnlaceExterno en la API)
#   - calendario -> solo si es de https://calendar.google.com/ (como en la API)
# Despues regenera mocks.js. Ejecutar cada vez que se edite el contenido:
#   powershell -NoProfile -File tools\generar-mock-federaciones-autonomicas.ps1
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$origen = Join-Path $raiz 'src\FEP.Api\Contenidos\federaciones-autonomicas.json'
$destino = Join-Path $raiz 'src\FEP.Web\wwwroot\mock\federacion-federaciones-autonomicas.json'
$imgLocal = 'assets/img/fep/federaciones'
$utf8 = New-Object Text.UTF8Encoding($false)

# images/6.jpg -> bandera-6.jpg (Canarias usa images/18b.jpg -> bandera-18.jpg)
function ImagenLocal($federacion) {
  if ([string]::IsNullOrWhiteSpace($federacion.imagen)) { return $null }
  return "$imgLocal/bandera-$($federacion.id).jpg"
}
function Web([string]$web) {
  if ([string]::IsNullOrWhiteSpace($web)) { return $null }
  $web = $web.Trim()
  if ($web -match '^https?://') { return $web }
  return "https://$web"
}

$datos = ConvertFrom-Json ([IO.File]::ReadAllText($origen, $utf8))
foreach ($f in $datos.federaciones) {
  $f.imagenUrl = ImagenLocal $f
  $f.web = Web $f.web
  $ruta = Join-Path $raiz ("src\FEP.Web\wwwroot\" + $f.imagenUrl.Replace('/', '\'))
  if (-not (Test-Path $ruta)) { throw "Falta la imagen local $ruta (copiarla de X:\TICS\WEB\refs\$($f.imagen))" }
}
$zoom = $datos.reservaZoom
if ($zoom) {
  if ($zoom.imagen) { $zoom.imagenUrl = "$imgLocal/" + [IO.Path]::GetFileName($zoom.imagen).ToLowerInvariant() }
  if ($zoom.calendario -and $zoom.calendario.url -notlike 'https://calendar.google.com/*') { $zoom.calendario = $null }
}
[IO.File]::WriteAllText($destino, (ConvertTo-Json -InputObject $datos -Depth 10), $utf8)
Write-Output "Mock generado: $destino"
& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
