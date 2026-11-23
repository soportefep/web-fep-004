# Genera src/FEP.Web/wwwroot/mock/federacion-equipo.json a partir del contenido de la API
# (src/FEP.Api/Contenidos/equipo-fep.json), con la misma forma que devuelve
# GET /api/v1/federacion/equipo:
#   - imagen    -> se conserva la ruta original (images/...)
#   - imagenUrl -> copia local en assets/img/fep/equipo/ (para ver el prototipo sin conexion)
# Despues regenera mocks.js. Ejecutar cada vez que se edite el contenido:
#   powershell -NoProfile -File tools\generar-mock-equipo-fep.ps1
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$origen = Join-Path $raiz 'src\FEP.Api\Contenidos\equipo-fep.json'
$destino = Join-Path $raiz 'src\FEP.Web\wwwroot\mock\federacion-equipo.json'
$utf8 = New-Object Text.UTF8Encoding($false)

$baseRefs = 'https://www.padelfederacion.es/refs/'
$imagenesLocales = @{
  'images/ORGANIGRAMA-FEP.png' = 'assets/img/fep/equipo/organigrama-fep.png'
  'images/MENORES-FEP.png'     = 'assets/img/fep/equipo/menores-fep.png'
}
function UrlRefs([string]$ruta) {
  if ([string]::IsNullOrWhiteSpace($ruta)) { return $null }
  if ($ruta -match '^https?://') { return $ruta }
  $partes = $ruta.Replace('\', '/').Split('/') | Where-Object { $_ } | ForEach-Object { [Uri]::EscapeDataString($_) }
  return $baseRefs + ($partes -join '/')
}

$datos = ConvertFrom-Json ([IO.File]::ReadAllText($origen, $utf8))

# El grupo "junta-directiva" viene vacio: se rellena con la Junta Directiva de Organos de gobierno
# (igual que hace EquipoFepRepository en la API), para que haya una sola fuente.
$organos = ConvertFrom-Json ([IO.File]::ReadAllText((Join-Path $raiz 'src\FEP.Api\Contenidos\organos-gobierno.json'), $utf8))
$junta = @($organos.organos | Where-Object { $_.clave -eq 'junta-directiva' } | ForEach-Object { $_.grupos } |
  ForEach-Object { $_.miembros } | ForEach-Object { [pscustomobject]@{ nombre = $_.nombre; cargo = $_.cargo } })

foreach ($a in $datos.areas) {
  if ($a.imagen) {
    $a.imagenUrl = if ($imagenesLocales.ContainsKey($a.imagen)) { $imagenesLocales[$a.imagen] } else { UrlRefs $a.imagen }
  }
  $a.grupos = @($a.grupos | ForEach-Object {
    if ($_.clave -eq 'junta-directiva' -and @($_.miembros).Count -eq 0) { $_.miembros = $junta }
    $_ | Select-Object clave, titulo, miembros   # sin el campo interno "comentario"
  })
}
[IO.File]::WriteAllText($destino, (ConvertTo-Json -InputObject $datos -Depth 10), $utf8)
Write-Output "Mock generado: $destino"
& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
