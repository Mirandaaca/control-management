/// El plazo del crédito: 30 días fijos de la configuración general (`DiasVencimientoCredito`), sin pantalla para
/// cambiarlo (docs/alcance.md, "3. Cobranzas"). La deuda vence a los 30 días de la venta, como la calcula la API.
const diasDelCredito = 30;

/// Cuándo vence la deuda de una venta hecha en [momentoUtc].
DateTime vencimientoDelCredito(DateTime momentoUtc) => momentoUtc.toUtc().add(const Duration(days: diasDelCredito));
