# Crea o actualiza la infraestructura del ambiente de pruebas con infra/main.bicep. Primero muestra qué cambiaría
# (what-if) y solo despliega si se confirma. Los secretos van por variables de entorno del proceso: no aparecen en ningún
# comando ni en la pantalla.
#
#   ./infra/scripts/desplegar-infra.ps1                   what-if y, si confirmas, el despliegue
#   ./infra/scripts/desplegar-infra.ps1 -SoloVerCambios   solo el what-if
param([switch] $SoloVerCambios)
. "$PSScriptRoot/comun.ps1"

Comprobar-Suscripcion
$secretos = Leer-Secretos
$ambiente = Leer-Ambiente

if ((az group exists -n $Grupo) -ne 'true') {
    if ($SoloVerCambios) { Write-Host "El grupo $Grupo todavía no existe."; return }
    if (-not (Confirmar "El grupo $Grupo no existe. ¿Lo creo en ${Region}?")) { return }
    az group create -n $Grupo -l $Region --query properties.provisioningState -o tsv
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el grupo.' }
}

$env:CONTROL_CORREO_PRESUPUESTO = $ambiente['correo']
$env:CONTROL_CLAVE_MYSQL = $secretos['mysql']
$env:CONTROL_CLAVE_JWT = $secretos['jwt']
$env:CONTROL_CLAVE_ADMIN = $secretos['admin']
$parametros = Join-Path $RaizDelRepo 'infra/pruebas.bicepparam'
try {
    # ResourceIdOnly: muestra qué se crea o cambia, sin las propiedades (que incluirían las configuraciones de la API).
    az deployment group what-if -g $Grupo -n $NombreDelDespliegue --parameters $parametros --result-format ResourceIdOnly
    if ($LASTEXITCODE -ne 0) { throw 'El what-if falló.' }
    if ($SoloVerCambios -or -not (Confirmar '¿Despliego estos cambios?')) { return }

    az deployment group create -g $Grupo -n $NombreDelDespliegue --parameters $parametros --query properties.outputs -o json
    if ($LASTEXITCODE -ne 0) { throw 'El despliegue falló. Mira el error de arriba y docs/despliegue.md.' }
    Write-Host 'Listo. Si es la primera vez, sigue con infra/scripts/activar-sitios-estaticos.ps1.' -ForegroundColor Green
}
finally {
    Remove-Item Env:CONTROL_CORREO_PRESUPUESTO, Env:CONTROL_CLAVE_MYSQL, Env:CONTROL_CLAVE_JWT, Env:CONTROL_CLAVE_ADMIN `
        -ErrorAction SilentlyContinue
}
