import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Landmark, Map as Mapa } from 'lucide-react';
import L from 'leaflet';
import { MapContainer, Marker, Polygon, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Cargando, ErrorDeCarga, EstadoVacio, PageHeader } from '../components/ui';
import { comoPosiciones, ESTILO_ZONA, ESTILO_ZONA_ELEGIDA, ICONO_PUNTO } from '../components/zonas/marcadores';
import { useAuth } from '../context/auth';
import { PANTALLAS } from '../context/permisos';

// Centro de Santa Cruz de la Sierra: donde abre el mapa si no hay nada dibujado.
const SANTA_CRUZ = [-17.7833, -63.1821];

// Encuadra la zona elegida si tiene límite; si no, todo lo que hay dibujado.
function Encuadrar({ zonas, puntos, elegida }) {
  const mapa = useMap();
  useEffect(() => {
    const posiciones = elegida?.limite
      ? comoPosiciones(elegida.limite)
      : [...zonas.flatMap((z) => (z.limite ? comoPosiciones(z.limite) : [])), ...puntos.map((p) => [p.latitud, p.longitud])];
    if (posiciones.length > 0) mapa.fitBounds(L.latLngBounds(posiciones), { padding: [40, 40], maxZoom: 16 });
  }, [mapa, zonas, puntos, elegida]);
  return null;
}

// La rueda del mouse acerca el mapa solo después de un clic en él: si no, quien desplaza la página termina acercándolo.
function RuedaAlEnfocar() {
  const mapa = useMapEvents({
    focus: () => mapa.scrollWheelZoom.enable(),
    blur: () => mapa.scrollWheelZoom.disable(),
  });
  return null;
}

function Leyenda() {
  return (
    <ul aria-label="Leyenda del mapa" className="flex flex-wrap gap-x-5 gap-y-2 px-4 py-3 border-t border-slate-200 text-sm text-slate-700">
      <li className="flex items-center gap-2">
        <span className="size-5 rounded-sm border-2 border-[#2A5899] bg-[#3A6FB5]/15" aria-hidden="true" />
        Zona
      </li>
      <li className="flex items-center gap-2">
        <span className="size-5 rounded-sm border-[3px] border-[#1C3A66] bg-[#3A6FB5]/25" aria-hidden="true" />
        Zona elegida
      </li>
      <li className="flex items-center gap-2">
        <span className="grid size-6 place-items-center rounded-md border-2 border-white bg-[#152B4C] shadow" aria-hidden="true">
          <Landmark size={12} color="#ffffff" strokeWidth={2.5} />
        </span>
        Punto de referencia
      </li>
    </ul>
  );
}

/**
 * Mapa de zonas (5.5): el límite de cada zona y sus puntos de referencia, como mercados. Lo abren el gerente general,
 * que dibuja, y el promotor, que solo mira. ?zona= elige una zona al abrir (el enlace "Ver en el mapa" de Zonas).
 */
export default function MapaZonas() {
  const { tiene } = useAuth();
  const [parametros, setParametros] = useSearchParams();
  const [zonas, setZonas] = useState(null);
  const [puntos, setPuntos] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');

  const cargar = useCallback(() => Promise.all([api.get('/zonas'), api.get('/puntos-referencia')])
    .then(([z, p]) => { setZonas(z.data); setPuntos(p.data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar el mapa de zonas.')))
    .finally(() => { setCargando(false); setReintentando(false); }), []);

  useEffect(() => { cargar(); }, [cargar]);

  const elegidaId = Number(parametros.get('zona')) || null;
  const elegida = zonas?.find((z) => z.id === elegidaId) ?? null;
  const elegir = (id) => setParametros(id ? { zona: String(id) } : {}, { replace: true });
  const puntosDeLaElegida = useMemo(() => puntos.filter((p) => p.zona.id === elegidaId), [puntos, elegidaId]);

  let contenido;
  if (cargando) contenido = <Cargando texto="Cargando el mapa…" />;
  else if (errorCarga) contenido = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (zonas.length === 0) contenido = (
    <EstadoVacio titulo="Todavía no hay zonas" icono={Mapa}
      accion={tiene(...PANTALLAS.zonas) && <Link to="/zonas" className="text-sm font-medium text-marca-700 underline">Ir a Zonas</Link>}>
      Cuando se registre una zona, aquí se dibuja su límite.
    </EstadoVacio>
  );
  else contenido = (
    <div className="grid lg:grid-cols-[18rem_minmax(0,1fr)]">
      <nav aria-label="Zonas" className="border-b border-slate-200 lg:border-b-0 lg:border-r">
        <ul className="max-h-56 overflow-y-auto p-2 lg:max-h-128">
          {zonas.map((z) => (
            <li key={z.id}>
              <button type="button" aria-pressed={z.id === elegidaId} onClick={() => elegir(z.id === elegidaId ? null : z.id)}
                className={`w-full rounded-lg px-3 py-2 text-left text-sm transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 ${
                  z.id === elegidaId ? 'bg-marca-50 text-marca-800' : 'text-slate-800 hover:bg-slate-50'}`}>
                <span className="block font-medium">{z.nombre}</span>
                <span className="block text-xs text-slate-600">
                  {z.limite ? 'Con límite' : 'Sin límite'}
                  {' · '}
                  {z.puntosDeReferencia === 1 ? '1 punto' : `${z.puntosDeReferencia} puntos`}
                </span>
              </button>
            </li>
          ))}
        </ul>
      </nav>
      <div className="min-w-0">
        {/* isolate: las capas de Leaflet (z-index 400 a 1000) quedan dentro del mapa y no tapan los modales. */}
        <div className="relative isolate h-104 sm:h-128" role="group"
          aria-label="Mapa de zonas: cada zona con su límite y sus puntos de referencia">
          <MapContainer center={SANTA_CRUZ} zoom={13} scrollWheelZoom={false} className="h-full w-full">
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <Encuadrar zonas={zonas} puntos={puntos} elegida={elegida} />
            <RuedaAlEnfocar />
            {zonas.filter((z) => z.limite).map((z) => (
              <Polygon key={z.id} positions={comoPosiciones(z.limite)} pathOptions={z.id === elegidaId ? ESTILO_ZONA_ELEGIDA : ESTILO_ZONA}
                eventHandlers={{ click: () => elegir(z.id) }}>
                <Tooltip permanent direction="center" className="font-semibold">{z.nombre}</Tooltip>
              </Polygon>
            ))}
            {puntos.map((p) => (
              <Marker key={p.id} position={[p.latitud, p.longitud]} icon={ICONO_PUNTO} title={`${p.nombre} (${p.zona.nombre})`}>
                <Tooltip>
                  <span className="font-semibold">{p.nombre}</span> · {p.zona.nombre}
                  {p.descripcion && <><br />{p.descripcion}</>}
                </Tooltip>
              </Marker>
            ))}
          </MapContainer>
          {elegida && !elegida.limite && (
            <div className="pointer-events-none absolute inset-x-0 top-3 z-500 flex justify-center px-4">
              <p role="status" className="rounded-lg bg-white px-4 py-2 text-sm text-slate-800 shadow-flotante">
                {elegida.nombre} todavía no tiene límite.
              </p>
            </div>
          )}
        </div>
        <Leyenda />
        {elegida && (
          <section aria-labelledby="titulo-puntos" className="border-t border-slate-200 px-4 py-3">
            <h3 id="titulo-puntos" className="text-sm font-semibold text-slate-900">Puntos de referencia de {elegida.nombre}</h3>
            {puntosDeLaElegida.length === 0
              ? <p className="mt-1 text-sm text-slate-600">Esta zona todavía no tiene puntos de referencia.</p>
              : (
                <ul className="mt-2 space-y-1 text-sm text-slate-800">
                  {puntosDeLaElegida.map((p) => (
                    <li key={p.id}><span className="font-medium">{p.nombre}</span>{p.descripcion && <span className="text-slate-600"> · {p.descripcion}</span>}</li>
                  ))}
                </ul>
              )}
          </section>
        )}
      </div>
    </div>
  );

  return (
    <Layout>
      <PageHeader title="Mapa de zonas" subtitle="El límite de cada zona y sus puntos de referencia. Elige una zona para verla de cerca." />
      <div className="bg-white rounded-xl shadow-tarjeta border border-slate-200 overflow-hidden">{contenido}</div>
    </Layout>
  );
}
