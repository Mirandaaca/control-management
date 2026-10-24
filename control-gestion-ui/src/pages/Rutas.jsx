import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Plus, RefreshCw, Route as IconoRuta, Trash2 } from 'lucide-react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Alert, Btn, Cargando, Confirmar, ErrorDeCarga, EstadoVacio, Input, PageHeader } from '../components/ui';
import EstadoRuta from '../components/rutas/EstadoRuta';
import FormularioRuta from '../components/rutas/FormularioRuta';
import MapaDeRuta, { LeyendaDeRuta } from '../components/rutas/MapaDeRuta';
import { AUSENTE, COMPLETADA, EN_CURSO, PLANIFICADA, VISITADO } from '../components/rutas/marcadores';
import Pastilla from '../components/rutas/Pastilla';
import { useAuth } from '../context/auth';
import { ELIGEN_PROMOTOR, PERMISOS } from '../context/permisos';
import { diaEnBolivia, formatearDia, formatearHora } from '../utils/formato';

// El seguimiento de una ruta en curso (7.3.20, paso 6.3): la app manda la ubicación cada 30 s y el panel vuelve a
// pedir la ruta con el mismo ritmo.
const CADA = 30_000;

function resultadoDeLaVisita(v) {
  if (v.resultado === VISITADO) return `Visitado a las ${formatearHora(v.horaVisita)}`;
  if (v.resultado === AUSENTE) return `Ausente a las ${formatearHora(v.horaVisita)}`;
  return 'Pendiente';
}

function estadoDeLaCabeza(ruta) {
  if (ruta.estado === EN_CURSO) return `En curso desde las ${formatearHora(ruta.fechaInicio)}`;
  if (ruta.estado === COMPLETADA) return `Completada a las ${formatearHora(ruta.fechaFin)}`;
  return `Planificada para el ${formatearDia(ruta.fecha)}`;
}

function Avance({ marcadas, total }) {
  const porcentaje = total > 0 ? Math.round((marcadas / total) * 100) : 0;
  return (
    <span className="flex items-center gap-2">
      <span className="h-1.5 flex-1 overflow-hidden rounded-full bg-slate-200" aria-hidden="true">
        <span className="block h-full rounded-full bg-marca-600" style={{ width: `${porcentaje}%` }} />
      </span>
      <span className="text-xs text-slate-600 tabular-nums whitespace-nowrap">{marcadas} de {total}</span>
    </span>
  );
}

// La ruta elegida: el seguimiento (7.3.20, pasos 6 a 6.3), como el 8.6.3.
function Seguimiento({ ruta, zona, puntos, onEliminar }) {
  const paradas = ruta.visitas.map((v) => ({
    clave: v.id, orden: v.orden, resultado: v.resultado, latitud: v.cliente.latitud, longitud: v.cliente.longitud,
    nombre: v.cliente.nombreCompleto, detalle: resultadoDeLaVisita(v),
  }));
  const enCurso = ruta.estado === EN_CURSO;
  const promotor = enCurso && ruta.ultimaUbicacion ? { ...ruta.ultimaUbicacion, nombre: ruta.promotor.nombreCompleto } : null;
  return (
    <section aria-labelledby="titulo-ruta" className="min-w-0">
      <div className="border-b border-slate-200 px-4 py-3">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="min-w-0">
            <h3 id="titulo-ruta" className="text-base font-semibold text-slate-900">{ruta.promotor.nombreCompleto} · {ruta.zona.nombre}</h3>
            <p className="text-sm text-slate-600 tabular-nums">
              <span className="font-mono text-xs" translate="no">{ruta.numero}</span> · {estadoDeLaCabeza(ruta)}
            </p>
          </div>
          {/* Solo una ruta que no se inició: para corregirla se elimina y se planifica otra. */}
          {onEliminar && ruta.estado === PLANIFICADA && (
            <Btn variant="outline" onClick={onEliminar}>
              <Trash2 size={14} aria-hidden="true" />
              Eliminar ruta
            </Btn>
          )}
        </div>
        {enCurso && (
          <p className="mt-0.5 flex items-center gap-1.5 text-sm text-slate-600">
            <RefreshCw size={14} aria-hidden="true" />
            El mapa se actualiza cada 30 segundos.
          </p>
        )}
        {enCurso && !ruta.ultimaUbicacion && (
          <p className="mt-0.5 text-sm text-slate-600">Todavía no llega la ubicación de {ruta.promotor.nombreCompleto}.</p>
        )}
      </div>
      <MapaDeRuta zona={zona} puntos={puntos} paradas={paradas} promotor={promotor} encuadre={ruta.id}
        etiqueta={`Mapa de la ruta ${ruta.numero}: las visitas en orden${promotor ? ' y la posición del promotor' : ''}`} />
      <LeyendaDeRuta conPromotor={ruta.estado !== PLANIFICADA} />
      <div className="border-t border-slate-200 px-4 py-3">
        <h4 className="text-sm font-semibold text-slate-900">Visitas</h4>
        <ol className="mt-2 space-y-1.5" aria-live="polite">
          {ruta.visitas.map((v) => (
            <li key={v.id} className="flex items-center gap-2.5 text-sm">
              <Pastilla orden={v.orden} resultado={v.resultado} ancho />
              <span className="min-w-0 flex-1 line-clamp-2 wrap-break-word text-slate-900" title={v.cliente.nombreCompleto}>
                {v.cliente.nombreCompleto}
              </span>
              <span className="shrink-0 text-slate-600 tabular-nums">{resultadoDeLaVisita(v)}</span>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}

/**
 * Rutas (5.4, 7.3.20, diseño 8.6.3): las rutas de un día con su avance y el seguimiento de la elegida. El gerente
 * planifica; el recorrido (iniciar, marcar y terminar) es del promotor, en la app. ?planificar=1&zona= abre el formulario
 * (desde "Clientes por visitar").
 */
export default function Rutas() {
  const { tiene } = useAuth();
  const puedePlanificar = tiene(PERMISOS.territorial.planificarRuta);
  const eligePromotor = tiene(...ELIGEN_PROMOTOR);
  const [parametros, setParametros] = useSearchParams();

  const [dia, setDia] = useState(diaEnBolivia());
  const [rutas, setRutas] = useState(null);
  const [elegidaId, setElegidaId] = useState(null);
  const [ruta, setRuta] = useState(null);
  const [zonas, setZonas] = useState([]);
  const [puntos, setPuntos] = useState([]);
  const [promotores, setPromotores] = useState([]);
  // El formulario necesita las zonas y los promotores: con ?planificar=1 se abre cuando llegan.
  const [catalogos, setCatalogos] = useState(false);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [errorRuta, setErrorRuta] = useState('');
  const [aviso, setAviso] = useState('');
  const [planificando, setPlanificando] = useState(() => parametros.get('planificar') === '1');
  const [aEliminar, setAEliminar] = useState(null);
  const [eliminando, setEliminando] = useState(false);
  const [errorEliminar, setErrorEliminar] = useState('');
  const zonaInicial = parametros.get('zona');

  useEffect(() => {
    Promise.all([
      api.get('/zonas'),
      api.get('/puntos-referencia'),
      eligePromotor ? api.get('/usuarios/promotores') : Promise.resolve({ data: [] }),
    ])
      .then(([z, p, u]) => { setZonas(z.data); setPuntos(p.data); setPromotores(u.data); setCatalogos(true); })
      .catch((err) => setErrorRuta(mensajeDeError(err, 'No se pudieron cargar las zonas.')));
  }, [eligePromotor]);

  const cargarLista = useCallback(() => api.get('/rutas', { params: { fecha: dia } })
    .then(({ data }) => {
      setRutas(data);
      setErrorCarga('');
      // Se queda con la elegida si sigue en el día; si no, la primera en curso o la primera.
      setElegidaId((actual) => (data.some((r) => r.id === actual)
        ? actual
        : (data.find((r) => r.estado === EN_CURSO) ?? data[0])?.id ?? null));
    })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudieron cargar las rutas.')))
    .finally(() => { setCargando(false); setReintentando(false); }), [dia]);

  useEffect(() => { cargarLista(); }, [cargarLista]);

  const cargarRuta = useCallback(() => {
    if (!elegidaId) return Promise.resolve();
    return api.get(`/rutas/${elegidaId}`)
      .then(({ data }) => { setRuta(data); setErrorRuta(''); })
      .catch((err) => setErrorRuta(mensajeDeError(err, 'No se pudo cargar la ruta.')));
  }, [elegidaId]);

  useEffect(() => { cargarRuta(); }, [cargarRuta]);

  // Con la ruta elegida en curso, la lista y la ruta se vuelven a pedir cada 30 s, sin el indicador de carga.
  const enCurso = ruta?.id === elegidaId && ruta?.estado === EN_CURSO;
  useEffect(() => {
    if (!enCurso) return undefined;
    const reloj = setInterval(() => { cargarLista(); cargarRuta(); }, CADA);
    return () => clearInterval(reloj);
  }, [enCurso, cargarLista, cargarRuta]);

  const cambiarDia = (nuevo) => {
    if (!nuevo) return;
    setAviso('');
    setCargando(true);
    setDia(nuevo);
  };

  const cerrarFormulario = () => {
    setPlanificando(false);
    if (parametros.has('planificar')) setParametros({}, { replace: true });
  };

  const alPlanificar = (nueva) => {
    cerrarFormulario();
    setAviso(`Se planificó ${nueva.numero} para ${nueva.promotor.nombreCompleto} el ${formatearDia(nueva.fecha)}.`);
    setElegidaId(nueva.id);
    if (nueva.fecha === dia) cargarLista();
    else { setCargando(true); setDia(nueva.fecha); }
  };

  const eliminar = async () => {
    setEliminando(true);
    setErrorEliminar('');
    try {
      await api.delete(`/rutas/${aEliminar.id}`);
      setAviso(`Se eliminó la ruta ${aEliminar.numero}.`);
      setAEliminar(null);
      setElegidaId(null);
      setRuta(null);
      cargarLista();
    } catch (err) {
      setErrorEliminar(mensajeDeError(err, 'No se pudo eliminar la ruta.'));
    } finally {
      setEliminando(false);
    }
  };

  const elegida = ruta?.id === elegidaId ? ruta : null;
  const zonaDeLaRuta = zonas.find((z) => z.id === elegida?.zona.id);
  const puntosDeLaRuta = puntos.filter((p) => p.zona.id === elegida?.zona.id);

  let lista;
  if (cargando) lista = <Cargando texto="Cargando rutas…" />;
  else if (errorCarga && !rutas) lista = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargarLista(); }} reintentando={reintentando} />
  );
  else if (rutas.length === 0) lista = (
    <EstadoVacio titulo={`No hay rutas para el ${formatearDia(dia)}`} icono={IconoRuta}>
      {puedePlanificar ? 'Planifica una para que el promotor la vea en la app.' : 'Cuando se planifique una, aparece aquí.'}
    </EstadoVacio>
  );
  else lista = (
    <ul className="space-y-1 p-2">
      {rutas.map((r) => {
        const esLaElegida = r.id === elegidaId;
        return (
          <li key={r.id}>
            <button type="button" aria-pressed={esLaElegida} onClick={() => setElegidaId(r.id)}
              className={`grid w-full gap-1 rounded-lg px-3 py-2 text-left text-sm transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 ${
                esLaElegida ? 'bg-marca-50 text-marca-800' : 'text-slate-800 hover:bg-slate-50'}`}>
              <span className="font-medium">{r.promotor.nombreCompleto}</span>
              <span className="text-xs text-slate-600">
                {r.zona.nombre} · <span className="font-mono" translate="no">{r.numero}</span>
              </span>
              <span className="flex items-center gap-2">
                <EstadoRuta estado={r.estado} blanca={esLaElegida} />
                <span className="min-w-0 flex-1"><Avance marcadas={r.marcadas} total={r.total} /></span>
              </span>
            </button>
          </li>
        );
      })}
    </ul>
  );

  return (
    <Layout>
      <PageHeader
        title="Rutas"
        subtitle="Las rutas de re-visita de los promotores, su avance y su seguimiento."
        action={puedePlanificar && (
          <Btn size="md" onClick={() => { setAviso(''); setPlanificando(true); }}>
            <Plus size={16} aria-hidden="true" />
            Planificar ruta
          </Btn>
        )}
      />
      {aviso && <Alert type="success" className="mb-4">{aviso}</Alert>}
      {errorCarga && rutas && <Alert type="error" className="mb-4">{errorCarga}</Alert>}

      <div className="grid overflow-hidden rounded-xl border border-slate-200 bg-white shadow-tarjeta lg:grid-cols-[18rem_minmax(0,1fr)]">
        <nav aria-label="Rutas del día" className="border-b border-slate-200 lg:border-b-0 lg:border-r">
          <div className="border-b border-slate-200 p-3">
            <Input label="Día" type="date" value={dia} onChange={(e) => cambiarDia(e.target.value)} />
          </div>
          <div className="max-h-72 overflow-y-auto lg:max-h-none">{lista}</div>
        </nav>
        <div className="min-w-0">
          {errorRuta && <div className="p-4"><Alert type="error">{errorRuta}</Alert></div>}
          {elegida
            ? (
              <Seguimiento ruta={elegida} zona={zonaDeLaRuta} puntos={puntosDeLaRuta}
                onEliminar={puedePlanificar ? () => { setErrorEliminar(''); setAEliminar(elegida); } : null} />
            )
            : !cargando && rutas?.length > 0 && !errorRuta && <Cargando texto="Cargando la ruta…" />}
          {!cargando && rutas?.length === 0 && (
            <p className="px-4 py-10 text-center text-sm text-slate-600">Elige otro día para ver sus rutas.</p>
          )}
        </div>
      </div>

      <Confirmar
        abierto={aEliminar !== null}
        titulo={`¿Eliminar la ruta ${aEliminar?.nombre ?? ''} de ${aEliminar?.promotor.nombreCompleto ?? ''}?`}
        textoAccion="Eliminar ruta"
        enviando={eliminando}
        error={errorEliminar}
        onConfirmar={eliminar}
        onCancelar={() => setAEliminar(null)}
      >
        <p>Después puedes planificar otra para ese día.</p>
      </Confirmar>

      {planificando && catalogos && (
        <FormularioRuta zonas={zonas} promotores={promotores} diaInicial={dia} zonaInicial={zonaInicial}
          onCerrar={cerrarFormulario} onPlanificada={alPlanificar} />
      )}
    </Layout>
  );
}
