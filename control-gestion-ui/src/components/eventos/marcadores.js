import L from 'leaflet';
import { PENDIENTE, RESUELTO } from './estados';

// Ámbar para pendiente y azul para resuelto, como la etiqueta de estado. El rojo y el verde son de los estados del
// cliente en los mapas (docs/diseno.md, sección 2). Los dos tienen 5:1 o más sobre blanco.
export const COLOR_DEL_ESTADO = { [PENDIENTE]: '#B45309', [RESUELTO]: '#2A5899' };

// Los íconos de lucide (clock y check) en blanco, dentro del círculo: el estado nunca va solo con su color.
const TRAZO = 'fill="none" stroke="#ffffff" stroke-width="2.75" stroke-linecap="round" stroke-linejoin="round"';
const ICONOS = {
  [PENDIENTE]: `<svg viewBox="0 0 24 24" width="14" height="14" ${TRAZO}><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>`,
  [RESUELTO]: `<svg viewBox="0 0 24 24" width="14" height="14" ${TRAZO}><path d="M20 6 9 17l-5-5"/></svg>`,
};

function crearIcono(estado) {
  return L.divIcon({
    className: '',
    html: `<span style="display:grid;place-items:center;width:28px;height:28px;border-radius:9999px;border:2px solid #ffffff;`
      + `background:${COLOR_DEL_ESTADO[estado]};box-shadow:0 1px 3px rgb(15 23 42 / 0.45)">${ICONOS[estado]}</span>`,
    iconSize: [28, 28],
    iconAnchor: [14, 14],
    tooltipAnchor: [14, 0],
  });
}

const ICONOS_POR_ESTADO = { [PENDIENTE]: crearIcono(PENDIENTE), [RESUELTO]: crearIcono(RESUELTO) };

/** El marcador de un evento: un círculo con el color y el ícono de su estado. */
export const iconoDelEstado = (estado) => ICONOS_POR_ESTADO[estado] ?? ICONOS_POR_ESTADO[PENDIENTE];
