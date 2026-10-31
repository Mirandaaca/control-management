import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Clock, Landmark, Map as Mapa, MapPin } from 'lucide-react';
import L from 'leaflet';
import { MapContainer, Marker, Pane, Polygon, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import EstadoCliente from '../components/EstadoCliente';
import { ErrorDeCarga, EsqueletoDeMapa, EstadoVacio, PageHeader, ZonaDeCarga } from '../components/ui';
import { ACTIVO, DISPONIBLE, iconoDelCliente, pinturaDelEstado } from '../components/clientes/marcadores';
import { cuantoFalta } from '../components/clientes/visitas';
import { comoPosiciones, ESTILO_ZONA, ESTILO_ZONA_ELEGIDA, ICONO_PUNTO } from '../components/zonas/marcadores';
import { useAuth } from '../context/auth';
import { PANTALLAS, PERMISOS } from '../context/permisos';
import { formatearFecha } from '../utils/formato';
import { CENTRO_DE_SANTA_CRUZ } from '../utils/mapa';

const CAPA_NOMBRES = 'nombresDeZonas';

// Encuadra la zona elegida (su límite o, si no tiene, sus clientes y sus puntos); sin zona, todo lo que hay.
function Encuadrar({ zonas, puntos, clientes, elegida }) {
  const mapa = useMap();
  useEffect(() => {
    const posiciones = elegida?.limite
      ? comoPosiciones(elegida.limite)
      : [
        ...(elegida ? [] : zonas.flatMap((z) => (z.limite ? comoPosiciones(z.limite) : []))),
        ...puntos.map((p) => [p.latitud, p.longitud]),
        ...clientes.map((c) => [Number(c.latitud), Number(c.longitud)]),
      ];
    if (posiciones.length > 0) mapa.fitBounds(L.latLngBounds(posiciones), { padding: [40, 40], maxZoom: 16 });
  }, [mapa, zonas, puntos, clientes, elegida]);
  return null;
}

// La rueda del mouse acerca el mapa solo después de un clic en él.
function RuedaAlEnfocar() {
  const mapa = useMapEvents({
    focus: () => mapa.scrollWheelZoom.enable(),
    blur: () => mapa.scrollWheelZoom.disable(),
  });
  return null;
}

// Primero los dos estados del sistema, en su orden, y después los asignados a mano por nombre.
const ordenDelEstado = (e) => (e.id === ACTIVO ? 0 : e.id === DISPONIBLE ? 1 : 2);

function Leyenda({ estados }) {
  return (
    <ul aria-label="Leyenda del mapa" className="flex flex-wrap gap-x-5 gap-y-2 px-4 py-3 border-t border-slate-200 text-sm text-slate-700">
      {estados.map((e) => {
        const { color, tipo } = pinturaDelEstado(e);
        const Icono = { reloj: Clock, pin: MapPin }[tipo];
        return (
          <li key={e.id} className="flex items-center gap-2">
            <span className="grid size-6 place-items-center rounded-full border-2 border-white shadow" style={{ background: color }}
              aria-hidden="true">
              {Icono && <Icono size={12} color="#ffffff" strokeWidth={2.75} />}
            </span>
            {e.nombre}
          </li>
        );
      })}
      <li className="flex items-center gap-2">
        <span className="grid size-6 place-items-center rounded-md border-2 border-white bg-[#152B4C] shadow" aria-hidden="true">
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

function Dato({ titulo, children }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-semibold text-slate-600">{titulo}</dt>
      <dd className="mt-0.5 text-sm text-slate-900">{children}</dd>
    </div>
  );
}

// El cliente elegido en el mapa (7.3.17, paso 3, y 7.3.18, paso 2).
function ClienteElegido({ cliente }) {
  const falta = cuantoFalta(cliente.proximaVisita);
  let proxima = 'Sin visitas: está disponible para re-visita.';
  if (!cliente.estado.delSistema) proxima = 'Fuera del ciclo de visitas';
  else if (cliente.proximaVisita) proxima = `${formatearFecha(cliente.proximaVisita)} · ${falta}`;
  return (
    <section aria-labelledby="titulo-cliente" aria-live="polite" className="border-t border-slate-200 px-4 py-3">
      <div className="flex flex-wrap items-center gap-2">
        <h3 id="titulo-cliente" className="text-sm font-semibold text-slate-900">{cliente.nombreCompleto}</h3>
        <span className="font-mono text-xs text-slate-600" translate="no">{cliente.codigo}</span>
        <EstadoCliente estado={cliente.estado} />
      </div>
      <dl className="mt-2 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Dato titulo="Dirección">{cliente.direccionTexto}</Dato>
        <Dato titulo="Celular">{cliente.telefono}</Dato>
        <Dato titulo="Última visita">{cliente.ultimaVisita ? formatearFecha(cliente.ultimaVisita, { hora: true }) : 'Nunca'}</Dato>
        <Dato titulo="Próxima visita">{proxima}</Dato>
      </dl>
    </section>
  );
}

const cuantosClientes = (n) => (n === 1 ? '1 cliente' : `${n} clientes`);

/**
 * Mapa de clientes (5.1 y 5.2): los clientes con el color de su estado, sobre los límites de las zonas y sus puntos de
 * referencia. Al entrar se ven todos; al elegir una zona, el mapa la encuadra y muestra solo los suyos. Sin cartera
 * completa (el promotor), solo sus zonas. ?zona= elige una zona al abrir.
 */
export default function MapaClientes() {
  const { usuario, tiene } = useAuth();
  const [parametros, setParametros] = useSearchParams();
  const [datos, setDatos] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [clienteId, setClienteId] = useState(null);

  const cargar = useCallback(() => Promise.all([api.get('/zonas'), api.get('/puntos-referencia'), api.get('/clientes/mapa')])
    .then(([z, p, c]) => { setDatos({ zonas: z.data, puntos: p.data, clientes: c.data }); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar el mapa de clientes.')))
    .finally(() => { setCargando(false); setReintentando(false); }), []);

  useEffect(() => { cargar(); }, [cargar]);

  // 7.3.17, ext. 1a: sin cartera completa, el usuario ve solo las zonas de las que es promotor.
  const veTodas = tiene(PERMISOS.seguridad.verCarteraCompleta);
  const zonas = useMemo(
    () => (datos?.zonas ?? []).filter((z) => veTodas || z.promotor?.id === usuario?.id),
    [datos, veTodas, usuario],
  );
  const elegidaId = Number(parametros.get('zona')) || null;
  const elegida = zonas.find((z) => z.id === elegidaId) ?? null;
  // Memorizados: el mapa se encuadra cuando cambian, no cada vez que se elige un cliente.
  const puntosVisibles = useMemo(() => {
    const ids = new Set(zonas.map((z) => z.id));
    return (datos?.puntos ?? []).filter((p) => ids.has(p.zona.id) && (!elegidaId || p.zona.id === elegidaId));
  }, [datos, zonas, elegidaId]);
  const elegir = (id) => {
    setClienteId(null);
    setParametros(id ? { zona: String(id) } : {}, { replace: true });
  };

  const clientes = useMemo(
    () => (datos?.clientes ?? []).filter((c) => (elegidaId ? c.zonaId === elegidaId : true)),
    [datos, elegidaId],
  );
  const estadosVisibles = [...new Map(clientes.map((c) => [c.estado.id, c.estado])).values()]
    .sort((a, b) => ordenDelEstado(a) - ordenDelEstado(b) || a.nombre.localeCompare(b.nombre));
  const elegido = clientes.find((c) => c.id === clienteId) ?? null;
  const porZona = (datos?.clientes ?? []).reduce((cuenta, c) => cuenta.set(c.zonaId, (cuenta.get(c.zonaId) ?? 0) + 1), new Map());

  const avisos = [];
  if (elegida && !elegida.limite) avisos.push(`${elegida.nombre} todavía no tiene límite.`);
  if (elegida && clientes.length === 0) avisos.push(`${elegida.nombre} todavía no tiene clientes en el mapa.`);

  let contenido;
  if (cargando) contenido = <ZonaDeCarga texto="Cargando el mapa…"><EsqueletoDeMapa /></ZonaDeCarga>;
  else if (errorCarga) contenido = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (zonas.length === 0) contenido = (
    <EstadoVacio titulo="Todavía no hay zonas" icono={Mapa}
      accion={tiene(...PANTALLAS.zonas) && <Link to="/zonas" className="text-sm font-medium text-marca-700 underline">Ir a Zonas</Link>}>
      Cuando se registre una zona con sus clientes, aquí se ven en el mapa.
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
                  {cuantosClientes(porZona.get(z.id) ?? 0)}
                  {z.promotor && ` · ${z.promotor.nombreCompleto}`}
                </span>
              </button>
            </li>
          ))}
        </ul>
      </nav>
      <div className="min-w-0">
        <div className="border-b border-slate-200 px-4 py-3">
          <h3 className="text-sm font-semibold text-slate-900">{elegida?.nombre ?? 'Todas las zonas'}</h3>
          <p className="text-sm text-slate-600">
            {cuantosClientes(clientes.length)} en el mapa. Toca un cliente para ver su estado y su próxima visita.
          </p>
        </div>
        {/* isolate: las capas de Leaflet (z-index 400 a 1000) quedan dentro del mapa y no tapan los modales. */}
        <div className="relative isolate h-104 sm:h-128" role="group"
          aria-label="Mapa de clientes: cada cliente con el color de su estado">
          <MapContainer center={CENTRO_DE_SANTA_CRUZ} zoom={13} scrollWheelZoom={false} className="h-full w-full">
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <Encuadrar zonas={zonas} puntos={puntosVisibles} clientes={clientes} elegida={elegida} />
            <RuedaAlEnfocar />
            <Pane name={CAPA_NOMBRES} style={{ zIndex: 450 }}>
              {zonas.filter((z) => z.limite).map((z) => (
                <Polygon key={z.id} pane="overlayPane" positions={comoPosiciones(z.limite)}
                  pathOptions={z.id === elegidaId ? ESTILO_ZONA_ELEGIDA : ESTILO_ZONA}
                  eventHandlers={{ click: () => elegir(z.id) }}>
                  <Tooltip pane={CAPA_NOMBRES} permanent direction="center" className="font-semibold">{z.nombre}</Tooltip>
                </Polygon>
              ))}
            </Pane>
            {puntosVisibles.map((p) => (
              <Marker key={`p${p.id}`} position={[p.latitud, p.longitud]} icon={ICONO_PUNTO} title={`${p.nombre} (${p.zona.nombre})`}>
                <Tooltip><span className="font-semibold">{p.nombre}</span> · {p.zona.nombre}</Tooltip>
              </Marker>
            ))}
            {clientes.map((c) => (
              <Marker key={c.id} position={[Number(c.latitud), Number(c.longitud)]} icon={iconoDelCliente(c.estado, c.id === clienteId)}
                title={`${c.nombreCompleto}: ${c.estado.nombre}`} zIndexOffset={c.id === clienteId ? 1000 : 0}
                eventHandlers={{ click: () => setClienteId(c.id) }}>
                <Tooltip><span className="font-semibold">{c.nombreCompleto}</span> · {c.estado.nombre}</Tooltip>
              </Marker>
            ))}
          </MapContainer>
          {avisos.length > 0 && (
            // px-14: a los costados del aviso queda lugar para los botones del zoom.
            <div className="pointer-events-none absolute inset-x-0 top-3 z-500 flex flex-col items-center gap-2 px-14">
              {avisos.map((a) => (
                <p key={a} role="status" className="rounded-lg bg-white px-4 py-2 text-sm text-slate-800 shadow-flotante">{a}</p>
              ))}
            </div>
          )}
        </div>
        <Leyenda estados={estadosVisibles} />
        {elegido
          ? <ClienteElegido cliente={elegido} />
          : clientes.length > 0 && (
            <p className="border-t border-slate-200 px-4 py-3 text-sm text-slate-600">Toca un cliente en el mapa para ver sus datos.</p>
          )}
      </div>
    </div>
  );

  return (
    <Layout>
      <PageHeader title="Mapa de clientes"
        subtitle="Los clientes de cada zona con el color de su estado. Elige una zona para verla de cerca." />
      <div className="bg-white rounded-xl shadow-tarjeta border border-slate-200 overflow-hidden">{contenido}</div>
    </Layout>
  );
}
