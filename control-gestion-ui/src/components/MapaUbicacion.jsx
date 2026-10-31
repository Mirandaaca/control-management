import { useEffect } from 'react';
import { CircleMarker, MapContainer, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import { CENTRO_DE_SANTA_CRUZ } from '../utils/mapa';

const redondear = (n) => Math.round(n * 1e6) / 1e6;

// La rueda del mouse acerca el mapa solo después de un clic en él: si no, dentro de un formulario con scroll, quien
// desplaza el formulario termina acercando el mapa.
function AlHacerClic({ onCambiar }) {
  const mapa = useMapEvents({
    click: (e) => onCambiar({ latitud: redondear(e.latlng.lat), longitud: redondear(e.latlng.lng) }),
    focus: () => mapa.scrollWheelZoom.enable(),
    blur: () => mapa.scrollWheelZoom.disable(),
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
        center={punto ?? CENTRO_DE_SANTA_CRUZ}
        zoom={punto ? 16 : 13}
        scrollWheelZoom={false}
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
