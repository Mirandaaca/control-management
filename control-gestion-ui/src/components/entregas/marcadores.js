import L from 'leaflet';
import { iconoDelPromotor, LETRA } from '../rutas/marcadores';

// Los ids de ControlGestion.API/Models/EstadosDePedido.cs.
export const PENDIENTE = 1;
export const EN_RUTA = 3;
export const ENTREGADO = 4;

// La pastilla de una entrega muestra su hora y su estado, como la de una parada de la ruta: nunca rojo ni verde, que son
// del estado del cliente (docs/diseno.md, "Mapas"). En ruta: blanca, con el borde, un paquete y la hora marca-600;
// entregado: marca-600 con un check; pendiente (todavía no sale a ruta): slate-100, con un reloj y la hora slate-700.
export const PASTILLAS_DE_ENTREGA = {
  [EN_RUTA]: { fondo: '#ffffff', borde: '#2A5899', texto: '#2A5899' },
  [ENTREGADO]: { fondo: '#2A5899', borde: '#ffffff', texto: '#ffffff' },
  [PENDIENTE]: { fondo: '#f1f5f9', borde: '#64748b', texto: '#334155' },
};

// El rastro: por dónde pasó el promotor en la última media hora. Continuo (la línea de la ruta es punteada) y algo
// transparente, para no tapar las calles.
export const ESTILO_RASTRO = { color: '#22477D', weight: 4, opacity: 0.6, lineCap: 'round', lineJoin: 'round' };

const TRAZO = 'fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"';
const PAQUETE = `<svg viewBox="0 0 24 24" width="14" height="14" ${TRAZO}>`
  + '<path d="M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73z"/>'
  + '<path d="M12 22V12"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="m7.5 4.27 9 5.15"/></svg>';
const CHECK = `<svg viewBox="0 0 24 24" width="14" height="14" ${TRAZO}><path d="M20 6 9 17l-5-5"/></svg>`;
const RELOJ = `<svg viewBox="0 0 24 24" width="14" height="14" ${TRAZO}><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>`;
const ICONOS = { [EN_RUTA]: PAQUETE, [ENTREGADO]: CHECK, [PENDIENTE]: RELOJ };

/** Para poner un texto de la base (el nombre de un promotor) dentro del HTML de un marcador. */
function escaparHtml(texto) {
  const reemplazos = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
  return String(texto).replace(/[&<>"']/g, (c) => reemplazos[c]);
}

/** La pastilla de una entrega en el mapa: su ícono y su hora. Se centra en el punto, como la de una parada. */
export function iconoDeEntrega(hora, estado) {
  const p = PASTILLAS_DE_ENTREGA[estado] ?? PASTILLAS_DE_ENTREGA[EN_RUTA];
  return L.divIcon({
    className: '',
    html: `<span style="position:absolute;transform:translate(-50%,-50%);display:inline-flex;align-items:center;justify-content:center;`
      + `gap:4px;height:26px;padding:0 8px;box-sizing:border-box;border:2px solid ${p.borde};border-radius:999px;`
      + `background:${p.fondo};color:${p.texto};white-space:nowrap;box-shadow:0 1px 3px rgb(15 23 42 / 0.45);${LETRA}">`
      + `${ICONOS[estado] ?? PAQUETE}${hora}</span>`,
    iconSize: [0, 0],
    iconAnchor: [0, 0],
    tooltipAnchor: [0, -14],
  });
}

/**
 * La posición de un promotor: el mismo círculo que en Rutas, con la hora de su última ubicación. Con nombre (en "Todos",
 * donde hay varios), "Carlos · 10:42".
 */
export function iconoDelRepartidor(hora, nombre) {
  return iconoDelPromotor(escaparHtml(nombre ? `${nombre} · ${hora}` : hora));
}
