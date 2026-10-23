# Activa el sitio estático de las dos cuentas de Storage (el panel y la app web). No es una propiedad de ARM, por eso no
# va en Bicep. index.html es el documento de inicio y también el de error: así, abrir /zonas/mapa directo devuelve el
# panel (con un 404, que el navegador muestra igual) y react-router dibuja la pantalla.
#
#   ./infra/scripts/activar-sitios-estaticos.ps1
. "$PSScriptRoot/comun.ps1"

Comprobar-Suscripcion
$salidas = Leer-Salidas
foreach ($cuenta in @($salidas['cuentaDelPanel'], $salidas['cuentaDeLaAppWeb'])) {
    # --auth-mode login: con Entra ID, sin las claves de la cuenta (que además están apagadas).
    az storage blob service-properties update --account-name $cuenta --auth-mode login --static-website `
        --index-document index.html --404-document index.html --query staticWebsite.enabled -o tsv
    if ($LASTEXITCODE -ne 0) { throw "No se pudo activar el sitio estático de $cuenta." }
}
Write-Host "Listo: $($salidas['urlDelPanel']) y $($salidas['urlDeLaAppWeb'])." -ForegroundColor Green
