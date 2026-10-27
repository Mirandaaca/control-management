import { useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { ArrowDown, ArrowUp, LoaderCircle, X } from 'lucide-react';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import { Alert, Btn, CampoFecha, Combobox, Modal, Selector } from '../ui';
import { diaEnBolivia, formatearDia } from '../../utils/formato';
import { mostrarPrimerProblema } from '../../utils/foco';
import MapaDeRuta from './MapaDeRuta';
import { PENDIENTE } from './marcadores';
import Pastilla from './Pastilla';

const INACTIVO = 4;

function detalleDeLaParada(p) {
  if (p.aMano) return 'Agregado a mano';
  if (p.diasDeAtraso === null || p.diasDeAtraso === undefined) return 'Sin visitas';
  if (p.diasDeAtraso <= 0) return 'Le toca ese día';
  return p.diasDeAtraso === 1 ? '1 día de atraso' : `${p.diasDeAtraso} días de atraso`;
}

/**
 * Planificar una ruta (7.3.20, paso 2): la zona, el promotor (se propone el de la zona) y el día. La API sugiere los
 * clientes con la re-visita vencida ese día, ya ordenados; el gerente agrega, quita o reordena y confirma.
 */
export default function FormularioRuta({ zonas, promotores, diaInicial, zonaInicial, onCerrar, onPlanificada }) {
  const hoy = diaEnBolivia();
  const zonaDe = (id) => zonas.find((z) => String(z.id) === String(id));
  const [zonaId, setZonaId] = useState(zonaInicial && zonaDe(zonaInicial) ? String(zonaInicial) : '');
  const [promotorId, setPromotorId] = useState(zonaDe(zonaInicial)?.promotor?.id ?? '');
  const [fecha, setFecha] = useState(diaInicial && diaInicial >= hoy ? diaInicial : hoy);
  const [paradas, setParadas] = useState([]);
  const [sugerida, setSugerida] = useState({ cargando: Boolean(zonaId), error: '', vacia: false });
  const [clientesDeLaZona, setClientesDeLaZona] = useState([]);
  const [errores, setErrores] = useState({});
  const [errorGeneral, setErrorGeneral] = useState('');
  const [enviando, setEnviando] = useState(false);
  const contenedor = useRef(null);
  // Descarta la respuesta de una sugerida vieja si el gerente cambió la zona o el día mientras llegaba.
  const pedido = useRef(0);

  const pedirSugerida = (zona, dia) => {
    const numero = ++pedido.current;
    Promise.all([
      api.get('/rutas/sugerida', { params: { zonaId: zona, fecha: dia } }),
      api.get('/clientes/mapa', { params: { zonaId: zona } }),
    ])
      .then(([s, c]) => {
        if (numero !== pedido.current) return;
        setParadas(s.data);
        setClientesDeLaZona(c.data.filter((cl) => cl.estado.id !== INACTIVO));
        setSugerida({ cargando: false, error: '', vacia: s.data.length === 0 });
      })
      .catch((err) => {
        if (numero !== pedido.current) return;
        setSugerida({ cargando: false, error: mensajeDeError(err, 'No se pudo armar la ruta sugerida.'), vacia: false });
      });
  };

  // Abierto desde "Clientes por visitar" con una zona: la sugerida se pide al abrir.
  useEffect(() => {
    if (zonaId) pedirSugerida(zonaId, fecha);
    // Solo al abrir: después la piden los cambios de zona y de día.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const recalcular = (zona, dia) => {
    setErrores({});
    setErrorGeneral('');
    if (!zona || !dia) return;
    setSugerida({ cargando: true, error: '', vacia: false });
    pedirSugerida(zona, dia);
  };

  const cambiarZona = (id) => {
    setZonaId(id);
    const promotorDeLaZona = zonaDe(id)?.promotor?.id;
    if (promotorDeLaZona && promotores.some((p) => p.id === promotorDeLaZona)) setPromotorId(promotorDeLaZona);
    recalcular(id, fecha);
  };

  const cambiarFecha = (dia) => {
    setFecha(dia);
    recalcular(zonaId, dia);
  };

  const mover = (desde, hacia) => setParadas((lista) => {
    const nueva = [...lista];
    const [movida] = nueva.splice(desde, 1);
    nueva.splice(hacia, 0, movida);
    return nueva;
  });

  const agregar = (id, opcion) => {
    const c = opcion.cliente;
    setParadas((lista) => [...lista, { ...c, aMano: true }]);
    setErrores((e) => ({ ...e, clientes: undefined }));
  };

  const planificar = async () => {
    setEnviando(true);
    setErrores({});
    setErrorGeneral('');
    try {
      const { data } = await api.post('/rutas', { zonaId: Number(zonaId) || null, promotorId: promotorId || null, fecha, clientes: paradas.map((p) => p.id) });
      onPlanificada(data);
    } catch (err) {
      const deLosCampos = erroresPorCampo(err);
      flushSync(() => {
        setErrores(deLosCampos);
        if (Object.keys(deLosCampos).length === 0) setErrorGeneral(mensajeDeError(err, 'No se pudo planificar la ruta.'));
      });
      mostrarPrimerProblema(contenedor.current);
    } finally {
      setEnviando(false);
    }
  };

  const zona = zonaDe(zonaId);
  const enLaRuta = new Set(paradas.map((p) => p.id));
  const paraAgregar = clientesDeLaZona.filter((c) => !enLaRuta.has(c.id))
    .map((c) => ({ valor: String(c.id), texto: c.nombreCompleto, detalle: `${c.codigo} · ${c.direccionTexto}`, cliente: c }));

  return (
    <Modal
      abierto
      onCerrar={onCerrar}
      titulo="Planificar ruta"
      descripcion="Elige la zona, el promotor y el día. La ruta sugerida empieza por el cliente más atrasado y sigue, cada vez, por el más cercano."
      ancho="lg"
      pie={(
        <>
          <Btn variant="outline" size="md" onClick={onCerrar} disabled={enviando}>Cancelar</Btn>
          <Btn size="md" onClick={planificar} disabled={enviando || sugerida.cargando}>
            {enviando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
            Planificar ruta
          </Btn>
        </>
      )}
    >
      <div ref={contenedor} className="space-y-4 pb-1">
        {errorGeneral && <Alert type="error">{errorGeneral}</Alert>}
        <div className="grid gap-3 sm:grid-cols-3">
          <Selector label="Zona" value={zonaId} placeholder="Elige la zona" error={errores.zonaId}
            opciones={zonas.map((z) => ({ valor: String(z.id), texto: z.nombre }))} onChange={cambiarZona} />
          <Selector label="Promotor" value={promotorId} placeholder="Elige el promotor" error={errores.promotorId}
            opciones={promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }))}
            onChange={(id) => { setPromotorId(id); setErrorGeneral(''); }} />
          <CampoFecha label="Día" value={fecha} min={hoy} error={errores.fecha}
            onChange={(e) => cambiarFecha(e.target.value)} />
        </div>

        {zonaId && (
          <section aria-labelledby="titulo-sugerida" className="space-y-3">
            <div>
              <h3 id="titulo-sugerida" className="text-sm font-semibold text-slate-900">Ruta sugerida</h3>
              <p className="text-sm text-slate-600">
                Los clientes con la re-visita vencida al {formatearDia(fecha)}, en el orden de visita.
              </p>
            </div>
            {sugerida.cargando && (
              <p role="status" className="flex items-center gap-2 text-sm text-slate-600">
                <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />
                Armando la ruta sugerida…
              </p>
            )}
            {sugerida.error && <Alert type="error">{sugerida.error}</Alert>}
            {!sugerida.cargando && sugerida.vacia && paradas.length === 0 && (
              <Alert type="info">
                Ningún cliente de {zona?.nombre} tiene la re-visita vencida al {formatearDia(fecha)}. Agrégalos a mano.
              </Alert>
            )}
            {errores.clientes && <Alert type="error">{errores.clientes}</Alert>}
            {paradas.length > 0 && (
              <ol aria-label="Clientes de la ruta, en el orden de visita" className="divide-y divide-slate-200 rounded-lg border border-slate-200">
                {paradas.map((p, i) => (
                  <li key={p.id} className="flex items-center gap-3 px-3 py-2">
                    <Pastilla orden={i + 1} resultado={PENDIENTE} ancho />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-slate-900">{p.nombreCompleto}</span>
                      <span className="block truncate text-xs text-slate-600">{detalleDeLaParada(p)} · {p.direccionTexto}</span>
                    </span>
                    <span className="flex shrink-0 items-center gap-0.5">
                      <Btn variant="ghost" onClick={() => mover(i, i - 1)} disabled={i === 0} aria-label={`Subir a ${p.nombreCompleto}`}>
                        <ArrowUp size={14} aria-hidden="true" />
                      </Btn>
                      <Btn variant="ghost" onClick={() => mover(i, i + 1)} disabled={i === paradas.length - 1}
                        aria-label={`Bajar a ${p.nombreCompleto}`}>
                        <ArrowDown size={14} aria-hidden="true" />
                      </Btn>
                      <Btn variant="ghost" onClick={() => setParadas((lista) => lista.filter((x) => x.id !== p.id))}
                        aria-label={`Quitar a ${p.nombreCompleto}`}>
                        <X size={14} aria-hidden="true" />
                      </Btn>
                    </span>
                  </li>
                ))}
              </ol>
            )}
            {!sugerida.cargando && !sugerida.error && (
              <div className="sm:w-80">
                <Combobox label="Agregar cliente" value="" placeholder="Busca un cliente de la zona" opciones={paraAgregar}
                  placeholderBusqueda="Nombre o código…" onChange={agregar} />
              </div>
            )}
            <div className="overflow-hidden rounded-lg border border-borde-control">
              <MapaDeRuta
                zona={zona}
                paradas={paradas.map((p, i) => ({
                  clave: p.id, orden: i + 1, resultado: PENDIENTE, latitud: p.latitud, longitud: p.longitud,
                  nombre: p.nombreCompleto, detalle: detalleDeLaParada(p),
                }))}
                encuadre={`${zonaId}|${paradas.length > 0}`}
                etiqueta={`Mapa de la ruta sugerida en ${zona?.nombre ?? 'la zona'}`}
                className="h-64"
              />
            </div>
          </section>
        )}
      </div>
    </Modal>
  );
}
