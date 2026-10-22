import L from 'leaflet';

// Las zonas usan la escala marca: el borde marca-600 (#2A5899) tiene más de 3:1 sobre los mosaicos claros de
// OpenStreetMap. Nunca rojo ni verde, que son los estados del cliente (docs/diseno.md, sección 2).
export const ESTILO_ZONA = { color: '#2A5899', weight: 2, fillColor: '#3A6FB5', fillOpacity: 0.12 };
export const ESTILO_ZONA_ELEGIDA = { color: '#1C3A66', weight: 3, fillColor: '#3A6FB5', fillOpacity: 0.2 };
// El límite que se está dibujando: punteado, para distinguirlo de uno guardado.
export const ESTILO_DIBUJO = { color: '#1C3A66', weight: 3, dashArray: '6 6', fillColor: '#3A6FB5', fillOpacity: 0.12 };

// El ícono landmark de lucide, en blanco.
const LANDMARK = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="#ffffff" stroke-width="2.5" stroke-linecap="round" '
  + 'stroke-linejoin="round"><path d="M3 22h18"/><path d="M6 18v-7"/><path d="M10 18v-7"/><path d="M14 18v-7"/><path d="M18 18v-7"/>'
  + '<path d="M12 2 20 7H4z"/></svg>';

/**
 * El punto de referencia es un cuadrado con el ícono de un edificio: la forma lo distingue de los círculos de los
 * clientes (5.1) y de los eventos, sin depender del color.
 */
export const ICONO_PUNTO = L.divIcon({
  className: '',
  html: `<span style="display:grid;place-items:center;width:28px;height:28px;border-radius:6px;border:2px solid #ffffff;`
    + `background:#152B4C;box-shadow:0 1px 3px rgb(15 23 42 / 0.45)">${LANDMARK}</span>`,
  iconSize: [28, 28],
  iconAnchor: [14, 14],
  tooltipAnchor: [14, 0],
});

/** Los vértices de la API ([{ latitud, longitud }]) como los pide Leaflet ([[lat, lng]]). */
export const comoPosiciones = (vertices) => vertices.map((v) => [Number(v.latitud), Number(v.longitud)]);
