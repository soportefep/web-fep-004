# Genera mocks de torneos por parejas a partir de la WEB PÚBLICA actual de la FEP (no de la BD):
#   - mock/competicion-campeonatos-parejas-{id}.json   (ficha: GET /api/v1/competicion/campeonatos-parejas/{id})
#   - mock/competicion-cuadros-{id}.json               (índice de cuadros)
#   - mock/competicion-cuadros-{id}-{G}-{Cat}-{Nivel}-{Cuadro}.json (cada cuadro de eliminatoria)
# Lee Camp_Parejas.asp?IdTorneo= (que redirige a Camp_Parejas_Veteranos.asp en los de veteranos) y cada Previo.asp
# (que redirige a Cuadro4…Cuadro128 / CuadroPrevia… / CuadroPrePrevia…). Las liguillas y los cuadros que no se
# pueden interpretar se omiten (se avisa por consola). Los datos son los que se VEN en la web: sin licencias, sin
# puntos de los cabezas de serie y sin horarios. Después regenera mocks.js.
#   powershell -NoProfile -File tools\generar-mock-web-publica.ps1 -Torneos 28714,30120,28544,23263
param([int[]]$Torneos = @(28714, 30120, 28544, 30197, 23263), [string]$Base = 'https://www.padelfederacion.es/')
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$carpeta = Join-Path $raiz 'src\FEP.Web\wwwroot\mock'
$utf8 = New-Object Text.UTF8Encoding($false)
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$listado = ConvertFrom-Json ([IO.File]::ReadAllText((Join-Path $carpeta 'competicion-campeonatos.json'), $utf8))
$GEN = @{ 'M' = 'masculino'; 'F' = 'femenino'; 'X' = 'mixto' }
$consultado = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss')

function Descargar([string]$ruta) {
  Start-Sleep -Milliseconds 250
  $r = Invoke-WebRequest ($Base + $ruta) -UseBasicParsing -TimeoutSec 60
  [pscustomobject]@{ Html = $r.Content; Url = $r.BaseResponse.ResponseUri.AbsoluteUri }
}
function Texto([string]$html) {
  $t = [regex]::Replace([string]$html, '(?s)<[^>]+>', ' ')
  ([Net.WebUtility]::HtmlDecode($t) -replace '[\s ]+', ' ').Trim()
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
  try { (New-Object Uri((New-Object Uri($Base)), $u)).AbsoluteUri -replace '^http://(www\.)?padelfederacion\.es/', 'https://www.padelfederacion.es/' } catch { $null }
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

# ---------------------------------------------------------------------------------------------------------
# Cuadro de eliminatoria: la tabla "tablaCuadros" es una rejilla (con rowspan). Cada columna de ronda tiene
# sus huecos (celdas tablalineaabajo*) en orden y los resultados (div.resultado) entre los dos huecos del partido.
# ---------------------------------------------------------------------------------------------------------
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
    if ($n -lt 1 -or ($n -band ($n - 1))) { return $null }      # tiene que ser potencia de 2
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
    # Resultado y horario (div.resultado / div.horario) de cada partido: la celda queda entre sus dos huecos.
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
  $ficha = Descargar "Camp_Parejas.asp?IdTorneo=$id"
  $h = $ficha.Html
  $paginas = @($h)
  # Los de veteranos muestran los cuadros por género (desplegable): se pide cada uno.
  if ($ficha.Url -match 'Camp_Parejas_Veteranos') {
    foreach ($g in 'M', 'F', 'X') { $paginas += (Descargar "Camp_Parejas_Veteranos.asp?IdTorneo=$id&genero=$g").Html }
  }

  # ---- Enlaces a cuadros (Previo.asp) con su texto
  $vistos = @{}
  $enlaces = New-Object Collections.ArrayList
  foreach ($pag in $paginas) {
    foreach ($m in [regex]::Matches($pag, '(?is)<a[^>]+href="(Previo\.asp\?[^"]+)"[^>]*>(.*?)</a>\s*(?:</span>)?([^<]*)')) {
      $q = ParamsPrevio $m.Groups[1].Value
      if (-not $GEN[$q['genero']]) { continue }
      $k = "$($q['genero'])|$($q['cat'])|$($q['nivel'])|$($q['cuadro'])".ToUpper()
      if ($vistos[$k]) { continue }
      $vistos[$k] = $true
      $etq = Texto $m.Groups[2].Value
      if (-not $etq) { $etq = Texto $m.Groups[3].Value }
      [void]$enlaces.Add([pscustomobject]@{ Genero = $q['genero']; Cat = $q['cat']; Nivel = [int]$q['nivel']; Cuadro = $q['cuadro']; Etiqueta = $etq; Href = $m.Groups[1].Value })
    }
  }

  # ---- Cada cuadro
  $torneo = [ordered]@{ id = $id; titulo = $item.titulo.Trim(); tipo = 'parejas'; fechaInicio = $item.fechaInicio; fechaFin = $item.fechaFin; lugar = $null }
  $lugar = [regex]::Match($h, '(?is)<i>\s*Lugar:\s*</i>(.*?)<br').Groups[1].Value
  if ($lugar) { $torneo.lugar = Texto $lugar } else { $torneo.lugar = $item.ciudad }
  $publicados = New-Object Collections.ArrayList
  $datos = @{}
  foreach ($e in $enlaces) {
    try { $c = Descargar ([Net.WebUtility]::HtmlDecode($e.Href)) } catch { Write-Warning "  $($e.Href): $($_.Exception.Message)"; continue }
    $plantilla = ([regex]::Match($c.Url, '/([A-Za-z0-9-]+)\.asp', 'IgnoreCase').Groups[1].Value)
    if ($plantilla -match '^Liguilla|^PPTSinSortear') { Write-Warning "  $($e.Genero) $($e.Cat) $($e.Nivel) $($e.Cuadro): $plantilla no se convierte"; continue }
    $leido = LeerCuadro $c.Html
    if (-not $leido -or -not $leido.Participantes.Count) { Write-Warning "  $($e.Genero) $($e.Cat) $($e.Nivel) $($e.Cuadro): sin cuadro publicado ($plantilla)"; continue }
    $sel = [ordered]@{ genero = $e.Genero; categoria = $e.Cat; nivel = $e.Nivel; cuadro = $e.Cuadro }
    [void]$publicados.Add($sel)
    $datos["$($e.Genero)-$($e.Cat)-$($e.Nivel)-$($e.Cuadro)"] = [pscustomobject]@{ Sel = $sel; Leido = $leido; Plantilla = $plantilla }
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
    $nombre = "competicion-cuadros-$id-$k.json"
    [IO.File]::WriteAllText((Join-Path $carpeta $nombre), (ConvertTo-Json -InputObject $cuadro -Depth 10), $utf8)
  }
  if ($publicados.Count) {
    $indice = [ordered]@{ torneo = $torneo; cuadros = @($publicados | ForEach-Object { $o = [ordered]@{}; foreach ($p in $_.Keys) { $o[$p] = $_[$p] }; $o.publicado = $true; $o }) }
    [IO.File]::WriteAllText((Join-Path $carpeta "competicion-cuadros-$id.json"), (ConvertTo-Json -InputObject $indice -Depth 10), $utf8)
  }

  # ---- Ficha (misma forma que la de silla de ruedas)
  $grupos = [ordered]@{}
  foreach ($e in $enlaces) {
    $k = "$($e.Genero)|$($e.Cat)"
    if (-not $grupos.Contains($k)) { $grupos[$k] = [ordered]@{ genero = $GEN[$e.Genero]; categoria = $e.Cat; cuadros = New-Object Collections.ArrayList } }
    $tipo = if ($e.Nivel -in 21, 31) { 'previa' } elseif ($e.Cuadro -match '^consol') { 'consolacion' } elseif ($e.Cuadro -match '^grupo') { 'grupo' } else { 'final' }
    $titulo = if ($e.Etiqueta) { $e.Etiqueta } else { $e.Cuadro }
    [void]$grupos[$k].cuadros.Add([ordered]@{ titulo = $titulo; nivel = $e.Nivel; cuadro = $e.Cuadro; tipo = $tipo; url = (UrlAbs $e.Href) })
  }
  $generos = @($enlaces | Group-Object Genero | ForEach-Object {
      [ordered]@{ genero = $GEN[$_.Name]; categorias = @($_.Group | ForEach-Object { $_.Cat } | Where-Object { $_ -and $_ -ne 'ABSOLUTO' } | Select-Object -Unique) } })

  $notif = Entre $h '<!-- Notificaciones -->' '<!-- lISTADO DE INSCRITOS -->'
  $adjuntos = @()
  $notifHtml = $null
  if ($notif) {
    $adjuntos = @([regex]::Matches($notif, '(?is)<div class="archivosadjuntos">\s*<a href="([^"]+)"[^>]*>.*?</a>(.*?)</div>') | ForEach-Object {
        $u = UrlAbs $_.Groups[1].Value
        [ordered]@{ titulo = (Texto $_.Groups[2].Value); tipo = $null; archivo = ([Uri]::UnescapeDataString(($u -split '/')[-1])); url = ($u -replace ' ', '%20') } })
    $notifHtml = HtmlUtil ([regex]::Replace($notif, '(?is)<div class="archivosadjuntos">.*?</div>', ''))
  }
  $inscritos = @(); $entrenadores = $null
  foreach ($m in [regex]::Matches($h, '(?is)<a href="(Camp_Parejas_Inscritos\.asp\?tipo=([MFXC])&(?:amp;)?id=\d+)"[^>]*>\s*(\d+)')) {
    $u = UrlAbs $m.Groups[1].Value
    if ($m.Groups[2].Value -eq 'C') { $entrenadores = [ordered]@{ numero = [int]$m.Groups[3].Value; url = $u } }
    else { $inscritos += [ordered]@{ genero = $GEN[$m.Groups[2].Value]; parejas = [int]$m.Groups[3].Value; url = $u } }
  }
  $cuotas = @([regex]::Matches($h, '(?is)<i>\s*Cuota:\s*</i>\s*([\d.,]+)') | ForEach-Object { [double]($_.Groups[1].Value -replace ',', '.') })
  $club = Texto ([regex]::Match($h, '(?is)<i>\s*Club:\s*</i>(.*?)<br').Groups[1].Value)
  $sedes = @(($club -replace '^--\s*(y\s+)?', '') -split '\s+y\s+' | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -ne '--' } |
      ForEach-Object { [ordered]@{ nombre = $_; web = $null; latitud = $null; longitud = $null } })
  $cartel = [regex]::Match((Entre $h '<!-- CARTEL -->' '<!-- Noticias relacionadas -->'), '(?is)<img[^>]+src="([^"]+)"').Groups[1].Value
  $info = HtmlUtil (Entre $h '<!-- VER INFO ADICIONAL -->' '<!-- CARTEL -->')
  $nota = HtmlUtil (Entre $h '<!-- NOta de Prensa -->' '<!-- Cuadros -->')
  $noticias = @([regex]::Matches((Entre $h '<!-- Noticias relacionadas -->' '<!-- .prl-entry-content -->'), '(?is)<a href="([^"]*Noticia[^"]*)"[^>]*>(.*?)</a>') |
      ForEach-Object { $t = Texto $_.Groups[2].Value; if ($t) { [ordered]@{ idNoticia = 0; titulo = $t; fecha = $null; url = (UrlAbs $_.Groups[1].Value) } } })

  $plazo = $item.inscripcion
  $abierta = [bool]($plazo -and $plazo.fechaLimite -and ([datetime]$plazo.fechaLimite) -gt (Get-Date))
  $paginaWeb = if ($ficha.Url -match 'Veteranos') { 'Camp_Parejas_Veteranos.asp' } else { 'Camp_Parejas.asp' }
  $mock = [ordered]@{
    id = $id; anio = $item.anio; titulo = $item.titulo.Trim(); categoria = $item.categoria; seleccion = [bool]$item.seleccion
    fechaInicio = $item.fechaInicio; fechaFin = $item.fechaFin; ciudad = $item.ciudad; sedes = $sedes; cuotas = $cuotas; generos = $generos
    notaPrensaHtml = $nota; marcadores = @(); cuadrosPublicados = [bool]$enlaces.Count; cuadros = @($grupos.Values)
    cuadroHonor = @(); notificacionesHtml = $notifHtml; adjuntos = $adjuntos; inscritosParejas = $inscritos; entrenadores = $entrenadores
    inscripcion = [ordered]@{
      abierta = $abierta; fechaLimite = $(if ($plazo) { $plazo.fechaLimite } else { $null }); individual = $false
      informacionHtml = $null; condicionesHtml = $null; formularioUrl = "https://www.padelfederacion.es/$paginaWeb`?IdTorneo=$id"
    }
    infoAdicionalHtml = $info; cartel = $(if ($cartel) { ($cartel -split '/')[-1] } else { $null }); cartelUrl = $(if ($cartel) { (UrlAbs $cartel) -replace ' ', '%20' } else { $null })
    noticias = $noticias; urlWebActual = "https://www.padelfederacion.es/$paginaWeb`?IdTorneo=$id"; consultado = $consultado
  }
  [IO.File]::WriteAllText((Join-Path $carpeta "competicion-campeonatos-parejas-$id.json"), (ConvertTo-Json -InputObject $mock -Depth 10), $utf8)
  Write-Output "  ficha: $($enlaces.Count) cuadros enlazados, $($publicados.Count) convertidos, $($adjuntos.Count) adjuntos, $($inscritos.Count) géneros con inscritos"
}
& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
