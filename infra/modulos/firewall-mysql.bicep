// Una regla del firewall de MySQL por cada IP de salida de la API. Va en un módulo porque Bicep no deja recorrer en un for
// un valor que solo se conoce al desplegar (las IP salen de la API recién creada); un parámetro de módulo, sí.
//
// Las IP del plan F1 son de la "unidad de escalado" de App Service: otras apps de Azure en la misma unidad podrían salir
// por ellas. Igual es mucho menos que "permitir los servicios de Azure". La contraseña y TLS hacen el resto.

param servidor string
param ips array

resource mysql 'Microsoft.DBforMySQL/flexibleServers@2024-12-30' existing = {
  name: servidor
}

resource reglas 'Microsoft.DBforMySQL/flexibleServers/firewallRules@2024-12-30' = [
  for (ip, i) in ips: {
    parent: mysql
    name: 'api-salida-${i}'
    properties: {
      startIpAddress: ip
      endIpAddress: ip
    }
  }
]
