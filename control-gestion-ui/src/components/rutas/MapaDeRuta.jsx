import { useEffect } from 'react';
import { Landmark, UserRound } from 'lucide-react';
import L from 'leaflet';
import { MapContainer, Marker, Polygon, Polyline, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet';
import { comoPosiciones, ESTILO_ZONA, ICONO_PUNTO } from '../zonas/marcadores';
import { formatearHora } from '../../utils/formato';
import { CENTRO_DE_SANTA_CRUZ } from '../../utils/mapa';
import { AUSENTE, ESTILO_LINEA, iconoDeParada, iconoDelPromotor, PASTILLAS, PENDIENTE, VISITADO } from './marcadores';
import Pastilla from './Pastilla';

// Encuadra la zona y las paradas solo cuando cambia "encuadre" (otra ruta, otra zona): la actualización de cada 30 s no
// mueve el mapa que el gerente está mirando. Dentro de un modal, además, vuelve a medir el tamaño del mapa.
function Encuadrar({ encuadre, posiciones }) {
  const mapa = useMap();
  useEffect(() => {
    const espera = setTimeout(() => {
      mapa.invalidateSize();
      if (posiciones.length > 0) mapa.fitBounds(L.latLngBounds(posiciones), { padding: [36, 36], maxZoom: 17 });
    }, 300);
    return () => clearTimeout(espera);
    // Las posiciones cambian con cada actualización; el encuadre, solo con la ruta.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mapa, encuadre]);
  return null;
}

function RuedaAlEnfocar() {
  const mapa = useMapEvents({
    focus: () => mapa.scrollWheelZoom.enable(),
    blur: () => mapa.scrollWheelZoom.disable(),
  });
  return null;
}

/** La leyenda del mapa de una ruta (8.6.3). Sin promotor (al planificar), no lo nombra. */
export function LeyendaDeRuta({ conPromotor = true }) {
  return (
    <ul aria-label="Leyenda del mapa" className="flex flex-wrap gap-x-5 gap-y-2 px-4 py-3 border-t border-slate-200 text-sm text-slate-700">
      {/* Los números de la leyenda son los del diseño 8.6.3. */}
      {[[4, PENDIENTE], [1, VISITADO], [2, AUSENTE]].map(([orden, resultado]) => (
        <li key={resultado} className="flex items-center gap-2">
          <Pastilla orden={orden} resultado={resultado} />
          {PASTILLAS[resultado].nombre}
        </li>
      ))}
      {conPromotor && (
        <li className="flex items-center gap-2">
          <span className="grid size-6 place-items-center rounded-full border-2 border-white bg-[#152B4C]" aria-hidden="true">
            <UserRound size={14} color="#ffffff" strokeWidth={2.5} />
          </span>
          Promotor, con la hora
        </li>
      )}
      <li className="flex items-center gap-2">
        <span className="grid size-6 place-items-center rounded-md border-2 border-white bg-[#152B4C]" aria-hidden="true">
          <Landmark size={12} color="#ffffff" strokeWidth={2.5} />
        </span>
        Punto de referencia
      </li>
      <li className="flex items-center gap-2">
        <span className="size-5 rounded-sm border-2 border-[#2A5899] bg-[#3A6FB5]/15" aria-hidden="true" />
        Zona
      </li>
    </ul>
  );
}

/**
 * El mapa de una ruta: el límite de la zona, sus puntos de referencia, cada parada en su pastilla, la línea que las une
 * en orden y, si llegó, la última ubicación del promotor con su hora (7.3.20, pasos 2.2 y 6).
 * - paradas: [{ clave, orden, resultado, latitud, longitud, nombre, detalle }], ya en orden.
 * - promotor: { latitud, longitud, fecha, nombre } o null.
 */
export default function MapaDeRuta({ zona, puntos = [], paradas, promotor, encuadre, etiqueta, className = 'h-80 sm:h-96' }) {
  const limite = zona?.limite ? comoPosiciones(zona.limite) : null;
  const linea = paradas.map((p) => [Number(p.latitud), Number(p.longitud)]);
  const posiciones = [...(limite ?? []), ...linea, ...(promotor ? [[Number(promotor.latitud), Number(promotor.longitud)]] : [])];
  return (
    // isolate: las capas de Leaflet quedan dentro del mapa y no tapan los modales.
    <div className={`relative isolate ${className}`} role="group" aria-label={etiqueta}>
      {/* zoomSnap 0.5: con zoom entero, una zona chica quedaba encuadrada con mucho margen. */}
      <MapContainer center={CENTRO_DE_SANTA_CRUZ} zoom={13} zoomSnap={0.5} scrollWheelZoom={false} className="h-full w-full">
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <Encuadrar encuadre={encuadre} posiciones={posiciones} />
        <RuedaAlEnfocar />
        {limite && <Polygon positions={limite} pathOptions={ESTILO_ZONA} interactive={false} />}
        {linea.length > 1 && <Polyline positions={linea} pathOptions={ESTILO_LINEA} interactive={false} />}
        {puntos.map((p) => (
          <Marker key={`p${p.id}`} position={[p.latitud, p.longitud]} icon={ICONO_PUNTO} title={p.nombre} keyboard={false}>
            <Tooltip>{p.nombre}</Tooltip>
          </Marker>
        ))}
        {paradas.map((p) => (
          <Marker key={p.clave} position={[Number(p.latitud), Number(p.longitud)]} icon={iconoDeParada(p.orden, p.resultado)}
            title={`${p.orden}. ${p.nombre}: ${p.detalle}`}>
            <Tooltip><span className="font-semibold">{p.orden}. {p.nombre}</span> · {p.detalle}</Tooltip>
          </Marker>
        ))}
        {promotor && (
          <Marker position={[Number(promotor.latitud), Number(promotor.longitud)]} icon={iconoDelPromotor(formatearHora(promotor.fecha))}
            zIndexOffset={1000} title={`${promotor.nombre}, a las ${formatearHora(promotor.fecha)}`} />
        )}
      </MapContainer>
    </div>
  );
}
