import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Landmark, Map as Mapa, MapPinPlus, Move, PenLine, Pencil, Trash2, Undo2 } from 'lucide-react';
import L from 'leaflet';
import { MapContainer, Marker, Pane, Polygon, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Alert, Btn, Cargando, Confirmar, ErrorDeCarga, EstadoVacio, PageHeader } from '../components/ui';
import { avisar } from '../components/ui/avisar';
import DibujoDeLimite from '../components/zonas/DibujoDeLimite';
import FormularioPunto from '../components/zonas/FormularioPunto';
import { comoPosiciones, ESTILO_ZONA, ESTILO_ZONA_ELEGIDA, ICONO_PUNTO } from '../components/zonas/marcadores';
import { useAuth } from '../context/auth';
import { PANTALLAS, PERMISOS } from '../context/permisos';

// Centro de Santa Cruz de la Sierra: donde abre el mapa si no hay nada dibujado.
const SANTA_CRUZ = [-17.7833, -63.1821];
const { territorial } = PERMISOS;
const CAPA_NOMBRES = 'nombresDeZonas';

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

// En el modo "punto", un clic en el mapa marca dónde está el punto de referencia nuevo.
function AlTocarPunto({ onTocar }) {
  useMapEvents({ click: (e) => onTocar(Math.round(e.latlng.lat * 1e6) / 1e6, Math.round(e.latlng.lng * 1e6) / 1e6) });
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

const cuantosPuntos = (n) => (n === 1 ? '1 punto' : `${n} puntos`);

/**
 * Mapa de zonas (5.5): el límite de cada zona y sus puntos de referencia, como mercados. Lo abren el gerente general,
 * que dibuja, y el promotor, que solo mira. ?zona= elige una zona al abrir (el enlace "Ver en el mapa" de Zonas).
 */
export default function MapaZonas() {
  const { tiene } = useAuth();
  const puedeDibujar = tiene(territorial.dibujarLimites);
  const puedeRegistrarPuntos = tiene(territorial.registrarPuntoReferencia);
  const [parametros, setParametros] = useSearchParams();
  const [zonas, setZonas] = useState(null);
  const [puntos, setPuntos] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');

  // El límite que se dibuja ("dibujar") o se edita ("editar"), con los vértices como [[lat, lng], …]; o "punto",
  // mientras se espera el clic que marca un punto de referencia nuevo.
  const [modo, setModo] = useState(null);
  const [borrador, setBorrador] = useState([]);
  const [errorLimite, setErrorLimite] = useState('');
  const [guardandoLimite, setGuardandoLimite] = useState(false);
  const [confirmarReemplazo, setConfirmarReemplazo] = useState(false);
  const [quitando, setQuitando] = useState(false);
  const [errorQuitar, setErrorQuitar] = useState('');

  const [formularioPunto, setFormularioPunto] = useState(null);
  const [aEliminarPunto, setAEliminarPunto] = useState(null);
  const [errorEliminarPunto, setErrorEliminarPunto] = useState('');
  const [eliminandoPunto, setEliminandoPunto] = useState(false);

  const cargar = useCallback(() => Promise.all([api.get('/zonas'), api.get('/puntos-referencia')])
    .then(([z, p]) => { setZonas(z.data); setPuntos(p.data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar el mapa de zonas.')))
    .finally(() => { setCargando(false); setReintentando(false); }), []);

  useEffect(() => { cargar(); }, [cargar]);

  const elegidaId = Number(parametros.get('zona')) || null;
  const elegida = zonas?.find((z) => z.id === elegidaId) ?? null;
  const elegir = (id) => setParametros(id ? { zona: String(id) } : {}, { replace: true });
  const puntosDeLaElegida = useMemo(() => puntos.filter((p) => p.zona.id === elegidaId), [puntos, elegidaId]);
  const reemplazarZona = (zona) => setZonas((lista) => lista.map((z) => (z.id === zona.id ? zona : z)));

  const empezar = (nuevoModo) => {
    setErrorLimite('');
    setBorrador(nuevoModo === 'editar' ? comoPosiciones(elegida.limite) : []);
    setModo(nuevoModo);
  };

  const terminarDibujo = () => {
    setModo(null);
    setBorrador([]);
    setErrorLimite('');
    setConfirmarReemplazo(false);
  };

  const guardarLimite = async () => {
    setGuardandoLimite(true);
    setErrorLimite('');
    try {
      const { data } = await api.put(`/zonas/${elegida.id}/limite`, {
        vertices: borrador.map(([latitud, longitud]) => ({ latitud, longitud })),
      });
      reemplazarZona(data);
      terminarDibujo();
      avisar.exito(`Se guardó el límite de ${data.nombre}.`);
    } catch (err) {
      setConfirmarReemplazo(false);
      setErrorLimite(mensajeDeError(err, 'No se pudo guardar el límite.'));
    } finally {
      setGuardandoLimite(false);
    }
  };

  // Un dibujo nuevo reemplaza al límite que ya había: eso se confirma. Editar los vértices del mismo, no.
  const alTerminar = () => (modo === 'dibujar' && elegida.limite ? setConfirmarReemplazo(true) : guardarLimite());

  const quitarLimite = async () => {
    setGuardandoLimite(true);
    setErrorQuitar('');
    try {
      await api.delete(`/zonas/${elegida.id}/limite`);
      reemplazarZona({ ...elegida, limite: null });
      setQuitando(false);
      avisar.exito(`Se quitó el límite de ${elegida.nombre}.`);
    } catch (err) {
      setErrorQuitar(mensajeDeError(err, 'No se pudo quitar el límite.'));
    } finally {
      setGuardandoLimite(false);
    }
  };

  const marcarPunto = (latitud, longitud) => {
    setModo(null);
    setFormularioPunto({ latitud, longitud });
  };

  // Un punto cambia la cuenta de su zona (y de la anterior, si se movió): se vuelve a pedir todo, que son pocas filas.
  const alGuardarPunto = (punto, esNuevo) => {
    setFormularioPunto(null);
    avisar.exito(esNuevo ? `Se registró ${punto.nombre} en ${punto.zona.nombre}.` : `Se guardó ${punto.nombre}.`);
    cargar();
  };

  const eliminarPunto = async () => {
    setEliminandoPunto(true);
    setErrorEliminarPunto('');
    try {
      await api.delete(`/puntos-referencia/${aEliminarPunto.id}`);
      avisar.exito(`Se eliminó ${aEliminarPunto.nombre}.`);
      setAEliminarPunto(null);
      cargar();
    } catch (err) {
      setErrorEliminarPunto(mensajeDeError(err, 'No se pudo eliminar el punto de referencia.'));
    } finally {
      setEliminandoPunto(false);
    }
  };

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
              {/* Mientras se dibuja, la zona no cambia: el dibujo es de la elegida. */}
              <button type="button" aria-pressed={z.id === elegidaId} disabled={modo !== null}
                onClick={() => elegir(z.id === elegidaId ? null : z.id)}
                className={`w-full rounded-lg px-3 py-2 text-left text-sm transition-colors cursor-pointer disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 ${
                  z.id === elegidaId ? 'bg-marca-50 text-marca-800' : 'text-slate-800 hover:bg-slate-50'}`}>
                <span className="block font-medium">{z.nombre}</span>
                <span className="block text-xs text-slate-600">
                  {z.limite ? 'Con límite' : 'Sin límite'}
                  {' · '}
                  {cuantosPuntos(z.puntosDeReferencia)}
                </span>
              </button>
            </li>
          ))}
        </ul>
      </nav>
      <div className="min-w-0">
        {!modo && ((elegida && puedeDibujar) || puedeRegistrarPuntos) && (
          <div className="flex flex-wrap items-center gap-2 border-b border-slate-200 px-4 py-3">
            <h3 className="mr-auto text-sm font-semibold text-slate-900">{elegida?.nombre ?? 'Elige una zona para dibujar su límite'}</h3>
            {puedeRegistrarPuntos && (
              <Btn variant="outline" onClick={() => setModo('punto')}>
                <MapPinPlus size={14} aria-hidden="true" />
                Agregar punto
              </Btn>
            )}
            {elegida && puedeDibujar && (
              <Btn variant="outline" onClick={() => empezar('dibujar')}>
                <PenLine size={14} aria-hidden="true" />
                {elegida.limite ? 'Redibujar límite' : 'Dibujar límite'}
              </Btn>
            )}
            {elegida?.limite && puedeDibujar && (
              <>
                <Btn variant="outline" onClick={() => empezar('editar')}>
                  <Move size={14} aria-hidden="true" />
                  Editar vértices
                </Btn>
                <Btn variant="ghost" onClick={() => { setErrorQuitar(''); setQuitando(true); }}>
                  <Trash2 size={14} aria-hidden="true" />
                  Quitar límite
                </Btn>
              </>
            )}
          </div>
        )}
        {modo === 'punto' && (
          <div className="flex flex-wrap items-center gap-2 border-b border-slate-200 bg-marca-50 px-4 py-3">
            <p role="status" className="mr-auto text-sm text-marca-800">Toca el mapa donde está el punto de referencia.</p>
            <Btn variant="outline" onClick={() => setModo(null)}>Cancelar</Btn>
          </div>
        )}
        {(modo === 'dibujar' || modo === 'editar') && (
          <div className="space-y-2 border-b border-slate-200 bg-marca-50 px-4 py-3">
            <div className="flex flex-wrap items-center gap-2">
              <p role="status" className="mr-auto text-sm text-marca-800">
                {modo === 'dibujar'
                  ? `Toca el mapa para marcar cada vértice de ${elegida.nombre} · ${cuantosPuntos(borrador.length)}`
                  : `Arrastra los vértices para mover el límite de ${elegida.nombre}.`}
              </p>
              {modo === 'dibujar' && (
                <Btn variant="outline" disabled={borrador.length === 0 || guardandoLimite} onClick={() => setBorrador((b) => b.slice(0, -1))}>
                  <Undo2 size={14} aria-hidden="true" />
                  Deshacer el último punto
                </Btn>
              )}
              <Btn disabled={borrador.length < 3 || guardandoLimite} onClick={alTerminar}>
                {guardandoLimite ? 'Guardando…' : modo === 'dibujar' ? 'Terminar' : 'Guardar cambios'}
              </Btn>
              <Btn variant="outline" disabled={guardandoLimite} onClick={terminarDibujo}>Cancelar</Btn>
            </div>
            {errorLimite && <Alert type="error">{errorLimite}</Alert>}
          </div>
        )}
        {/* isolate: las capas de Leaflet (z-index 400 a 1000) quedan dentro del mapa y no tapan los modales. */}
        <div className="relative isolate h-104 sm:h-128" role="group"
          aria-label="Mapa de zonas: cada zona con su límite y sus puntos de referencia">
          <MapContainer center={SANTA_CRUZ} zoom={13} scrollWheelZoom={false}
            className={`h-full w-full ${modo === 'dibujar' || modo === 'punto' ? 'cursor-crosshair' : ''}`}>
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <Encuadrar zonas={zonas} puntos={puntos} elegida={elegida} />
            <RuedaAlEnfocar />
            {/* El nombre de cada zona va en su propia capa, debajo de los puntos (600): si no, tapa al que está en el
                centro de la zona. La capa se crea antes que sus hijos, así existe cuando se abre cada nombre. */}
            <Pane name={CAPA_NOMBRES} style={{ zIndex: 450 }}>
              {/* Mientras se dibuja, el límite guardado de la elegida se esconde y ninguna zona responde al clic. */}
              {zonas.filter((z) => z.limite && !((modo === 'dibujar' || modo === 'editar') && z.id === elegidaId)).map((z) => (
                <Polygon key={z.id} pane="overlayPane" positions={comoPosiciones(z.limite)}
                  pathOptions={z.id === elegidaId ? ESTILO_ZONA_ELEGIDA : ESTILO_ZONA}
                  eventHandlers={{ click: () => { if (!modo) elegir(z.id); } }}>
                  <Tooltip pane={CAPA_NOMBRES} permanent direction="center" className="font-semibold">{z.nombre}</Tooltip>
                </Polygon>
              ))}
            </Pane>
            {puntos.map((p) => (
              <Marker key={p.id} position={[p.latitud, p.longitud]} icon={ICONO_PUNTO} title={`${p.nombre} (${p.zona.nombre})`}>
                <Tooltip>
                  <span className="font-semibold">{p.nombre}</span> · {p.zona.nombre}
                  {p.descripcion && <><br />{p.descripcion}</>}
                </Tooltip>
              </Marker>
            ))}
            {(modo === 'dibujar' || modo === 'editar') && <DibujoDeLimite modo={modo} vertices={borrador} onCambiar={setBorrador} />}
            {modo === 'punto' && <AlTocarPunto onTocar={marcarPunto} />}
          </MapContainer>
          {elegida && !elegida.limite && !modo && (
            // px-14: a los costados del aviso queda lugar para los botones del zoom, que no se tapan en el celular.
            <div className="pointer-events-none absolute inset-x-0 top-3 z-500 flex justify-center px-14">
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
                <ul className="mt-2 divide-y divide-slate-200 text-sm text-slate-800">
                  {puntosDeLaElegida.map((p) => (
                    <li key={p.id} className="flex flex-wrap items-center gap-2 py-1.5">
                      <span className="mr-auto min-w-0">
                        <span className="font-medium">{p.nombre}</span>
                        {p.descripcion && <span className="text-slate-600"> · {p.descripcion}</span>}
                      </span>
                      {puedeRegistrarPuntos && (
                        <>
                          <Btn variant="ghost" onClick={() => setFormularioPunto(p)} aria-label={`Editar ${p.nombre}`}>
                            <Pencil size={14} aria-hidden="true" />
                            Editar
                          </Btn>
                          <Btn variant="ghost" onClick={() => { setErrorEliminarPunto(''); setAEliminarPunto(p); }} aria-label={`Eliminar ${p.nombre}`}>
                            <Trash2 size={14} aria-hidden="true" />
                            Eliminar
                          </Btn>
                        </>
                      )}
                    </li>
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

      <Confirmar
        abierto={confirmarReemplazo}
        titulo={`¿Reemplazar el límite de ${elegida?.nombre ?? ''}?`}
        textoAccion="Reemplazar límite"
        variante="primary"
        enviando={guardandoLimite}
        onConfirmar={guardarLimite}
        onCancelar={() => setConfirmarReemplazo(false)}
      >
        <p>El límite que dibujaste reemplaza al que tenía la zona. El anterior no se puede recuperar.</p>
      </Confirmar>

      {formularioPunto && (
        <FormularioPunto punto={formularioPunto} zonas={zonas} zonaPorDefecto={elegidaId}
          onCerrar={() => setFormularioPunto(null)} onGuardado={alGuardarPunto} />
      )}

      <Confirmar
        abierto={aEliminarPunto !== null}
        titulo={`¿Eliminar ${aEliminarPunto?.nombre ?? ''}?`}
        textoAccion="Eliminar"
        enviando={eliminandoPunto}
        error={errorEliminarPunto}
        onConfirmar={eliminarPunto}
        onCancelar={() => setAEliminarPunto(null)}
      >
        <p>El punto de referencia deja de verse en el mapa. Esto no se puede deshacer.</p>
      </Confirmar>

      <Confirmar
        abierto={quitando}
        titulo={`¿Quitar el límite de ${elegida?.nombre ?? ''}?`}
        textoAccion="Quitar límite"
        enviando={guardandoLimite}
        error={errorQuitar}
        onConfirmar={quitarLimite}
        onCancelar={() => setQuitando(false)}
      >
        <p>La zona queda sin límite en el mapa. Sus clientes y sus puntos de referencia no cambian.</p>
      </Confirmar>
    </Layout>
  );
}
