# Genera los mocks del calendario de las federaciones territoriales (CalendarioFT.asp), con la misma forma que
# devuelve la API:
#   - src/FEP.Web/wwwroot/mock/competicion-calendario-federaciones.json
#       GET /api/v1/competicion/calendario/federaciones (FederacionCalendarioTerritorialDto[])
#   - src/FEP.Web/wwwroot/mock/competicion-calendario-federaciones-{id}.json (una por federacion)
#       GET /api/v1/competicion/calendario/federaciones/{id} (CalendarioTerritorialDto), con TODAS sus temporadas
# Origen (solo lectura; no usa la BD):
#   - federaciones: mock/clubes-filtros.json (misma consulta a SFEP_Federaciones, IdFederacion 1-22, por
#     Abreviatura) + territorio y nombre oficial de src/FEP.Api/Contenidos/federaciones-autonomicas.json
#   - pruebas: instantanea de la web publica (GET /CalendarioFT?fed=N&ano=AAAA; el ASP lee fed y ano tambien de la
#     query) para cada temporada del desplegable "Ano del Torneo" (son las temporadas de la FEP: el ASP no
#     consultaba las de la federacion). Una temporada de la federacion = una con alguna prueba.
#   - enlace de cada prueba: la ficha a la que redirige Torneos_Previo.asp (el ASP enviaba a Campeonatos_Previo.asp,
#     que no existe); se reutilizan los ya resueltos en estos mocks, en competicion-calendario.json y en
#     competicion-campeonatos.json
#   - documentos: la pagina no los mostraba para las federaciones territoriales -> actualizado/archivo/url null
#   - anio: la temporada por defecto de la API (ReglasCalendario.TemporadaPorDefecto con el ano en curso)
# Despues regenera mocks.js:
#   powershell -NoProfile -File tools\generar-mock-calendario-ft.ps1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'nombre-propio.ps1')   # sede en formato "Nombre Propio", como la API
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$mockDir = Join-Path $raiz 'src\FEP.Web\wwwroot\mock'
$contenidoFed = Join-Path $raiz 'src\FEP.Api\Contenidos\federaciones-autonomicas.json'
$filtrosClubes = Join-Path $mockDir 'clubes-filtros.json'
$utf8 = New-Object Text.UTF8Encoding($false)
$cp1252 = [Text.Encoding]::GetEncoding(1252)
$web = 'https://www.padelfederacion.es/'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'

# HtmlDecode de .NET Framework no decodifica las entidades fuera del plano basico: se resuelven antes a mano.
function Html([string]$t) {
  $t = [regex]::Replace(($t -replace '<[^>]+>', ''), '&#(\d{5,7});', { param($m) [char]::ConvertFromUtf32([int]$m.Groups[1].Value) })
  ([Net.WebUtility]::HtmlDecode($t) -replace '[\s\u00A0]+', ' ').Trim()
}

function Pedir([string]$query) {
  $url = $web + 'CalendarioFT' + $(if ($query) { "?$query" } else { '' })
  for ($intento = 1; $intento -le 3; $intento++) {
    try {
      $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 120
      return $cp1252.GetString($r.RawContentStream.ToArray())
    } catch {
      if ($intento -eq 3) { throw }
      Start-Sleep -Seconds 3
    }
  }
}

# Torneos_Previo.asp responde 302 a la ficha: se lee la cabecera Location sin seguirla.
function ResolverFicha([int]$id) {
  $req = [Net.HttpWebRequest]::Create($web + "Torneos_Previo.asp?IdTorneo=$id")
  $req.AllowAutoRedirect = $false; $req.Timeout = 60000
  try { $resp = $req.GetResponse() } catch [Net.WebException] { $resp = $_.Exception.Response }
  if (-not $resp) { return $null }
  try {
    $loc = $resp.Headers['Location']
    if (-not $loc) { return $null }
    return ([Uri]::new([Uri]$web, $loc)).AbsoluteUri
  } finally { $resp.Close() }
}

# Misma regla que ReglasCalendario.TemporadaPorDefecto
function TemporadaPorDefecto([int[]]$anios, [int]$actual) {
  if (-not $anios.Count) { return $null }
  if ($anios -contains $actual) { return $actual }
  $anteriores = @($anios | Where-Object { $_ -lt $actual })
  if ($anteriores.Count) { return ($anteriores | Measure-Object -Maximum).Maximum }
  return ($anios | Measure-Object -Minimum).Minimum
}

$MESES = @{ ENE = 1; FEB = 2; MAR = 3; ABR = 4; MAY = 5; JUN = 6; JUL = 7; AGO = 8; SEP = 9; OCT = 10; NOV = 11; DIC = 12 }

# Enlaces ya resueltos (id -> enlace, nuevaVentana)
$conocidos = @{}
$previos = @(Get-ChildItem $mockDir -Filter 'competicion-calendario*.json') + @(Get-Item (Join-Path $mockDir 'competicion-campeonatos.json'))
foreach ($f in $previos) {
  $j = ConvertFrom-Json ([IO.File]::ReadAllText($f.FullName, $utf8))
  $lista = if ($j -is [array]) { @() } else { @($j.items) }
  foreach ($c in $lista) {
    if (-not $c) { continue }
    $id = if ($null -ne $c.idTorneo) { $c.idTorneo } else { $c.id }
    if ($id -gt 0 -and $c.enlace) { $conocidos[[int]$id] = @{ enlace = $c.enlace; nueva = [bool]$c.nuevaVentana } }
  }
}

# 1) Federaciones (SFEP_Federaciones, por Abreviatura) con su territorio y nombre oficial
$territorios = @{}
foreach ($f in (ConvertFrom-Json ([IO.File]::ReadAllText($contenidoFed, $utf8))).federaciones) {
  $territorios[[int]$f.id] = $f
}
$federaciones = @((ConvertFrom-Json ([IO.File]::ReadAllText($filtrosClubes, $utf8))).federaciones |
  Where-Object { $_.id -ge 1 -and $_.id -le 22 })   # DECISION FEP: 22 federaciones (1-22)
if (-not $federaciones.Count) { throw 'No hay federaciones en clubes-filtros.json' }

# 2) Temporadas del desplegable (las de la FEP)
$portada = Pedir ''
$sel = [regex]::Match($portada, '(?s)<select name="ano".*?</select>').Value
$anios = @([regex]::Matches($sel, 'value="(\d{4})"') | ForEach-Object { [int]$_.Groups[1].Value })
if (-not $anios.Count) { throw 'No se han encontrado temporadas en la web.' }
$anioActual = (Get-Date).Year

$listado = New-Object Collections.ArrayList
$resueltos = 0
foreach ($fed in $federaciones) {
  $id = [int]$fed.id
  $t = $territorios[$id]
  $resumen = [ordered]@{
    id          = $id
    territorio  = $(if ($t) { $t.territorio } else { $fed.nombre })
    nombre      = $(if ($t) { $t.nombre } else { $null })
    abreviatura = $fed.nombre
    temporadas  = @()
  }

  $items = New-Object Collections.ArrayList
  $conPruebas = New-Object Collections.ArrayList
  foreach ($anio in $anios) {
    $html = Pedir "fed=$id&ano=$anio"
    $i = $html.IndexOf('id="formCamp"'); $j = $html.IndexOf('</form>', $i)
    $tabla = $html.Substring($i, $j - $i)
    $n = 0
    foreach ($tr in [regex]::Matches($tabla, '(?s)<tr[^>]*>(.*?)</tr>')) {
      $c = $tr.Groups[1].Value
      $tds = [regex]::Matches($c, '(?s)<td[^>]*class="pielineasPPT tamletra10"[^>]*>(.*?)</td>')
      if ($tds.Count -ne 5) { continue }
      $v = @($tds | ForEach-Object { Html $_.Groups[1].Value })
      if (-not $v[2]) { continue }

      $ini = $fin = $null
      $mi = [regex]::Match($v[0], '^(\d{1,2}) (\w{3})$'); $mf = [regex]::Match($v[1], '^(\d{1,2}) (\w{3})$')
      if ($mi.Success) {
        $m1 = $MESES[$mi.Groups[2].Value.ToUpper()]
        $ini = '{0:0000}-{1:00}-{2:00}' -f $anio, $m1, [int]$mi.Groups[1].Value
        if ($mf.Success) {
          $m2 = $MESES[$mf.Groups[2].Value.ToUpper()]
          $fin = '{0:0000}-{1:00}-{2:00}' -f ($(if ($m2 -lt $m1) { $anio + 1 } else { $anio })), $m2, [int]$mf.Groups[1].Value
        }
      }
      $porConfirmar = (-not $ini) -or ($ini -like '*-01-01' -and $fin -eq ('{0}-12-31' -f $ini.Substring(0, 4)))

      $idTorneo = $null; $enlace = $null; $nueva = $false
      $ir = [regex]::Match($c, 'irCamp\((\d+)\)')
      if ($ir.Success) {
        $idTorneo = [int]$ir.Groups[1].Value
        if ($conocidos.ContainsKey($idTorneo)) { $enlace = $conocidos[$idTorneo].enlace; $nueva = $conocidos[$idTorneo].nueva }
        else {
          $enlace = ResolverFicha $idTorneo; $resueltos++; Start-Sleep -Milliseconds 100
          $conocidos[$idTorneo] = @{ enlace = $enlace; nueva = $false }
        }
        # Torneo inexistente o borrado: la API no lo enlaza (OUTER APPLY sin fila)
        if (-not $enlace) { $idTorneo = $null }
      }

      [void]$items.Add([ordered]@{
        anio = [int]$anio; fechaInicio = $ini; fechaFin = $fin; fechaPorConfirmar = [bool]$porConfirmar
        titulo = $v[2]; categoria = $(if ($v[3]) { $v[3] } else { $null }); sede = $(NombrePropio $v[4])
        idTorneo = $idTorneo; enlace = $enlace; nuevaVentana = $nueva
      })
      $n++
    }
    if ($n) {
      [void]$conPruebas.Add([int]$anio)
      $total = [regex]::Match($html, 'Datos del Calendario<strong> \((\d+)\)').Groups[1].Value
      $aviso = if ($total -and [int]$total -ne $n) { "  (la web dice ${total}: cuenta tambien las no visibles)" } else { '' }
      Write-Host ("{0} ({1}) {2}: {3} pruebas{4}" -f $resumen.abreviatura, $id, $anio, $n, $aviso)
    }
    Start-Sleep -Milliseconds 100
  }

  $resumen.temporadas = @($conPruebas | Sort-Object -Descending)
  [void]$listado.Add($resumen)

  # Orden de la API: temporada mas reciente primero y, dentro, por fecha (sin fecha al final), fin y titulo
  $ordenados = @($items | Sort-Object -Property `
    @{ Expression = { [int]$_.anio }; Descending = $true },
    @{ Expression = { if ($_.fechaInicio) { 0 } else { 1 } } },
    @{ Expression = { [string]$_.fechaInicio } },
    @{ Expression = { [string]$_.fechaFin } },
    @{ Expression = { [string]$_.titulo } })

  $temporadas = @($resumen.temporadas | ForEach-Object { [ordered]@{ anio = $_; actualizado = $null; archivo = $null; url = $null } })
  # Mismo orden de propiedades que CalendarioTerritorialDto
  $salida = [ordered]@{
    federacion = $resumen
    anio       = $(TemporadaPorDefecto $resumen.temporadas $anioActual)
    temporadas = $temporadas
    items      = $ordenados
  }
  $destino = Join-Path $mockDir "competicion-calendario-federaciones-$id.json"
  # Compacto: los mocks de federaciones grandes (Andalucia) pesan mucho con sangria y mocks.js los incrusta todos
  [IO.File]::WriteAllText($destino, ($salida | ConvertTo-Json -Depth 6 -Compress), $utf8)
  Write-Host ("Escrito {0}: {1} temporadas, {2} pruebas" -f (Split-Path $destino -Leaf), $temporadas.Count, $ordenados.Count)
}

$destinoListado = Join-Path $mockDir 'competicion-calendario-federaciones.json'
[IO.File]::WriteAllText($destinoListado, (ConvertTo-Json -InputObject @($listado) -Depth 4), $utf8)
Write-Host ("Escrito {0}: {1} federaciones ({2} fichas resueltas en Torneos_Previo.asp)" -f (Split-Path $destinoListado -Leaf), $listado.Count, $resueltos)

& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
