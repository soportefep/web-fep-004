# Genera src/FEP.Web/wwwroot/mock/competicion-programa-impulso.json con la misma forma que devuelve
# GET /api/v1/competicion/programa-impulso:
#   - presentacion, imagen y documentos -> de src/FEP.Api/Contenidos/programa-impulso.json
#       - imagen    -> se conserva la ruta original (Noticias/...)
#       - imagenUrl -> copia local en assets/img/fep/programa-impulso/ (prototipo sin conexion)
#       - documentos.url -> URL absoluta y codificada en https://www.padelfederacion.es/refs/
#   - anio, anios, items (torneos) -> salen de la BD: se CONSERVAN del mock actual (instantanea de la web
#     publica del 13/11/2026, ver docs/api/analisis/Impulso.md). No se tocan aqui.
# Despues regenera mocks.js. Ejecutar cada vez que se edite el contenido:
#   powershell -NoProfile -File tools\generar-mock-programa-impulso.ps1
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$origen = Join-Path $raiz 'src\FEP.Api\Contenidos\programa-impulso.json'
$destino = Join-Path $raiz 'src\FEP.Web\wwwroot\mock\competicion-programa-impulso.json'
$utf8 = New-Object Text.UTF8Encoding($false)

$baseRefs = 'https://www.padelfederacion.es/refs/'
$imagenesLocales = @{
  'Noticias/Impulso2017.jpg' = 'assets/img/fep/programa-impulso/impulso-2017.jpg'
}
function UrlRefs([string]$ruta) {
  if ([string]::IsNullOrWhiteSpace($ruta)) { return $null }
  if ($ruta -match '^https?://') { return $ruta }
  $partes = $ruta.Replace('\', '/').Split('/') | Where-Object { $_ } | ForEach-Object { [Uri]::EscapeDataString($_) }
  return $baseRefs + ($partes -join '/')
}

$datos = ConvertFrom-Json ([IO.File]::ReadAllText($origen, $utf8))

# Torneos del mock actual (si no existe, vacio: el front mostrara "no hay torneos")
$anio = $null; $anios = @(); $items = @()
if (Test-Path $destino) {
  $actual = ConvertFrom-Json ([IO.File]::ReadAllText($destino, $utf8))
  $anio = $actual.anio
  $anios = @($actual.anios)
  $items = @($actual.items)
}

$imagenUrl = if ($datos.imagen -and $imagenesLocales.ContainsKey($datos.imagen)) { $imagenesLocales[$datos.imagen] } else { UrlRefs $datos.imagen }
$documentos = @($datos.documentos | ForEach-Object {
  [pscustomobject][ordered]@{
    clave       = $_.clave
    titulo      = $_.titulo
    descripcion = $_.descripcion
    temporada   = $_.temporada
    fecha       = $_.fecha
    tipo        = $_.tipo
    archivo     = $_.archivo
    url         = UrlRefs $_.archivo
  }
})

# Mismo orden de propiedades que ProgramaImpulsoDto
$mock = [pscustomobject][ordered]@{
  actualizado  = $datos.actualizado
  titulo       = $datos.titulo
  introduccion = @($datos.introduccion)
  imagen       = $datos.imagen
  imagenUrl    = $imagenUrl
  imagenAlt    = $datos.imagenAlt
  documentos   = $documentos
  anio         = $anio
  anios        = $anios
  items        = $items
}
[IO.File]::WriteAllText($destino, (ConvertTo-Json -InputObject $mock -Depth 10), $utf8)
Write-Output "Mock generado: $destino ($($items.Count) torneos conservados)"
& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
