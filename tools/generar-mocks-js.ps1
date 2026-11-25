# Genera src/FEP.Web/wwwroot/mock/mocks.js a partir de los mock/*.json.
# mocks.js permite ver el prototipo abriendo index.html directamente desde disco (file://),
# donde el navegador bloquea fetch(). Ejecutar cada vez que cambie un .json de mock:
#   powershell -NoProfile -File tools\generar-mocks-js.ps1
$ErrorActionPreference = 'Stop'
$mock = Join-Path $PSScriptRoot '..\src\FEP.Web\wwwroot\mock' | Resolve-Path
$utf8 = New-Object Text.UTF8Encoding($false)
$sb = New-Object Text.StringBuilder
[void]$sb.AppendLine('/* Generado por tools/generar-mocks-js.ps1 a partir de mock/*.json. NO EDITAR A MANO. */')
[void]$sb.AppendLine('window.FEP_MOCKS = {')
$ficheros = Get-ChildItem $mock -Filter *.json | Sort-Object Name
foreach ($f in $ficheros) {
  $json = [IO.File]::ReadAllText($f.FullName, $utf8).Trim()
  # Valida que es JSON correcto antes de incrustarlo
  $null = ConvertFrom-Json $json
  # Evita cerrar el <script> si algun texto contuviera "</"
  $json = $json -replace '</', '<\/'
  [void]$sb.AppendLine(('  "{0}": {1},' -f $f.BaseName, $json))
}
[void]$sb.AppendLine('};')
[IO.File]::WriteAllText((Join-Path $mock 'mocks.js'), $sb.ToString(), $utf8)
Write-Output "mocks.js generado con $($ficheros.Count) mocks"
