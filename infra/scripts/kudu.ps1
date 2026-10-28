# Habla con Kudu, la consola de App Service (https://<api>.scm.azurewebsites.net), con el token de tu sesión de Azure CLI:
# la API no tiene credenciales básicas de publicación. El token nunca se muestra.
#
#   ./infra/scripts/kudu.ps1 -Disco                     cuánto ocupa y cuánto queda del 1 GB de /home
#   ./infra/scripts/kudu.ps1 -BajarFotos <destino.zip>  las fotos y los comprobantes (/home/archivos), para el respaldo
param(
    [switch] $Disco,
    [string] $BajarFotos
)
. "$PSScriptRoot/comun.ps1"

if ($Disco -eq [bool]$BajarFotos) { throw 'Pasa -Disco o -BajarFotos <destino.zip>, uno de los dos.' }

Comprobar-Suscripcion
$kudu = "https://$((Leer-Salidas)['nombreDeLaApi']).scm.azurewebsites.net"
$token = az account get-access-token --resource https://management.azure.com --query accessToken -o tsv
$cabeceras = @{ Authorization = "Bearer $token" }

if ($Disco) {
    $cuerpo = @{ command = 'sh -c "df -h /home; du -sh /home/site /home/archivos /home/LogFiles 2>/dev/null"'; dir = '/home' } |
        ConvertTo-Json
    (Invoke-RestMethod -Method Post "$kudu/api/command" -Headers $cabeceras -ContentType 'application/json' -Body $cuerpo).Output
}
else {
    # La API de zip de Kudu ya parte de /home: "home/archivos" pedía /home/home/archivos.
    Invoke-WebRequest "$kudu/api/zip/archivos/" -Headers $cabeceras -OutFile $BajarFotos
    Write-Host "Listo: $BajarFotos ($([math]::Round((Get-Item $BajarFotos).Length / 1MB, 1)) MB)." -ForegroundColor Green
}
