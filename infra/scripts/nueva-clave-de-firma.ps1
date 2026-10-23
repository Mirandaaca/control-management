# Genera el keystore con el que CI firma el APK de pruebas. La contraseña sale de secretos.txt y llega a keytool por una
# variable de entorno (-storepass:env): no pasa por el comando ni por la pantalla. Nunca reemplaza uno que ya existe: con
# otra clave, el APK nuevo no se instala encima del viejo.
#
#   ./infra/scripts/nueva-clave-de-firma.ps1
. "$PSScriptRoot/comun.ps1"

if (Test-Path $Keystore) { throw "Ya existe ${Keystore}: no se reemplaza." }

$env:CONTROL_CLAVE_KEYSTORE = (Leer-Secretos)['keystore']
try {
    keytool -genkeypair -keystore $Keystore -storetype PKCS12 -alias pruebas -keyalg RSA -keysize 2048 -validity 3650 `
        -dname 'CN=Control de Gestion (pruebas), O=Del Campo SRL, C=BO' `
        -storepass:env CONTROL_CLAVE_KEYSTORE -keypass:env CONTROL_CLAVE_KEYSTORE
    if ($LASTEXITCODE -ne 0) { throw 'keytool falló.' }
}
finally {
    Remove-Item Env:CONTROL_CLAVE_KEYSTORE -ErrorAction SilentlyContinue
}
Write-Host "Listo: $Keystore (alias pruebas). Súbelo a GitHub con infra/scripts/copiar-secreto.ps1 keystore-base64." -ForegroundColor Green
