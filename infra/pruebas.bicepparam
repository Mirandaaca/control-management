// Los parámetros del ambiente de pruebas. Los secretos y el correo no están aquí: los lee de variables de entorno, que
// pone infra/scripts/desplegar-infra.ps1 desde la carpeta de los secretos (fuera del repo). Así no pasan por ningún
// comando ni por la pantalla.
using 'main.bicep'

param correoDelPresupuesto = readEnvironmentVariable('CONTROL_CORREO_PRESUPUESTO')
param claveMysql = readEnvironmentVariable('CONTROL_CLAVE_MYSQL')
param claveJwt = readEnvironmentVariable('CONTROL_CLAVE_JWT')
param claveAdmin = readEnvironmentVariable('CONTROL_CLAVE_ADMIN')
