// La API en App Service: Linux, plan F1 (gratis) y el .NET 9 que trae App Service. Se despliega el código, sin Docker.
//
// Los límites del F1: 60 minutos de CPU al día, 1 GB de memoria, 1 GB de disco y sin Always On (se duerme tras un rato
// sin uso). La semana de la presentación se sube a B1 a mano: docs/despliegue.md.

param nombre string
param nombreDelPlan string
param region string

@description('Los orígenes que pueden llamar a la API desde un navegador: el panel y la app web.')
param origenes array

param servidorMysql string
param usuarioMysql string

@secure()
param claveMysql string

@secure()
param claveJwt string

@secure()
param claveAdmin string

@description('La identidad de GitHub Actions, que despliega el código.')
param idDelQueDespliega string

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: nombreDelPlan
  location: region
  kind: 'linux'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: true
  }
}

var origenesDeCors = [for (origen, i) in origenes: { name: 'Cors__Origenes__${i}', value: origen }]

var configuraciones = concat(
  [
    // Con Development se cargarían los orígenes de CORS locales y lo que es solo para desarrollo.
    { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
    // App Service atiende el https y le pasa la petición por http: así la API ve el https y la IP de quien pide.
    { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
    // Las fotos van debajo de /home, que no se borra al reiniciar ni al desplegar, y fuera de /home/site, que es de los
    // despliegues.
    { name: 'Almacenamiento__Ruta', value: '/home/archivos' }
    {
      name: 'ConnectionStrings__DefaultConnection'
      value: 'Server=${servidorMysql};Port=3306;Database=control;User=${usuarioMysql};Password=${claveMysql};SslMode=VerifyFull'
    }
    { name: 'Jwt__Key', value: claveJwt }
    { name: 'Seed__AdminPassword', value: claveAdmin }
    // En Information, EF escribe cada SQL en el log: gasta disco y CPU, que en el F1 son pocos. Va la categoría Microsoft
    // entera (incluye Microsoft.EntityFrameworkCore) porque en Linux un punto en el nombre se cambia por guion bajo.
    { name: 'Logging__LogLevel__Microsoft', value: 'Warning' }
    // CI sube la API ya compilada.
    { name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'false' }
  ],
  origenesDeCors
)

resource sitio 'Microsoft.Web/sites@2024-04-01' = {
  name: nombre
  location: region
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      // .NET 10 (LTS, con soporte hasta el 14/11/2028). La imagen solo trae ese runtime: una API de otra versión no arranca.
      linuxFxVersion: 'DOTNETCORE|10.0'
      // El F1 no tiene Always On.
      alwaysOn: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      // Los logs comparten el 1 GB de disco con el código y las fotos.
      logsDirectorySizeLimit: 35
      appSettings: configuraciones
    }
  }
}

// Sin credenciales básicas de publicación: CI entra con OIDC, y nadie por FTP.
resource sinFtp 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: sitio
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource sinScmBasico 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: sitio
  name: 'scm'
  properties: {
    allow: false
  }
}

resource logs 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: sitio
  name: 'logs'
  properties: {
    httpLogs: {
      fileSystem: {
        enabled: true
        retentionInMb: 35
        retentionInDays: 3
      }
    }
  }
}

var colaboradorDeSitiosWeb = 'de139f84-1756-47ae-9be6-808fbbe84772'

resource rol 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sitio.id, idDelQueDespliega, colaboradorDeSitiosWeb)
  scope: sitio
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', colaboradorDeSitiosWeb)
    principalId: idDelQueDespliega
    principalType: 'ServicePrincipal'
  }
}

output dominio string = sitio.properties.defaultHostName
output ipsDeSalida string = sitio.properties.possibleOutboundIpAddresses
