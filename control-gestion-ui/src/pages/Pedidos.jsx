import { useCallback, useEffect, useState } from 'react';
import { ArrowRight, Ban, CalendarClock, Eye, Package, Pencil, Plus, UserCheck } from 'lucide-react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import AsignarPedidos from '../components/pedidos/AsignarPedidos';
import AvanzarPedidos from '../components/pedidos/AvanzarPedidos';
import CambioDeEstado from '../components/pedidos/CambioDeEstado';
import DetallePedido from '../components/pedidos/DetallePedido';
import EstadoPedido from '../components/pedidos/EstadoPedido';
import FormularioPedido from '../components/pedidos/FormularioPedido';
import { esFinal, esModificable, SIGUIENTE } from '../components/pedidos/estados';
import {
  Alert, BarraDeFiltros, Btn, Buscador, CampoFecha, Casilla, ErrorDeCarga, EstadoVacio, PageHeader,
  Paginacion, Selector,
} from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { useAuth } from '../context/auth';
import { ELIGEN_PROMOTOR, PERMISOS } from '../context/permisos';
import { useDemorado } from '../hooks/useDemorado';
import { useFiltrosEnLaUrl } from '../hooks/useFiltrosEnLaUrl';
import { useRecarga } from '../hooks/useRecarga';
import { usePeticionVigente } from '../hooks/usePeticionVigente';
import { formatearFecha, formatearMonto } from '../utils/formato';
import { atajosDePeriodo } from '../utils/fechas';

const { pedidos: permisos, seguridad } = PERMISOS;
const TAMANO_PAGINA = 25;
const SIN_FILTROS = { texto: '', estado: '', zona: '', promotor: '', asignado: '', desde: '', hasta: '' };
// Valor del filtro "Asignado a" para los pedidos que todavía no tienen promotor asignado.
const SIN_ASIGNAR = 'sin-asignar';

export default function Pedidos() {
  const { tiene } = useAuth();
  const puedeRegistrar = tiene(permisos.registrarPedido);
  const puedeModificar = tiene(permisos.modificarPedido);
  const puedeAvanzar = tiene(permisos.cambiarEstado);
  const puedeReprogramar = tiene(permisos.reprogramarEntrega);
  const puedeAnular = tiene(permisos.anularPedido);
  const puedeAsignar = tiene(permisos.asignarPedidos);
  const puedeElegir = puedeAsignar || puedeAvanzar;
  const veCarteraCompleta = tiene(seguridad.verCarteraCompleta);
  const eligePromotor = tiene(...ELIGEN_PROMOTOR);

  const [pagina, setPagina] = useState(null);
  const { filtros, setFiltros, numeroPagina, setNumeroPagina } = useFiltrosEnLaUrl(SIN_FILTROS);
  const busqueda = useDemorado(filtros.texto.trim(), 300);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');

  const [estados, setEstados] = useState([]);
  const [zonas, setZonas] = useState([]);
  const [promotores, setPromotores] = useState([]);
  const [detalle, setDetalle] = useState(null);
  // null: cerrado. { pedido: null }: nuevo. { pedido }: editar ese pedido.
  const [formulario, setFormulario] = useState(null);
  // { tipo: 'avanzar' | 'reprogramar' | 'anular', pedido }
  const [cambio, setCambio] = useState(null);
  // Los pedidos elegidos para asignar o avanzar, por id: se conservan al cambiar de página y después de cada acción,
  // así se asignan y se avanzan sin volver a elegirlos.
  const [elegidos, setElegidos] = useState({});
  const [asignando, setAsignando] = useState(false);
  const [avanzando, setAvanzando] = useState(false);

  // Cada elegido, con su estado más reciente: avanzar los manda con el estado que se ve. Uno entregado o anulado sale.
  const actualizarElegidos = useCallback((actualizados) => setElegidos((e) => {
    const presentes = actualizados.filter((p) => e[p.id]);
    if (presentes.length === 0) return e;
    const nuevos = { ...e };
    for (const p of presentes) {
      if (esFinal(p.estado.id)) delete nuevos[p.id];
      else nuevos[p.id] = p;
    }
    return nuevos;
  }), []);

  // Los catálogos de los filtros. Si fallan, la lista igual funciona: se avisa arriba.
  useEffect(() => {
    Promise.all([
      api.get('/pedidos/estados'),
      api.get('/zonas'),
      eligePromotor ? api.get('/usuarios/promotores') : Promise.resolve({ data: [] }),
    ])
      .then(([e, z, p]) => { setEstados(e.data); setZonas(z.data); setPromotores(p.data); })
      .catch((err) => avisar.error(mensajeDeError(err, 'No se pudieron cargar los filtros.')));
  }, [eligePromotor]);

  const nuevaPeticion = usePeticionVigente();
  const cargar = useCallback(() => {
    const siVigente = nuevaPeticion();
    return api.get('/pedidos', {
      params: {
        pagina: numeroPagina,
        tamano: TAMANO_PAGINA,
        buscar: busqueda || undefined,
        estadoId: filtros.estado || undefined,
        zonaId: filtros.zona || undefined,
        promotorId: filtros.promotor || undefined,
        repartidorId: filtros.asignado && filtros.asignado !== SIN_ASIGNAR ? filtros.asignado : undefined,
        sinAsignar: filtros.asignado === SIN_ASIGNAR || undefined,
        desde: filtros.desde || undefined,
        hasta: filtros.hasta || undefined,
      },
    })
      .then(siVigente(({ data }) => { setPagina(data); actualizarElegidos(data.items); setErrorCarga(''); }))
      .catch(siVigente((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de pedidos.'))))
      .finally(siVigente(() => { setCargando(false); setReintentando(false); }));
  }, [numeroPagina, busqueda, filtros.estado, filtros.zona, filtros.promotor, filtros.asignado, filtros.desde,
    filtros.hasta, actualizarElegidos, nuevaPeticion]);

  const { recargando, terminar } = useRecarga(cargar);
  useEffect(() => { cargar().finally(() => terminar(cargar)); }, [cargar, terminar]);

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };

  // El detalle trae las líneas y el historial: la lista solo trae el resumen. Editar también lo necesita.
  const abrirPedido = (id, alAbrir) => {
    avisar.quitarError();
    api.get(`/pedidos/${id}`)
      .then(({ data }) => alAbrir(data))
      .catch((err) => avisar.error(mensajeDeError(err, 'No se pudo abrir el pedido.')));
  };

  const abrirFormulario = (pedido = null) => {
    setDetalle(null);
    setFormulario({ pedido });
  };

  const alGuardar = (pedido, esNuevo) => {
    setFormulario(null);
    avisar.exito(esNuevo
      ? `Se registró ${pedido.numero} por ${formatearMonto(pedido.total)}, con entrega el ${formatearFecha(pedido.fechaEntregaEstimada, { hora: true })}.`
      : `${pedido.numero} quedó modificado.`);
    cargar();
  };

  const alCambiar = (mensaje) => {
    setCambio(null);
    setAsignando(false);
    setAvanzando(false);
    setDetalle(null);
    avisar.exito(mensaje);
    cargar();
  };

  const abrirCambio = (tipo, pedido) => {
    setDetalle(null);
    setCambio({ tipo, pedido });
  };

  const elegir = (pedido, elegido) => setElegidos((e) => {
    const nuevos = { ...e };
    if (elegido) nuevos[pedido.id] = pedido;
    else delete nuevos[pedido.id];
    return nuevos;
  });
  const listaDeElegidos = Object.values(elegidos);

  const puedeEditarlo = (pedido) => puedeModificar && esModificable(pedido.estado.id);
  const puedeAvanzarlo = (pedido) => puedeAvanzar && SIGUIENTE[pedido.estado.id] !== undefined;

  const abrirAvanzar = () => {
    avisar.quitarError();
    if (listaDeElegidos.some((p) => SIGUIENTE[p.estado.id]))
      setAvanzando(true);
    else
      avisar.error('Ninguno de los pedidos elegidos puede pasar a En ruta: los que ya están En ruta se entregan desde la app.');
  };

  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const pedidos = pagina?.items ?? [];

  let estado = null;
  if (errorCarga && !pagina) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (pagina?.total === 0 && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay pedidos" icono={Package}
      accion={puedeRegistrar && <Btn size="md" onClick={() => abrirFormulario()}><Plus size={16} aria-hidden="true" />Nuevo pedido</Btn>}>
      {veCarteraCompleta ? 'Los pedidos de los clientes aparecen aquí.' : 'Los pedidos de tu cartera y los que te asignen aparecen aquí.'}
    </EstadoVacio>
  );

  const opcionesDePromotor = promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }));
  const encabezados = [
    ...(puedeElegir ? [{ texto: 'Elegir', oculto: true }] : []),
    'Número', 'Cliente', ...(veCarteraCompleta ? ['Promotor'] : []), 'Entrega', 'Asignado a',
    { texto: 'Total', derecha: true }, 'Estado', '',
  ];

  return (
    <Layout>
      <PageHeader
        title="Pedidos"
        subtitle={veCarteraCompleta
          ? 'Los pedidos de todos los clientes, su estado y su entrega.'
          : 'Los pedidos de tu cartera y los que tienes asignados para entregar.'}
        action={puedeRegistrar && (
          <Btn size="md" onClick={() => abrirFormulario()}>
            <Plus size={16} aria-hidden="true" />
            Nuevo pedido
          </Btn>
        )}
      />

      {errorCarga && pagina && <Alert type="error" className="mb-4">{errorCarga}</Alert>}
      {listaDeElegidos.length > 0 && (
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-marca-200 bg-marca-50 px-4 py-3">
          <p className="text-sm font-medium text-marca-800" aria-live="polite">
            {listaDeElegidos.length === 1 ? '1 pedido elegido' : `${listaDeElegidos.length} pedidos elegidos`}
          </p>
          <div className="flex flex-wrap gap-2">
            <Btn variant="ghost" onClick={() => setElegidos({})}>Quitar la selección</Btn>
            {puedeAvanzar && (
              <Btn variant="outline" onClick={abrirAvanzar}>
                <ArrowRight size={14} aria-hidden="true" />
                Pasar a En ruta
              </Btn>
            )}
            {puedeAsignar && (
              <Btn onClick={() => setAsignando(true)}>
                <UserCheck size={14} aria-hidden="true" />
                Asignar
              </Btn>
            )}
          </div>
        </div>
      )}

      <Table
        cargando={cargando && 'Cargando pedidos…'} recargando={recargando}
        headers={encabezados}
        empty="Ningún pedido coincide con los filtros."
        estado={estado}
        barra={(cargando || (pagina && (pagina.total > 0 || hayFiltros))) && (
          <BarraDeFiltros
            resultado={pagina ? `${pagina.total} ${pagina.total === 1 ? 'pedido' : 'pedidos'}` : ''}
            hayFiltros={hayFiltros}
            onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}
          >
            <Buscador label="Buscar" placeholder="Número, cliente o código…" value={filtros.texto} onChange={(texto) => filtrar({ texto })} />
            <div className="w-full sm:w-40">
              <Selector label="Estado" todos="Todos" value={filtros.estado}
                opciones={estados.map((e) => ({ valor: String(e.id), texto: e.nombre }))}
                onChange={(valor) => filtrar({ estado: valor })} />
            </div>
            <div className="w-full sm:w-44">
              <Selector label="Zona" todos="Todas" value={filtros.zona}
                opciones={zonas.map((z) => ({ valor: String(z.id), texto: z.nombre }))}
                onChange={(zona) => filtrar({ zona })} />
            </div>
            {veCarteraCompleta && eligePromotor && (
              <div className="w-full sm:w-48">
                <Selector label="Promotor del cliente" todos="Todos" value={filtros.promotor} opciones={opcionesDePromotor}
                  onChange={(promotor) => filtrar({ promotor })} />
              </div>
            )}
            {eligePromotor && (
              <div className="w-full sm:w-48">
                <Selector label="Asignado a" todos="Todos" value={filtros.asignado}
                  opciones={[{ valor: SIN_ASIGNAR, texto: 'Sin asignar' }, ...opcionesDePromotor]}
                  onChange={(asignado) => filtrar({ asignado })} />
              </div>
            )}
            <div className="w-full sm:w-40">
              <CampoFecha label="Entrega desde" value={filtros.desde} onChange={(e) => filtrar({ desde: e.target.value })}
                atajos={atajosDePeriodo()} onAtajo={filtrar} />
            </div>
            <div className="w-full sm:w-40">
              <CampoFecha label="Entrega hasta" value={filtros.hasta} onChange={(e) => filtrar({ hasta: e.target.value })} />
            </div>
          </BarraDeFiltros>
        )}
        pie={pagina && <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina}
            deshabilitada={recargando} />}
      >
        {pedidos.map((p) => (
          <Tr key={p.id}>
            {puedeElegir && (
              <Td>
                <Casilla checked={Boolean(elegidos[p.id])} disabled={esFinal(p.estado.id)}
                  onCheckedChange={(valor) => elegir(p, valor === true)}>
                  <span className="sr-only">Elegir {p.numero}</span>
                </Casilla>
              </Td>
            )}
            <Td mono>{p.numero}</Td>
            <Td className="min-w-40">
              <span className="font-medium line-clamp-2" title={p.cliente.nombreCompleto}>{p.cliente.nombreCompleto}</span>
            </Td>
            {veCarteraCompleta && (
              <Td className="min-w-36">
                <span className="line-clamp-2" title={p.promotor?.nombreCompleto}>{p.promotor?.nombreCompleto ?? '—'}</span>
              </Td>
            )}
            <Td className="whitespace-nowrap tabular-nums">{formatearFecha(p.fechaEntregaEstimada, { hora: true })}</Td>
            <Td className="min-w-36">
              {p.repartidor
                ? <span className="line-clamp-2" title={p.repartidor.nombreCompleto}>{p.repartidor.nombreCompleto}</span>
                : <span className="text-slate-600 whitespace-nowrap">Sin asignar</span>}
            </Td>
            <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(p.total)}</Td>
            <Td className="whitespace-nowrap"><EstadoPedido estado={p.estado} /></Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                <Btn variant="ghost" onClick={() => abrirPedido(p.id, setDetalle)} aria-label={`Ver ${p.numero}`}>
                  <Eye size={14} aria-hidden="true" />
                  Ver
                </Btn>
                {puedeAvanzarlo(p) && (
                  <Btn variant="ghost" onClick={() => abrirCambio('avanzar', p)}
                    aria-label={`Pasar a ${SIGUIENTE[p.estado.id].nombre} el pedido ${p.numero}`}>
                    <ArrowRight size={14} aria-hidden="true" />
                    Pasar a {SIGUIENTE[p.estado.id].nombre}
                  </Btn>
                )}
              </div>
            </Td>
          </Tr>
        ))}
      </Table>

      <DetallePedido
        pedido={detalle}
        onCerrar={() => setDetalle(null)}
        acciones={detalle && (
          <>
            {puedeAnular && !esFinal(detalle.estado.id) && (
              <Btn variant="danger" size="md" onClick={() => abrirCambio('anular', detalle)}>
                <Ban size={16} aria-hidden="true" />Anular
              </Btn>
            )}
            {puedeReprogramar && !esFinal(detalle.estado.id) && (
              <Btn variant="outline" size="md" onClick={() => abrirCambio('reprogramar', detalle)}>
                <CalendarClock size={16} aria-hidden="true" />Reprogramar
              </Btn>
            )}
            {puedeEditarlo(detalle) && (
              <Btn variant="outline" size="md" onClick={() => abrirFormulario(detalle)}>
                <Pencil size={16} aria-hidden="true" />Editar
              </Btn>
            )}
            {puedeAvanzarlo(detalle) && (
              <Btn size="md" onClick={() => abrirCambio('avanzar', detalle)}>
                <ArrowRight size={16} aria-hidden="true" />Pasar a {SIGUIENTE[detalle.estado.id].nombre}
              </Btn>
            )}
          </>
        )}
      />

      {cambio && <CambioDeEstado cambio={cambio} onCerrar={() => setCambio(null)} onHecho={alCambiar} />}

      {asignando && (
        <AsignarPedidos
          pedidos={listaDeElegidos}
          promotores={promotores}
          onCerrar={() => setAsignando(false)}
          onHecho={(mensaje, asignados) => { actualizarElegidos(asignados); alCambiar(mensaje); }}
        />
      )}

      {avanzando && (
        <AvanzarPedidos
          pedidos={listaDeElegidos}
          onCerrar={() => setAvanzando(false)}
          onHecho={(mensaje, avanzados) => { actualizarElegidos(avanzados); alCambiar(mensaje); }}
          onCambiaron={(mensaje) => { setAvanzando(false); avisar.error(mensaje); cargar(); }}
        />
      )}

      {formulario && (
        <FormularioPedido pedido={formulario.pedido} onCerrar={() => setFormulario(null)} onGuardado={alGuardar} />
      )}
    </Layout>
  );
}
