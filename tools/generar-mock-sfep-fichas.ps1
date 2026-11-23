<#
  Genera src/FEP.Web/wwwroot/mock/sfep-fichas.json: fichas FICTICIAS para el prototipo de SFEP (Fichas jugadores/as).
  SIN CONEXIÓN A BASE DE DATOS. Nombres combinados al azar (semilla fija) y licencias en el rango 9xxxxx.
  Uso (desde la raíz del proyecto): powershell -NoProfile -ExecutionPolicy Bypass -File tools\generar-mock-sfep-fichas.ps1
  Después: tools\generar-mocks-js.ps1
#>
$ErrorActionPreference = 'Stop'
$rnd = New-Object System.Random 2027
function Pick($arr) { $arr[$rnd.Next($arr.Count)] }

$nombresM = 'ALEJANDRO','PABLO','DANIEL','JAVIER','SERGIO','MARIO','HUGO','ÁLVARO','DIEGO','ADRIÁN','IVÁN','RUBÉN','MARCOS','JORGE','LUCAS','IKER','ÓSCAR','RAÚL'
$nombresF = 'LUCÍA','MARTA','PAULA','SARA','LAURA','ELENA','CARMEN','IRENE','NURIA','ANDREA','CLAUDIA','NOELIA','ALBA','JULIA','SILVIA','ROCÍO','EVA','INÉS'
$apellidos = 'GARCÍA','MARTÍN','LÓPEZ','SÁNCHEZ','PÉREZ','GÓMEZ','RUIZ','DÍAZ','MORENO','ÁLVAREZ','ROMERO','NAVARRO','TORRES','DOMÍNGUEZ','VÁZQUEZ','RAMOS','GIL','SERRANO','BLANCO','MOLINA','ORTEGA','DELGADO','CASTRO','RUBIO','MARÍN','SANZ','IGLESIAS','NÚÑEZ','MEDINA','GARRIDO'
$federaciones = @(
  @{ id = 3;  nombre = 'FEDERACIÓN AUTONÓMICA DE EJEMPLO' },
  @{ id = 31; nombre = 'FEDERACIÓN DE PÁDEL DE MADRID' },
  @{ id = 32; nombre = 'FEDERACIÓN ANDALUZA DE PÁDEL' },
  @{ id = 33; nombre = 'FEDERACIÓ CATALANA DE PÀDEL' },
  @{ id = 34; nombre = 'FEDERACIÓN DE PÁDEL DE LA COMUNIDAD VALENCIANA' },
  @{ id = 35; nombre = 'FEDERACIÓN ARAGONESA DE PÁDEL' }
)
$clubesPorFed = @{}
foreach ($f in $federaciones) { $clubesPorFed[$f.id] = 1..5 | ForEach-Object { "CLUB DE PÁDEL EJEMPLO $($f.id)-$_" } }

function Categoria($anio) {
  $edad = 2026 - $anio
  if ($edad -lt 12) { return 'Benjamín' } elseif ($edad -lt 14) { return 'Alevín' } elseif ($edad -lt 16) { return 'Infantil' }
  elseif ($edad -lt 18) { return 'Cadete' } elseif ($edad -lt 19) { return 'Júnior' } elseif ($edad -lt 35) { return 'Absoluto' }
  elseif ($edad -lt 45) { return 'Veteranos +35' } elseif ($edad -lt 55) { return 'Veteranos +45' } else { return 'Veteranos +55' }
}

$fichas = New-Object System.Collections.ArrayList
for ($i = 0; $i -lt 320; $i++) {
  $gen = if ($rnd.NextDouble() -lt .58) { 'M' } else { 'F' }
  $nombre = if ($gen -eq 'M') { Pick $nombresM } else { Pick $nombresF }
  $fed = if ($rnd.NextDouble() -lt .3) { $federaciones[0] } else { Pick $federaciones }
  $club = Pick $clubesPorFed[$fed.id]
  $anio = 1956 + $rnd.Next(0, 63)
  $p = $rnd.NextDouble()
  $estado = if ($p -lt .66) { 'activa' } elseif ($p -lt .74) { 'renovada sin pagar' } elseif ($p -lt .80) { 'pendiente de validar' } elseif ($p -lt .94) { 'caducada' } else { 'baja' }
  $r = $rnd.NextDouble()
  $tipo = if ($r -lt .93) { 'Jugador/a' } elseif ($r -lt .97) { 'Técnico/a' } else { 'Juez-árbitro' }
  $pago = Pick @('tarjeta', 'domiciliación', 'transferencia')
  $ultima = if ($estado -eq 'caducada' -or $estado -eq 'baja') { 2025 - $rnd.Next(0, 2) } else { 2026 }
  $desde = [Math]::Max(2012, $ultima - $rnd.Next(0, 9))
  $ficha = [ordered]@{
    licencia     = [string](900100 + $i * 7)
    nombre       = $nombre
    apellidos    = "$(Pick $apellidos) $(Pick $apellidos)"
    genero       = $gen
    anioNacimiento = $anio
    categoria    = Categoria $anio
    tipo         = $tipo
    federacion   = [ordered]@{ id = $fed.id; nombre = $fed.nombre }
    club         = $club
    estado       = $estado
    temporada    = $ultima
    vigenteHasta = "$ultima-12-31"
    seguro       = if ($estado -eq 'baja') { 'Sin seguro' } else { 'Seguro deportivo incluido' }
    pago         = $pago
    domiciliacionRecibida = ($pago -eq 'domiciliación' -and $estado -eq 'renovada sin pagar')
    extranjero   = ($rnd.NextDouble() -lt .04)
    temporadas   = @($desde..$ultima)
  }
  if ($estado -eq 'activa' -and $rnd.NextDouble() -lt .05) {
    $destino = Pick $clubesPorFed[$fed.id]
    if ($destino -ne $club) { $ficha.transferenciaPendiente = [ordered]@{ deClub = $club; aClub = $destino; fecha = "2026-11-$('{0:00}' -f (10 + $rnd.Next(0, 12)))" } }
  }
  [void]$fichas.Add($ficha)
}

$salida = [ordered]@{
  datosDePrueba = $true
  nota = 'DATOS DE PRUEBA del prototipo SFEP: fichas ficticias generadas por tools/generar-mock-sfep-fichas.ps1. Sin conexión a base de datos. En producción: GET /api/v1/sfep/fichas filtrado por la federación del usuario.'
  temporada = 2026
  federaciones = $federaciones
  items = $fichas
}
$ruta = Join-Path $PSScriptRoot '..\src\FEP.Web\wwwroot\mock\sfep-fichas.json'
$json = $salida | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Resolve-Path (Split-Path $ruta)).Path + '\sfep-fichas.json', $json, (New-Object Text.UTF8Encoding($false)))
"sfep-fichas.json: $($fichas.Count) fichas"
