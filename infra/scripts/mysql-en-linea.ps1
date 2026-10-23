# Corre un SQL contra la base en línea, o baja un respaldo con mysqldump. La PC no tiene cliente de MySQL: se usa el de la
# imagen mysql:8.4 en el servidor de pruebas (192.168.6.249), que sale a internet por la misma IP que la PC.
#
# Antes, ./infra/scripts/firewall-temporal.ps1; al terminar, con -Quitar.
#
#   ./infra/scripts/mysql-en-linea.ps1 -Sql <archivo.sql>        corre el archivo y muestra el resultado
#   ./infra/scripts/mysql-en-linea.ps1 -Respaldo <destino.sql>   baja la base entera (estructura y datos)
#
# La contraseña viaja como la primera línea de la entrada estándar, que el servidor lee con `read`: nunca está en un
# comando, en la lista de procesos ni en la pantalla. La conexión valida el certificado de Azure (VERIFY_IDENTITY).
param(
    [string] $Sql,
    [string] $Respaldo
)
. "$PSScriptRoot/comun.ps1"

if ([bool]$Sql -eq [bool]$Respaldo) { throw 'Pasa -Sql <archivo> o -Respaldo <destino>, uno de los dos.' }

Comprobar-Suscripcion
$servidor = (Leer-Salidas)['servidorMysql']
$clave = (Leer-Secretos)['mysql']
$conexion = "-h $servidor -u controladmin --ssl-mode=VERIFY_IDENTITY --ssl-ca=/etc/pki/tls/certs/ca-bundle.crt"
# En Windows, PowerShell termina cada línea con CR LF: el servidor quita los CR antes de leer. Si no, la contraseña
# llegaría con un CR de más y MySQL respondería "Access denied".
$antes = "tr -d '\r' | { read -r MYSQL_PWD; export MYSQL_PWD; docker run --rm -i -e MYSQL_PWD mysql:8.4"

if ($Sql) {
    $entrada = @($clave) + (Get-Content $Sql)
    $entrada | ssh $ServidorDePruebas "$antes mysql $conexion -t control; }"
}
else {
    # --set-gtid-purged=OFF y --no-tablespaces: el administrador de Azure no tiene esos privilegios, y el respaldo se
    # restaura en otra base sin ellos.
    $clave | ssh $ServidorDePruebas "$antes mysqldump $conexion --single-transaction --set-gtid-purged=OFF --no-tablespaces --routines control; }" |
        Set-Content -Encoding utf8 $Respaldo
    Write-Host "Listo: $Respaldo. Las fotos van aparte (docs/despliegue.md, 'Respaldo')." -ForegroundColor Green
}
if ($LASTEXITCODE -ne 0) { throw 'MySQL respondió con un error (¿pusiste la regla temporal del firewall?).' }
