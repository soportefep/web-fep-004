# Genera src/FEP.Web/wwwroot/mock/federacion-organos-gobierno.json a partir del contenido de la API
# (src/FEP.Api/Contenidos/organos-gobierno.json), con la misma forma que devuelve
# GET /api/v1/federacion/organos-gobierno:
#   - imagenes -> copias locales en assets/img/fep/organos/ (para ver el prototipo sin conexion)
#   - actas (docs/...) -> URL absoluta en https://www.padelfederacion.es/refs/
#   - actas ordenadas de la mas reciente a la mas antigua
# Despues regenera mocks.js. Ejecutar cada vez que se edite el contenido:
#   powershell -NoProfile -File tools\generar-mock-organos-gobierno.ps1
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$origen = Join-Path $raiz 'src\FEP.Api\Contenidos\organos-gobierno.json'
$destino = Join-Path $raiz 'src\FEP.Web\wwwroot\mock\federacion-organos-gobierno.json'
$utf8 = New-Object Text.UTF8Encoding($false)

$baseRefs = 'https://www.padelfederacion.es/refs/'
$imagenesLocales = @{
  'images/ORGANIGRAMA JUNTA-V2.jpg' = 'assets/img/fep/organos/organigrama-junta.jpg'
  'images/CTAP-FEP.png'             = 'assets/img/fep/organos/ctap-fep.png'
}
function UrlRefs([string]$ruta) {
  if ([string]::IsNullOrWhiteSpace($ruta)) { return $null }
  if ($ruta -match '^https?://') { return $ruta }
  $partes = $ruta.Replace('\', '/').Split('/') | Where-Object { $_ } | ForEach-Object { [Uri]::EscapeDataString($_) }
  return $baseRefs + ($partes -join '/')
}

$datos = ConvertFrom-Json ([IO.File]::ReadAllText($origen, $utf8))
foreach ($o in $datos.organos) {
  if ($o.imagenUrl) {
    $o.imagenUrl = if ($imagenesLocales.ContainsKey($o.imagenUrl)) { $imagenesLocales[$o.imagenUrl] } else { UrlRefs $o.imagenUrl }
  }
  # Orden por fecha descendente; a igual fecha se respeta el orden del fichero (PowerShell 5.1 no tiene -Stable)
  $i = 0
  $actas = @($o.actas | Where-Object { $_ } | ForEach-Object { $_.url = UrlRefs $_.url; [pscustomobject]@{ acta = $_; pos = $i++ } } |
    Sort-Object -Property @{ Expression = { $_.acta.fecha }; Descending = $true }, @{ Expression = { $_.pos }; Descending = $false } |
    ForEach-Object { $_.acta })
  $o.actas = $actas
}
[IO.File]::WriteAllText($destino, (ConvertTo-Json -InputObject $datos -Depth 10), $utf8)
Write-Output "Mock generado: $destino"
& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
