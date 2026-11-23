# Genera src/FEP.Web/wwwroot/mock/competicion-rankings-<clave>-normativa.json a partir del contenido de la API
# (src/FEP.Api/Contenidos/rankings/<clave>.json), con la misma forma que devuelve
# GET /api/v1/competicion/rankings/<clave>/normativa:
#   - clave = nombre del fichero
#   - cada bloque con todas sus propiedades: texto (null si no tiene), elementos ([] si no es lista), tabla (null)
#   - destacado null en los elementos que no lo traen; columnas/filas de tabla nunca null
# Procesa todos los ficheros de la carpeta (hoy solo absoluto).
# Despues regenera mocks.js. Ejecutar cada vez que se edite el contenido:
#   powershell -NoProfile -File tools\generar-mock-rankings-normativa.ps1
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$carpeta = Join-Path $raiz 'src\FEP.Api\Contenidos\rankings'
$mock = Join-Path $raiz 'src\FEP.Web\wwwroot\mock'
$utf8 = New-Object Text.UTF8Encoding($false)

# La coma evita que PowerShell desenrolle el array al devolverlo (vacio -> $null, uno -> escalar)
function Lista($valor) { if ($null -eq $valor) { return ,@() } return ,@($valor) }
function Asegura($objeto, [string]$nombre, $valor) {
  if ($objeto.PSObject.Properties[$nombre]) { $objeto.$nombre = $valor }
  else { $objeto | Add-Member -NotePropertyName $nombre -NotePropertyValue $valor }
}

foreach ($f in Get-ChildItem $carpeta -Filter *.json | Sort-Object Name) {
  $clave = $f.BaseName.ToLowerInvariant()
  $datos = ConvertFrom-Json ([IO.File]::ReadAllText($f.FullName, $utf8))
  Asegura $datos 'clave' $clave
  if (-not $datos.PSObject.Properties['resumen']) { Asegura $datos 'resumen' $null }
  foreach ($s in (Lista $datos.secciones)) {
    foreach ($b in (Lista $s.bloques)) {
      if (-not $b.PSObject.Properties['texto']) { Asegura $b 'texto' $null }
      foreach ($e in (Lista $b.elementos)) {
        if (-not $e.PSObject.Properties['destacado']) { Asegura $e 'destacado' $null }
      }
      Asegura $b 'elementos' (Lista $b.elementos)
      if ($b.PSObject.Properties['tabla'] -and $b.tabla) {
        Asegura $b.tabla 'columnas' (Lista $b.tabla.columnas)
        # Cada fila es un array: se conserva como array aunque tenga una sola celda
        Asegura $b.tabla 'filas' @((Lista $b.tabla.filas) | ForEach-Object { ,@($_) })
      } else {
        Asegura $b 'tabla' $null
      }
    }
    Asegura $s 'bloques' (Lista $s.bloques)
  }
  Asegura $datos 'secciones' (Lista $datos.secciones)

  $destino = Join-Path $mock "competicion-rankings-$clave-normativa.json"
  [IO.File]::WriteAllText($destino, (ConvertTo-Json -InputObject $datos -Depth 12), $utf8)
  Write-Output "Mock generado: $destino"
}
& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
