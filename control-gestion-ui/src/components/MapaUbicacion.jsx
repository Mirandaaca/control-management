import { useEffect } from 'react';
import { CircleMarker, MapContainer, TileLayer, useMap, useMapEvents } from 'react-leaflet';

// Centro de Santa Cruz de la Sierra: donde abre el mapa si todavía no hay un punto marcado.
const SANTA_CRUZ = [-17.7833, -63.1821];

const redondear = (n) => Math.round(n * 1e6) / 1e6;

function AlHacerClic({ onCambiar }) {
  useMapEvents({
    click: (e) => onCambiar({ latitud: redondear(e.latlng.lat), longitud: redondear(e.latlng.lng) }),
  });
  return null;
}

// Dentro de un modal el mapa nace mientras el modal se anima, y Leaflet mide mal su tamaño: hay que pedirle que lo
// vuelva a medir. También se centra en el punto cuando llega de afuera (por ejemplo, del GPS).
function Ajustar({ latitud, longitud }) {
  const mapa = useMap();
  useEffect(() => {
    const espera = setTimeout(() => mapa.invalidateSize(), 250);
    return () => clearTimeout(espera);
  }, [mapa]);
  useEffect(() => {
    if (latitud != null && longitud != null) mapa.setView([latitud, longitud], Math.max(mapa.getZoom(), 16));
  }, [mapa, latitud, longitud]);
  return null;
}

/**
 * Mapa de OpenStreetMap con la ubicación de un cliente. Con onCambiar, un clic marca el punto (así lo pide la tesis
 * para el panel: la secretaria marca la ubicación en el mapa); sin él, solo la muestra.
 */
export default function MapaUbicacion({ valor, onCambiar, alto = 260, etiqueta = 'Mapa con la ubicación del cliente' }) {
  const punto = valor?.latitud != null && valor?.longitud != null ? [Number(valor.latitud), Number(valor.longitud)] : null;
  return (
    <div className="rounded-lg overflow-hidden border border-borde-control" style={{ height: alto }} role="group" aria-label={etiqueta}>
      <MapContainer
        center={punto ?? SANTA_CRUZ}
        zoom={punto ? 16 : 13}
        scrollWheelZoom={Boolean(onCambiar)}
        className={`h-full w-full ${onCambiar ? 'cursor-crosshair' : ''}`}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        {punto && (
          <CircleMarker center={punto} radius={9} pathOptions={{ color: '#ffffff', weight: 3, fillColor: '#2a5899', fillOpacity: 1 }} />
        )}
        {onCambiar && <AlHacerClic onCambiar={onCambiar} />}
        <Ajustar latitud={punto?.[0]} longitud={punto?.[1]} />
      </MapContainer>
    </div>
  );
}
