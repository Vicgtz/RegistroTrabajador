# Publica la API y el cliente para el servidor de pruebas (entorno "Pruebas"), sin Visual Studio y sin la carga de trabajo wasm-tools.
#   API:     http://192.168.68.124:5002  -> copiar RegistroTrabajo.Api\bin\publish\pruebas  a C:\inetpub\RegistroTrabajo\api
#   Cliente: http://192.168.68.124:5003  -> copiar RegistroTrabajo\bin\publish\pruebas      a C:\inetpub\RegistroTrabajo\client
# Uso:  .\publicar-pruebas.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

foreach ($p in 'RegistroTrabajo.Api', 'RegistroTrabajo') {
    $salida = Join-Path $p 'bin\publish\pruebas'
    if (Test-Path $salida) { Remove-Item $salida -Recurse -Force }   # evita archivos viejos mezclados
    dotnet publish $p -p:PublishProfile=Pruebas --nologo
    if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de $p" }
}

Write-Host ""
Write-Host "Listo. Copie al servidor:"
Write-Host "  $PSScriptRoot\RegistroTrabajo.Api\bin\publish\pruebas   ->  C:\inetpub\RegistroTrabajo\api"
Write-Host "  $PSScriptRoot\RegistroTrabajo\bin\publish\pruebas       ->  C:\inetpub\RegistroTrabajo\client"
