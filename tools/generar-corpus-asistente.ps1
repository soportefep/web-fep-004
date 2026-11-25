# Genera el corpus del asistente (chatbot de la web) a partir de los PDF de docs/info_fep:
#   - src/FEP.Api/Contenidos/asistente/documentos.json  catalogo de documentos. Se edita a mano: titulo, descripcion,
#                                                       anio, vigente (false = historico o sustituido) y url (enlace
#                                                       publico al PDF). El script anade los PDF nuevos con valores
#                                                       por defecto, conserva lo editado y quita los que ya no existen.
#   - src/FEP.Api/Contenidos/asistente/corpus.json      fragmentos de texto (se regenera entero; no editar a mano).
#
# Requiere pdftotext (Poppler). Viene con Git for Windows (C:\Program Files\Git\ucrt64\bin\pdftotext.exe) o se pasa
# con -Pdftotext. Los PDF escaneados (sin capa de texto) quedan en el catalogo con "sinTexto": true y no se indexan:
# hay que pasarlos por OCR (o sustituirlos por la version con texto) y volver a ejecutar.
#
# Ejecutar cada vez que se anada, cambie o retire un documento (y reiniciar la API):
#   powershell -NoProfile -File tools\generar-corpus-asistente.ps1
#
# El fichero es ASCII a proposito (Windows PowerShell 5.1 lee como ANSI los .ps1 sin BOM).
param(
  [string]$Pdftotext = ''
)
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$origen = Join-Path $raiz 'docs\info_fep'
$destino = Join-Path $raiz 'src\FEP.Api\Contenidos\asistente'
$utf8 = New-Object Text.UTF8Encoding($false)

# Tamano de cada fragmento (caracteres): se corta por parrafos y solo dentro de un parrafo si este supera el maximo.
$objetivo = 1400
$maximo = 2200
$minimo = 250

if (-not $Pdftotext) {
  $cmd = Get-Command pdftotext -ErrorAction SilentlyContinue
  if ($cmd) { $Pdftotext = $cmd.Source }
  elseif (Test-Path 'C:\Program Files\Git\ucrt64\bin\pdftotext.exe') { $Pdftotext = 'C:\Program Files\Git\ucrt64\bin\pdftotext.exe' }
  elseif (Test-Path 'C:\Program Files\Git\mingw64\bin\pdftotext.exe') { $Pdftotext = 'C:\Program Files\Git\mingw64\bin\pdftotext.exe' }
  else { throw 'No se encuentra pdftotext. Instala Poppler o pasa la ruta con -Pdftotext.' }
}

# Carpeta de docs/info_fep -> categoria (la herramienta buscar_normativa del asistente puede filtrar por ella).
$categorias = @{
  'reglamentos'             = 'reglamentos'
  'circulares'              = 'circulares'
  'estatutos'               = 'estatutos'
  'mantenimiento de pistas' = 'instalaciones'
  'memorias deportivas'     = 'memorias'
}

function Normaliza-Espacios([string]$s) { return ($s -replace '[\u00A0\t ]+', ' ').Trim() }

function Titulo-Por-Defecto([string]$nombre) {
  $t = [IO.Path]::GetFileNameWithoutExtension($nombre) -replace '[_]+', ' ' -replace '\(\d\)$', '' -replace '\.doc$', ''
  return (Normaliza-Espacios $t)
}

# Circulares: "Circular 09/2026: <Asunto>" a partir del texto de la primera pagina.
function Titulo-Circular([string]$texto, [string]$porDefecto) {
  $plano = Normaliza-Espacios ($texto -replace '\s+', ' ')
  $num = [regex]::Match($plano, 'CIRCULAR\s*(n[u\u00fa]m\.?\s*)?(\d{1,2})[/.-](\d{4})', 'IgnoreCase')
  $asunto = [regex]::Match($plano, 'Asunto:\s*(.+?)\s+(Destinatarios|Fecha)\s*:', 'IgnoreCase')
  if (-not $num.Success -or -not $asunto.Success) { return $porDefecto }
  $a = ($asunto.Groups[1].Value -replace '^CIRCULAR\s*\S+\s*', '').Trim()
  return ('Circular {0}/{1}: {2}' -f $num.Groups[2].Value.PadLeft(2, '0'), $num.Groups[3].Value, $a)
}

function Anio-De([string]$nombre) {
  $m = [regex]::Matches($nombre, '(19[89]\d|20[0-4]\d)')
  if ($m.Count -gt 0) { return [int]$m[$m.Count - 1].Value }
  $m = [regex]::Match($nombre, '\d{1,2}[-.]\d{1,2}[-.](\d{2})\b')
  if ($m.Success) { return 2000 + [int]$m.Groups[1].Value }
  return $null
}

function Id-De([string]$relativa) {
  $s = $relativa.ToLowerInvariant().Normalize([Text.NormalizationForm]::FormD)
  $s = -join ($s.ToCharArray() | Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne 'NonSpacingMark' })
  return ($s -replace '\.pdf$', '' -replace '[^a-z0-9]+', '-').Trim('-')
}

# --- Catalogo existente (conserva lo editado a mano; clave = id, estable aunque cambien los acentos del nombre) ---
$rutaCatalogo = Join-Path $destino 'documentos.json'
$previos = @{}
if (Test-Path $rutaCatalogo) {
  foreach ($d in (ConvertFrom-Json ([IO.File]::ReadAllText($rutaCatalogo, $utf8))).documentos) { $previos[$d.id] = $d }
}

New-Item -ItemType Directory -Force $destino | Out-Null
$tmp = [IO.Path]::GetTempFileName()
$documentos = New-Object System.Collections.Generic.List[object]
$fragmentos = New-Object System.Collections.Generic.List[object]
$sinTexto = New-Object System.Collections.Generic.List[string]

try {
  foreach ($pdf in Get-ChildItem $origen -Recurse -Filter *.pdf | Sort-Object FullName) {
    $relativa = ($pdf.FullName.Substring($origen.ToString().Length + 1) -replace '\\', '/').Normalize([Text.NormalizationForm]::FormC)
    $carpeta = ($relativa -split '/')[0].ToLowerInvariant()
    $categoria = if ($categorias.ContainsKey($carpeta)) { $categorias[$carpeta] } else { 'otros' }
    $id = Id-De $relativa

    & $Pdftotext -enc UTF-8 -q $pdf.FullName $tmp
    if ($LASTEXITCODE -ne 0) { Write-Warning "pdftotext fallo con $relativa"; continue }
    $paginas = ([IO.File]::ReadAllText($tmp, $utf8)) -split "`f"
    if ($paginas.Count -gt 1 -and -not $paginas[-1].Trim()) { $paginas = $paginas[0..($paginas.Count - 2)] }

    # Cabeceras y pies repetidos (p. ej. el titulo del reglamento en cada pagina) y numeros de pagina sueltos.
    $frecuencia = @{}
    foreach ($p in $paginas) {
      foreach ($l in ($p -split "`r?`n" | ForEach-Object { Normaliza-Espacios $_ } | Where-Object { $_ } | Select-Object -Unique)) {
        $frecuencia[$l] = 1 + [int]$frecuencia[$l]
      }
    }
    $umbral = [Math]::Max(3, [Math]::Ceiling($paginas.Count * 0.5))
    $repetidas = @{}
    foreach ($k in $frecuencia.Keys) { if ($frecuencia[$k] -ge $umbral -and $k.Length -lt 120) { $repetidas[$k] = $true } }

    $previo = $previos[$id]
    $anio = Anio-De $pdf.Name
    $tituloInicial = if ($categoria -eq 'circulares') { Titulo-Circular $paginas[0] (Titulo-Por-Defecto $pdf.Name) }
                     elseif ($categoria -eq 'memorias' -and $pdf.Name -match 'memoria' -and $anio) { 'Memoria deportiva FEP {0}' -f $anio }
                     else { Titulo-Por-Defecto $pdf.Name }
    $doc = [ordered]@{
      id          = $id
      fichero     = $relativa
      categoria   = $categoria
      titulo      = if ($previo -and $previo.titulo) { $previo.titulo } else { $tituloInicial }
      descripcion = if ($previo) { $previo.descripcion } else { $null }
      anio        = if ($previo -and $previo.anio) { $previo.anio } else { $anio }
      vigente     = if ($previo -and $null -ne $previo.vigente) { $previo.vigente } else { $categoria -ne 'memorias' }
      url         = if ($previo) { $previo.url } else { $null }
      paginas     = $paginas.Count
      sinTexto    = $false
    }

    # Parrafos con su pagina (las lineas de un parrafo se unen; una linea vacia separa parrafos).
    $parrafos = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $paginas.Count; $i++) {
      $actual = New-Object System.Text.StringBuilder
      foreach ($bruta in ($paginas[$i] -split "`r?`n")) {
        $l = Normaliza-Espacios $bruta
        if ($repetidas.ContainsKey($l) -or $l -match '^(p[a\u00e1]g(ina)?\.?\s*)?\d{1,3}(\s*(de|/)\s*\d{1,3})?$') { $l = '' }
        if (-not $l) {
          if ($actual.Length) { $parrafos.Add(@{ p = $i + 1; t = $actual.ToString() }); [void]$actual.Clear() }
          continue
        }
        if ($actual.Length) {
          if ($actual[$actual.Length - 1] -eq '-') { [void]$actual.Remove($actual.Length - 1, 1) } else { [void]$actual.Append(' ') }
        }
        [void]$actual.Append($l)
      }
      if ($actual.Length) { $parrafos.Add(@{ p = $i + 1; t = $actual.ToString() }) }
    }

    $total = ($parrafos | ForEach-Object { $_.t.Length } | Measure-Object -Sum).Sum
    if ($total -lt 200) {
      $doc.sinTexto = $true
      $sinTexto.Add($relativa)
      $documentos.Add([pscustomobject]$doc)
      continue
    }

    # Trozos: parrafos, y los parrafos enormes partidos por frases.
    $trozos = New-Object System.Collections.Generic.List[object]
    foreach ($par in $parrafos) {
      if ($par.t.Length -le $maximo) { $trozos.Add($par); continue }
      $sb = New-Object System.Text.StringBuilder
      foreach ($frase in [regex]::Split($par.t, '(?<=[.;:])\s+')) {
        if ($sb.Length -and $sb.Length + $frase.Length -gt $objetivo) { $trozos.Add(@{ p = $par.p; t = $sb.ToString() }); [void]$sb.Clear() }
        if ($sb.Length) { [void]$sb.Append(' ') }
        [void]$sb.Append($frase)
      }
      if ($sb.Length) { $trozos.Add(@{ p = $par.p; t = $sb.ToString() }) }
    }

    # Fragmentos: se acumulan trozos hasta el objetivo; un resto final corto se une al fragmento anterior.
    $grupos = New-Object System.Collections.Generic.List[object]
    $actualGrupo = $null
    foreach ($t in $trozos) {
      if ($actualGrupo -and $actualGrupo.texto.Length -ge $minimo -and $actualGrupo.texto.Length + $t.t.Length -gt $objetivo) {
        $grupos.Add($actualGrupo); $actualGrupo = $null
      }
      if (-not $actualGrupo) { $actualGrupo = @{ desde = $t.p; hasta = $t.p; texto = $t.t } }
      else { $actualGrupo.texto += "`n" + $t.t; $actualGrupo.hasta = $t.p }
    }
    if ($actualGrupo) {
      if ($actualGrupo.texto.Length -lt $minimo -and $grupos.Count) {
        $grupos[$grupos.Count - 1].texto += "`n" + $actualGrupo.texto
        $grupos[$grupos.Count - 1].hasta = $actualGrupo.hasta
      } else { $grupos.Add($actualGrupo) }
    }
    $n = 0
    foreach ($g in $grupos) {
      $n++
      $fragmentos.Add([pscustomobject][ordered]@{
        id          = ('{0}#{1}' -f $id, $n)
        documento   = $id
        paginaDesde = $g.desde
        paginaHasta = $g.hasta
        texto       = $g.texto
      })
    }
    $documentos.Add([pscustomobject]$doc)
  }
} finally {
  Remove-Item $tmp -ErrorAction SilentlyContinue
}

$generado = (Get-Date).ToString('yyyy-MM-dd')
$catalogo = [ordered]@{
  generado   = $generado
  nota       = 'Generado y mantenido con tools/generar-corpus-asistente.ps1. Editables a mano: titulo, descripcion, anio, vigente y url (se conservan al regenerar).'
  documentos = $documentos.ToArray()
}
$corpus = [ordered]@{
  generado   = $generado
  fragmentos = $fragmentos.ToArray()
}
[IO.File]::WriteAllText($rutaCatalogo, (ConvertTo-Json $catalogo -Depth 5), $utf8)
[IO.File]::WriteAllText((Join-Path $destino 'corpus.json'), (ConvertTo-Json $corpus -Depth 5 -Compress), $utf8)

Write-Host ('Documentos: {0} (indexados {1}, sin texto {2}). Fragmentos: {3}.' -f $documentos.Count, ($documentos.Count - $sinTexto.Count), $sinTexto.Count, $fragmentos.Count)
if ($sinTexto.Count) {
  Write-Warning ("Sin capa de texto (necesitan OCR):`n  " + ($sinTexto -join "`n  "))
}
