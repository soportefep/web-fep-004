<#
  Genera src/FEP.Web/wwwroot/mock/sfep-fichas.json: fichas FICTICIAS para el prototipo de SFEP (Fichas jugadores/as).
  SIN CONEXIÓN A BASE DE DATOS. Personas inventadas (nombres combinados al azar con semilla fija).
  DNI, teléfonos y correos son de prueba: DNI con prefijo 99, teléfonos 600 000 xxx y correos @ejemplo.test.

  Una ficha es una PERSONA, que puede tener 1, 2 o 3 licencias: jugador/a, técnico/a y juez-árbitro
  (columnas Lic.Jug, Lic.Tec y Lic.Arb del SFEP actual). Campos: los criterios de búsqueda de Gestion/Fichas.asp.

  Uso (desde la raíz del proyecto): powershell -NoProfile -ExecutionPolicy Bypass -File tools\generar-mock-sfep-fichas.ps1
  Después: tools\generar-mocks-js.ps1
#>
$ErrorActionPreference = 'Stop'
$rnd = New-Object System.Random 2027
function Pick($arr) { $arr[$rnd.Next($arr.Count)] }
function SiNo($p) { if ($rnd.NextDouble() -lt $p) { 'S' } else { 'N' } }

$nombresM = 'ALEJANDRO','PABLO','DANIEL','JAVIER','SERGIO','MARIO','HUGO','ÁLVARO','DIEGO','ADRIÁN','IVÁN','RUBÉN','MARCOS','JORGE','LUCAS','IKER','ÓSCAR','RAÚL'
$nombresF = 'LUCÍA','MARTA','PAULA','SARA','LAURA','ELENA','CARMEN','IRENE','NURIA','ANDREA','CLAUDIA','NOELIA','ALBA','JULIA','SILVIA','ROCÍO','EVA','INÉS'
$apellidos = 'GARCÍA','MARTÍN','LÓPEZ','SÁNCHEZ','PÉREZ','GÓMEZ','RUIZ','DÍAZ','MORENO','ÁLVAREZ','ROMERO','NAVARRO','TORRES','DOMÍNGUEZ','VÁZQUEZ','RAMOS','GIL','SERRANO','BLANCO','MOLINA','ORTEGA','DELGADO','CASTRO','RUBIO','MARÍN','SANZ','IGLESIAS','NÚÑEZ','MEDINA','GARRIDO'
$letrasDni = 'TRWAGMYFPDXBNJZSQVHLCKE'

# Comunidades y provincias de residencia (combo comunidad -> provincia de Fichas.asp) y localidades de ejemplo
$comunidades = @(
  [ordered]@{ id = 13; nombre = 'Comunidad de Madrid'; provincias = @([ordered]@{ id = 28; nombre = 'Madrid'; localidades = @('Madrid','Alcobendas','Getafe','Las Rozas') }) },
  [ordered]@{ id = 1; nombre = 'Andalucía'; provincias = @(
      [ordered]@{ id = 41; nombre = 'Sevilla'; localidades = @('Sevilla','Dos Hermanas') },
      [ordered]@{ id = 29; nombre = 'Málaga'; localidades = @('Málaga','Marbella') },
      [ordered]@{ id = 18; nombre = 'Granada'; localidades = @('Granada','Motril') }) },
  [ordered]@{ id = 9; nombre = 'Cataluña'; provincias = @(
      [ordered]@{ id = 8; nombre = 'Barcelona'; localidades = @('Barcelona','Sabadell') },
      [ordered]@{ id = 17; nombre = 'Girona'; localidades = @('Girona','Figueres') },
      [ordered]@{ id = 43; nombre = 'Tarragona'; localidades = @('Tarragona','Reus') }) },
  [ordered]@{ id = 10; nombre = 'Comunitat Valenciana'; provincias = @(
      [ordered]@{ id = 46; nombre = 'Valencia'; localidades = @('Valencia','Gandia') },
      [ordered]@{ id = 3; nombre = 'Alicante'; localidades = @('Alicante','Elche') },
      [ordered]@{ id = 12; nombre = 'Castellón'; localidades = @('Castellón de la Plana','Vila-real') }) },
  [ordered]@{ id = 2; nombre = 'Aragón'; provincias = @(
      [ordered]@{ id = 50; nombre = 'Zaragoza'; localidades = @('Zaragoza','Calatayud') },
      [ordered]@{ id = 22; nombre = 'Huesca'; localidades = @('Huesca','Jaca') }) },
  [ordered]@{ id = 99; nombre = 'Comunidad de Ejemplo'; provincias = @(
      [ordered]@{ id = 991; nombre = 'Provincia de Ejemplo A'; localidades = @('Localidad Ejemplo 1','Localidad Ejemplo 2') },
      [ordered]@{ id = 992; nombre = 'Provincia de Ejemplo B'; localidades = @('Localidad Ejemplo 3') }) }
)
$federaciones = @(
  [ordered]@{ id = 3;  nombre = 'FEDERACIÓN AUTONÓMICA DE EJEMPLO'; abreviatura = 'FAE'; comunidad = 99 },
  [ordered]@{ id = 31; nombre = 'FEDERACIÓN DE PÁDEL DE MADRID'; abreviatura = 'FPM'; comunidad = 13 },
  [ordered]@{ id = 32; nombre = 'FEDERACIÓN ANDALUZA DE PÁDEL'; abreviatura = 'FAP'; comunidad = 1 },
  [ordered]@{ id = 33; nombre = 'FEDERACIÓ CATALANA DE PÀDEL'; abreviatura = 'FCP'; comunidad = 9 },
  [ordered]@{ id = 34; nombre = 'FEDERACIÓN DE PÁDEL DE LA COMUNIDAD VALENCIANA'; abreviatura = 'FPCV'; comunidad = 10 },
  [ordered]@{ id = 35; nombre = 'FEDERACIÓN ARAGONESA DE PÁDEL'; abreviatura = 'FARP'; comunidad = 2 }
)
$clubesPorFed = @{}
foreach ($f in $federaciones) { $clubesPorFed[$f.id] = 1..5 | ForEach-Object { "CLUB DE PÁDEL EJEMPLO $($f.id)-$_" } }

# Categorías del buscador de Fichas.asp (BEN, ALE, INF, CAD, JUN, >35 … >55); los demás adultos, Absoluta
function Categoria($anio) {
  $edad = 2026 - $anio
  if ($edad -lt 12) { 'Benjamín' } elseif ($edad -lt 14) { 'Alevín' } elseif ($edad -lt 16) { 'Infantil' }
  elseif ($edad -lt 18) { 'Cadete' } elseif ($edad -lt 19) { 'Júnior' } elseif ($edad -lt 35) { 'Absoluta' }
  elseif ($edad -lt 40) { '+35' } elseif ($edad -lt 45) { '+40' } elseif ($edad -lt 50) { '+45' } elseif ($edad -lt 55) { '+50' } else { '+55' }
}
function EstadoLicencia() {
  $p = $rnd.NextDouble()
  if ($p -lt .66) { 'activa' } elseif ($p -lt .74) { 'renovada sin pagar' } elseif ($p -lt .80) { 'pendiente de validar' } elseif ($p -lt .94) { 'caducada' } else { 'baja' }
}
function Licencia($tipo, $numero, $estado) {
  $temp = if ($estado -eq 'caducada' -or $estado -eq 'baja') { 2025 - $rnd.Next(0, 2) } else { 2026 }
  [ordered]@{ tipo = $tipo; numero = [string]$numero; estado = $estado; temporada = $temp; vigenteHasta = "$temp-12-31" }
}
function SinTilde($t) { ($t.Normalize([Text.NormalizationForm]::FormD) -replace '\p{Mn}', '').ToLower() -replace '[^a-z]', '' }

$fichas = New-Object System.Collections.ArrayList
$nTec = 0; $nArb = 0
for ($i = 0; $i -lt 320; $i++) {
  $gen = if ($rnd.NextDouble() -lt .58) { 'M' } else { 'F' }
  $nombre = if ($gen -eq 'M') { Pick $nombresM } else { Pick $nombresF }
  $ap1 = Pick $apellidos; $ap2 = Pick $apellidos
  $fed = if ($rnd.NextDouble() -lt .3) { $federaciones[0] } else { Pick $federaciones }
  $club = Pick $clubesPorFed[$fed.id]
  $anio = 1956 + $rnd.Next(0, 63)
  $edad = 2026 - $anio

  # Licencias de la persona: casi siempre jugador/a; a veces también técnico/a y/o juez-árbitro
  $lic = New-Object System.Collections.ArrayList
  $r = $rnd.NextDouble()
  $esJug = $r -lt .9
  $estadoJug = EstadoLicencia
  if ($esJug) { [void]$lic.Add((Licencia 'jugador' (900100 + $i * 7) $estadoJug)) }
  $adulto = $edad -ge 18
  if ($esJug) {
    $quiereTec = $adulto -and $rnd.NextDouble() -lt .12
    $quiereArb = $adulto -and $rnd.NextDouble() -lt .07
  } else {
    # Sin licencia de jugador/a: técnico/a, juez-árbitro o ambas
    $q = $rnd.NextDouble()
    $quiereTec = $adulto -and $q -lt .7
    $quiereArb = $adulto -and ($q -ge .55)
  }
  if ($quiereTec) { $nTec++; [void]$lic.Add((Licencia 'tecnico' (3000 + $nTec * 3) (EstadoLicencia))) }
  if ($quiereArb) { $nArb++; [void]$lic.Add((Licencia 'arbitro' (100 + $nArb * 2) (EstadoLicencia))) }
  if ($lic.Count -eq 0) { [void]$lic.Add((Licencia 'jugador' (900100 + $i * 7) $estadoJug)) }
  $principal = $lic[0]

  # Residencia: casi siempre en la comunidad de su federación
  $com = if ($rnd.NextDouble() -lt .9) { $comunidades | Where-Object { $_.id -eq $fed.comunidad } } else { Pick $comunidades }
  $prov = Pick $com.provincias
  $loc = Pick $prov.localidades

  $espanol = SiNo .94
  $comunitario = if ($espanol -eq 'S') { 'S' } else { SiNo .5 }
  $pago = Pick @('tarjeta', 'domiciliación', 'transferencia')
  $numDni = 99000000 + $rnd.Next(0, 999999)
  $dni = if ($espanol -eq 'S') { "$numDni$($letrasDni[$numDni % 23])" } else { "Y99$('{0:00000}' -f $rnd.Next(0, 99999))$($letrasDni[$rnd.Next(23)])" }
  $desde = [Math]::Max(2012, $principal.temporada - $rnd.Next(0, 9))

  $ficha = [ordered]@{
    idFicha        = 400000 + $i * 13
    licencia       = $principal.numero
    nombre         = $nombre
    apellido1      = $ap1
    apellido2      = $ap2
    apellidos      = "$ap1 $ap2"
    dni            = $dni
    genero         = $gen
    anioNacimiento = $anio
    categoria      = Categoria $anio
    menor          = ($edad -lt 18)
    federacion     = [ordered]@{ id = $fed.id; nombre = $fed.nombre; abreviatura = $fed.abreviatura }
    club           = $club
    licencias      = $lic
    tipo           = $principal.tipo
    estado         = $principal.estado
    temporada      = $principal.temporada
    vigenteHasta   = $principal.vigenteHasta
    tramitadoJug   = ($esJug -and $estadoJug -eq 'activa' -and $rnd.NextDouble() -lt .85)
    residencia     = [ordered]@{ comunidad = [ordered]@{ id = $com.id; nombre = $com.nombre }; provincia = [ordered]@{ id = $prov.id; nombre = $prov.nombre }; localidad = $loc }
    telefono       = "600 000 $('{0:000}' -f $rnd.Next(0, 1000))"
    email          = "$(SinTilde $nombre).$(SinTilde $ap1)$i@ejemplo.test"
    idTpv          = if ($pago -eq 'tarjeta') { [string](10000 + $rnd.Next(0, 89999)) } else { $null }
    espanol        = ($espanol -eq 'S')
    comunitario    = ($comunitario -eq 'S')
    residente      = ($espanol -eq 'S' -or $rnd.NextDouble() -lt .7)
    afiliadoFip    = ($rnd.NextDouble() -lt .05)
    extranjero     = ($espanol -eq 'N')
    seguro         = if ($principal.estado -eq 'baja') { 'Sin seguro' } else { 'Seguro deportivo incluido' }
    pago           = $pago
    domiciliacionRecibida = ($pago -eq 'domiciliación' -and $principal.estado -eq 'renovada sin pagar')
    temporadas     = @($desde..$principal.temporada)
  }
  if ($principal.estado -eq 'activa' -and $rnd.NextDouble() -lt .05) {
    $destino = Pick $clubesPorFed[$fed.id]
    if ($destino -ne $club) { $ficha.transferenciaPendiente = [ordered]@{ deClub = $club; aClub = $destino; fecha = "2026-11-$('{0:00}' -f (10 + $rnd.Next(0, 12)))" } }
  }
  [void]$fichas.Add($ficha)
}

$salida = [ordered]@{
  datosDePrueba = $true
  nota = 'DATOS DE PRUEBA del prototipo SFEP: personas ficticias generadas por tools/generar-mock-sfep-fichas.ps1 (DNI 99…, teléfonos 600 000 xxx, correos @ejemplo.test). Sin conexión a base de datos. Cada ficha puede tener licencia de jugador/a, técnico/a y juez-árbitro. En producción: GET /api/v1/sfep/fichas filtrado por la federación del usuario.'
  temporada = 2026
  federaciones = $federaciones
  comunidades = $comunidades
  items = $fichas
}
$ruta = Join-Path $PSScriptRoot '..\src\FEP.Web\wwwroot\mock\sfep-fichas.json'
$json = $salida | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText((Resolve-Path (Split-Path $ruta)).Path + '\sfep-fichas.json', $json, (New-Object Text.UTF8Encoding($false)))
$multi = @($fichas | Where-Object { $_.licencias.Count -gt 1 }).Count
"sfep-fichas.json: $($fichas.Count) fichas ($multi con más de una licencia)"
