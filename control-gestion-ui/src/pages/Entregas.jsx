import { useCallback, useEffect, useState } from 'react';
import { RefreshCw, Truck } from 'lucide-react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import {
  Alert, Avance, ErrorDeCarga, EsqueletoDeLista, EsqueletoDeMapa, EstadoVacio, PageHeader, ZonaDeCarga,
} from '../components/ui';
import MapaDeEntregas, { LeyendaDeEntregas } from '../components/entregas/MapaDeEntregas';
import { ENTREGADO, PENDIENTE } from '../components/entregas/marcadores';
import PastillaDeEntrega from '../components/entregas/PastillaDeEntrega';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { usePeticionVigente } from '../hooks/usePeticionVigente';
import { formatearDia, formatearHora, leerFechaUtc } from '../utils/formato';

// Mientras alguien entrega, la app manda su ubicación cada 10 s y el panel pide las entregas con el mismo ritmo (tarea
// 31). Sin entregas en ruta, cada minuto: así aparece lo que salga a ruta desde otra pestaña.
const CON_ENTREGAS_EN_RUTA = 10_000;
const SIN_ENTREGAS_EN_RUTA = 60_000;
const TODOS = 'todos';

/**
 * Cuánto hace que llegó la ubicación, contado con la hora del servidor (`ahora` de la respuesta), no con el reloj de la
 * PC, que puede estar corrido.
 */
function textoDeLaUbicacion(ubicacion, ahora) {
  if (!ubicacion) return 'Todavía no llega su ubicación';
  const segundos = Math.max(0, Math.round((leerFechaUtc(ahora) - leerFechaUtc(ubicacion.fecha)) / 1000));
  if (segundos < 60) return `Ubicación de hace ${segundos} s`;
  if (segundos < 3600) return `Ubicación de hace ${Math.floor(segundos / 60)} min`;
  return `Última ubicación a las ${formatearHora(ubicacion.fecha)}`;
}

function estadoDeLaEntrega(e) {
  if (e.estado.id === ENTREGADO) return e.fechaEntregaReal ? `Entregado a las ${formatearHora(e.fechaEntregaReal)}` : 'Entregado';
  if (e.estado.id === PENDIENTE) return 'Pendiente: todavía no sale a ruta';
  return 'En ruta';
}

/** Lo que el mapa necesita de un promotor: su posición con la hora y el rastro, que termina donde está. */
function promotorDelMapa(p, ahora) {
  const rastro = p.rastro.map((r) => [Number(r.latitud), Number(r.longitud)]);
  return {
    id: p.promotor.id,
    nombre: p.promotor.nombreCompleto,
    etiqueta: textoDeLaUbicacion(p.ultimaUbicacion, ahora),
    ubicacion: p.ultimaUbicacion ? { ...p.ultimaUbicacion, hora: formatearHora(p.ultimaUbicacion.fecha) } : null,
    rastro,
  };
}

function entregasDelMapa(p) {
  return p.entregas.filter((e) => e.ubicacionDelCliente).map((e) => ({
    clave: e.id,
    hora: formatearHora(e.fechaEntregaEstimada),
    estado: e.estado.id,
    latitud: e.ubicacionDelCliente.latitud,
    longitud: e.ubicacionDelCliente.longitud,
    nombre: e.cliente.nombreCompleto,
    detalle: `${estadoDeLaEntrega(e)} (${p.promotor.nombreCompleto})`,
  }));
}

function ListaDeEntregas({ entregas }) {
  return (
    <ol className="mt-2 space-y-2.5">
      {entregas.map((e) => (
        <li key={e.id} className="flex items-start gap-2.5 text-sm">
          <PastillaDeEntrega hora={formatearHora(e.fechaEntregaEstimada)} estado={e.estado.id} ancho />
          <span className="min-w-0 flex-1">
            <span className="block font-medium text-slate-900 wrap-break-word">{e.cliente.nombreCompleto}</span>
            <span className="block text-slate-600 wrap-break-word">{e.direccionEntrega}</span>
            <span className="block text-xs text-slate-600">
              <span className="font-mono" translate="no">{e.numero}</span>
              {!e.ubicacionDelCliente && ' · El cliente no tiene ubicación: no está en el mapa.'}
            </span>
          </span>
          <span className="shrink-0 text-right text-slate-700 tabular-nums">{estadoDeLaEntrega(e)}</span>
        </li>
      ))}
    </ol>
  );
}

/**
 * Entregas de hoy (tarea 31, trabajo futuro; con la forma de Rutas, 8.6.3): los promotores con pedidos asignados para
 * hoy, dónde está cada uno, por dónde pasó en la última media hora y cómo va con sus entregas. La app manda la ubicación
 * mientras el promotor tiene entregas en ruta y la app abierta.
 */
export default function Entregas() {
  const { tiene } = useAuth();
  const asigna = tiene(PERMISOS.pedidos.asignarPedidos);
  const [datos, setDatos] = useState(null);
  const [elegidoId, setElegidoId] = useState(TODOS);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');

  const nuevaPeticion = usePeticionVigente();
  const cargar = useCallback(() => {
    const siVigente = nuevaPeticion();
    return api.get('/pedidos/entregas')
      .then(siVigente(({ data }) => { setDatos(data); setErrorCarga(''); }))
      .catch(siVigente((err) => setErrorCarga(mensajeDeError(err, 'No se pudieron cargar las entregas de hoy.'))))
      .finally(siVigente(() => { setCargando(false); setReintentando(false); }));
  }, [nuevaPeticion]);

  useEffect(() => { cargar(); }, [cargar]);

  // Se vuelve a pedir sin el indicador de carga y sin mover el mapa. Con la pestaña oculta no se pide nada; al volver a
  // ella, enseguida.
  const cargado = datos !== null;
  const hayEnRuta = datos?.promotores.some((p) => p.enRuta > 0) ?? false;
  useEffect(() => {
    if (!cargado) return undefined;
    let reloj = null;
    const seguir = () => { reloj ??= setInterval(cargar, hayEnRuta ? CON_ENTREGAS_EN_RUTA : SIN_ENTREGAS_EN_RUTA); };
    const pausar = () => { clearInterval(reloj); reloj = null; };
    const alCambiarLaVisibilidad = () => {
      if (document.visibilityState === 'hidden') pausar();
      else { cargar(); seguir(); }
    };
    if (document.visibilityState !== 'hidden') seguir();
    document.addEventListener('visibilitychange', alCambiarLaVisibilidad);
    return () => { pausar(); document.removeEventListener('visibilitychange', alCambiarLaVisibilidad); };
  }, [cargado, hayEnRuta, cargar]);

  const promotores = datos?.promotores ?? [];
  // Si el elegido ya no tiene entregas hoy (se las reasignaron), se vuelve a "Todos".
  const elegido = promotores.find((p) => p.promotor.id === elegidoId) ?? null;
  const visibles = elegido ? [elegido] : promotores;
  const total = promotores.reduce((suma, p) => suma + p.total, 0);
  const entregadas = promotores.reduce((suma, p) => suma + p.entregadas, 0);

  let lista;
  if (cargando) lista = <ZonaDeCarga texto="Cargando las entregas…"><EsqueletoDeLista /></ZonaDeCarga>;
  else if (errorCarga && !datos) lista = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (promotores.length === 0) lista = (
    <EstadoVacio titulo="Hoy no hay entregas asignadas" icono={Truck}>
      {asigna
        ? 'Asigna pedidos para hoy en Pedidos: aparecen aquí con el promotor que los entrega.'
        : 'Cuando se asignen pedidos para hoy, aparecen aquí con el promotor que los entrega.'}
    </EstadoVacio>
  );
  else {
    const opcion = (id, contenido) => {
      const esElElegido = (id === TODOS && !elegido) || elegido?.promotor.id === id;
      return (
        <li key={id}>
          <button type="button" aria-pressed={esElElegido} onClick={() => setElegidoId(id)}
            className={`grid w-full gap-1 rounded-lg px-3 py-2 text-left text-sm transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 ${
              esElElegido ? 'bg-marca-50 text-marca-800' : 'text-slate-800 hover:bg-slate-50'}`}>
            {contenido}
          </button>
        </li>
      );
    };
    lista = (
      <ul className="space-y-1 p-2">
        {opcion(TODOS, (
          <>
            <span className="font-medium">Todos los promotores</span>
            <span className="min-w-0"><Avance hechas={entregadas} total={total} sufijo="entregadas" /></span>
          </>
        ))}
        {promotores.map((p) => opcion(p.promotor.id, (
          <>
            <span className="font-medium">{p.promotor.nombreCompleto}</span>
            <span className="text-xs text-slate-600">{textoDeLaUbicacion(p.ultimaUbicacion, datos.ahora)}</span>
            <span className="min-w-0"><Avance hechas={p.entregadas} total={p.total} sufijo="entregadas" /></span>
          </>
        )))}
      </ul>
    );
  }

  const mapaDePromotores = visibles.map((p) => promotorDelMapa(p, datos.ahora));
  const mapaDeEntregas = visibles.flatMap(entregasDelMapa);
  const enRutaVisibles = visibles.reduce((suma, p) => suma + p.enRuta, 0);

  return (
    <Layout>
      <PageHeader
        title="Entregas de hoy"
        subtitle="Dónde está cada promotor que entrega los pedidos de hoy y cómo va con sus entregas."
      />
      {errorCarga && datos && <Alert type="error" className="mb-4">{errorCarga}</Alert>}

      <div className="grid overflow-hidden rounded-xl border border-slate-200 bg-white shadow-tarjeta lg:grid-cols-[18rem_minmax(0,1fr)]">
        <nav aria-label="Promotores con entregas hoy" className="border-b border-slate-200 lg:border-b-0 lg:border-r">
          <p className="border-b border-slate-200 px-4 py-3 text-sm font-medium text-slate-700 tabular-nums">
            Hoy, {datos ? formatearDia(datos.dia) : '…'}
          </p>
          <div className="max-h-72 overflow-y-auto lg:max-h-none">{lista}</div>
        </nav>
        <div className="min-w-0">
          {cargando && <ZonaDeCarga texto="Cargando el mapa…"><EsqueletoDeMapa /></ZonaDeCarga>}
          {!cargando && promotores.length > 0 && (
            <section aria-labelledby="titulo-entregas" className="min-w-0">
              <div className="border-b border-slate-200 px-4 py-3">
                <h3 id="titulo-entregas" className="text-base font-semibold text-slate-900">
                  {elegido ? elegido.promotor.nombreCompleto : 'Todos los promotores'}
                </h3>
                <p className="text-sm text-slate-600 tabular-nums">
                  {elegido
                    ? `${elegido.entregadas} de ${elegido.total} entregadas · ${elegido.enRuta} en ruta`
                    : `${promotores.length} ${promotores.length === 1 ? 'promotor' : 'promotores'} · ${entregadas} de ${total} entregadas · ${enRutaVisibles} en ruta`}
                </p>
                {hayEnRuta && (
                  <p className="mt-0.5 flex items-center gap-1.5 text-sm text-slate-600">
                    <RefreshCw size={14} aria-hidden="true" />
                    El mapa se actualiza cada 10 segundos.
                  </p>
                )}
                {elegido && !elegido.ultimaUbicacion && (
                  <p className="mt-0.5 text-sm text-slate-600">Todavía no llega la ubicación de {elegido.promotor.nombreCompleto}.</p>
                )}
              </div>
              <MapaDeEntregas promotores={mapaDePromotores} entregas={mapaDeEntregas} encuadre={elegido?.promotor.id ?? TODOS}
                conNombres={!elegido}
                etiqueta={elegido
                  ? `Mapa de las entregas de ${elegido.promotor.nombreCompleto}, con su posición y su rastro`
                  : 'Mapa de las entregas de hoy, con la posición y el rastro de cada promotor'} />
              <LeyendaDeEntregas />
              <div className="border-t border-slate-200 px-4 py-3" aria-live="polite">
                {visibles.map((p) => (
                  <div key={p.promotor.id} className="mb-3 last:mb-0">
                    <h4 className="text-sm font-semibold text-slate-900">
                      {elegido ? 'Entregas' : `Entregas de ${p.promotor.nombreCompleto}`}
                    </h4>
                    <ListaDeEntregas entregas={p.entregas} />
                  </div>
                ))}
              </div>
            </section>
          )}
          {!cargando && datos && promotores.length === 0 && (
            <p className="px-4 py-10 text-center text-sm text-slate-600">El mapa aparece cuando haya entregas para hoy.</p>
          )}
        </div>
      </div>
    </Layout>
  );
}
