# Actualiza src/FEP.Web/wwwroot/mock/competicion-campeonatos-equipos-{id}.json (misma forma que devuelve
# GET /api/v1/competicion/campeonatos-equipos/{id}) con la parte que sale de src/FEP.Api/Contenidos/campeonatos-equipos.json:
#   - videos            -> retransmisiones del torneo (en el ASP, escritas a mano para el 28133)
#   - titulos de cuadro -> nombresCuadros fijados a mano para el torneo (genero + nivel + cuadro)
# El resto de cada mock sale de la BD: es una instantanea de la web publica del 13/11/2026 (torneos 28133, 28676,
# 28854 y 30148; ver docs/api/analisis/Camp_Equipos.md) y se CONSERVA tal cual.
# Despues regenera mocks.js. Ejecutar cada vez que se edite el contenido:
#   powershell -NoProfile -File tools\generar-mock-campeonatos-equipos.ps1
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$origen = Join-Path $raiz 'src\FEP.Api\Contenidos\campeonatos-equipos.json'
$carpeta = Join-Path $raiz 'src\FEP.Web\wwwroot\mock'
$utf8 = New-Object Text.UTF8Encoding($false)

$contenido = ConvertFrom-Json ([IO.File]::ReadAllText($origen, $utf8))
$generos = @{ 'masculino' = 'M'; 'femenino' = 'F'; 'mixto' = 'X' }

$ficheros = Get-ChildItem $carpeta -Filter 'competicion-campeonatos-equipos-*.json' | Sort-Object Name
foreach ($f in $ficheros) {
  $mock = ConvertFrom-Json ([IO.File]::ReadAllText($f.FullName, $utf8))
  $id = [int]$mock.id

  # Igual que ContenidoCampeonatosEquiposRepository: solo vídeos insertables de YouTube (https, /embed/).
  $mock.videos = @($contenido.videos | Where-Object {
      $_.torneo -eq $id -and $_.titulo -and $_.url -match '^https://(www\.)?youtube(-nocookie)?\.com/embed/'
    } | ForEach-Object { [pscustomobject][ordered]@{ titulo = $_.titulo.Trim(); url = $_.url } })

  # Nombres de cuadro fijados a mano (tienen prioridad sobre el de la BD y el fijo del ASP).
  foreach ($n in @($contenido.nombresCuadros | Where-Object { $_.torneo -eq $id })) {
    foreach ($g in @($mock.cuadros | Where-Object { $generos[$_.genero] -eq $n.genero.ToUpper() })) {
      foreach ($c in @($g.cuadros | Where-Object { $_.nivel -eq $n.nivel -and $_.cuadro -eq $n.cuadro })) { $c.titulo = $n.titulo.Trim() }
    }
  }

  [IO.File]::WriteAllText($f.FullName, (ConvertTo-Json -InputObject $mock -Depth 10), $utf8)
  Write-Output "Mock actualizado: $($f.Name) ($($mock.videos.Count) videos)"
}
& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
