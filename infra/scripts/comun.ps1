# Lo que comparten los scripts del ambiente de pruebas (docs/despliegue.md). Se carga con: . "$PSScriptRoot/comun.ps1"
#
# Los secretos viven en la carpeta de abajo, fuera del repo y solo con permiso para el usuario de Windows. Ningún script
# los muestra: van a variables de entorno del proceso, a la entrada estándar de un comando o al portapapeles.

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Carpeta = Join-Path $HOME '.control-gestion\pruebas'
$ArchivoDeSecretos = Join-Path $Carpeta 'secretos.txt'
$ArchivoDelAmbiente = Join-Path $Carpeta 'ambiente.txt'
$Keystore = Join-Path $Carpeta 'pruebas.jks'
$Grupo = 'rg-control-pruebas'
$Region = 'canadacentral'
$NombreDelDespliegue = 'ambiente-de-pruebas'
$RaizDelRepo = Split-Path (Split-Path $PSScriptRoot)
$ServidorDePruebas = 'crisbuntu2021@192.168.6.249'

# Un archivo de líneas clave=valor, como el claves.txt de docs/datos-de-ejemplo.
function Leer-Archivo([string] $ruta) {
    if (-not (Test-Path $ruta)) { throw "No existe $ruta. Corre primero infra/scripts/nuevos-secretos.ps1." }
    $valores = @{}
    foreach ($linea in Get-Content $ruta) {
        $i = $linea.IndexOf('=')
        if ($i -gt 0) { $valores[$linea.Substring(0, $i)] = $linea.Substring($i + 1) }
    }
    $valores
}

function Leer-Secretos { Leer-Archivo $ArchivoDeSecretos }
function Leer-Ambiente { Leer-Archivo $ArchivoDelAmbiente }

# Todo va en la suscripción "Azure for Students" y en ninguna otra: lo que se cree fuera se podría cobrar en otro perfil
# de facturación. Se comprueba el nombre y el id antes de tocar nada.
function Comprobar-Suscripcion {
    $ambiente = Leer-Ambiente
    $esperada = $ambiente['suscripcion']
    az account set --subscription $esperada 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo elegir la suscripción $esperada. Inicia sesión con: az login --tenant $($ambiente['tenant'])"
    }
    $actual = az account show --query '{nombre:name, id:id, estado:state}' -o json | ConvertFrom-Json
    if ($actual.nombre -ne 'Azure for Students' -or $actual.id -ne $esperada -or $actual.estado -ne 'Enabled') {
        throw "La suscripción activa no es la del ambiente de pruebas ('$($actual.nombre)', $($actual.id), $($actual.estado)). No se hizo nada."
    }
    Write-Host "Suscripción: Azure for Students ($esperada)." -ForegroundColor DarkGray
}

# Las salidas de Bicep (sin secretos): URL de la API, nombres de las cuentas, del servidor MySQL…
function Leer-Salidas {
    $salidas = az deployment group show -g $Grupo -n $NombreDelDespliegue --query properties.outputs -o json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $salidas) { throw 'No hay salidas: corre primero infra/scripts/desplegar-infra.ps1.' }
    $valores = @{}
    foreach ($p in $salidas.PSObject.Properties) { $valores[$p.Name] = $p.Value.value }
    $valores
}

function Confirmar([string] $pregunta) {
    (Read-Host "$pregunta (s/n)") -eq 's'
}
