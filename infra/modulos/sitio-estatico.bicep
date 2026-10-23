// Una cuenta de Storage para un sitio estático (el panel, o la app web para el iPhone de Julio). No duerme y cuesta
// centavos al mes. El sitio estático no es una propiedad de ARM: lo activa infra/scripts/activar-sitios-estaticos.ps1.

param nombre string
param region string

@description('La identidad de GitHub Actions, que sube los archivos al contenedor $web.')
param idDelQueDespliega string

resource cuenta 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: nombre
  location: region
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    // El sitio estático se sirve igual por su dirección web. Los blobs no son públicos, y nadie entra con las claves de la
    // cuenta: CI y los scripts usan Entra ID (--auth-mode login).
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
  }
}

var colaboradorDeDatosDeBlobs = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource rol 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(cuenta.id, idDelQueDespliega, colaboradorDeDatosDeBlobs)
  scope: cuenta
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', colaboradorDeDatosDeBlobs)
    principalId: idDelQueDespliega
    principalType: 'ServicePrincipal'
  }
}

// Un origen no lleva la barra del final, y primaryEndpoints.web sí.
output origen string = replace(cuenta.properties.primaryEndpoints.web, '.net/', '.net')
