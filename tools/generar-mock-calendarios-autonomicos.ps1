# Genera src/FEP.Web/wwwroot/mock/competicion-calendarios-autonomicos-<categoria>.json a partir del contenido
# de la API (src/FEP.Api/Contenidos/calendarios-autonomicos/<categoria>.json), con la misma forma que devuelve
# GET /api/v1/competicion/calendarios-autonomicos/<categoria>:
#   - categoria = nombre del fichero
#   - listas que el fichero omite (introduccion, federaciones, pruebas, fases, momentos) -> vacias, nunca null
#   - fechaFin vacia -> fechaInicio (pruebas y fases de un dia); momentos ordenados por "orden"
# Procesa todos los ficheros de la carpeta (menores y veteranos).
# Despues regenera mocks.js. Ejecutar cada vez que se edite el contenido:
#   powershell -NoProfile -File tools\generar-mock-calendarios-autonomicos.ps1
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$carpeta = Join-Path $raiz 'src\FEP.Api\Contenidos\calendarios-autonomicos'
$mock = Join-Path $raiz 'src\FEP.Web\wwwroot\mock'
$utf8 = New-Object Text.UTF8Encoding($false)

# La coma evita que PowerShell desenrolle el array al devolverlo (vacio -> $null, uno -> escalar)
function Lista($valor) { if ($null -eq $valor) { return ,@() } return ,@($valor) }
function Asegura($objeto, [string]$nombre, $valor) {
  if ($objeto.PSObject.Properties[$nombre]) { $objeto.$nombre = $valor }
  else { $objeto | Add-Member -NotePropertyName $nombre -NotePropertyValue $valor }
}

foreach ($f in Get-ChildItem $carpeta -Filter *.json | Sort-Object Name) {
  $categoria = $f.BaseName.ToLowerInvariant()
  $datos = ConvertFrom-Json ([IO.File]::ReadAllText($f.FullName, $utf8))
  Asegura $datos 'categoria' $categoria
  Asegura $datos 'introduccion' (Lista $datos.introduccion)
  if ($datos.actualizacionesRanking) {
    Asegura $datos.actualizacionesRanking 'momentos' @($datos.actualizacionesRanking.momentos | Where-Object { $_ } | Sort-Object { [int]$_.orden })
  }
  foreach ($fed in (Lista $datos.federaciones)) {
    foreach ($p in (Lista $fed.pruebas)) {
      if (-not $p.fechaFin) { Asegura $p 'fechaFin' $p.fechaInicio }
      foreach ($fase in (Lista $p.fases)) { if (-not $fase.fechaFin) { Asegura $fase 'fechaFin' $fase.fechaInicio } }
      Asegura $p 'fases' (Lista $p.fases)
    }
    Asegura $fed 'pruebas' (Lista $fed.pruebas)
  }
  Asegura $datos 'federaciones' (Lista $datos.federaciones)

  $destino = Join-Path $mock "competicion-calendarios-autonomicos-$categoria.json"
  [IO.File]::WriteAllText($destino, (ConvertTo-Json -InputObject $datos -Depth 12), $utf8)
  Write-Output "Mock generado: $destino"
}
& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
