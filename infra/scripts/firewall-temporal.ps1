# Deja entrar a MySQL desde la IP pública de la red de Cristopher, solo mientras dura un trabajo a mano (el UPDATE de la
# cobranza vencida o un respaldo). El servidor de pruebas (192.168.6.249) sale por la misma IP.
#
#   ./infra/scripts/firewall-temporal.ps1           agrega la regla
#   ./infra/scripts/firewall-temporal.ps1 -Quitar   la quita (no la dejes puesta)
param([switch] $Quitar)
. "$PSScriptRoot/comun.ps1"

Comprobar-Suscripcion
$servidor = (Leer-Salidas)['servidorMysql'].Split('.')[0]
$regla = 'cristopher-temporal'

if ($Quitar) {
    az mysql flexible-server firewall-rule delete -g $Grupo -n $servidor --rule-name $regla --yes
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo quitar la regla.' }
    Write-Host 'Listo: la regla temporal ya no está.' -ForegroundColor Green
    return
}

$ip = (Invoke-RestMethod 'https://api.ipify.org').Trim()
az mysql flexible-server firewall-rule create -g $Grupo -n $servidor --rule-name $regla --start-ip-address $ip `
    --end-ip-address $ip --query name -o tsv
if ($LASTEXITCODE -ne 0) { throw 'No se pudo agregar la regla.' }
Write-Host "Listo: $ip puede entrar a MySQL. Al terminar: ./infra/scripts/firewall-temporal.ps1 -Quitar" -ForegroundColor Yellow
