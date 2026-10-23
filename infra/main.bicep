// El ambiente de pruebas en Azure (tarea 13b, docs/despliegue.md): la API en App Service F1, su MySQL flexible B1ms, el
// panel y la app web en sitios estáticos de Storage, la identidad con la que despliega GitHub Actions y una alerta de
// presupuesto. Todo es gratis o cuesta centavos; el grupo de recursos se crea a mano, y esto lo corre
// infra/scripts/desplegar-infra.ps1.
//
// Ningún secreto sale de aquí: los tres llegan como @secure() y solo van a las configuraciones de la API.

@description('La región de todo. Es la del grupo: spaincentral, la más cerca de Bolivia de las que permiten MySQL en esta suscripción.')
param region string = resourceGroup().location

@description('A dónde va la alerta de presupuesto. No va en el repo: lo lee pruebas.bicepparam de una variable de entorno.')
param correoDelPresupuesto string

@secure()
@description('La contraseña del administrador de MySQL.')
param claveMysql string

@secure()
@description('La clave con la que la API firma los tokens (Jwt__Key).')
param claveJwt string

@secure()
@description('La contraseña de admin@control.com, que la API siembra al arrancar (Seed__AdminPassword).')
param claveAdmin string

@description('El repo de GitHub que despliega, tal como lo escribe GitHub (el sujeto del token distingue mayúsculas).')
param repoDeGithub string = 'Mirandaaca/control-management'

@description('El entorno de GitHub Actions que puede desplegar.')
param entornoDeGithub string = 'pruebas'

@description('Las reglas del firewall de MySQL, una por IP de salida de la API (31 en el F1). MySQL las aplica de a una: unos 40 minutos. Con false no se tocan, y quedan las que ya están.')
param actualizarFirewall bool = true

@description('El primer día del mes en que empieza el presupuesto.')
param inicioDelPresupuesto string = utcNow('yyyy-MM-01')

// Los nombres de la API, de MySQL y de las cuentas de Storage son únicos en todo Azure: llevan 6 letras propias del grupo.
var sufijo = take(uniqueString(resourceGroup().id), 6)
var nombres = {
  plan: 'plan-control-pruebas'
  api: 'control-pruebas-api-${sufijo}'
  mysql: 'control-pruebas-mysql-${sufijo}'
  panel: 'controlpanel${sufijo}'
  appWeb: 'controlappweb${sufijo}'
  identidad: 'id-control-pruebas-github'
  presupuesto: 'presupuesto-control-pruebas'
}

// El administrador de MySQL no puede llamarse admin, root ni azure_superuser.
var usuarioMysql = 'controladmin'

module identidad 'modulos/identidad.bicep' = {
  name: 'identidad'
  params: {
    nombre: nombres.identidad
    region: region
    repoDeGithub: repoDeGithub
    entornoDeGithub: entornoDeGithub
  }
}

module mysql 'modulos/mysql.bicep' = {
  name: 'mysql'
  params: {
    nombre: nombres.mysql
    region: region
    usuario: usuarioMysql
    clave: claveMysql
  }
}

module panel 'modulos/sitio-estatico.bicep' = {
  name: 'panel'
  params: {
    nombre: nombres.panel
    region: region
    idDelQueDespliega: identidad.outputs.idDelPrincipal
  }
}

module appWeb 'modulos/sitio-estatico.bicep' = {
  name: 'app-web'
  params: {
    nombre: nombres.appWeb
    region: region
    idDelQueDespliega: identidad.outputs.idDelPrincipal
  }
}

module api 'modulos/api.bicep' = {
  name: 'api'
  params: {
    nombre: nombres.api
    nombreDelPlan: nombres.plan
    region: region
    // El panel y la app web llaman a la API desde el navegador: sus orígenes van en Cors__Origenes__0 y __1.
    origenes: [panel.outputs.origen, appWeb.outputs.origen]
    servidorMysql: mysql.outputs.servidor
    usuarioMysql: usuarioMysql
    claveMysql: claveMysql
    claveJwt: claveJwt
    claveAdmin: claveAdmin
    idDelQueDespliega: identidad.outputs.idDelPrincipal
  }
}

// Solo las IP de salida de la API entran a MySQL. Nunca "permitir los servicios de Azure": abriría la base a cualquier
// cliente de Azure.
module firewall 'modulos/firewall-mysql.bicep' = if (actualizarFirewall) {
  name: 'firewall-mysql'
  params: {
    servidor: nombres.mysql
    ips: split(api.outputs.ipsDeSalida, ',')
  }
}

module presupuesto 'modulos/presupuesto.bicep' = {
  // Un despliegue de la suscripción queda anotado en una región: con otra, el mismo nombre falla (InvalidDeploymentLocation).
  name: 'presupuesto-${region}'
  scope: subscription()
  params: {
    nombre: nombres.presupuesto
    correo: correoDelPresupuesto
    inicio: inicioDelPresupuesto
  }
}

output urlDeLaApi string = 'https://${api.outputs.dominio}'
output urlDelPanel string = panel.outputs.origen
output urlDeLaAppWeb string = appWeb.outputs.origen
output nombreDeLaApi string = nombres.api
output cuentaDelPanel string = nombres.panel
output cuentaDeLaAppWeb string = nombres.appWeb
output servidorMysql string = mysql.outputs.servidor
output idDeClienteDeLaIdentidad string = identidad.outputs.idDeCliente
