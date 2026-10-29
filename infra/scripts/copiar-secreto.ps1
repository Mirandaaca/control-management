# Copia un secreto al portapapeles para pegarlo en GitHub (Settings > Environments > pruebas), sin mostrarlo.
#
#   ./infra/scripts/copiar-secreto.ps1 keystore-base64   el keystore, en base64 (ANDROID_KEYSTORE_BASE64)
#   ./infra/scripts/copiar-secreto.ps1 keystore          su contraseña (ANDROID_KEYSTORE_PASSWORD)
#   ./infra/scripts/copiar-secreto.ps1 campo             la contraseña de los usuarios de ejemplo, para pasársela a Julio
#   ./infra/scripts/copiar-secreto.ps1 navegacion        la clave de OpenRouteService, para los user-secrets de la API:
#       dotnet user-secrets set "Navegacion:Clave" (Get-Clipboard) --project ControlGestion.API
param([Parameter(Mandatory)] [ValidateSet('keystore-base64', 'keystore', 'campo', 'navegacion')] [string] $Nombre)
. "$PSScriptRoot/comun.ps1"

$valor = if ($Nombre -eq 'keystore-base64') {
    [Convert]::ToBase64String([IO.File]::ReadAllBytes($Keystore))
} else {
    (Leer-Secretos)[$Nombre]
}
Set-Clipboard -Value $valor
Write-Host "Copiado '$Nombre' al portapapeles. Pégalo, y después copia cualquier otra cosa para que no quede ahí." -ForegroundColor Yellow
