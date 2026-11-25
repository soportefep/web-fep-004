# Genera src/FEP.Web/wwwroot/mock/competicion-calendario.json con la misma forma que devuelve
# GET /api/v1/competicion/calendario (CalendarioCompeticionDto), con TODAS las temporadas:
#   - temporadas, documentos, fecha de actualizacion y pruebas -> instantanea de la web publica actual
#     (GET /CalendarioFEP?ano=AAAA; el ASP lee "ano" tambien de la query). Solo lectura; no usa la BD.
#       - por prueba: fechas (dia y mes de la web + ano de la temporada), titulo, categoria, sede
#       - enlace: la ficha a la que redirige Torneos_Previo.asp (el ASP enviaba a Campeonatos_Previo.asp, que no
#         existe); se reutilizan los ya resueltos en este mock y en competicion-campeonatos.json
#   - licitacion -> de src/FEP.Api/Contenidos/calendario-fep.json, solo si la web muestra hoy el banner
#     (HabitaSolicitud = 'S'); imagenUrl apunta a la copia local en assets/img/fep/calendario-fep/
#   - anio -> la temporada que la web muestra sin parametro (la seleccionada en el desplegable)
# Despues regenera mocks.js:
#   powershell -NoProfile -File tools\generar-mock-calendario-fep.ps1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'nombre-propio.ps1')   # sede en formato "Nombre Propio", como la API
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$contenido =Join-Path $raiz 'src\FEP.Api\Contenidos\calendario-fep.json'
$destino = Join-Path $raiz 'src\FEP.Web\wwwroot\mock\competicion-calendario.json'
$campeonatos = Join-Path $raiz 'src\FEP.Web\wwwroot\mock\competicion-campeonatos.json'
$utf8 = New-Object Text.UTF8Encoding($false)
$cp1252 = [Text.Encoding]::GetEncoding(1252)
$web = 'https://www.padelfederacion.es/'
$baseRefs = 'https://www.padelfederacion.es/refs/'
$imagenesLocales = @{ 'images/Torneos2027.jpg' = 'assets/img/fep/calendario-fep/torneos-2027.jpg' }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'

function UrlRefs([string]$ruta) {
  if ([string]::IsNullOrWhiteSpace($ruta)) { return $null }
  $partes = $ruta.Replace('\', '/').Split('/') | Where-Object { $_ } | ForEach-Object { [Uri]::EscapeDataString($_) }
  return $baseRefs + ($partes -join '/')
}

# HtmlDecode de .NET Framework no decodifica las entidades fuera del plano basico: se resuelven antes a mano.
function Html([string]$t) {
  $t = [regex]::Replace(($t -replace '<[^>]+>', ''), '&#(\d{5,7});', { param($m) [char]::ConvertFromUtf32([int]$m.Groups[1].Value) })
  ([Net.WebUtility]::HtmlDecode($t) -replace '[\s\u00A0]+', ' ').Trim()
}

function Pedir([string]$ano) {
  $url = $web + 'CalendarioFEP' + $(if ($ano) { "?ano=$ano" } else { '' })
  $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 90
  $cp1252.GetString($r.RawContentStream.ToArray())
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

$MESES = @{ ENE = 1; FEB = 2; MAR = 3; ABR = 4; MAY = 5; JUN = 6; JUL = 7; AGO = 8; SEP = 9; OCT = 10; NOV = 11; DIC = 12 }
$MESES_LARGOS = @{ enero = 1; febrero = 2; marzo = 3; abril = 4; mayo = 5; junio = 6; julio = 7; agosto = 8; septiembre = 9; octubre = 10; noviembre = 11; diciembre = 12 }

# Enlaces ya resueltos (id -> enlace, nuevaVentana)
$conocidos = @{}
foreach ($f in @($campeonatos, $destino)) {
  if (-not (Test-Path $f)) { continue }
  foreach ($c in (ConvertFrom-Json ([IO.File]::ReadAllText($f, $utf8))).items) {
    $id = if ($null -ne $c.idTorneo) { $c.idTorneo } else { $c.id }
    if ($id -gt 0 -and $c.enlace) { $conocidos[[int]$id] = @{ enlace = $c.enlace; nueva = [bool]$c.nuevaVentana } }
  }
}

# 1) Temporadas del desplegable y temporada por defecto (la seleccionada sin parametro)
$portada = Pedir ''
$sel = [regex]::Match($portada, '(?s)<select name="ano".*?</select>').Value
$anios = @([regex]::Matches($sel, 'value="(\d{4})"') | ForEach-Object { [int]$_.Groups[1].Value })
if (-not $anios.Count) { throw 'No se han encontrado temporadas en la web.' }
$porDefecto = [int]([regex]::Match($sel, 'value="(\d{4})"\s*selected').Groups[1].Value)
$licitacionAbierta = $portada -match 'href="Organizacion_Torneos"'

$temporadas = New-Object Collections.ArrayList
$items = New-Object Collections.ArrayList
$resueltos = 0
foreach ($anio in $anios) {
  $html = if ($anio -eq $porDefecto) { $portada } else { Pedir "$anio" }

  # Documento y fecha de actualizacion ("martes, 13 de octubre de 2026")
  $act = $null
  $fa = [regex]::Match($html, 'Fecha de Actualizaci\S*n<br>\s*[^,<]*,\s*(\d{1,2}) de (\w+) de (\d{4})')
  if ($fa.Success) { $act = '{0}-{1:00}-{2:00}' -f $fa.Groups[3].Value, $MESES_LARGOS[$fa.Groups[2].Value.ToLower()], [int]$fa.Groups[1].Value }
  $doc = [regex]::Match($html, '<a href="https?://[^"]*/refs/docs/([^"]*)" target="_blank">\s*<img src="refs/images/iconodescargar').Groups[1].Value
  $archivo = if ($doc.Trim()) { 'docs/' + [Net.WebUtility]::HtmlDecode($doc.Trim()) } else { $null }
  [void]$temporadas.Add([ordered]@{ anio = $anio; actualizado = $act; archivo = $archivo; url = UrlRefs $archivo })

  # Pruebas: filas de 5 celdas de la tabla del formulario formCamp
  $i = $html.IndexOf('id="formCamp"'); $j = $html.IndexOf('</table>', $i)
  $tabla = $html.Substring($i, $j - $i)
  $n = 0
  foreach ($tr in [regex]::Matches($tabla, '(?s)<tr[^>]*>(.*?)</tr>')) {
    $c = $tr.Groups[1].Value
    $tds = [regex]::Matches($c, '(?s)<td[^>]*class="pielineasPPT tamletra10"[^>]*>(.*?)</td>')
    if ($tds.Count -ne 5) { continue }
    $v = @($tds | ForEach-Object { Html $_.Groups[1].Value })

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

    $id = $null; $enlace = $null; $nueva = $false
    $ir = [regex]::Match($c, 'irCamp\((\d+)\)')
    if ($ir.Success) {
      $id = [int]$ir.Groups[1].Value
      if ($conocidos.ContainsKey($id)) { $enlace = $conocidos[$id].enlace; $nueva = $conocidos[$id].nueva }
      else {
        $enlace = ResolverFicha $id; $resueltos++; Start-Sleep -Milliseconds 150
        if ($enlace) { $conocidos[$id] = @{ enlace = $enlace; nueva = $false } }
      }
    }

    [void]$items.Add([ordered]@{
      anio = [int]$anio; fechaInicio = $ini; fechaFin = $fin; fechaPorConfirmar = [bool]$porConfirmar
      titulo = $v[2]; categoria = $(if ($v[3]) { $v[3] } else { $null }); sede = $(NombrePropio $v[4])
      idTorneo = $id; enlace = $enlace; nuevaVentana = $nueva
    })
    $n++
  }
  $total = [regex]::Match($html, 'Datos del Calendario<strong> \((\d+)\)').Groups[1].Value
  $aviso = if ($total -and [int]$total -ne $n) { "  (la web dice ${total}: cuenta tambien las no visibles)" } else { '' }
  Write-Host ("{0}: {1} pruebas{2}" -f $anio, $n, $aviso)
}

# Orden de la API: temporada mas reciente primero y, dentro, por fecha (sin fecha al final), fin y titulo
$ordenados = @($items | Sort-Object -Property `
  @{ Expression = { [int]$_.anio }; Descending = $true },
  @{ Expression = { if ($_.fechaInicio) { 0 } else { 1 } } },
  @{ Expression = { [string]$_.fechaInicio } },
  @{ Expression = { [string]$_.fechaFin } },
  @{ Expression = { [string]$_.titulo } })

# Convocatoria para organizar torneos (Contenidos/), solo si la web la muestra
$licitacion = $null
if ($licitacionAbierta) {
  $l = (ConvertFrom-Json ([IO.File]::ReadAllText($contenido, $utf8))).licitacion
  if ($l) {
    $licitacion = [ordered]@{
      titulo    = $l.titulo
      texto     = $l.texto
      imagen    = $l.imagen
      imagenUrl = $(if ($imagenesLocales.ContainsKey($l.imagen)) { $imagenesLocales[$l.imagen] } else { UrlRefs $l.imagen })
      imagenAlt = $l.imagenAlt
      enlace    = $l.enlace
      enlaceUrl = $web + $l.enlace
    }
  }
}

# Mismo orden de propiedades que CalendarioCompeticionDto
$salida = [ordered]@{ anio = $porDefecto; temporadas = @($temporadas); licitacion = $licitacion; items = $ordenados }
[IO.File]::WriteAllText($destino, ($salida | ConvertTo-Json -Depth 6), $utf8)
Write-Host ("Escrito {0}: {1} temporadas, {2} pruebas ({3} fichas resueltas en Torneos_Previo.asp)" -f $destino, $anios.Count, $ordenados.Count, $resueltos)

& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
