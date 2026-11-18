import { Fragment, useEffect, useMemo, useState } from 'react';
import { ChevronDown, ChevronUp, Wallet } from 'lucide-react';
import api from '../../api/axios';
import { mensajeDeError } from '../../api/errores';
import { useAuth } from '../../context/auth';
import { PERMISOS } from '../../context/permisos';
import { LIMITE_DE_CLIENTES, useOpcionesDeClientes } from '../../hooks/useOpcionesDeClientes';
import { useExportar, useReporte } from '../../hooks/useReporte';
import { formatearFecha, formatearMonto } from '../../utils/formato';
import EstadoCobranza from '../cobranzas/EstadoCobranza';
import { ESTADOS_DE_COBRANZA, textoDeMora } from '../cobranzas/estados';
import { Table, Td, Tr } from '../Table';
import { Alert, BarraDeFiltros, Btn, CampoFecha, Combobox, EstadoVacio, Paginacion, Selector } from '../ui';
import { avisar } from '../ui/avisar';
import BotonExportar from './BotonExportar';
import { atajosDePeriodo } from '../../utils/fechas';

const TAMANO_PAGINA = 25;
const SIN_FILTROS = { estado: '', promotor: '', cliente: '', desde: '', hasta: '' };
// Las incobrables y las anuladas ya no se cobran: tienen su fila, pero la fila Total no las suma.
const FUERA_DEL_TOTAL = [4, 5];
// La opción para volver a todos los clientes: el combobox no acepta '' como valor de una opción.
const TODOS = '__todos';
// "Cada cobranza" es el 8.6.4; "Por cliente" (tarea 30), lo que debe cada cliente con sus cobranzas.
const POR_COBRANZA = 'cobranza';
const POR_CLIENTE = 'cliente';
const VISTAS = [{ valor: POR_COBRANZA, texto: 'Cada cobranza' }, { valor: POR_CLIENTE, texto: 'Por cliente' }];

const cobranzas = (n) => (n === 1 ? '1 cobranza' : `${n} cobranzas`);
const clientes = (n) => (n === 1 ? '1 cliente' : `${n} clientes`);

/** Las cobranzas de un cliente, al abrirlo en "Por cliente": cada deuda con su emisión, su vencimiento y su saldo. */
function CobranzasDelCliente({ deuda }) {
  return (
    <table className="w-full text-sm">
      <caption className="sr-only">Las cobranzas de {deuda.cliente.nombreCompleto}</caption>
      <thead>
        <tr className="text-left text-xs font-medium uppercase text-slate-600">
          <th scope="col" className="py-1.5 pr-3 font-medium">Emitida / Vence</th>
          <th scope="col" className="py-1.5 pr-3 text-right font-medium">Total</th>
          <th scope="col" className="py-1.5 pr-3 text-right font-medium">Pagado</th>
          <th scope="col" className="py-1.5 pr-3 text-right font-medium">Saldo</th>
          <th scope="col" className="py-1.5 font-medium">Estado</th>
        </tr>
      </thead>
      <tbody className="divide-y divide-slate-200">
        {deuda.cobranzas.map((c) => (
          <tr key={c.id}>
            <td className="py-1.5 pr-3 whitespace-nowrap tabular-nums">
              {formatearFecha(c.fechaEmision)} → {formatearFecha(c.fechaVencimiento)}
              {c.diasDeMora != null && <span className="block text-xs font-medium text-red-700">{textoDeMora(c.diasDeMora)}</span>}
            </td>
            <td className="py-1.5 pr-3 text-right tabular-nums whitespace-nowrap">{formatearMonto(c.montoTotal)}</td>
            <td className="py-1.5 pr-3 text-right tabular-nums whitespace-nowrap">{formatearMonto(c.montoPagado)}</td>
            <td className={`py-1.5 pr-3 text-right tabular-nums whitespace-nowrap ${c.saldoPendiente > 0 ? 'font-semibold' : 'text-slate-600'}`}>
              {formatearMonto(c.saldoPendiente)}
            </td>
            <td className="py-1.5"><EstadoCobranza estado={c.estado} /></td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

/**
 * Reporte de cobranzas (3.3, 7.3.12), con el diseño 8.6.4: los totales por estado arriba, en una tabla con la fila
 * Total, y debajo los filtros y las cobranzas, de la que vence primero a la última. "Ver: Por cliente" (tarea 30) agrupa
 * por cliente, de quien más debe a quien menos, y cada cliente se abre para ver sus cobranzas.
 */
export default function ReporteCobranzas({ barra, panel }) {
  const { tiene } = useAuth();
  const veCarteraCompleta = tiene(PERMISOS.seguridad.verCarteraCompleta);
  const [vista, setVista] = useState(POR_COBRANZA);
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [abiertos, setAbiertos] = useState(() => new Set());
  const [promotores, setPromotores] = useState([]);
  const opcionesDeClientes = useOpcionesDeClientes({ incluirInactivos: true });

  useEffect(() => {
    if (!veCarteraCompleta) return;
    api.get('/usuarios/promotores')
      .then(({ data }) => setPromotores(data))
      .catch((err) => avisar.error(mensajeDeError(err, 'No se pudo cargar la lista de promotores.')));
  }, [veCarteraCompleta]);

  const filtrosDeLaApi = useMemo(() => ({
    estado: filtros.estado || undefined,
    promotorId: filtros.promotor || undefined,
    clienteId: filtros.cliente || undefined,
    desde: filtros.desde || undefined,
    hasta: filtros.hasta || undefined,
  }), [filtros]);
  const parametros = useMemo(() => ({ ...filtrosDeLaApi, pagina: numeroPagina, tamano: TAMANO_PAGINA }), [filtrosDeLaApi, numeroPagina]);
  const porCliente = vista === POR_CLIENTE;
  const { datos: reporte, cargando, recargando, erroresFiltro, estado: estadoDeCarga } = useReporte(
    porCliente ? '/cobranzas/reporte/por-cliente' : '/cobranzas/reporte', parametros, 'No se pudo generar el reporte de cobranzas.');
  // El Excel trae las dos: las cobranzas y la hoja "Por cliente".
  const { exportar, exportando } = useExportar('/cobranzas/reporte/excel', filtrosDeLaApi, 'cobranzas');

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };
  const cambiarVista = (nueva) => { setVista(nueva); setNumeroPagina(1); setAbiertos(new Set()); };
  const alternar = (clienteId) => setAbiertos((antes) => {
    const despues = new Set(antes);
    if (despues.has(clienteId)) despues.delete(clienteId);
    else despues.add(clienteId);
    return despues;
  });
  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  // Al cambiar de vista, hasta que llega la respuesta nueva, lo que hay es de la otra: se trata como si cargara.
  const pagina = porCliente ? reporte?.clientes : reporte?.cobranzas;
  const deOtraVista = Boolean(reporte) && !pagina;
  const sinCobranzas = pagina?.total === 0;
  const conFueraDelTotal = reporte?.porEstado.some((e) => FUERA_DEL_TOTAL.includes(e.estado));

  let estado = estadoDeCarga;
  if (!estado && sinCobranzas && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay cobranzas" icono={Wallet}>Las ventas a crédito confirmadas aparecen en este reporte.</EstadoVacio>
  );

  const filtrosDeLaBarra = (
    <BarraDeFiltros resultado={pagina ? (porCliente ? clientes(pagina.total) : cobranzas(pagina.total)) : ''} hayFiltros={hayFiltros}
      onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}>
      <div className="w-full sm:w-36">
        <Selector label="Ver" value={vista} opciones={VISTAS} onChange={cambiarVista} />
      </div>
      <div className="w-full sm:w-32">
        <Selector label="Estado" todos="Todos" value={filtros.estado}
          opciones={Object.entries(ESTADOS_DE_COBRANZA).map(([valor, e]) => ({ valor, texto: e.texto }))}
          onChange={(estadoElegido) => filtrar({ estado: estadoElegido })} />
      </div>
      {veCarteraCompleta && (
        <div className="w-full sm:w-36">
          <Selector label="Promotor" todos="Todos" value={filtros.promotor}
            opciones={promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }))}
            onChange={(promotor) => filtrar({ promotor })} />
        </div>
      )}
      <div className="w-full sm:w-36">
        <Combobox label="Cliente" placeholder="Todos" placeholderBusqueda="Nombre, código o celular…" value={filtros.cliente}
          opciones={[{ valor: TODOS, texto: 'Todos' }, ...opcionesDeClientes.opciones]} onBuscar={opcionesDeClientes.buscar}
          cargando={opcionesDeClientes.cargando} errorBusqueda={opcionesDeClientes.error} limite={LIMITE_DE_CLIENTES}
          onChange={(cliente) => filtrar({ cliente: cliente === TODOS ? '' : cliente })} />
      </div>
      <div className="w-full sm:w-36">
        <CampoFecha label="Emitida desde" value={filtros.desde} error={erroresFiltro.desde}
          onChange={(e) => filtrar({ desde: e.target.value })}
          atajos={atajosDePeriodo()} onAtajo={filtrar} />
      </div>
      <div className="w-full sm:w-36">
        <CampoFecha label="Hasta" value={filtros.hasta} error={erroresFiltro.hasta}
          onChange={(e) => filtrar({ hasta: e.target.value })} />
      </div>
    </BarraDeFiltros>
  );
  const mostrarBarra = cargando || deOtraVista || hayFiltros || pagina?.total > 0;
  const pie = pagina && (
    <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina} deshabilitada={recargando} />
  );

  return (
    <>
      {barra(<BotonExportar onExportar={exportar} exportando={exportando} disabled={!reporte || sinCobranzas} />)}
      <div {...panel}>
        {sinCobranzas && hayFiltros && (
          <Alert type="info" className="mb-4">Ninguna cobranza coincide con los filtros: no hay nada que exportar.</Alert>
        )}

        {cargando && (
          <div className="mb-5">
            <Table cargando compacta titulo="Totales por estado"
              headers={['Totales por estado', { texto: 'Cobranzas', derecha: true }, { texto: 'Emitido', derecha: true },
                { texto: 'Cobrado', derecha: true }, { texto: 'Saldo', derecha: true }]} />
          </div>
        )}
        {reporte?.porEstado.length > 0 && (
          <div className="mb-5">
            <Table
              compacta
              titulo="Totales por estado"
              headers={['Totales por estado', { texto: 'Cobranzas', derecha: true }, { texto: 'Emitido', derecha: true },
                { texto: 'Cobrado', derecha: true }, { texto: 'Saldo', derecha: true }]}
            >
              {reporte.porEstado.map((e) => (
                <Tr key={e.estado}>
                  <Td><EstadoCobranza estado={e.estado} /></Td>
                  <Td className="text-right tabular-nums">{e.cantidad}</Td>
                  <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(e.emitido)}</Td>
                  <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(e.cobrado)}</Td>
                  <Td className={`text-right tabular-nums whitespace-nowrap ${e.saldo > 0 ? 'font-semibold' : 'text-slate-600'}`}>
                    {formatearMonto(e.saldo)}
                  </Td>
                </Tr>
              ))}
              <tr className="font-semibold">
                <Td>Total</Td>
                <Td className="text-right tabular-nums">{reporte.total.cantidad}</Td>
                <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(reporte.total.emitido)}</Td>
                <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(reporte.total.cobrado)}</Td>
                <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(reporte.total.saldo)}</Td>
              </tr>
            </Table>
            {conFueraDelTotal && (
              <p className="mt-2 text-sm text-slate-600">La fila Total no suma las cobranzas incobrables ni las anuladas.</p>
            )}
          </div>
        )}

        {porCliente ? (
          <Table
            cargando={(cargando || deOtraVista) && 'Generando el reporte…'} recargando={recargando}
            compacta
            titulo="Lo que debe cada cliente"
            headers={[{ texto: 'Cliente', segundo: 'Promotor' }, { texto: 'Cobranzas', derecha: true }, { texto: 'Emitido', derecha: true },
              { texto: 'Cobrado', derecha: true }, { texto: 'Saldo', derecha: true }, 'Vencido', { texto: 'Sus cobranzas', oculto: true }]}
            empty="Ningún cliente tiene cobranzas con estos filtros."
            estado={estado}
            barra={mostrarBarra && filtrosDeLaBarra}
            pie={pie}
          >
            {(pagina?.items ?? []).map((d) => {
              const abierto = abiertos.has(d.cliente.id);
              const detalleId = `cobranzas-del-cliente-${d.cliente.id}`;
              return (
                <Fragment key={d.cliente.id}>
                  <Tr>
                    <Td>
                      <span className="block font-medium whitespace-nowrap">{d.cliente.nombreCompleto}</span>
                      <span className="block text-slate-600 whitespace-nowrap">{d.promotor?.nombreCompleto ?? 'Sin promotor'}</span>
                    </Td>
                    <Td className="text-right tabular-nums">{d.cantidad}</Td>
                    <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(d.emitido)}</Td>
                    <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(d.cobrado)}</Td>
                    <Td className={`text-right tabular-nums whitespace-nowrap ${d.saldo > 0 ? 'font-semibold' : 'text-slate-600'}`}>
                      {formatearMonto(d.saldo)}
                    </Td>
                    <Td className="whitespace-nowrap tabular-nums">
                      {d.vencido > 0 ? (
                        <>
                          <span className="block">{formatearMonto(d.vencido)}</span>
                          {d.diasDeMora != null && <span className="block text-xs font-medium text-red-700">{textoDeMora(d.diasDeMora)}</span>}
                        </>
                      ) : <span className="text-slate-600">—</span>}
                    </Td>
                    <Td className="text-right">
                      <Btn variant="ghost" onClick={() => alternar(d.cliente.id)} aria-expanded={abierto} aria-controls={detalleId}>
                        {abierto ? <ChevronUp size={14} aria-hidden="true" /> : <ChevronDown size={14} aria-hidden="true" />}
                        <span className="whitespace-nowrap">
                          {abierto ? 'Ocultar' : (d.cantidad === 1 ? 'Ver su cobranza' : `Ver sus ${d.cantidad} cobranzas`)}
                        </span>
                        <span className="sr-only"> de {d.cliente.nombreCompleto}</span>
                      </Btn>
                    </Td>
                  </Tr>
                  <tr id={detalleId} hidden={!abierto}>
                    <td colSpan={7} className="bg-slate-50 px-3 py-2">
                      <CobranzasDelCliente deuda={d} />
                    </td>
                  </tr>
                </Fragment>
              );
            })}
          </Table>
        ) : (
          <Table
            cargando={(cargando || deOtraVista) && 'Generando el reporte…'} recargando={recargando}
            compacta
            headers={[{ texto: 'Cliente', segundo: 'Promotor' }, { texto: 'Emitida', segundo: 'Vence' }, { texto: 'Total', derecha: true },
              { texto: 'Pagado', derecha: true }, { texto: 'Saldo', derecha: true }, 'Estado']}
            empty="Ninguna cobranza coincide con los filtros."
            estado={estado}
            barra={mostrarBarra && filtrosDeLaBarra}
            pie={pie}
          >
            {(pagina?.items ?? []).map((c) => (
              <Tr key={c.id}>
                <Td>
                  <span className="block font-medium whitespace-nowrap">{c.cliente.nombreCompleto}</span>
                  <span className="block text-slate-600 whitespace-nowrap">{c.promotor.nombreCompleto}</span>
                </Td>
                <Td className="whitespace-nowrap tabular-nums">
                  <span className="block">{formatearFecha(c.fechaEmision)}</span>
                  <span className="block">{formatearFecha(c.fechaVencimiento)}</span>
                  {c.diasDeMora != null && <span className="block text-xs font-medium text-red-700">{textoDeMora(c.diasDeMora)}</span>}
                </Td>
                <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(c.montoTotal)}</Td>
                <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(c.montoPagado)}</Td>
                <Td className={`text-right tabular-nums whitespace-nowrap ${c.saldoPendiente > 0 ? 'font-semibold' : 'text-slate-600'}`}>
                  {formatearMonto(c.saldoPendiente)}
                </Td>
                <Td><EstadoCobranza estado={c.estado} /></Td>
              </Tr>
            ))}
          </Table>
        )}
        {porCliente && conFueraDelTotal && pagina?.total > 0 && (
          <p className="mt-2 text-sm text-slate-600">El total de cada cliente tampoco suma sus cobranzas incobrables ni anuladas.</p>
        )}
      </div>
    </>
  );
}
