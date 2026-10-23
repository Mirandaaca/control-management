import L from 'leaflet';

// Los valores de ControlGestion.API/Models: EstadoVisita y EstadoRuta.
export const PENDIENTE = 0;
export const VISITADO = 1;
export const AUSENTE = 2;

export const PLANIFICADA = 0;
export const EN_CURSO = 1;
export const COMPLETADA = 2;

// Las pastillas muestran el orden y el estado de la visita, no el del cliente: nunca rojo ni verde, que son del estado
// del cliente (docs/diseno.md, "Mapas"). Pendiente es blanca con el borde y el número marca-600; visitado, marca-600 con
// un check; ausente, slate-600 con user-x.
export const PASTILLAS = {
  [PENDIENTE]: { fondo: '#ffffff', borde: '#2A5899', texto: '#2A5899', nombre: 'Pendiente' },
  [VISITADO]: { fondo: '#2A5899', borde: '#ffffff', texto: '#ffffff', nombre: 'Visitado' },
  [AUSENTE]: { fondo: '#475569', borde: '#ffffff', texto: '#ffffff', nombre: 'Ausente' },
};

// La línea de la ruta une las paradas en su orden: marca-700, punteada.
export const ESTILO_LINEA = { color: '#22477D', weight: 3, dashArray: '8 6', lineCap: 'round', lineJoin: 'round' };

const TRAZO = 'fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"';
const CHECK = `<svg viewBox="0 0 24 24" width="14" height="14" ${TRAZO}><path d="M20 6 9 17l-5-5"/></svg>`;
const USER_X = `<svg viewBox="0 0 24 24" width="14" height="14" ${TRAZO}><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/>`
  + '<circle cx="9" cy="7" r="4"/><line x1="17" x2="22" y1="8" y2="13"/><line x1="22" x2="17" y1="8" y2="13"/></svg>';
const USER_ROUND = `<svg viewBox="0 0 24 24" width="16" height="16" ${TRAZO}><circle cx="12" cy="8" r="5"/><path d="M20 21a8 8 0 0 0-16 0"/></svg>`;
const ICONOS = { [PENDIENTE]: '', [VISITADO]: CHECK, [AUSENTE]: USER_X };

// Leaflet pone el estilo de letra de su contenedor: los marcadores usan la del panel.
const LETRA = 'font-family:var(--font-sans);font-size:14px;line-height:1;font-weight:700;font-variant-numeric:tabular-nums';

/**
 * La pastilla de una parada: su número de orden y el estado de la visita. Su ancho cambia con el número, así que el
 * ícono mide 0 × 0 y la pastilla se centra en el punto con un translate.
 */
export function iconoDeParada(orden, resultado) {
  const p = PASTILLAS[resultado] ?? PASTILLAS[PENDIENTE];
  return L.divIcon({
    className: '',
    html: `<span style="position:absolute;transform:translate(-50%,-50%);display:inline-flex;align-items:center;justify-content:center;`
      + `gap:3px;min-width:30px;height:26px;padding:0 8px;box-sizing:border-box;border:2px solid ${p.borde};border-radius:999px;`
      + `background:${p.fondo};color:${p.texto};white-space:nowrap;box-shadow:0 1px 3px rgb(15 23 42 / 0.45);${LETRA}">`
      + `${orden}${ICONOS[resultado] ?? ''}</span>`,
    iconSize: [0, 0],
    iconAnchor: [0, 0],
    tooltipAnchor: [0, -14],
  });
}

/**
 * La posición del promotor: un círculo marca-900 con una persona, de otra forma que las pastillas, y la hora de su
 * última ubicación al lado. El punto es el centro del círculo.
 */
export function iconoDelPromotor(hora) {
  return L.divIcon({
    className: '',
    html: '<span style="position:absolute;transform:translate(-16px,-50%);display:flex;align-items:center;gap:6px">'
      + '<span style="display:grid;place-items:center;width:32px;height:32px;box-sizing:border-box;border:2px solid #ffffff;'
      + 'border-radius:50%;background:#152B4C;color:#ffffff;box-shadow:0 0 0 6px rgb(21 43 76 / 0.18),0 1px 3px rgb(15 23 42 / 0.45)">'
      + `${USER_ROUND}</span><span style="padding:2px 8px;border-radius:4px;background:#ffffff;color:#0f172a;`
      + `box-shadow:0 1px 3px rgb(0 0 0 / 0.4);${LETRA};font-weight:600;line-height:20px">${hora}</span></span>`,
    iconSize: [0, 0],
    iconAnchor: [0, 0],
    tooltipAnchor: [16, 0],
  });
}
