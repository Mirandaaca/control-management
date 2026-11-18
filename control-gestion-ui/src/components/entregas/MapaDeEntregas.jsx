import { UserRound } from 'lucide-react';
import { MapContainer, Marker, Polyline, TileLayer, Tooltip } from 'react-leaflet';
import { CENTRO_DE_SANTA_CRUZ } from '../../utils/mapa';
import { Encuadrar, RuedaAlEnfocar } from '../rutas/MapaDeRuta';
import { EN_RUTA, ENTREGADO, ESTILO_RASTRO, iconoDeEntrega, iconoDelRepartidor, PENDIENTE } from './marcadores';
import PastillaDeEntrega from './PastillaDeEntrega';

const TEXTOS_DE_LA_LEYENDA = [
  [EN_RUTA, 'En ruta, por entregar'],
  [ENTREGADO, 'Entregado'],
  [PENDIENTE, 'Pendiente: todavía no sale a ruta'],
];

/** La leyenda del mapa de las entregas, con las mismas muestras que el mapa. */
export function LeyendaDeEntregas() {
  return (
    <ul aria-label="Leyenda del mapa" className="flex flex-wrap gap-x-5 gap-y-2 px-4 py-3 border-t border-slate-200 text-sm text-slate-700">
      {TEXTOS_DE_LA_LEYENDA.map(([estado, texto]) => (
        <li key={estado} className="flex items-center gap-2">
          <PastillaDeEntrega estado={estado} />
          {texto}
        </li>
      ))}
      <li className="flex items-center gap-2">
        <span className="grid size-6 place-items-center rounded-full border-2 border-white bg-[#152B4C]" aria-hidden="true">
          <UserRound size={14} color="#ffffff" strokeWidth={2.5} />
        </span>
        Promotor, con la hora de su ubicación
      </li>
      <li className="flex items-center gap-2">
        <span className="h-1 w-6 rounded-full bg-[#22477D]/60" aria-hidden="true" />
        Por dónde pasó en los últimos 30 minutos
      </li>
    </ul>
  );
}

/**
 * El mapa de las entregas de hoy (tarea 31): cada entrega en una pastilla con su hora y su estado, la posición de cada
 * promotor con la hora y, detrás, su rastro de la última media hora.
 * - promotores: [{ id, nombre, ubicacion: { latitud, longitud, fecha, hora } | null, rastro: [[lat, lng]], etiqueta }].
 * - entregas: [{ clave, hora, estado, latitud, longitud, nombre, detalle }], solo las que tienen ubicación.
 * - encuadre: cambia al elegir otro promotor; la actualización de cada 10 s no mueve el mapa.
 */
export default function MapaDeEntregas({ promotores, entregas, encuadre, etiqueta, conNombres = false, className = 'h-80 sm:h-96' }) {
  const posiciones = [
    ...entregas.map((e) => [Number(e.latitud), Number(e.longitud)]),
    ...promotores.flatMap((p) => (p.ubicacion ? [[Number(p.ubicacion.latitud), Number(p.ubicacion.longitud)]] : [])),
  ];
  return (
    // isolate: las capas de Leaflet quedan dentro del mapa y no tapan los modales.
    <div className={`relative isolate ${className}`} role="group" aria-label={etiqueta}>
      <MapContainer center={CENTRO_DE_SANTA_CRUZ} zoom={13} zoomSnap={0.5} scrollWheelZoom={false} className="h-full w-full">
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <Encuadrar encuadre={encuadre} posiciones={posiciones} />
        <RuedaAlEnfocar />
        {promotores.map((p) => p.rastro.length > 1 && (
          <Polyline key={`r${p.id}`} positions={p.rastro} pathOptions={ESTILO_RASTRO} interactive={false} />
        ))}
        {entregas.map((e) => (
          <Marker key={e.clave} position={[Number(e.latitud), Number(e.longitud)]} icon={iconoDeEntrega(e.hora, e.estado)}
            title={`${e.hora} · ${e.nombre}: ${e.detalle}`}>
            <Tooltip><span className="font-semibold">{e.hora} · {e.nombre}</span> · {e.detalle}</Tooltip>
          </Marker>
        ))}
        {promotores.map((p) => p.ubicacion && (
          <Marker key={`p${p.id}`} position={[Number(p.ubicacion.latitud), Number(p.ubicacion.longitud)]}
            icon={iconoDelRepartidor(p.ubicacion.hora, conNombres ? p.nombre.split(' ')[0] : null)}
            zIndexOffset={1000} title={`${p.nombre}: ${p.etiqueta}`} />
        ))}
      </MapContainer>
    </div>
  );
}
