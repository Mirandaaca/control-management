import { useEffect } from 'react';
import L from 'leaflet';
import { Marker, Polygon, Polyline, useMapEvents } from 'react-leaflet';
import { ESTILO_DIBUJO } from './marcadores';

// Seis decimales son unos 10 cm: lo mismo que guarda la API.
const redondear = (n) => Math.round(n * 1e6) / 1e6;

// El asa de un vértice: un círculo blanco con borde marca-800, sin las imágenes de los íconos de Leaflet.
const ASA = L.divIcon({
  className: '',
  html: '<span style="display:block;width:18px;height:18px;border-radius:9999px;background:#ffffff;border:3px solid #1C3A66;'
    + 'box-shadow:0 1px 3px rgb(15 23 42 / 0.45)"></span>',
  iconSize: [18, 18],
  iconAnchor: [9, 9],
});

/**
 * Cada clic agrega un vértice. Un doble clic son dos clics en el mismo lugar y luego el zoom de Leaflet: mientras se
 * dibuja, el zoom con doble clic se apaga, y un clic sobre el último vértice no agrega otro.
 */
function AlTocar({ ultimo, onTocar }) {
  const mapa = useMapEvents({
    click: (e) => {
      if (ultimo && mapa.latLngToContainerPoint(ultimo).distanceTo(e.containerPoint) < 8) return;
      onTocar([redondear(e.latlng.lat), redondear(e.latlng.lng)]);
    },
  });

  useEffect(() => {
    mapa.doubleClickZoom.disable();
    return () => mapa.doubleClickZoom.enable();
  }, [mapa]);

  return null;
}

/**
 * El límite que se está dibujando o editando, dentro del MapContainer. Sin librería (decisión de la tarea 12): en el
 * modo "dibujar" cada toque o clic agrega un vértice; en "editar" las asas se arrastran, también con el dedo. Los
 * vértices son [[lat, lng], …]; quien lo usa decide cuándo guardar.
 */
export default function DibujoDeLimite({ modo, vertices, onCambiar }) {
  const mover = (indice, { lat, lng }) =>
    onCambiar(vertices.map((v, i) => (i === indice ? [redondear(lat), redondear(lng)] : v)));

  return (
    <>
      {modo === 'dibujar' && <AlTocar ultimo={vertices.at(-1)} onTocar={(vertice) => onCambiar([...vertices, vertice])} />}
      {vertices.length >= 3 && <Polygon positions={vertices} pathOptions={ESTILO_DIBUJO} interactive={false} />}
      {vertices.length === 2 && <Polyline positions={vertices} pathOptions={ESTILO_DIBUJO} interactive={false} />}
      {vertices.map((vertice, i) => (
        // El índice como clave: los vértices solo se agregan al final o se quitan del final.
        <Marker key={i} position={vertice} icon={ASA} draggable={modo === 'editar'} keyboard={false}
          title={modo === 'editar' ? `Vértice ${i + 1}: arrástralo para moverlo` : `Vértice ${i + 1}`}
          eventHandlers={{ dragend: (e) => mover(i, e.target.getLatLng()) }} />
      ))}
    </>
  );
}
