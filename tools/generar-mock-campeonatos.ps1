# Genera src/FEP.Web/wwwroot/mock/competicion-campeonatos.json con TODAS las temporadas, leyendo la web
# publica actual (POST /Campeonatos con ano=AAAA, sin filtros), con la misma forma que devuelve
# GET /api/v1/competicion/campeonatos (CampeonatosDto):
#   - temporadas: las del desplegable "Ano del Torneo"
#   - por torneo: tipo, categoria, seleccion, silla de ruedas, titulo, fechas, ciudad, clubes y plazo abierto
#   - enlace a la ficha: el de los cuadros (LinkCuadros) o, si no, la redireccion de Torneos_Previo.asp
#     (se reutilizan los enlaces ya resueltos del mock anterior para no repetir peticiones)
# Despues regenera mocks.js. Solo lectura sobre la web publica; no usa la BD ni credenciales.
#   powershell -NoProfile -File tools\generar-mock-campeonatos.ps1 [-Anios 2024,2023]
param([int[]]$Anios)
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$destino = Join-Path $raiz 'src\FEP.Web\wwwroot\mock\competicion-campeonatos.json'
$utf8 = New-Object Text.UTF8Encoding($false)
$cp1252 = [Text.Encoding]::GetEncoding(1252)
$web = 'https://www.padelfederacion.es/'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# HtmlDecode de .NET Framework no decodifica las entidades fuera del plano basico (emojis como &#128104;):
# se resuelven antes a mano.
function Html([string]$t) {
  $t = [regex]::Replace(($t -replace '<[^>]+>', ''), '&#(\d{5,7});', { param($m) [char]::ConvertFromUtf32([int]$m.Groups[1].Value) })
  [Net.WebUtility]::HtmlDecode($t).Trim()
}

function PedirCampeonatos([string]$ano) {
  $r = Invoke-WebRequest -Uri ($web + 'Campeonatos') -Method Post -UseBasicParsing -TimeoutSec 90 `
    -Body @{ ano = $ano; modalidad = '--'; tipo = '--' }
  $cp1252.GetString($r.RawContentStream.ToArray())
}

# Torneos_Previo.asp responde 302 a la ficha: se lee la cabecera Location sin seguirla.
function ResolverFicha([int]$id) {
  $req = [Net.HttpWebRequest]::Create($web + 'Torneos_Previo.asp')
  $req.Method = 'POST'; $req.AllowAutoRedirect = $false; $req.Timeout = 60000
  $req.ContentType = 'application/x-www-form-urlencoded'
  $cuerpo = [Text.Encoding]::ASCII.GetBytes("idtorneo=$id")
  $req.ContentLength = $cuerpo.Length
  $s = $req.GetRequestStream(); $s.Write($cuerpo, 0, $cuerpo.Length); $s.Close()
  try { $resp = $req.GetResponse() } catch [Net.WebException] { $resp = $_.Exception.Response }
  if (-not $resp) { return $null }
  try {
    $loc = $resp.Headers['Location']
    if (-not $loc) { return $null }
    return ([Uri]::new([Uri]$web, $loc)).AbsoluteUri
  } finally { $resp.Close() }
}

$MESES = @{ ENE = 1; FEB = 2; MAR = 3; ABR = 4; MAY = 5; JUN = 6; JUL = 7; AGO = 8; SEP = 9; OCT = 10; NOV = 11; DIC = 12 }
$MESES_LARGOS = @{ enero = 1; febrero = 2; marzo = 3; abril = 4; mayo = 5; junio = 6; julio = 7; agosto = 8; septiembre = 9; octubre = 10; noviembre = 11; diciembre = 12 }

# Enlaces ya resueltos en el mock anterior (id -> enlace)
$conocidos = @{}
if (Test-Path $destino) {
  foreach ($c in (ConvertFrom-Json ([IO.File]::ReadAllText($destino, $utf8))).items) {
    if ($c.id -gt 0 -and $c.enlace) { $conocidos[[int]$c.id] = $c.enlace }
  }
}

# 1) Temporadas del desplegable (pagina sin ano = la mas reciente)
$portada = PedirCampeonatos ''
$selAnio = [regex]::Match($portada, '(?s)<select name="ano".*?</select>').Value
$temporadas = @([regex]::Matches($selAnio, 'value="(\d{4})"') | ForEach-Object { [int]$_.Groups[1].Value })
if (-not $temporadas.Count) { throw 'No se han encontrado temporadas en la web.' }
$aLeer = if ($Anios) { $Anios } else { $temporadas }

# Si se piden solo algunas temporadas, se conservan las demas del mock anterior.
$items = New-Object Collections.ArrayList
if ($Anios -and (Test-Path $destino)) {
  foreach ($c in (ConvertFrom-Json ([IO.File]::ReadAllText($destino, $utf8))).items) {
    if ($Anios -notcontains [int]$c.anio) { [void]$items.Add($c) }
  }
}

$patron = '(?s)<div class="barratorneostipo3 [^"]*">(?<tipo>.*?)</div>\s*<div class="barratorneostipo2 [^"]*">(?<cat>.*?)</div>' +
  '.*?<div class="titulotorneo">\s*<a href="(?<href>[^"]*)"[^>]*>(?<titulo>.*?)</a>' +
  '.*?<div class="barrafechas">(?<fechas>.*?)</div>\s*<div class="barrafechas">(?<lugar>.*?)</div>' +
  '(?<resto>.*?)(?=<span class="solicitaclave2">|</form>)'

$resueltos = 0
foreach ($anio in $aLeer) {
  $html = PedirCampeonatos "$anio"
  $total = [int]([regex]::Match($html, 'Filtros del listado de Campeonatos <strong>\((\d+)\)').Groups[1].Value)
  $bloques = [regex]::Matches($html, $patron)
  foreach ($m in $bloques) {
    $tipoTxt = (Html $m.Groups['tipo'].Value).ToUpperInvariant()
    $catTxt = (Html $m.Groups['cat'].Value).ToUpperInvariant()
    $silla = $catTxt -eq 'SILLA RUEDAS'
    $seleccion = $catTxt -like 'SEL_*'
    $categoria = switch -Regex ($catTxt) {
      '^(SEL_)?ABS' { 'absoluto' } '^SILLA' { 'absoluto' } '^(SEL_)?MEN' { 'menores' } '^(SEL_)?VET' { 'veteranos' } default { $null }
    }
    $tipo = switch ($tipoTxt) { 'PAREJAS' { 'parejas' } 'EQUIPOS' { 'equipos' } default { $null } }

    # Fechas "22 NOV - 24 NOV" (el ano es el de la temporada; si termina en un mes anterior, al siguiente)
    $f = [regex]::Match((Html $m.Groups['fechas'].Value), '(\d{1,2})\s+([A-Z]{3})\s*-\s*(\d{1,2})\s+([A-Z]{3})')
    $ini = $fin = $null
    if ($f.Success) {
      $mi = $MESES[$f.Groups[2].Value]; $mf = $MESES[$f.Groups[4].Value]
      $ini = '{0:0000}-{1:00}-{2:00}' -f $anio, $mi, [int]$f.Groups[1].Value
      $fin = '{0:0000}-{1:00}-{2:00}' -f ($(if ($mf -lt $mi) { $anio + 1 } else { $anio })), $mf, [int]$f.Groups[3].Value
    }

    # "CIUDAD - CLUB" ("Sin definir" = sin club)
    $lugar = Html $m.Groups['lugar'].Value
    $p = $lugar.IndexOf(' - ')
    $ciudad = if ($p -ge 0) { $lugar.Substring(0, $p).Trim() } else { $lugar.TrimEnd(' ', '-') }
    $club = if ($p -ge 0) { $lugar.Substring($p + 3).Trim() } else { '' }
    $clubes = @(if ($club -and $club -ne 'Sin definir') { $club })

    # Enlace: cuadros externos o ficha (Torneos_Previo.asp)
    $href = $m.Groups['href'].Value
    $id = 0; $enlace = $null; $nueva = $false
    $ir = [regex]::Match($href, 'irCamp\((\d+)\)')
    if ($ir.Success) {
      $id = [int]$ir.Groups[1].Value
      if ($conocidos.ContainsKey($id)) { $enlace = $conocidos[$id] }
      else { $enlace = ResolverFicha $id; $resueltos++; Start-Sleep -Milliseconds 150 }
    } elseif ($href -and $href -ne '#') {
      $enlace = ($href -replace '\\', '/') -replace '^https?:/+', 'https://'
      $nueva = $true
    }

    # Plazo abierto (solo lo pinta la web si sigue vigente)
    $inscripcion = $null
    $ab = [regex]::Match($m.Groups['resto'].Value, '(?s)<div class="barraabierto">(.*?)</div>')
    if ($ab.Success) {
      $txt = Html $ab.Groups[1].Value
      $d = [regex]::Match($txt, '(\d{1,2}) de (\w+) de (\d{4})\s+(\d{1,2}):(\d{2})')
      if ($d.Success) {
        $tipoPlazo = if ($txt -match 'Pre-Inscripci') { 'preinscripcion' } elseif ($txt -match 'Inscripci\S* Equipos') { 'inscripcion-equipos' } else { 'inscripcion' }
        $inscripcion = [ordered]@{
          tipo        = $tipoPlazo
          fechaLimite = '{0}-{1:00}-{2:00}T{3:00}:{4}:00' -f $d.Groups[3].Value, $MESES_LARGOS[$d.Groups[2].Value.ToLower()], [int]$d.Groups[1].Value, [int]$d.Groups[4].Value, $d.Groups[5].Value
        }
      }
    }

    [void]$items.Add([ordered]@{
      id = $id; anio = [int]$anio; titulo = Html $m.Groups['titulo'].Value
      tipo = $tipo; categoria = $categoria; seleccion = $seleccion; sillaRuedas = $silla
      fechaInicio = $ini; fechaFin = $fin; ciudad = $ciudad; clubes = $clubes
      enlace = $enlace; nuevaVentana = $nueva; inscripcion = $inscripcion
    })
  }
  $aviso = if ($bloques.Count -ne $total) { "  <-- la web dice $total" } else { '' }
  Write-Host ("{0}: {1} torneos{2}" -f $anio, $bloques.Count, $aviso)
}

# Orden de la API: temporada mas reciente primero y, dentro, por fecha de inicio descendente (estable)
$ordenados = @($items | Sort-Object -Property @{ Expression = { [int]$_.anio }; Descending = $true }, @{ Expression = { [string]$_.fechaInicio }; Descending = $true })

$salida = [ordered]@{ anio = $temporadas[0]; anios = $temporadas; tipo = $null; categoria = $null; items = $ordenados }
[IO.File]::WriteAllText($destino, ($salida | ConvertTo-Json -Depth 6), $utf8)
Write-Host ("Escrito {0}: {1} temporadas, {2} torneos ({3} fichas resueltas en Torneos_Previo.asp)" -f $destino, $temporadas.Count, $ordenados.Count, $resueltos)

& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
