// Una alerta si la suscripción gasta del crédito: el ambiente de pruebas debería costar US$ 0. Va sobre toda la
// suscripción, no solo el grupo, para ver también lo que se cree fuera de él.
targetScope = 'subscription'

param nombre string
param correo string
param inicio string
param monto int = 5

resource presupuesto 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: nombre
  properties: {
    category: 'Cost'
    amount: monto
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: inicio
    }
    notifications: {
      gastoReal50: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 50
        thresholdType: 'Actual'
        contactEmails: [correo]
      }
      gastoReal100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [correo]
      }
      pronostico100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [correo]
      }
    }
  }
}
