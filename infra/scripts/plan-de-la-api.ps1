# Cambia el plan de la API entre F1 (gratis, el de siempre) y B1, para una presentación o una semana de pruebas. B1 no
# se duerme (Always On) y no tiene el límite de 60 minutos de CPU al día; cuesta unos US$ 13 al mes del crédito (unos
# US$ 3 por semana). docs/despliegue.md, "Subir a B1 por unos días".
#
#   ./infra/scripts/plan-de-la-api.ps1        dice el plan de ahora
#   ./infra/scripts/plan-de-la-api.ps1 -B1    sube a B1 y enciende Always On (pide confirmar: cuesta)
#   ./infra/scripts/plan-de-la-api.ps1 -F1    apaga Always On y vuelve a F1
#
# desplegar-infra.ps1 deja siempre F1 (infra/modulos/api.bicep): si lo corres con la API en B1, vuelve a F1.
param(
    [switch] $B1,
    [switch] $F1
)
. "$PSScriptRoot/comun.ps1"

if ($B1 -and $F1) { throw 'Pasa -B1 o -F1, no los dos.' }

Comprobar-Suscripcion
$salidas = Leer-Salidas
$api = $salidas['nombreDeLaApi']
$plan = az webapp show -g $Grupo -n $api --query serverFarmId -o tsv
if ($LASTEXITCODE -ne 0 -or -not $plan) { throw "No se encontró la API $api." }

function Plan-Actual { az appservice plan show --ids $plan --query sku.name -o tsv }

function Esperar-La-Api {
    Write-Host 'Esperando que la API responda…' -ForegroundColor DarkGray
    foreach ($i in 1..12) {
        try {
            $estado = (Invoke-WebRequest "$($salidas['urlDeLaApi'])/health" -UseBasicParsing -TimeoutSec 20).Content
            if ($estado -eq 'Healthy') { Write-Host 'La API responde: Healthy.' -ForegroundColor Green; return }
        }
        catch { }
        Start-Sleep -Seconds 10
    }
    Write-Host 'La API no respondió en 2 minutos: mira su log con ./infra/scripts/kudu.ps1 o el Log stream del portal.' -ForegroundColor Red
}

if ($B1) {
    if ((Plan-Actual) -eq 'B1') { Write-Host 'La API ya está en B1.'; return }
    if (-not (Confirmar 'B1 cuesta unos US$ 13 al mes del crédito (unos US$ 3 por semana). ¿Subir la API a B1?')) { return }
    az appservice plan update --ids $plan --sku B1 --query sku.name -o tsv | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo cambiar el plan.' }
    az webapp config set -g $Grupo -n $api --always-on true --query alwaysOn -o tsv | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'El plan ya es B1, pero no se pudo encender Always On.' }
    Write-Host 'Listo: la API está en B1, con Always On. Al terminar: ./infra/scripts/plan-de-la-api.ps1 -F1' -ForegroundColor Yellow
    Esperar-La-Api
}
elseif ($F1) {
    if ((Plan-Actual) -eq 'F1') { Write-Host 'La API ya está en F1.'; return }
    # El F1 no acepta Always On: se apaga antes de bajar el plan.
    az webapp config set -g $Grupo -n $api --always-on false --query alwaysOn -o tsv | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo apagar Always On.' }
    az appservice plan update --ids $plan --sku F1 --query sku.name -o tsv | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Always On quedó apagado, pero no se pudo volver a F1.' }
    Write-Host 'Listo: la API está en F1, gratis. Se duerme si no se usa: la primera respuesta puede tardar.' -ForegroundColor Green
    Esperar-La-Api
}
else {
    $alwaysOn = az webapp config show -g $Grupo -n $api --query alwaysOn -o tsv
    Write-Host "La API está en $(Plan-Actual) (Always On: $alwaysOn)."
}
