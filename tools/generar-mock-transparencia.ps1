# Genera src/FEP.Web/wwwroot/mock/federacion-transparencia.json a partir del contenido de la API
# (src/FEP.Api/Contenidos/transparencia.json), con la misma forma que devuelve
# GET /api/v1/federacion/transparencia:
#   - documentos: se conserva la ruta original (archivo = docs/...) y se calcula la URL absoluta
#     en https://www.padelfederacion.es/refs/ (espacios codificados)
#   - las listas que un apartado no usa (parrafos, lista, enlaces, personas, importes, documentos)
#     se escriben vacias, como hace el repositorio de la API
# Despues regenera mocks.js. Ejecutar cada vez que se edite el contenido:
#   powershell -NoProfile -File tools\generar-mock-transparencia.ps1
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$origen = Join-Path $raiz 'src\FEP.Api\Contenidos\transparencia.json'
$destino = Join-Path $raiz 'src\FEP.Web\wwwroot\mock\federacion-transparencia.json'
$utf8 = New-Object Text.UTF8Encoding($false)

$baseRefs = 'https://www.padelfederacion.es/refs/'
function UrlRefs([string]$ruta) {
  if ([string]::IsNullOrWhiteSpace($ruta)) { return $null }
  if ($ruta -match '^https?://') { return $ruta }
  $partes = $ruta.Replace('\', '/').Split('/') | Where-Object { $_ } | ForEach-Object { [Uri]::EscapeDataString($_) }
  return $baseRefs + ($partes -join '/')
}
# @() con ',' para que ConvertTo-Json escriba [] y [x] (y no null o el objeto suelto)
function Lista($valor) { if ($null -eq $valor) { return ,@() } return ,@($valor) }

$datos = ConvertFrom-Json ([IO.File]::ReadAllText($origen, $utf8))
$categorias = foreach ($c in $datos.categorias) {
  $apartados = foreach ($a in $c.apartados) {
    [ordered]@{
      clave         = $a.clave
      titulo        = $a.titulo
      pendiente     = [bool]$a.pendiente
      actualizacion = $a.actualizacion
      parrafos      = Lista $a.parrafos
      lista         = Lista $a.lista
      enlaces       = Lista $a.enlaces
      personas      = Lista ($a.personas | Where-Object { $_ } | ForEach-Object {
                        [ordered]@{
                          nombre                = $_.nombre
                          cargo                 = $_.cargo
                          grupo                 = $_.grupo
                          funciones             = Lista $_.funciones
                          retribucionAnualBruta = $_.retribucionAnualBruta
                          nota                  = $_.nota
                        } })
      importes      = Lista $a.importes
      documentos    = Lista ($a.documentos | Where-Object { $_ } | ForEach-Object {
                        [ordered]@{
                          titulo      = $_.titulo
                          descripcion = $_.descripcion
                          periodo     = $_.periodo
                          archivo     = $_.archivo
                          url         = UrlRefs $_.archivo
                        } })
    }
  }
  [ordered]@{ clave = $c.clave; titulo = $c.titulo; descripcion = $c.descripcion; apartados = Lista $apartados }
}
$salida = [ordered]@{ actualizado = $datos.actualizado; categorias = Lista $categorias }
[IO.File]::WriteAllText($destino, (ConvertTo-Json -InputObject $salida -Depth 12), $utf8)
Write-Output "Mock generado: $destino"
& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
