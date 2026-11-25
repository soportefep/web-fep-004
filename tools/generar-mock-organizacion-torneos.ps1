# Genera los mocks de la convocatoria para organizar torneos (Organizacion_Torneos.asp) con la misma forma que la API:
#   src/FEP.Web/wwwroot/mock/competicion-organizacion-torneos.json          GET /api/v1/competicion/organizacion-torneos
#   src/FEP.Web/wwwroot/mock/competicion-organizacion-torneos-clubes.json   GET /api/v1/competicion/organizacion-torneos/clubes
#                                                                          (todos los clubes: el front filtra por provincia)
# Origen:
#   - temporada, plazo, titulo, textos, documentos (titulos), pasos y proteccion de datos
#       -> src/FEP.Api/Contenidos/organizacion-torneos.json (se regeneran siempre)
#   - torneos, comunidades/provincias, ficheros de los documentos y clubes -> salen de la BD. Por defecto se CONSERVAN
#     del mock actual. Con -DesdeWebPublica se vuelven a tomar de la web publica (solo lectura: Organizacion_Torneos,
#     cargarfechastorneo.asp, comboProvincias.asp y comboclubesLicitacion.asp; unas 125 peticiones GET). Pasado el
#     plazo la web actual redirige a Organizacion_FueraPlazo.asp y la instantanea no se puede rehacer.
# Despues regenera mocks.js. Uso:
#   powershell -NoProfile -File tools\generar-mock-organizacion-torneos.ps1 [-DesdeWebPublica]
param([switch]$DesdeWebPublica)
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$origen = Join-Path $raiz 'src\FEP.Api\Contenidos\organizacion-torneos.json'
$destino = Join-Path $raiz 'src\FEP.Web\wwwroot\mock\competicion-organizacion-torneos.json'
$destinoClubes = Join-Path $raiz 'src\FEP.Web\wwwroot\mock\competicion-organizacion-torneos-clubes.json'
$utf8 = New-Object Text.UTF8Encoding($false)
$web = 'https://www.padelfederacion.es/'
$baseRefs = $web + 'refs/'
Add-Type -AssemblyName System.Web

function UrlRefs([string]$ruta) {
  if ([string]::IsNullOrWhiteSpace($ruta)) { return $null }
  $partes = $ruta.Replace('\', '/').Split('/') | Where-Object { $_ } | ForEach-Object { [Uri]::EscapeDataString($_) }
  return $baseRefs + ($partes -join '/')
}
function Limpio([string]$s) { ([System.Web.HttpUtility]::HtmlDecode($s) -replace '\s+', ' ').Trim() }

$datos = ConvertFrom-Json ([IO.File]::ReadAllText($origen, $utf8))

# --------------------------------------------------------------------------- datos de la BD
$torneos = @(); $comunidades = @(); $archivos = @{}; $clubes = $null
if ($DesdeWebPublica) {
  $enc = [Text.Encoding]::GetEncoding(1252)   # la web actual sirve iso-8859-1 / Windows-1252
  $wc = New-Object Net.WebClient
  function Bajar([string]$ruta) { $b = $wc.DownloadData($web + $ruta); Start-Sleep -Milliseconds 60; return $enc.GetString($b) }

  $pagina = Bajar 'Organizacion_Torneos'
  if ($pagina -notmatch 'name="torneo"') { throw 'La web actual no muestra el formulario (plazo cerrado): no se puede rehacer la instantanea.' }

  # Documentos: archivo3 (Calendario Provisional = ArchivosLicitacion) y archivo2 (Condiciones = CalendarioLicitacion)
  $docs = [regex]::Matches($pagina, 'href="https?://padelfederacion\.es/refs/docs/([^"]+)"')
  if ($docs.Count -ge 1) { $archivos['calendario-provisional'] = [Uri]::UnescapeDataString($docs[0].Groups[1].Value) }
  if ($docs.Count -ge 2) { $archivos['condiciones'] = [Uri]::UnescapeDataString($docs[1].Groups[1].Value) }

  # Desplegable de torneos: grupo segun el optgroup anterior
  $select = [regex]::Match($pagina, '(?s)<select name="torneo".*?</select>').Value
  $grupo = $null; $lista = @()
  foreach ($m in [regex]::Matches($select, '(?s)<optgroup label="([^"]*)">|<option value="(\d+)"\s*>(.*?)</option>')) {
    if ($m.Groups[1].Success) {
      switch -regex ($m.Groups[1].Value) {
        'MENORES' { $grupo = 'menores' }
        'ABSOLUTOS' { $grupo = 'absolutos' }
        'VETERANOS' { $grupo = 'veteranos' }
        'SILLA' { $grupo = 'silla' }
      }
      continue
    }
    $id = [int]$m.Groups[2].Value
    if ($id -eq 0) { continue }
    # En silla de ruedas el ASP anadia " / fecha fin" al nombre
    $titulo = (Limpio $m.Groups[3].Value) -replace '\s/\s\d{2}/\d{2}/\d{4}$', ''
    $f = Limpio ((Bajar "cargarfechastorneo.asp?id=$id") -replace '<[^>]+>', ' ')
    $fi = [regex]::Match($f, 'inicio Torneo:\s*(\d{2})/(\d{2})/(\d{4})')
    $ff = [regex]::Match($f, 'final Torneo:\s*(\d{2})/(\d{2})/(\d{4})')
    # Fianza: 0 ("A Negociar...") o vacia (" euros") -> null, como la API
    $fz = [regex]::Match($f, 'Fianza del torneo:\s*([\d.,]+)\s*euros')
    $fianza = $null
    if ($fz.Success) {
      $valor = [decimal]::Parse(($fz.Groups[1].Value -replace '\.', '' -replace ',', '.'), [Globalization.CultureInfo]::InvariantCulture)
      if ($valor -gt 0) { $fianza = $valor }
    }
    $lista += [pscustomobject][ordered]@{
      id          = $id
      titulo      = $titulo
      grupo       = $grupo
      fechaInicio = if ($fi.Success) { '{0}-{1}-{2}' -f $fi.Groups[3].Value, $fi.Groups[2].Value, $fi.Groups[1].Value } else { $null }
      fechaFin    = if ($ff.Success) { '{0}-{1}-{2}' -f $ff.Groups[3].Value, $ff.Groups[2].Value, $ff.Groups[1].Value } else { $null }
      fianza      = $fianza
    }
  }
  # Como la API: por fecha de inicio (sin fecha, al final) y, a igual fecha, por id
  $torneos = @($lista | Sort-Object @{ Expression = { if ($_.fechaInicio) { $_.fechaInicio } else { '9999' } } }, id)

  # Comunidades (sql_com, por nombre) y sus provincias (comboProvincias.asp)
  $selCom = [regex]::Match($pagina, '(?s)<select name="comunidad".*?</select>').Value
  foreach ($m in [regex]::Matches($selCom, '<option value="(\d+)"\s*>([^<]*)</option>')) {
    $idc = [int]$m.Groups[1].Value
    if ($idc -eq 0) { continue }
    $prov = Bajar "comboProvincias.asp?id=$idc"
    $provincias = @([regex]::Matches($prov, '<option value=(\d+)>([^<]*)</option>') | ForEach-Object {
      [pscustomobject][ordered]@{ id = [int]$_.Groups[1].Value; nombre = Limpio $_.Groups[2].Value }
    })
    $comunidades += [pscustomobject][ordered]@{ id = $idc; nombre = Limpio $m.Groups[2].Value; provincias = $provincias }
  }

  # Clubes de cada provincia (comboclubesLicitacion.asp)
  $clubes = @()
  foreach ($p in ($comunidades | ForEach-Object { $_.provincias } | Sort-Object id)) {
    $html = Bajar "comboclubesLicitacion.asp?id=$($p.id)"
    foreach ($m in [regex]::Matches($html, '<option value=([0-9A-Za-z]+)>([^<]*)</option>')) {
      $clubes += [pscustomobject][ordered]@{ id = $m.Groups[1].Value; nombre = Limpio $m.Groups[2].Value; provinciaId = $p.id }
    }
  }
} elseif (Test-Path $destino) {
  $actual = ConvertFrom-Json ([IO.File]::ReadAllText($destino, $utf8))
  $torneos = @($actual.torneos)
  $comunidades = @($actual.comunidades)
  foreach ($d in $actual.documentos) { $archivos[$d.clave] = $d.archivo -replace '^docs/', '' }
}

# --------------------------------------------------------------------------- mock principal
$documentos = @($datos.documentos | Where-Object { $archivos.ContainsKey($_.clave) } | ForEach-Object {
  $archivo = 'docs/' + $archivos[$_.clave]
  [pscustomobject][ordered]@{ clave = $_.clave; titulo = $_.titulo; descripcion = $_.descripcion; archivo = $archivo; url = UrlRefs $archivo }
})
$grupos = @(
  [pscustomobject][ordered]@{ clave = 'menores'; titulo = 'Torneos de menores' }
  [pscustomobject][ordered]@{ clave = 'absolutos'; titulo = 'Torneos absolutos y sub-23' }
  [pscustomobject][ordered]@{ clave = 'veteranos'; titulo = 'Torneos de veteranos' }
  [pscustomobject][ordered]@{ clave = 'silla'; titulo = [regex]::Unescape('Torneos de p\u00e1del en silla') }
)
$envio = if ($datos.envio) { $datos.envio } else { 'Organizacion_P0.asp' }

# Mismo orden de propiedades que OrganizacionTorneosDto. "abierta": estado de la instantanea (el front vuelve a
# comprobar fechaLimite con el reloj del navegador).
$mock = [pscustomobject][ordered]@{
  temporada       = $datos.temporada
  abierta         = $true
  fechaLimite     = $datos.fechaLimite
  titulo          = $datos.titulo
  introduccion    = @($datos.introduccion)
  avisoCerrado    = $datos.avisoCerrado
  documentos      = $documentos
  pasos           = @($datos.pasos | Sort-Object orden)
  grupos          = $grupos
  torneos         = $torneos
  comunidades     = $comunidades
  proteccionDatos = @($datos.proteccionDatos)
  envioUrl        = $web + $envio
  actualizado     = $datos.actualizado
}
[IO.File]::WriteAllText($destino, (ConvertTo-Json -InputObject $mock -Depth 10), $utf8)
Write-Output "Mock generado: $destino ($($torneos.Count) torneos, $($comunidades.Count) comunidades, $($documentos.Count) documentos)"

# Clubes: un club por linea (son ~3.500) para que el fichero no crezca con la sangria de ConvertTo-Json
if ($null -ne $clubes) {
  $lineas = @($clubes | ForEach-Object { '  ' + (ConvertTo-Json -InputObject $_ -Compress) })
  [IO.File]::WriteAllText($destinoClubes, "[`n" + ($lineas -join ",`n") + "`n]`n", $utf8)
  Write-Output "Mock generado: $destinoClubes ($($clubes.Count) clubes)"
} elseif (-not (Test-Path $destinoClubes)) {
  [IO.File]::WriteAllText($destinoClubes, "[]`n", $utf8)
}

& (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
