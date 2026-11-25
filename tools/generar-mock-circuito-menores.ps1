# Genera los mocks de un torneo del CIRCUITO DE MENORES a partir de su web oficial
# (http://circuitomenores.padelfederacion.es), NO de la BD: es la versión para menores de
# tools\generar-mock-web-publica.ps1 (mismo formato de salida y misma lectura de cuadros).
#   - mock/competicion-campeonatos-parejas-{id}.json   ficha del torneo
#   - mock/competicion-cuadros-{id}.json               índice de cuadros
#   - mock/competicion-cuadros-{id}-{G}-{Cat}-{Nivel}-{Cuadro}.json  cada cuadro (final, consolación, previa, pre-previa)
# En competicion-campeonatos.json, el enlace del torneo pasa a la ficha de la web nueva (Camp_Parejas.asp?IdTorneo=)
# para que la PoC lo abra en campeonato-parejas.html; la web del circuito queda en urlWebActual.
# Los datos son los que se VEN en la web pública del circuito (sin licencias ni horarios). Después regenera mocks.js.
#   powershell -NoProfile -File tools\generar-mock-circuito-menores.ps1 -Torneos 28909
param([int[]]$Torneos = @(28909), [string]$Base = 'http://circuitomenores.padelfederacion.es/')
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$carpeta = Join-Path $raiz 'src\FEP.Web\wwwroot\mock'
$utf8 = New-Object Text.UTF8Encoding($false)
$win1252 = [Text.Encoding]::GetEncoding(1252)
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$rutaListado = Join-Path $carpeta 'competicion-campeonatos.json'
$listado = ConvertFrom-Json ([IO.File]::ReadAllText($rutaListado, $utf8))
$GEN = @{ 'M' = 'masculino'; 'F' = 'femenino'; 'X' = 'mixto' }
$consultado = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss')

# La web del circuito se sirve en Windows-1252 (salvo que la página diga lo contrario)
function Decodificar($r) {
  $ms = New-Object IO.MemoryStream
  $r.RawContentStream.Position = 0
  $r.RawContentStream.CopyTo($ms)
  $bytes = $ms.ToArray()
  $prueba = $win1252.GetString($bytes)
  if ($prueba -match '(?i)charset\s*=\s*"?utf-8') { return $utf8.GetString($bytes) }
  $prueba
}
function Descargar([string]$ruta, $cuerpo = $null) {
  Start-Sleep -Milliseconds 250
  if ($cuerpo) { $r = Invoke-WebRequest ($Base + $ruta) -Method Post -Body $cuerpo -UseBasicParsing -TimeoutSec 60 }
  else { $r = Invoke-WebRequest ($Base + $ruta) -UseBasicParsing -TimeoutSec 60 }
  [pscustomobject]@{ Html = (Decodificar $r); Url = $r.BaseResponse.ResponseUri.AbsoluteUri }
}
function Texto([string]$html) {
  $t = [regex]::Replace([string]$html, '(?s)<[^>]+>', ' ')
  ([Net.WebUtility]::HtmlDecode($t) -replace '[\s ]+', ' ').Trim()
}
function Entre([string]$h, [string]$desde, [string]$hasta) {
  $i = $h.IndexOf($desde, [StringComparison]::OrdinalIgnoreCase)
  if ($i -lt 0) { return $null }
  $i += $desde.Length
  $j = $h.IndexOf($hasta, $i, [StringComparison]::OrdinalIgnoreCase)
  if ($j -lt 0) { return $null }
  $h.Substring($i, $j - $i)
}
function UrlAbs([string]$u) {
  if (-not $u) { return $null }
  $u = [Net.WebUtility]::HtmlDecode($u.Trim())
  try { (New-Object Uri((New-Object Uri($Base)), $u)).AbsoluteUri } catch { $null }
}
function HtmlUtil([string]$h) {
  if (-not $h) { return $null }
  $h = ($h -replace '(?is)<h4>.*?</h4>', '' -replace '(?is)<script.*?</script>', '').Trim()
  if ((Texto $h).Length -lt 3) { return $null }
  $h
}
function ParamsPrevio([string]$href) {
  $q = @{}
  foreach ($p in ([Net.WebUtility]::HtmlDecode($href) -split '\?', 2)[1] -split '&') {
    $kv = $p -split '=', 2
    if ($kv.Count -eq 2) { $q[$kv[0].ToLower()] = [Uri]::UnescapeDataString($kv[1]).Trim() }
  }
  $q
}

# Cuadro de eliminatoria (idéntico a tools\generar-mock-web-publica.ps1): la tabla "tablaCuadros" es una rejilla
# con rowspan; cada columna de ronda tiene sus huecos en orden y los resultados entre los dos huecos del partido.
function LeerCuadro([string]$html) {
  $tabla = [regex]::Match($html, '(?is)<table[^>]*class="tablaCuadros"[^>]*>(.*?)</table>')
  if (-not $tabla.Success) { return $null }
  $celdas = New-Object Collections.ArrayList
  $ocupado = @{}
  $r = 0
  foreach ($tr in [regex]::Matches($tabla.Groups[1].Value, '(?is)<tr[^>]*>(.*?)</tr>')) {
    $c = 0
    foreach ($td in [regex]::Matches($tr.Groups[1].Value, '(?is)<t[dh]([^>]*)>(.*?)</t[dh]>')) {
      while ($ocupado.ContainsKey("$r,$c")) { $c++ }
      $attrs = $td.Groups[1].Value
      $rs = 1; $cs = 1
      if ($attrs -match 'rowspan="?(\d+)') { $rs = [int]$Matches[1] }
      if ($attrs -match 'colspan="?(\d+)') { $cs = [int]$Matches[1] }
      $cls = ''
      if ($attrs -match 'class="([^"]*)"') { $cls = $Matches[1] }
      [void]$celdas.Add([pscustomobject]@{ Fila = $r; Col = $c; Clase = $cls; Html = $td.Groups[2].Value })
      for ($i = 0; $i -lt $rs; $i++) { for ($j = 0; $j -lt $cs; $j++) { $ocupado["$($r + $i),$($c + $j)"] = $true } }
      $c += $cs
    }
    $r++
  }
  $cab = $celdas | Where-Object { $_.Fila -eq 0 }
  $colCS = ($cab | Where-Object { (Texto $_.Html) -match '^C\.?\s*S\.?$' } | Select-Object -First 1).Col
  $colsRonda = @($cab | Where-Object { $t = Texto $_.Html; $t -and $t -notmatch '^(Lin|C\.?\s*S\.?)$' } | ForEach-Object { $_.Col })
  if (-not $colsRonda.Count) { return $null }
  $semillas = @{}
  if ($null -ne $colCS) {
    foreach ($s in $celdas | Where-Object { $_.Col -eq $colCS -and $_.Fila -gt 0 }) {
      $t = Texto $s.Html
      if ($t -match '^\d{1,2}$') { $semillas[$s.Fila] = [int]$t }
    }
  }
  $participantes = New-Object Collections.ArrayList
  $partidos = New-Object Collections.ArrayList
  $primera = $true
  foreach ($col in $colsRonda) {
    $huecos = @($celdas | Where-Object { $_.Col -eq $col -and $_.Clase -match '^tablalineaabajo' } | Sort-Object Fila)
    $n = $huecos.Count
    if ($n -lt 1 -or ($n -band ($n - 1))) { return $null }
    $avance = [int][Math]::Round([Math]::Log($n, 2))
    for ($p = 0; $p -lt $n; $p++) {
      $nombre = Texto $huecos[$p].Html
      if (-not $nombre) { continue }
      if ($nombre -match '^(EXENTO|BYE|-+)$') {
        [void]$participantes.Add([ordered]@{ avance = $avance; posicion = $p + 1; tipo = 'exento'; nombre = 'EXENTO' }); continue
      }
      $o = [ordered]@{ avance = $avance; posicion = $p + 1; tipo = 'pareja'; nombre = $nombre; jugadores = @($nombre -split '\s+-\s+' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
      if ($primera -and $semillas.ContainsKey($huecos[$p].Fila)) { $o.cabezaSerie = $semillas[$huecos[$p].Fila] }
      [void]$participantes.Add($o)
    }
    $porPartido = [ordered]@{}
    foreach ($res in $celdas | Where-Object { $_.Col -eq $col -and $_.Html -match 'class="(resultado|horario)"' }) {
      $texto = Texto ([regex]::Match($res.Html, '(?is)<div[^>]*class="resultado"[^>]*>(.*?)</div>').Groups[1].Value)
      $hora = Texto ([regex]::Match($res.Html, '(?is)<div[^>]*class="horario"[^>]*>(.*?)</div>').Groups[1].Value)
      if (-not $texto -and -not $hora) { continue }
      for ($m = 0; $m -lt [Math]::Floor($n / 2); $m++) {
        if ($huecos[2 * $m].Fila -lt $res.Fila -and $res.Fila -le $huecos[2 * $m + 1].Fila) {
          if (-not $porPartido.Contains("$m")) { $porPartido["$m"] = [ordered]@{ avance = $avance; posicion = $m + 1; resultado = $null; horario = $null; enfrentamiento = $false } }
          if ($texto) { $porPartido["$m"].resultado = $texto }
          if ($hora) { $porPartido["$m"].horario = [ordered]@{ dia = $null; lugar = $null; obs = $hora } }
          break
        }
      }
    }
    foreach ($x in $porPartido.Values) { [void]$partidos.Add($x) }
    $primera = $false
  }
  $obs = [regex]::Match($html, '(?is)<div class="cuadroTextoObs">(.*?)</div>').Groups[1].Value
  [pscustomobject]@{ Participantes = $participantes; Partidos = $partidos; Notas = (HtmlUtil $obs) }
}

# ---------------------------------------------------------------------------------------------------------
foreach ($id in $Torneos) {
  $item = $listado.items | Where-Object { $_.id -eq $id } | Select-Object -First 1
  if (-not $item) { Write-Warning "$id no está en competicion-campeonatos.json"; continue }
  Write-Output "== $id $($item.titulo)"
  $ficha = Descargar "Campeonato?IdTorneo=$id"
  $h = $ficha.Html

  # Categorías (desplegable) y enlaces a cuadros: la página los muestra tras elegir género y categoría (POST)
  $cats = @([regex]::Matches($h, '(?is)<select[^>]*id="categoria"[^>]*>(.*?)</select>').Groups[1].Value |
      ForEach-Object { [regex]::Matches($_, '(?is)<option[^>]*value="([^"]+)"') } | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -ne '--' })
  $enlaces = New-Object Collections.ArrayList
  $vistos = @{}
  foreach ($g in 'M', 'F') {
    foreach ($cat in $cats) {
      $pag = (Descargar 'Campeonato' @{ IdTorneo = "$id"; genero = $g; categoria = $cat }).Html
      foreach ($m in [regex]::Matches($pag, '(?is)<a[^>]+href="(Cuadros/Previo\.asp\?[^"]+)"[^>]*>(.*?)</a>')) {
        $q = ParamsPrevio $m.Groups[1].Value
        if (-not $GEN[$q['genero']]) { continue }
        $k = "$($q['genero'])|$($q['cat'])|$($q['nivel'])|$($q['cuadro'])".ToUpper()
        if ($vistos[$k]) { continue }
        $vistos[$k] = $true
        [void]$enlaces.Add([pscustomobject]@{ Genero = $q['genero']; Cat = $q['cat']; Nivel = [int]$q['nivel']; Cuadro = $q['cuadro']; Etiqueta = (Texto $m.Groups[2].Value); Href = $m.Groups[1].Value })
      }
    }
  }

  # ---- Cada cuadro
  $lugar = Texto ([regex]::Match($h, '(?is)Lugar:\s*</[^>]+>(.*?)<br').Groups[1].Value)
  $torneo = [ordered]@{ id = $id; titulo = $item.titulo.Trim(); tipo = 'parejas'; fechaInicio = $item.fechaInicio; fechaFin = $item.fechaFin; lugar = $(if ($lugar) { $lugar } else { $item.ciudad }) }
  $publicados = New-Object Collections.ArrayList
  $datos = [ordered]@{}
  $parejasPorGenero = @{}
  foreach ($e in $enlaces) {
    try { $c = Descargar ([Net.WebUtility]::HtmlDecode($e.Href)) } catch { Write-Warning "  $($e.Href): $($_.Exception.Message)"; continue }
    $plantilla = ([regex]::Match($c.Url, '/([A-Za-z0-9-]+)\.asp', 'IgnoreCase').Groups[1].Value)
    if ($plantilla -match '^Liguilla|^PPTSinSortear') { Write-Warning "  $($e.Genero) $($e.Cat) $($e.Nivel) $($e.Cuadro): $plantilla no se convierte"; continue }
    $leido = LeerCuadro $c.Html
    if (-not $leido -or -not $leido.Participantes.Count) { Write-Warning "  $($e.Genero) $($e.Cat) $($e.Nivel) $($e.Cuadro): sin cuadro publicado ($plantilla)"; continue }
    $sel = [ordered]@{ genero = $e.Genero; categoria = $e.Cat; nivel = $e.Nivel; cuadro = $e.Cuadro }
    [void]$publicados.Add($sel)
    $datos["$($e.Genero)-$($e.Cat)-$($e.Nivel)-$($e.Cuadro)"] = [pscustomobject]@{ Sel = $sel; Leido = $leido; Plantilla = $plantilla }
    # Parejas distintas por género (para "parejas inscritas"): las de la 1.ª ronda de los cuadros principales
    if ($e.Cuadro -eq 'Final') {
      if (-not $parejasPorGenero[$e.Genero]) { $parejasPorGenero[$e.Genero] = @{} }
      $max = ($leido.Participantes | ForEach-Object { $_.avance } | Measure-Object -Maximum).Maximum
      $leido.Participantes | Where-Object { $_.avance -eq $max -and $_.tipo -eq 'pareja' } | ForEach-Object { $parejasPorGenero[$e.Genero]["$($e.Cat)|$($_.nombre)"] = $true }
    }
    Write-Output "  $($e.Genero) $($e.Cat) nivel $($e.Nivel) $($e.Cuadro) ($plantilla): $($leido.Participantes.Count) huecos, $($leido.Partidos.Count) resultados"
  }
  $otros = @($publicados | ForEach-Object { [ordered]@{ genero = $_.genero; categoria = $_.categoria; nivel = $_.nivel; cuadro = $_.cuadro } })
  foreach ($k in $datos.Keys) {
    $x = $datos[$k]
    $semillas = @($x.Leido.Participantes | Where-Object { $_.cabezaSerie } | Sort-Object { $_.cabezaSerie } | ForEach-Object {
        [ordered]@{ cabezaSerie = $_.cabezaSerie; nombre = ($_.jugadores -join '/'); puntos = $null } })
    $cuadro = [ordered]@{
      torneo = $torneo; genero = $x.Sel.genero; categoria = $x.Sel.categoria; nivel = $x.Sel.nivel; cuadro = $x.Sel.cuadro
      publicado = $true; notas = $x.Leido.Notas; formato = 'eliminatoria'; plantilla = $x.Plantilla
      participantes = @($x.Leido.Participantes); partidos = @($x.Leido.Partidos); cabezasSerie = $semillas
      clasificacion = @(); otrosCuadros = $otros
    }
    [IO.File]::WriteAllText((Join-Path $carpeta "competicion-cuadros-$id-$k.json"), (ConvertTo-Json -InputObject $cuadro -Depth 10), $utf8)
  }
  if ($publicados.Count) {
    $indice = [ordered]@{ torneo = $torneo; cuadros = @($publicados | ForEach-Object { $o = [ordered]@{}; foreach ($p in $_.Keys) { $o[$p] = $_[$p] }; $o.publicado = $true; $o }) }
    [IO.File]::WriteAllText((Join-Path $carpeta "competicion-cuadros-$id.json"), (ConvertTo-Json -InputObject $indice -Depth 10), $utf8)
  }

  # ---- Ficha (misma forma que las de tools\generar-mock-web-publica.ps1)
  $grupos = [ordered]@{}
  foreach ($e in $enlaces) {
    $k = "$($e.Genero)|$($e.Cat)"
    if (-not $grupos.Contains($k)) { $grupos[$k] = [ordered]@{ genero = $GEN[$e.Genero]; categoria = $e.Cat; cuadros = New-Object Collections.ArrayList } }
    $tipo = if ($e.Nivel -in 21, 31) { 'previa' } elseif ($e.Cuadro -match '^consol') { 'consolacion' } else { 'final' }
    $titulo = if ($e.Etiqueta) { $e.Etiqueta } else { $e.Cuadro }
    # Mismo formato de enlace que los torneos de la web principal (Previo.asp): la ficha nueva lo convierte en
    # cuadro.html y muestra el cuadro desde los mocks. El original del circuito queda en $e.Href.
    $urlCuadro = 'https://www.padelfederacion.es/Previo.asp?IdTorneo={0}&Genero={1}&Nivel={2}&Cat={3}&Cuadro={4}' -f $id, $e.Genero, $e.Nivel, [Uri]::EscapeDataString($e.Cat), [Uri]::EscapeDataString($e.Cuadro)
    [void]$grupos[$k].cuadros.Add([ordered]@{ titulo = $titulo; nivel = $e.Nivel; cuadro = $e.Cuadro; tipo = $tipo; url = $urlCuadro })
  }
  $generos = @($enlaces | Group-Object Genero | ForEach-Object { [ordered]@{ genero = $GEN[$_.Name]; categorias = @($_.Group | ForEach-Object { $_.Cat } | Select-Object -Unique) } })

  $notif = Entre $h '<h4>Notificaciones</h4>' '<!--Info general -->'
  $adjuntos = @()
  if ($notif) {
    $adjuntos = @([regex]::Matches($notif, '(?is)<a[^>]+href="([^"]+\.pdf)"[^>]*>(.*?)</a>') | ForEach-Object {
        $u = UrlAbs $_.Groups[1].Value
        $t = Texto $_.Groups[2].Value
        $archivo = [Uri]::UnescapeDataString(($u -split '/')[-1])
        [ordered]@{ titulo = $(if ($t) { $t } else { $archivo -replace '\.pdf$', '' }); tipo = $null; archivo = $archivo; url = ($u -replace ' ', '%20') } })
  }
  $notifHtml = HtmlUtil $notif
  $cuotas = @([regex]::Matches($h, '(?is)Cuota Inscripci[^:]*:\s*(?:</[^>]+>)?\s*([\d.,]+)') | ForEach-Object { [double]($_.Groups[1].Value -replace ',', '.') })
  $club = Texto ([regex]::Match($h, '(?is)Club Apoyo:\s*(?:</[^>]+>)?(.*?)<br').Groups[1].Value)
  $sedes = @($club -split '\s+Y\s+' | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -ne '--' } |
      ForEach-Object { [ordered]@{ nombre = $_; web = $null; latitud = $null; longitud = $null } })
  $inscritos = @('M', 'F' | Where-Object { $parejasPorGenero[$_] } | ForEach-Object {
      [ordered]@{ genero = $GEN[$_]; parejas = $parejasPorGenero[$_].Count; url = "$($Base)Parejas-Inscritas?IdTorneo=$id" } })
  $urlCircuito = "$($Base)Campeonato?IdTorneo=$id"

  $mock = [ordered]@{
    id = $id; anio = $item.anio; titulo = $item.titulo.Trim(); categoria = 'menores'; seleccion = $false
    fechaInicio = $item.fechaInicio; fechaFin = $item.fechaFin; ciudad = $item.ciudad; sedes = $sedes; cuotas = $cuotas; generos = $generos
    notaPrensaHtml = $null; marcadores = @(); cuadrosPublicados = [bool]$enlaces.Count; cuadros = @($grupos.Values)
    cuadroHonor = @(); notificacionesHtml = $notifHtml; adjuntos = $adjuntos; inscritosParejas = $inscritos
    entrenadores = [ordered]@{ numero = $null; url = "$($Base)Coach-Inscritos?IdTorneo=$id" }
    inscripcion = [ordered]@{ abierta = $false; fechaLimite = $null; individual = $false; informacionHtml = $null; condicionesHtml = $null; formularioUrl = $urlCircuito }
    infoAdicionalHtml = $null; cartel = $null; cartelUrl = $null; noticias = @()
    urlWebActual = $urlCircuito; consultado = $consultado
  }
  [IO.File]::WriteAllText((Join-Path $carpeta "competicion-campeonatos-parejas-$id.json"), (ConvertTo-Json -InputObject $mock -Depth 10), $utf8)

  # Listado: el torneo se abre en la ficha de la web nueva (antes enlazaba a la web del circuito)
  $item.enlace = "https://www.padelfederacion.es/Camp_Parejas.asp?IdTorneo=$id"
  Write-Output "  ficha: $($enlaces.Count) cuadros enlazados, $($publicados.Count) convertidos, $($adjuntos.Count) adjuntos, parejas: $(($inscritos | ForEach-Object { "$($_.genero) $($_.parejas)" }) -join ', ')"
}
[IO.File]::WriteAllText($rutaListado, (ConvertTo-Json -InputObject $listado -Depth 12), $utf8)
& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
