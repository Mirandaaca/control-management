import L from 'leaflet';
import { COLOR_ACTIVO, COLOR_DISPONIBLE, colorDePaleta } from '../paletaDeEstados';

// Los ids de ControlGestion.API/Models/EstadosDeCliente.cs.
export const ACTIVO = 1;
export const DISPONIBLE = 2;

// Los íconos de lucide (clock y map-pin) en blanco: rojo y verde nunca van solos (docs/diseno.md, sección 2).
const TRAZO = 'fill="none" stroke="#ffffff" stroke-width="2.75" stroke-linecap="round" stroke-linejoin="round"';
const RELOJ = `<svg viewBox="0 0 24 24" width="14" height="14" ${TRAZO}><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>`;
const PIN = `<svg viewBox="0 0 24 24" width="14" height="14" ${TRAZO}><path d="M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0"/><circle cx="12" cy="10" r="3"/></svg>`;

/**
 * El color y el ícono del marcador de un estado: Activo en rojo con el reloj, Disponible en verde con el pin, y los
 * asignados a mano con su color de la paleta, sin ícono (su nombre va en el tooltip y en la leyenda).
 */
export function pinturaDelEstado(estado) {
  if (estado?.id === ACTIVO) return { color: COLOR_ACTIVO, icono: RELOJ, tipo: 'reloj' };
  if (estado?.id === DISPONIBLE) return { color: COLOR_DISPONIBLE, icono: PIN, tipo: 'pin' };
  return { color: colorDePaleta(estado?.color)?.color ?? '#475569', icono: '', tipo: null };
}

const iconos = new Map();

/** El marcador de un cliente: un círculo de 28 px con borde blanco, del color de su estado. El elegido, más grande. */
export function iconoDelCliente(estado, elegido = false) {
  const { color, icono } = pinturaDelEstado(estado);
  const clave = `${color}|${elegido}`;
  if (!iconos.has(clave)) {
    const lado = elegido ? 36 : 28;
    const anillo = elegido ? ',0 0 0 4px rgb(21 43 76 / 0.35)' : '';
    iconos.set(clave, L.divIcon({
      className: '',
      html: `<span style="display:grid;place-items:center;width:${lado}px;height:${lado}px;border-radius:9999px;`
        + `border:2px solid #ffffff;background:${color};box-shadow:0 1px 3px rgb(15 23 42 / 0.45)${anillo}">${icono}</span>`,
      iconSize: [lado, lado],
      iconAnchor: [lado / 2, lado / 2],
      tooltipAnchor: [lado / 2, 0],
    }));
  }
  return iconos.get(clave);
}
