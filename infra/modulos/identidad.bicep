// La identidad con la que GitHub Actions despliega, sin contraseñas: GitHub le da un token al trabajo del entorno, y Entra
// ID lo cambia por uno de esta identidad (OIDC). Una identidad administrada no pide registrar una aplicación en Entra ID.

param nombre string
param region string
param repoDeGithub string
param entornoDeGithub string

resource identidad 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: nombre
  location: region
}

// Solo los trabajos del entorno de GitHub pueden usarla: un PR o un fork no llegan a Azure.
resource credencial 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identidad
  name: 'github-${entornoDeGithub}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${repoDeGithub}:environment:${entornoDeGithub}'
    audiences: ['api://AzureADTokenExchange']
  }
}

output idDeCliente string = identidad.properties.clientId
output idDelPrincipal string = identidad.properties.principalId
