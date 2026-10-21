const ZONA_BOLIVIA = 'America/La_Paz';

const partesDeFecha = new Intl.DateTimeFormat('es-BO', {
  timeZone: ZONA_BOLIVIA, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
});
const diaISO = new Intl.DateTimeFormat('en-CA', { timeZone: ZONA_BOLIVIA });

/**
 * La API guarda en UTC, pero devuelve las fechas leídas de MySQL sin la "Z" final. Sin ella, new Date() las tomaría
 * como hora local de la PC y quedarían corridas 4 horas.
 */
export function leerFechaUtc(valor) {
  if (valor instanceof Date) return valor;
  const texto = String(valor);
  const traeZona = /(Z|[+-]\d{2}:?\d{2})$/i.test(texto);
  return new Date(traeZona ? texto : `${texto}Z`);
}

/** Un momento exacto (registro, pago, evento) en la hora de Bolivia: dd/mm/aaaa, o dd/mm/aaaa hh:mm con { hora: true }. */
export function formatearFecha(valor, { hora = false } = {}) {
  if (!valor) return '—';
  const fecha = leerFechaUtc(valor);
  if (Number.isNaN(fecha.getTime())) return '—';
  const p = Object.fromEntries(partesDeFecha.formatToParts(fecha).map(({ type, value }) => [type, value]));
  const dia = `${p.day}/${p.month}/${p.year}`;
  return hora ? `${dia} ${p.hour}:${p.minute}` : dia;
}

/** Solo la hora (hh:mm) de un momento exacto, en la hora de Bolivia. */
export function formatearHora(valor) {
  const completa = formatearFecha(valor, { hora: true });
  return completa === '—' ? completa : completa.slice(-5);
}

/**
 * Una fecha de calendario, como el día planificado de una ruta: se eligió como día y no tiene hora. No se convierte de
 * zona, porque la medianoche UTC en Bolivia cae el día anterior.
 */
export function formatearDia(valor) {
  if (!valor) return '—';
  const [anio, mes, dia] = String(valor).slice(0, 10).split('-');
  return anio && mes && dia ? `${dia}/${mes}/${anio}` : '—';
}

/** El día de hoy en Bolivia, más o menos los días indicados, como aaaa-mm-dd (el formato de un <input type="date">). */
export function diaEnBolivia(desfaseDias = 0) {
  return diaISO.format(new Date(Date.now() + desfaseDias * 86400000));
}

const montoBolivia = new Intl.NumberFormat('es-BO', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Un monto en bolivianos: "Bs 1.234,50". */
export function formatearMonto(valor) {
  return `Bs ${montoBolivia.format(Number(valor ?? 0))}`;
}

const cantidadBolivia = new Intl.NumberFormat('es-BO', { maximumFractionDigits: 3 });

/** Una cantidad de stock o de una línea de venta: "1.250" o "2,5", con hasta 3 decimales. */
export function formatearCantidad(valor) {
  return cantidadBolivia.format(Number(valor ?? 0));
}

/**
 * Cuántos días de calendario de Bolivia faltan para un momento (negativo si ya pasó). Se cuentan días, no horas:
 * una visita para mañana a las 8 está "en 1 día" aunque falten 30 horas.
 */
export function diasDesdeHoy(valor) {
  if (!valor) return null;
  const fecha = leerFechaUtc(valor);
  if (Number.isNaN(fecha.getTime())) return null;
  return Math.round((Date.parse(diaISO.format(fecha)) - Date.parse(diaEnBolivia())) / 86400000);
}
