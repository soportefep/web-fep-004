<#
.SYNOPSIS
  Registro unico de supuestos, decisiones FEP y puntos a revisar de las paginas migradas.

.DESCRIPTION
  Fuente de verdad: docs/migracion/registro-puntos.csv (una fila por punto, id estable P0001...).
  El Excel docs/api/supuestos-decisiones-fep.xlsx es la vista para la FEP: se rellena ahi el Estado
  y la Respuesta, y este script los vuelve a pasar al CSV.

  Acciones:
    Importar  Copia Estado / Respuesta del Excel al CSV (por id). No toca nada mas.
    Extraer   Lee docs/api/analisis/*.md y anade al CSV los puntos nuevos (marcas DECISION FEP,
              SUPUESTO y secciones "Pendiente de confirmar", "Correcciones de datos", "SUPUESTOS",
              "Decisiones"). Nunca cambia estado ni respuesta de un punto existente; actualiza su
              linea de origen y marca vigente=No si el punto ya no esta en el analisis.
    Exportar  Regenera el Excel desde el CSV (necesita Microsoft Excel instalado).
    Todo      Importar (si existe el Excel) -> Extraer -> Exportar.  Accion por defecto.

  Uso tras migrar una pagina (lo hace el agente asp-pagina-migrador):
    powershell -NoProfile -File tools\registro-puntos.ps1
  Puntos ya respondidos que afectan a una pagina:
    powershell -NoProfile -File tools\registro-puntos.ps1 -Accion Consultar -Pagina ranking-menores.html
#>
param(
  [ValidateSet('Todo', 'Importar', 'Extraer', 'Exportar', 'Consultar')]
  [string]$Accion = 'Todo',
  [string]$Pagina = '',
  [string]$Excel = ''   # otra ruta para el Excel (pruebas); por defecto docs/api/supuestos-decisiones-fep.xlsx
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csvPath = Join-Path $root 'docs\migracion\registro-puntos.csv'
$xlsxPath = if ($Excel) { [IO.Path]::GetFullPath($Excel) } else { Join-Path $root 'docs\api\supuestos-decisiones-fep.xlsx' }
$analisisDir = Join-Path $root 'docs\api\analisis'

function U([string]$s) { [regex]::Unescape($s) }   # script en ASCII: acentos como \uXXXX

$T_DEC = U 'Decisi\u00f3n FEP'
$T_SUP = 'Supuesto'
$T_ATC = 'A tener en cuenta'
$T_COR = U 'Correcci\u00f3n de datos (revisar)'
$T_TEC = U 'Decisi\u00f3n t\u00e9cnica'
$TIPOS = @($T_DEC, $T_SUP, $T_ATC, $T_COR, $T_TEC)
$ESTADOS = @('Pendiente', 'Confirmado', 'Rechazado', 'Aplicado', 'No aplica', 'Informativo')
$SI = U 'S\u00ed'
$TECNICO = U 'T\u00e9cnico'
$COLS = @('id', 'pagina', 'asp', 'bloque', 'tipo', 'revisa', 'momento', 'seccion', 'texto', 'fuente',
          'vigente', 'estado', 'respuesta', 'fecha_respuesta', 'alta')

# Quien revisa cada tipo y cuando (ver docs/migracion/plan-fases.md, "Revision de supuestos y decisiones").
$REVISA = @{ $T_DEC = 'FEP'; $T_COR = 'FEP'; $T_SUP = $TECNICO; $T_ATC = $TECNICO; $T_TEC = '-' }
$MOMENTO = @{
  $T_DEC = 'Cierre de bloque'; $T_COR = 'Cierre de bloque'; $T_ATC = 'Cierre de bloque'
  $T_SUP = 'Al conectar la API a la BD'; $T_TEC = '-'
}

function Get-Bloque([string]$asp) {
  switch -regex ($asp) {
    '^index\.asp$' { return 'Fase 1 - Portada' }
    '^(Organos_Gobierno|Equipo_FEP|Federaciones|Datos_Federacion|Clubes?|Ley_De_Transparencia|Microsite|Bienvenida|Contacto|Politica_|Aviso_Legal|Padel-Adaptado|MujerYPadel|MyP_|Hoteles_|Mapa_Web)' { return 'Fase 2 - Institucional' }
    '^(News|Noticias?)' { return 'Fase 2 - Noticias' }
    '^Documentos_' { return 'Fase 2 - Documentos' }
    '^(Calendario|Campeonatos|Camp_|Impulso)' { return 'Fase 2 - Calendario y campeonatos' }
    '^Ranking' { return 'Fase 2 - Rankings' }
    '^(Previo|Cuadro|Liguilla|EquiposEnfrentamientos)' { return 'Fase 3 - Cuadros' }
    '^Formacion_' { return (U 'Fase 2 - Formaci\u00f3n') }
    default { return 'Sin asignar' }
  }
}

function Clave([string]$pagina, [string]$texto) {
  ($pagina + '|' + ($texto.ToLowerInvariant() -replace '[^\p{L}\p{Nd}]+', ' ').Trim())
}

# ---------------------------------------------------------------- CSV
function Leer-Registro {
  if (-not (Test-Path $csvPath)) { return , (New-Object System.Collections.ArrayList) }
  $list = New-Object System.Collections.ArrayList
  foreach ($r in (Import-Csv -Path $csvPath -Encoding UTF8)) { [void]$list.Add($r) }
  return , $list
}
function Guardar-Registro($rows) {
  $sorted = $rows | Sort-Object id
  # UTF-8 sin BOM, como el resto de ficheros del repo.
  $lines = @($sorted | Select-Object $COLS | ConvertTo-Csv -NoTypeInformation)
  if ($lines.Count -eq 0) { $lines = @(($COLS | ForEach-Object { '"' + $_ + '"' }) -join ',') }
  [IO.File]::WriteAllLines($csvPath, [string[]]$lines, (New-Object Text.UTF8Encoding $false))
}

# ---------------------------------------------------------------- Extraer
function Limpiar([string]$s) { (($s -replace '\*\*', '' -replace '`', '') -replace '\s+', ' ').Trim() }

function Extraer-Analisis {
  $items = New-Object System.Collections.ArrayList
  foreach ($f in Get-ChildItem $analisisDir -Filter *.md | Sort-Object Name) {
    $lines = [IO.File]::ReadAllLines($f.FullName, [Text.Encoding]::UTF8)
    $m = [regex]::Matches($lines[0], '`([^`]+)`')
    $asp = if ($m.Count -ge 1) { $m[0].Groups[1].Value } else { $f.BaseName + '.asp' }
    $html = if ($m.Count -ge 2) { $m[1].Groups[1].Value } else { '' }
    if ($f.BaseName -eq 'index') { $asp = 'index.asp'; $html = 'index.html' }

    $section = ''; $cur = $null; $tableHdr = $null; $inCode = $false
    $blocks = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $lines.Count; $i++) {
      $l = $lines[$i]
      if ($l -match '^\s*```') { $inCode = -not $inCode; $cur = $null; continue }
      if ($inCode) { continue }
      if ($l -match '^#{2,4}\s+(.*)$') { $section = $Matches[1].Trim(); $cur = $null; $tableHdr = $null; continue }
      if ($l -match '^#\s') { continue }
      if ($l.Trim() -eq '') { $cur = $null; continue }
      if ($l -match '^\s*\|') {
        if ($l -match '^\s*\|[\s\-:|]+\|\s*$') { continue }
        $cells = ($l.Trim().Trim('|') -split '\|') | ForEach-Object { $_.Trim() }
        if ($null -eq $tableHdr) { $tableHdr = $cells; continue }
        $parts = for ($k = 0; $k -lt $cells.Count; $k++) {
          $h = if ($k -lt $tableHdr.Count) { $tableHdr[$k] } else { "Col$k" }
          '{0}: {1}' -f $h, $cells[$k]
        }
        $cur = $null
        [void]$blocks.Add([pscustomobject]@{ Section = $section; Line = $i + 1; Text = ($parts -join ' | ') })
        continue
      }
      $tableHdr = $null
      if ($l -match '^(\s*)(?:[-*]|\d+\.)\s+(.*)$') {
        if ($Matches[1].Length -ge 2 -and $null -ne $cur) { $cur.Text += ' ' + $Matches[2]; continue }
        $cur = [pscustomobject]@{ Section = $section; Line = $i + 1; Text = $Matches[2] }
        [void]$blocks.Add($cur); continue
      }
      if ($null -ne $cur) { $cur.Text += ' ' + $l.Trim(); continue }
      $cur = [pscustomobject]@{ Section = $section; Line = $i + 1; Text = $l.Trim() }
      [void]$blocks.Add($cur)
    }

    foreach ($b in $blocks) {
      $t = $b.Text; $sec = $b.Section; $tipo = $null
      # Las marcas van en mayusculas en los analisis: comparacion sensible a mayusculas.
      if ($t -cmatch 'DECISI.N FEP') { $tipo = $T_DEC }
      elseif ($t -cmatch 'SUPUESTO' -or $sec -match '^SUPUESTOS') { $tipo = $T_SUP }
      elseif ($sec -match '^Pendiente de confirmar') { $tipo = $T_ATC }
      elseif ($sec -match '^Correcciones de datos') { $tipo = $T_COR }
      elseif ($sec -eq 'Decisiones' -or $sec -match 'decisi.n t.cnica') { $tipo = $T_TEC }
      if (-not $tipo) { continue }
      $txt = (Limpiar $t) -creplace '^(DECISI.N FEP|SUPUESTO)\s*[:.]?\s*', ''
      if ($txt.Length -lt 3) { continue }
      [void]$items.Add([pscustomobject]@{
        pagina = $html; asp = $asp; tipo = $tipo; seccion = $sec; texto = $txt
        fuente = ('docs/api/analisis/{0}:{1}' -f $f.Name, $b.Line)
      })
    }
  }
  return , $items
}

function Accion-Extraer {
  $reg = Leer-Registro
  $porClave = @{}
  foreach ($r in $reg) { $porClave[(Clave $r.pagina $r.texto)] = $r }
  $max = 0
  foreach ($r in $reg) { $n = [int]($r.id -replace '\D', ''); if ($n -gt $max) { $max = $n } }
  $hoy = (Get-Date).ToString('yyyy-MM-dd')
  $vistos = @{}; $nuevos = 0; $nuevosPorPagina = @{}
  foreach ($it in (Extraer-Analisis)) {
    $k = Clave $it.pagina $it.texto
    if ($vistos.ContainsKey($k)) { continue }
    $vistos[$k] = $true
    if ($porClave.ContainsKey($k)) {
      $r = $porClave[$k]; $r.fuente = $it.fuente; $r.seccion = $it.seccion; $r.vigente = $SI
      continue
    }
    $max++; $nuevos++
    $nuevosPorPagina[$it.pagina] = 1 + [int]$nuevosPorPagina[$it.pagina]
    $estado = if ($it.tipo -eq $T_TEC) { 'Informativo' } else { 'Pendiente' }
    $row = [pscustomobject]@{
      id = ('P{0:D4}' -f $max); pagina = $it.pagina; asp = $it.asp; bloque = (Get-Bloque $it.asp)
      tipo = $it.tipo; revisa = $REVISA[$it.tipo]; momento = $MOMENTO[$it.tipo]; seccion = $it.seccion
      texto = $it.texto; fuente = $it.fuente; vigente = $SI; estado = $estado; respuesta = ''
      fecha_respuesta = ''; alta = $hoy
    }
    [void]$reg.Add($row); $porClave[$k] = $row
  }
  $retirados = 0
  foreach ($r in $reg) {
    if (-not $vistos.ContainsKey((Clave $r.pagina $r.texto)) -and $r.vigente -ne 'No') { $r.vigente = 'No'; $retirados++ }
  }
  Guardar-Registro $reg
  "Extraer: $nuevos puntos nuevos, $retirados ya no aparecen en su analisis (vigente=No). Total: $($reg.Count)."
  foreach ($p in ($nuevosPorPagina.Keys | Sort-Object)) { "  + $p : $($nuevosPorPagina[$p])" }
}

# ---------------------------------------------------------------- Excel
function Con-Excel([scriptblock]$body) {
  $xl = New-Object -ComObject Excel.Application
  $xl.Visible = $false; $xl.DisplayAlerts = $false
  try { & $body $xl } finally { $xl.Quit(); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl) }
}

function Accion-Importar {
  if (-not (Test-Path $xlsxPath)) { 'Importar: no hay Excel todavia.'; return }
  $reg = Leer-Registro
  $porId = @{}; foreach ($r in $reg) { $porId[$r.id] = $r }
  $res = Con-Excel {
    param($xl)
    $wb = $xl.Workbooks.Open($xlsxPath, 0, $true)
    try {
      $ws = $null
      foreach ($w in $wb.Worksheets) { if ($w.Name -like 'Supuestos*') { $ws = $w } }
      if ($null -eq $ws) { return 'Importar: el Excel no tiene la hoja de puntos.' }
      $used = $ws.UsedRange; $vals = $used.Value2
      $nr = $used.Rows.Count; $nc = $used.Columns.Count; $r0 = $used.Row
      $hdrRow = 0; $ci = @{}
      for ($r = 1; $r -le [Math]::Min($nr, 10); $r++) {
        for ($c = 1; $c -le $nc; $c++) {
          $v = [string]$vals[$r, $c]
          if ($v -eq 'Id') { $hdrRow = $r }
        }
        if ($hdrRow) { for ($c = 1; $c -le $nc; $c++) { $ci[[string]$vals[$r, $c]] = $c }; break }
      }
      if (-not $hdrRow) { return 'Importar: el Excel no tiene columna Id (formato antiguo); no se importa nada.' }
      $cEst = $ci['Estado']; $cResp = $ci[(U 'Respuesta / comentario FEP')]; $cFecha = $ci['Fecha respuesta']
      $cambios = 0
      for ($r = $hdrRow + 1; $r -le $nr; $r++) {
        $id = [string]$vals[$r, $ci['Id']]
        if (-not $id -or -not $porId.ContainsKey($id)) { continue }
        $row = $porId[$id]
        $est = ([string]$vals[$r, $cEst]).Trim(); $resp = ([string]$vals[$r, $cResp]).Trim()
        $fecha = if ($cFecha) { ([string]$ws.Cells.Item($r0 + $r - 1, $cFecha).Text).Trim() } else { '' }
        if ($est -and $est -ne $row.estado -and $ESTADOS -contains $est) { $row.estado = $est; $cambios++ }
        if ($resp -ne $row.respuesta) { $row.respuesta = $resp; $cambios++ }
        if ($fecha -ne $row.fecha_respuesta) { $row.fecha_respuesta = $fecha }
        if (($row.respuesta -or $row.estado -notin @('Pendiente', 'Informativo')) -and -not $row.fecha_respuesta) {
          $row.fecha_respuesta = (Get-Date).ToString('yyyy-MM-dd')
        }
      }
      return "Importar: $cambios cambios de estado/respuesta pasados al registro."
    } finally { $wb.Close($false) }
  }
  Guardar-Registro $reg
  $res
}

function Accion-Exportar {
  $reg = Leer-Registro
  $orden = @{}; for ($i = 0; $i -lt $TIPOS.Count; $i++) { $orden[$TIPOS[$i]] = $i }
  $rows = @($reg | Sort-Object @{e = { if ($_.pagina -eq 'index.html') { '' } else { $_.pagina } } },
                               @{e = { $orden[$_.tipo] } }, id)
  $color = @{ $T_DEC = 0xC7CEFF; $T_SUP = 0x9CEBFF; $T_ATC = 0xF7EBDD; $T_COR = 0xDAEFE2; $T_TEC = 0xEDEDED }
  $navy = 0x64381F; $grey = 0xBFBFBF

  Con-Excel {
    param($xl)
    function Cabecera($ws, $row, $ncols) {
      $r = $ws.Range($ws.Cells.Item($row, 1), $ws.Cells.Item($row, $ncols))
      $r.Font.Bold = $true; $r.Font.Color = 0xFFFFFF; $r.Interior.Color = $navy
      $r.VerticalAlignment = -4108; $r.WrapText = $true
    }
    $wb = $xl.Workbooks.Add()
    while ($wb.Worksheets.Count -lt 3) { [void]$wb.Worksheets.Add([Type]::Missing, $wb.Worksheets.Item($wb.Worksheets.Count)) }
    foreach ($w in $wb.Worksheets) { $w.Cells.Font.Name = 'Arial'; $w.Cells.Font.Size = 10 }

    # --- Puntos
    $ws = $wb.Worksheets.Item(2); $ws.Name = 'Supuestos y decisiones'
    $hdr = @('Id', (U 'P\u00e1gina'), 'ASP de origen', 'Bloque', 'Tipo', 'Revisa', 'Momento',
             (U 'Secci\u00f3n del an\u00e1lisis'), 'Detalle', (U 'Fuente (an\u00e1lisis:l\u00ednea)'), 'Vigente',
             'Estado', (U 'Respuesta / comentario FEP'), 'Fecha respuesta')
    $widths = @(8, 24, 24, 24, 18, 10, 16, 24, 85, 38, 9, 13, 45, 13)
    $n = $rows.Count; $h = 3; $last = $h + $n
    # [string] en toda asignacion escalar a Value2: si se asigna lo que devuelve U (PSObject), PowerShell
    # fija el tipo de Value2 como String y despues rechaza los arrays ("Object[,] a String").
    $ws.Cells.Item(1, 1).Value2 = [string]((U 'Supuestos, decisiones y puntos a tener en cuenta de las p\u00e1ginas migradas') + " ($n puntos)")
    $ws.Cells.Item(1, 1).Font.Size = 13; $ws.Cells.Item(1, 1).Font.Bold = $true
    $ws.Cells.Item(2, 1).Value2 = [string](U 'Rellenar solo Estado, Respuesta y Fecha (en amarillo). No cambiar la columna Id: es la que une cada fila con docs/migracion/registro-puntos.csv.')
    $ws.Cells.Item(2, 1).Font.Italic = $true; $ws.Cells.Item(2, 1).Font.Size = 9; $ws.Cells.Item(2, 1).Font.Color = 0x555555
    $data = New-Object 'object[,]' ($n + 1), $hdr.Count
    # [string]: lo que devuelve U llega envuelto en PSObject y Excel no acepta el array.
    for ($c = 0; $c -lt $hdr.Count; $c++) { $data[0, $c] = [string]$hdr[$c] }
    for ($k = 1; $k -le $n; $k++) {
      $r = $rows[$k - 1]
      $v = @($r.id, $r.pagina, $r.asp, $r.bloque, $r.tipo, $r.revisa, $r.momento, $r.seccion, $r.texto,
             $r.fuente, $r.vigente, $r.estado, $r.respuesta, $r.fecha_respuesta)
      for ($c = 0; $c -lt $v.Count; $c++) { $data[$k, $c] = [string]$v[$c] }
    }
    $rng = $ws.Range($ws.Cells.Item($h, 1), $ws.Cells.Item($last, $hdr.Count))
    $ws.Range($ws.Cells.Item($h + 1, 14), $ws.Cells.Item($last, 14)).NumberFormat = '@'
    $rng.Value2 = $data
    $rng.WrapText = $true; $rng.VerticalAlignment = -4160
    $rng.Borders.LineStyle = 1; $rng.Borders.Color = $grey
    Cabecera $ws $h $hdr.Count
    for ($c = 0; $c -lt $widths.Count; $c++) { $ws.Columns.Item($c + 1).ColumnWidth = $widths[$c] }
    $ws.Range('A1:A2').WrapText = $false
    $ws.Range($ws.Cells.Item($h + 1, 10), $ws.Cells.Item($last, 10)).Font.Name = 'Consolas'
    $ws.Range($ws.Cells.Item($h + 1, 10), $ws.Cells.Item($last, 10)).Font.Size = 9
    for ($k = 1; $k -le $n; $k++) {
      $ws.Cells.Item($h + $k, 5).Interior.Color = $color[[string]$data[$k, 4]]
      if ($data[$k, 10] -eq 'No') { $ws.Range($ws.Cells.Item($h + $k, 1), $ws.Cells.Item($h + $k, 11)).Font.Color = 0x999999 }
    }
    $ws.Range($ws.Cells.Item($h + 1, 5), $ws.Cells.Item($last, 5)).Font.Bold = $true
    $ws.Range($ws.Cells.Item($h + 1, 12), $ws.Cells.Item($last, 14)).Interior.Color = 0xCCFFFF
    $val = $ws.Range($ws.Cells.Item($h + 1, 12), $ws.Cells.Item($last, 12)).Validation
    $val.Delete(); [void]$val.Add(3, 1, 1, ($ESTADOS -join $xl.International(5)))
    $rng.Rows.AutoFit() | Out-Null
    [void]$rng.AutoFilter()
    $ws.Activate(); $xl.ActiveWindow.SplitRow = $h; $xl.ActiveWindow.SplitColumn = 2
    $xl.ActiveWindow.FreezePanes = $true; $xl.ActiveWindow.Zoom = 90; $ws.Cells.Item(1, 1).Select() | Out-Null
    $sh = "'Supuestos y decisiones'"
    $cPag = "$sh!`$B`$$($h + 1):`$B`$$last"; $cTipo = "$sh!`$E`$$($h + 1):`$E`$$last"
    $cEst = "$sh!`$L`$$($h + 1):`$L`$$last"; $cVig = "$sh!`$K`$$($h + 1):`$K`$$last"

    # --- Resumen
    $rs = $wb.Worksheets.Item(3); $rs.Name = 'Resumen'
    $rs.Cells.Item(1, 1).Value2 = [string](U 'Resumen por p\u00e1gina y tipo (solo puntos vigentes)')
    $rs.Cells.Item(1, 1).Font.Size = 13; $rs.Cells.Item(1, 1).Font.Bold = $true
    $paginas = @($rows | ForEach-Object { $_.pagina } | Select-Object -Unique)
    $rh = 3; $cols = @((U 'P\u00e1gina')) + $TIPOS + @('Total', 'Pendientes')
    for ($c = 0; $c -lt $cols.Count; $c++) { $rs.Cells.Item($rh, $c + 1).Value2 = [string]$cols[$c] }
    Cabecera $rs $rh $cols.Count
    for ($c = 0; $c -lt $TIPOS.Count; $c++) { $rs.Cells.Item($rh, $c + 2).Interior.Color = $color[$TIPOS[$c]]; $rs.Cells.Item($rh, $c + 2).Font.Color = 0 }
    $p = 0
    foreach ($pg in $paginas) {
      $p++; $row = $rh + $p
      $rs.Cells.Item($row, 1).Value2 = $pg
      for ($c = 0; $c -lt $TIPOS.Count; $c++) {
        $L = [char](66 + $c)
        $rs.Cells.Item($row, $c + 2).Formula = "=COUNTIFS($cPag,`$A$row,$cTipo,$L`$$rh,$cVig,""<>No"")"
      }
      $rs.Cells.Item($row, $TIPOS.Count + 2).Formula = "=SUM(B${row}:F${row})"
      $rs.Cells.Item($row, $TIPOS.Count + 3).Formula = "=COUNTIFS($cPag,`$A$row,$cEst,""Pendiente"",$cVig,""<>No"")"
    }
    $tr = $rh + $p + 1
    $rs.Cells.Item($tr, 1).Value2 = 'Total'
    for ($c = 2; $c -le $cols.Count; $c++) { $L = [char](64 + $c); $rs.Cells.Item($tr, $c).Formula = "=SUM(${L}$($rh + 1):${L}$($tr - 1))" }
    $rs.Range($rs.Cells.Item($tr, 1), $rs.Cells.Item($tr, $cols.Count)).Font.Bold = $true
    $all = $rs.Range($rs.Cells.Item($rh, 1), $rs.Cells.Item($tr, $cols.Count)); $all.Borders.LineStyle = 1; $all.Borders.Color = $grey
    $rs.Range($rs.Cells.Item($rh + 1, 2), $rs.Cells.Item($tr, $cols.Count)).HorizontalAlignment = -4108
    $rs.Columns.Item(1).ColumnWidth = 32; for ($c = 2; $c -le $cols.Count; $c++) { $rs.Columns.Item($c).ColumnWidth = 15 }
    $rs.Rows.Item($rh).RowHeight = 30

    # --- Leeme
    $ls = $wb.Worksheets.Item(1); $ls.Name = U 'L\u00e9eme'
    $txt = @(
      @((U 'Supuestos y decisiones de la migraci\u00f3n ASP \u2192 ASP.NET Core'), 'T'),
      @('', ''),
      @((U 'Para qu\u00e9 sirve'), 'H'),
      @((U 'Cada vez que el agente asp-pagina-migrador migra una p\u00e1gina, deja en su an\u00e1lisis (docs/api/analisis/) lo que no pudo confirmar desde el ASP y lo que necesita una decisi\u00f3n de la FEP. Todos esos puntos se acumulan en un registro \u00fanico (docs/migracion/registro-puntos.csv); este libro es la vista para revisarlos y responderlos.'), ''),
      @((U 'Las respuestas vuelven al registro y el agente las lee antes de migrar la siguiente p\u00e1gina, para no volver a suponer lo que ya est\u00e1 decidido.'), ''),
      @('', ''),
      @((U 'Qui\u00e9n revisa y cu\u00e1ndo'), 'H'),
      @("$T_DEC|" + (U 'La FEP, al cerrar cada bloque (institucional, calendarios, rankings...) y antes de activar sus redirecciones 301. Elecci\u00f3n de negocio o de contenido.'), 'R'),
      @("$T_COR|" + (U 'La FEP, al cerrar cada bloque. Cambio que la migraci\u00f3n ya aplica sobre el contenido original: aceptarlo o rechazarlo.'), 'R'),
      @("$T_SUP|" + (U 'El equipo t\u00e9cnico, al conectar la API a la base de datos real (tipos de columnas, NULL, hora local...). No hace falta preguntarlo a la FEP.'), 'R'),
      @("$T_ATC|" + (U 'El equipo t\u00e9cnico, como tareas del bloque: redirecciones, p\u00e1ginas que siguen en la web actual, enlaces ca\u00eddos.'), 'R'),
      @("$T_TEC|" + (U 'Nadie: decisi\u00f3n de dise\u00f1o ya tomada, solo para informaci\u00f3n (estado Informativo).'), 'R'),
      @('', ''),
      @('Estados', 'H'),
      @((U 'Pendiente (sin respuesta) \u00b7 Confirmado (se acepta tal cual) \u00b7 Rechazado (hay que cambiarlo: explicar en Respuesta) \u00b7 Aplicado (el cambio ya est\u00e1 hecho en el c\u00f3digo) \u00b7 No aplica \u00b7 Informativo.'), ''),
      @((U 'Vigente = No: el punto ya no aparece en su an\u00e1lisis (se corrigi\u00f3 o se reescribi\u00f3). Se conserva con su respuesta.'), ''),
      @('', ''),
      @((U 'C\u00f3mo actualizarlo'), 'H'),
      @((U 'Rellenar Estado, Respuesta y Fecha en la hoja de puntos y guardar. Despu\u00e9s: powershell -NoProfile -File tools\\registro-puntos.ps1 (importa las respuestas, a\u00f1ade los puntos de p\u00e1ginas nuevas y regenera este libro). No cambiar la columna Id.'), ''),
      @(((U 'Generado el ') + (Get-Date).ToString('dd/MM/yyyy HH:mm') + (U ' desde docs/migracion/registro-puntos.csv.')), 'N')
    )
    $r = 0
    foreach ($t in $txt) {
      $r++
      if ($t[1] -eq 'R') {
        $parts = $t[0] -split '\|', 2
        $ls.Cells.Item($r, 1).Value2 = [string]$parts[0]; $ls.Cells.Item($r, 2).Value2 = [string]$parts[1]
        $ls.Cells.Item($r, 1).Interior.Color = $color[$parts[0]]; $ls.Cells.Item($r, 1).Font.Bold = $true
        $ls.Cells.Item($r, 1).VerticalAlignment = -4160; $ls.Cells.Item($r, 2).WrapText = $true
      } else {
        $ls.Cells.Item($r, 1).Value2 = [string]$t[0]
        if ($t[1] -eq '' -and $t[0]) {
          $m = $ls.Range($ls.Cells.Item($r, 1), $ls.Cells.Item($r, 2)); $m.Merge(); $m.WrapText = $true
          $m.VerticalAlignment = -4160; $ls.Rows.Item($r).RowHeight = 42
        }
      }
      if ($t[1] -eq 'T') { $ls.Cells.Item($r, 1).Font.Size = 14; $ls.Cells.Item($r, 1).Font.Bold = $true }
      if ($t[1] -eq 'H') { $ls.Cells.Item($r, 1).Font.Size = 11; $ls.Cells.Item($r, 1).Font.Bold = $true; $ls.Cells.Item($r, 1).Font.Color = $navy }
      if ($t[1] -eq 'N') { $ls.Cells.Item($r, 1).Font.Italic = $true; $ls.Cells.Item($r, 1).Font.Color = 0x555555 }
    }
    $ls.Columns.Item(1).ColumnWidth = 30; $ls.Columns.Item(2).ColumnWidth = 100
    $ls.Range('A8:B12').Rows.AutoFit() | Out-Null
    $ls.Activate(); $xl.ActiveWindow.DisplayGridlines = $false; $ls.Cells.Item(1, 1).Select() | Out-Null

    if (Test-Path $xlsxPath) {
      # Copia del Excel anterior antes de sustituirlo, por si tenia respuestas que no se pudieron importar.
      $bak = Join-Path $env:TEMP ('supuestos-decisiones-fep-' + (Get-Date).ToString('yyyyMMdd-HHmmss') + '.xlsx')
      Copy-Item $xlsxPath $bak
      Write-Host "Copia del Excel anterior: $bak"
      Remove-Item $xlsxPath -Confirm:$false
    }
    $wb.SaveAs($xlsxPath, 51); $wb.Close($false)
  }
  "Exportar: $($rows.Count) puntos -> $xlsxPath"
}

function Accion-Consultar {
  $reg = Leer-Registro
  $sel = $reg | Where-Object { $_.estado -notin @('Pendiente', 'Informativo') -and $_.vigente -ne 'No' }
  if ($Pagina) { $sel = $sel | Where-Object { $_.pagina -eq $Pagina } }
  $pend = @($reg | Where-Object { $_.estado -eq 'Pendiente' -and $_.vigente -ne 'No' -and (-not $Pagina -or $_.pagina -eq $Pagina) })
  "Respondidos: $(@($sel).Count) - Pendientes: $($pend.Count)"
  foreach ($r in $sel) { '[{0}] {1} | {2} | {3}: {4} -> {5}' -f $r.id, $r.pagina, $r.tipo, $r.estado, $r.texto, $r.respuesta }
}

function Excel-Bloqueado {
  if (-not (Test-Path $xlsxPath)) { return $false }
  try { $fs = [IO.File]::Open($xlsxPath, 'Open', 'ReadWrite', 'None'); $fs.Close(); return $false } catch { return $true }
}
if ($Accion -in @('Todo', 'Exportar', 'Importar') -and (Excel-Bloqueado)) {
  Write-Error ('El Excel esta abierto (' + $xlsxPath + '). Guarda los cambios, cierralo y vuelve a ejecutar; ' +
               'si no, las respuestas sin guardar no se importarian y no se podria regenerar.')
  exit 1
}

switch ($Accion) {
  'Importar' { Accion-Importar }
  'Extraer' { Accion-Extraer }
  'Exportar' { Accion-Exportar }
  'Consultar' { Accion-Consultar }
  'Todo' { Accion-Importar; Accion-Extraer; Accion-Exportar }
}
