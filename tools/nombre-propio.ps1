# Misma regla que Texto.NombrePropio (src/FEP.Api/Infrastructure/Texto.cs): sedes en formato "Nombre Propio"
# (DECISION FEP, 13/11/2026). Solo se transforma el texto entero en mayusculas (o en minusculas); si ya mezcla
# mayusculas y minusculas se respeta. Palabras de enlace en minuscula tras un espacio (no al principio); siglas sin
# vocales, numeros romanos y unas pocas siglas conocidas, en mayusculas.
#
# Uso:
#   . (Join-Path $PSScriptRoot 'nombre-propio.ps1')        # desde los generadores de mocks: define NombrePropio
#   powershell -NoProfile -File tools\nombre-propio.ps1 -AplicarMocks
#       aplica la regla al campo "sede" de los mocks del calendario FEP y territorial y regenera mocks.js
param([switch]$AplicarMocks)

function U([string]$s) { [regex]::Unescape($s) }   # script en ASCII: acentos como \uXXXX

$script:NP_Cultura = [Globalization.CultureInfo]::GetCultureInfo('es-ES')
$script:NP_Enlaces = @('de', 'del', 'al', 'y', 'e', 'o', 'u', 'en', 'con', 'por', 'para')
# Articulos: en minuscula solo tras una preposicion ("de la Torre"); si no, son parte del toponimo ("La Nucia").
$script:NP_Articulos = @('la', 'las', 'los', 'el')
$script:NP_Preposiciones = @('de', 'en', 'con', 'por', 'para', 'a')
$script:NP_Siglas = @('FEP', 'FIP', 'WPT', 'UD', 'SA', 'SAD', 'CDE')
$script:NP_Vocales = (U 'aeiouyáéíóúàèìòùäëïöü').ToCharArray()

function NombrePropio([string]$valor) {
  if ($null -eq $valor) { return $null }
  $limpio = $valor.Trim()
  if (-not $limpio) { return $null }
  if ($limpio -cmatch '\p{Lu}' -and $limpio -cmatch '\p{Ll}') { return $limpio }

  $c = $script:NP_Cultura
  $script:NP_Anterior = $null
  return [regex]::Replace($limpio, '\p{L}+', {
      param($m)
      $p = $m.Value
      $previa = $script:NP_Anterior
      $script:NP_Anterior = $p
      $sinVocal = $p.Length -gt 1 -and $p.ToLower($c).IndexOfAny($script:NP_Vocales) -lt 0
      if ($script:NP_Siglas -contains $p -or $p -match '^[IVXivx]+$' -or $sinVocal) { return $p.ToUpper($c) }
      $trasEspacio = $m.Index -gt 0 -and $limpio[$m.Index - 1] -eq ' '
      $articulo = $script:NP_Articulos -contains $p -and $null -ne $previa -and $script:NP_Preposiciones -contains $previa
      if ($trasEspacio -and ($script:NP_Enlaces -contains $p -or $articulo)) { return $p.ToLower($c) }
      return $p.Substring(0, 1).ToUpper($c) + $p.Substring(1).ToLower($c)
    })
}

if ($AplicarMocks) {
  $ErrorActionPreference = 'Stop'
  $mockDir = Join-Path $PSScriptRoot '..\src\FEP.Web\wwwroot\mock' | Resolve-Path
  $utf8 = New-Object Text.UTF8Encoding($false)
  $patron = '("sede"\s*:\s*)"((?:[^"\\]|\\.)*)"'
  $ficheros = @(Get-ChildItem $mockDir -Filter 'competicion-calendario*.json' |
    Where-Object { $_.Name -notlike 'competicion-calendarios-*' })
  foreach ($f in $ficheros) {
    $texto = [IO.File]::ReadAllText($f.FullName, $utf8)
    $cambios = 0
    $nuevo = [regex]::Replace($texto, $patron, {
        param($m)
        $original = ConvertFrom-Json ('"' + $m.Groups[2].Value + '"')
        $sede = NombrePropio $original
        if ($sede -ceq $original) { return $m.Value }
        $script:cambios++
        return $m.Groups[1].Value + (ConvertTo-Json $sede)
      })
    if ($nuevo -cne $texto) { [IO.File]::WriteAllText($f.FullName, $nuevo, $utf8) }
    Write-Host ('{0}: {1} sedes cambiadas' -f $f.Name, $script:cambios)
    $script:cambios = 0
  }
  & (Join-Path $PSScriptRoot 'generar-mocks-js.ps1')
}
