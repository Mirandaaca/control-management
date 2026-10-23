// MySQL flexible, en lo que Azure da gratis 12 meses: B1ms, 32 GB de disco y 32 GB de respaldo. Cualquier extra se cobra:
// alta disponibilidad, respaldo geo-redundante, IOPS extra o crecimiento automático del disco.
//
// lower_case_table_names vale 1 en Azure (en el MySQL de desarrollo, 0) y solo se elige al crear el servidor: las
// pruebas corren contra un MySQL con 1, en CI y en el servidor de pruebas.

param nombre string
param region string
param usuario string

@secure()
param clave string

resource servidor 'Microsoft.DBforMySQL/flexibleServers@2024-12-30' = {
  name: nombre
  location: region
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    // La misma versión que el MySQL de desarrollo.
    version: '8.4'
    administratorLogin: usuario
    administratorLoginPassword: clave
    storage: {
      storageSizeGB: 32
      autoGrow: 'Disabled'
      autoIoScaling: 'Disabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

// La misma intercalación que la base de desarrollo: de ella depende, por ejemplo, que buscar "Jose" encuentre "José".
resource base 'Microsoft.DBforMySQL/flexibleServers/databases@2024-12-30' = {
  parent: servidor
  name: 'control'
  properties: {
    charset: 'utf8mb4'
    collation: 'utf8mb4_0900_ai_ci'
  }
}

output servidor string = servidor.properties.fullyQualifiedDomainName
