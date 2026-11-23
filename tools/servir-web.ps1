# Servidor estatico minimo para ver src/FEP.Web/wwwroot en local sin .NET, Node ni Python
# (las paginas usan los mock/*.json cuando la API no responde). Solo para desarrollo.
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\servir-web.ps1
# Por defecto sirve src\FEP.Web\wwwroot en http://localhost:5080/
param([string]$Raiz = (Join-Path $PSScriptRoot '..\src\FEP.Web\wwwroot'), [int]$Puerto = 5080)
$Raiz = (Resolve-Path $Raiz).Path
$tipos = @{ '.html'='text/html; charset=utf-8'; '.css'='text/css; charset=utf-8'; '.js'='text/javascript; charset=utf-8';
  '.json'='application/json; charset=utf-8'; '.png'='image/png'; '.jpg'='image/jpeg'; '.jpeg'='image/jpeg'; '.svg'='image/svg+xml'; '.webp'='image/webp'; '.ico'='image/x-icon' }
$l = New-Object Net.HttpListener
$l.Prefixes.Add("http://localhost:$Puerto/")
$l.Start()
Write-Output "Sirviendo $Raiz en http://localhost:$Puerto/"
while ($l.IsListening) {
  $c = $l.GetContext()
  try {
    $ruta = [Uri]::UnescapeDataString($c.Request.Url.AbsolutePath.TrimStart('/'))
    if (-not $ruta) { $ruta = 'index.html' }
    $f = [IO.Path]::GetFullPath((Join-Path $Raiz $ruta))
    if ($f.StartsWith([IO.Path]::GetFullPath($Raiz)) -and (Test-Path $f -PathType Leaf)) {
      $b = [IO.File]::ReadAllBytes($f)
      $ext = [IO.Path]::GetExtension($f).ToLower()
      $c.Response.ContentType = if ($tipos[$ext]) { $tipos[$ext] } else { 'application/octet-stream' }
      $c.Response.OutputStream.Write($b, 0, $b.Length)
    } else { $c.Response.StatusCode = 404 }
  } catch { $c.Response.StatusCode = 500 } finally { $c.Response.Close() }
}
