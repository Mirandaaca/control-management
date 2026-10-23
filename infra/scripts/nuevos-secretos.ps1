# Crea la carpeta de los secretos del ambiente de pruebas y los genera. Nunca los muestra, y nunca reemplaza los que ya
# existen: perder la clave de MySQL o la de JWT obliga a cambiarlas en Azure, y perder la del keystore, a desinstalar el APK.
#
#   ./infra/scripts/nuevos-secretos.ps1 -Suscripcion <id> -Tenant <id> -Correo <correo del presupuesto>
param(
    [Parameter(Mandatory)] [string] $Suscripcion,
    [Parameter(Mandatory)] [string] $Tenant,
    [Parameter(Mandatory)] [string] $Correo
)
. "$PSScriptRoot/comun.ps1"

if (Test-Path $ArchivoDeSecretos) { throw "Ya existe ${ArchivoDeSecretos}: no se reemplaza." }

New-Item -ItemType Directory -Force $Carpeta | Out-Null
# Solo el usuario de Windows puede leer la carpeta (sin los permisos heredados).
icacls $Carpeta /inheritance:r /grant:r "$($env:USERNAME):(OI)(CI)F" | Out-Null

# Letras y números, con al menos una mayúscula, una minúscula y un número: lo pide MySQL de Azure, y así ninguna clave
# rompe una cadena de conexión (un ";") ni un comando.
function Nueva-Clave([int] $largo) {
    $letras = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    do {
        $bytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes($largo)
        $clave = -join ($bytes | ForEach-Object { $letras[$_ % $letras.Length] })
    } until ($clave -cmatch '[A-Z]' -and $clave -cmatch '[a-z]' -and $clave -match '[0-9]')
    $clave
}

@(
    "mysql=$(Nueva-Clave 32)"      # el administrador de MySQL (controladmin)
    "jwt=$(Nueva-Clave 64)"        # Jwt__Key: la API firma los tokens con ella
    "admin=$(Nueva-Clave 24)"      # admin@control.com (Seed__AdminPassword)
    "campo=$(Nueva-Clave 20)"      # los usuarios de ejemplo (campo-*@test.local), los que usa Julio
    "keystore=$(Nueva-Clave 32)"   # el keystore del APK de pruebas y su clave
) | Set-Content -Encoding utf8 $ArchivoDeSecretos

@(
    "suscripcion=$Suscripcion"
    "tenant=$Tenant"
    "correo=$Correo"
) | Set-Content -Encoding utf8 $ArchivoDelAmbiente

Write-Host "Listo: los secretos están en $Carpeta." -ForegroundColor Green
Write-Host 'Guarda una copia de esa carpeta en otro lado (un USB o un gestor de contraseñas): si se pierde el keystore, el APK nuevo no se instala encima del viejo.'
