import { useCallback, useEffect, useState } from 'react';
import { CircleCheck, Clock, MapPin } from 'lucide-react';
import L from 'leaflet';
import { MapContainer, Marker, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import DetalleEvento from '../components/eventos/DetalleEvento';
import GaleriaDeFotos from '../components/eventos/GaleriaDeFotos';
import { ESTADOS, PENDIENTE, RESUELTO } from '../components/eventos/estados';
import { COLOR_DEL_ESTADO, iconoDelEstado } from '../components/eventos/marcadores';
import {
  Alert, BarraDeFiltros, Cargando, ErrorDeCarga, EstadoVacio, Input, PageHeader, Selector,
} from '../components/ui';
import { useAuth } from '../context/auth';
import { ELIGEN_PROMOTOR } from '../context/permisos';
import { formatearFecha } from '../utils/formato';

// Centro de Santa Cruz de la Sierra: donde abre el mapa si no hay puntos.
const SANTA_CRUZ = [-17.7833, -63.1821];
const SIN_FILTROS = { tipo: '', estado: '', zona: '', promotor: '', desde: '', hasta: '' };
const numero = new Intl.NumberFormat('es-BO');

// Cada vez que cambian los puntos, el mapa los encuadra a todos.
function Encuadrar({ puntos }) {
  const mapa = useMap();
  useEffect(() => {
    if (puntos.length === 0) return;
    mapa.fitBounds(L.latLngBounds(puntos.map((p) => [p.latitud, p.longitud])), { padding: [40, 40], maxZoom: 16 });
  }, [mapa, puntos]);
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
      {[[PENDIENTE, Clock], [RESUELTO, CircleCheck]].map(([estado, Icono]) => (
        <li key={estado} className="flex items-center gap-2">
          <span className="grid size-6 place-items-center rounded-full border-2 border-white shadow" style={{ background: COLOR_DEL_ESTADO[estado] }}>
            <Icono size={12} color="#ffffff" strokeWidth={2.75} aria-hidden="true" />
          </span>
          {ESTADOS[estado]}
        </li>
      ))}
    </ul>
  );
}

/**
 * Mapa de eventos (4.3): dónde pasaron las incidencias, con el color y el ícono de su estado, y los filtros de tipo,
 * estado, promotor, zona y fechas. Un clic en un punto abre su detalle, con las fotos.
 */
export default function MapaEventos() {
  const { tiene } = useAuth();
  const eligePromotor = tiene(...ELIGEN_PROMOTOR);

  const [mapa, setMapa] = useState(null);
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [errorAccion, setErrorAccion] = useState('');
  const [errorFiltros, setErrorFiltros] = useState('');
  const [tipos, setTipos] = useState([]);
  const [zonas, setZonas] = useState([]);
  const [promotores, setPromotores] = useState([]);
  const [detalle, setDetalle] = useState(null);

  // Los catálogos de los filtros. Si fallan, el mapa igual funciona: se avisa arriba, y "Reintentar" los vuelve a pedir.
  const cargarFiltros = useCallback(() => Promise.all([
    api.get('/eventos/tipos'),
    api.get('/zonas'),
    eligePromotor ? api.get('/usuarios/promotores') : Promise.resolve({ data: [] }),
  ])
    .then(([t, z, p]) => { setTipos(t.data); setZonas(z.data); setPromotores(p.data); setErrorFiltros(''); })
    .catch((err) => setErrorFiltros(`No se pudieron cargar los filtros: ${mensajeDeError(err, 'error desconocido.')}`)),
  [eligePromotor]);

  useEffect(() => { cargarFiltros(); }, [cargarFiltros]);

  const cargar = useCallback(() => api.get('/eventos/mapa', {
    params: {
      tipoEventoId: filtros.tipo || undefined,
      estado: filtros.estado || undefined,
      zonaId: filtros.zona || undefined,
      promotorId: filtros.promotor || undefined,
      desde: filtros.desde || undefined,
      hasta: filtros.hasta || undefined,
    },
  })
    .then(({ data }) => { setMapa(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar el mapa de eventos.')))
    .finally(() => { setCargando(false); setReintentando(false); }),
  [filtros]);

  useEffect(() => { cargar(); }, [cargar]);

  const filtrar = (cambios) => setFiltros((f) => ({ ...f, ...cambios }));

  const abrirEvento = (id) => {
    setErrorAccion('');
    api.get(`/eventos/${id}`)
      .then(({ data }) => setDetalle(data))
      .catch((err) => setErrorAccion(mensajeDeError(err, 'No se pudo abrir el evento.')));
  };

  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const puntos = mapa?.puntos ?? [];
  const recortado = mapa && mapa.total > puntos.length;

  let contenido;
  if (cargando) contenido = <Cargando texto="Cargando el mapa…" />;
  else if (errorCarga && !mapa) contenido = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); cargarFiltros(); }} reintentando={reintentando} />
  );
  else if (mapa.total === 0 && !hayFiltros) contenido = (
    <EstadoVacio titulo="Todavía no hay eventos" icono={MapPin}>Las incidencias que registren los promotores aparecen en el mapa.</EstadoVacio>
  );
  else contenido = (
    <>
      {/* isolate: las capas de Leaflet (z-index 400 a 1000) quedan dentro del mapa y no tapan el modal del detalle. */}
      <div className="relative isolate h-104 sm:h-128" role="group" aria-label="Mapa de eventos: cada punto es un evento; elige uno para ver su detalle">
        <MapContainer center={SANTA_CRUZ} zoom={13} scrollWheelZoom={false} className="h-full w-full">
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <Encuadrar puntos={puntos} />
          <RuedaAlEnfocar />
          {puntos.map((p) => {
            const etiqueta = `${p.numero} · ${p.tipo.nombre} · ${ESTADOS[p.estado]}${p.cliente ? ` · ${p.cliente.nombreCompleto}` : ''}`;
            return (
              <Marker key={p.id} position={[p.latitud, p.longitud]} icon={iconoDelEstado(p.estado)}
                title={`${etiqueta}. Abrir el detalle.`} eventHandlers={{ click: () => abrirEvento(p.id) }}>
                <Tooltip>
                  {etiqueta}
                  <br />
                  {formatearFecha(p.fechaEvento, { hora: true })}
                </Tooltip>
              </Marker>
            );
          })}
        </MapContainer>
        {puntos.length === 0 && (
          <div className="pointer-events-none absolute inset-0 z-500 grid place-items-center p-4">
            <p className="rounded-lg bg-white px-4 py-2 text-sm text-slate-800 shadow-flotante">Ningún evento coincide con los filtros.</p>
          </div>
        )}
      </div>
      <Leyenda />
    </>
  );

  return (
    <Layout>
      <PageHeader title="Mapa de eventos" subtitle="Dónde pasaron las incidencias en campo. Elige un punto para ver su detalle y sus fotos." />

      {errorAccion && <Alert type="error" className="mb-4">{errorAccion}</Alert>}
      {errorFiltros && <Alert type="error" className="mb-4">{errorFiltros}</Alert>}
      {errorCarga && mapa && <Alert type="error" className="mb-4">{errorCarga}</Alert>}
      {recortado && (
        <Alert type="info" className="mb-4">
          Se muestran los {numero.format(puntos.length)} eventos más recientes de {numero.format(mapa.total)}: usa los filtros para ver los demás.
        </Alert>
      )}

      <div className="bg-white rounded-xl shadow-tarjeta border border-slate-200 overflow-hidden">
        {!cargando && mapa && (mapa.total > 0 || hayFiltros) && (
          <BarraDeFiltros
            resultado={`${numero.format(mapa.total)} ${mapa.total === 1 ? 'evento' : 'eventos'}`}
            hayFiltros={hayFiltros}
            onLimpiar={() => setFiltros(SIN_FILTROS)}
          >
            <div className="w-full sm:w-48">
              <Selector label="Tipo" todos="Todos" value={filtros.tipo}
                opciones={tipos.map((t) => ({ valor: String(t.id), texto: t.nombre }))}
                onChange={(tipo) => filtrar({ tipo })} />
            </div>
            <div className="w-full sm:w-36">
              <Selector label="Estado" todos="Todos" value={filtros.estado}
                opciones={Object.entries(ESTADOS).map(([valor, texto]) => ({ valor, texto }))}
                onChange={(valor) => filtrar({ estado: valor })} />
            </div>
            <div className="w-full sm:w-44">
              <Selector label="Zona" todos="Todas" value={filtros.zona}
                opciones={zonas.map((z) => ({ valor: String(z.id), texto: z.nombre }))}
                onChange={(zona) => filtrar({ zona })} />
            </div>
            {eligePromotor && (
              <div className="w-full sm:w-48">
                <Selector label="Registró" todos="Todos" value={filtros.promotor}
                  opciones={promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }))}
                  onChange={(promotor) => filtrar({ promotor })} />
              </div>
            )}
            <div className="w-full sm:w-40">
              <Input label="Desde" type="date" autoComplete="off" value={filtros.desde} onChange={(e) => filtrar({ desde: e.target.value })} />
            </div>
            <div className="w-full sm:w-40">
              <Input label="Hasta" type="date" autoComplete="off" value={filtros.hasta} onChange={(e) => filtrar({ hasta: e.target.value })} />
            </div>
          </BarraDeFiltros>
        )}
        {contenido}
      </div>

      <DetalleEvento
        evento={detalle}
        onCerrar={() => setDetalle(null)}
        fotos={detalle && <GaleriaDeFotos evento={detalle} puedeAgregar={false} puedeEliminar={false} onCambio={setDetalle} />}
      />
    </Layout>
  );
}
