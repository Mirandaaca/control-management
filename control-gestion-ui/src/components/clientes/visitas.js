import { diasDesdeHoy } from '../../utils/formato';

const dias = (n) => (n === 1 ? '1 día' : `${n} días`);

/**
 * Cuánto falta para la próxima visita de un cliente (5.2), contado en días de Bolivia: "en 15 días", "hoy" o
 * "vencida hace 3 días". Null si nunca se visitó.
 */
export function cuantoFalta(proximaVisita) {
  const n = diasDesdeHoy(proximaVisita);
  if (n === null) return null;
  if (n > 0) return `en ${dias(n)}`;
  if (n === 0) return 'hoy';
  return `vencida hace ${dias(-n)}`;
}

/** Los días de atraso de la lista por visitar (5.3): "14 días", "Hoy" o "Sin visitas". */
export function textoDelAtraso(diasDeAtraso) {
  if (diasDeAtraso === null || diasDeAtraso === undefined) return 'Sin visitas';
  if (diasDeAtraso <= 0) return 'Hoy';
  return dias(diasDeAtraso);
}
