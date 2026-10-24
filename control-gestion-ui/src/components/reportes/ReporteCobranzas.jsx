import { useEffect, useMemo, useState } from 'react';
import { Wallet } from 'lucide-react';
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
import { Alert, BarraDeFiltros, Combobox, EstadoVacio, Input, Paginacion, Selector } from '../ui';
import BotonExportar from './BotonExportar';

const TAMANO_PAGINA = 25;
const SIN_FILTROS = { estado: '', promotor: '', cliente: '', desde: '', hasta: '' };
// Las incobrables y las anuladas ya no se cobran: tienen su fila, pero la fila Total no las suma.
const FUERA_DEL_TOTAL = [4, 5];
// La opción para volver a todos los clientes: el combobox no acepta '' como valor de una opción.
const TODOS = '__todos';

const cobranzas = (n) => (n === 1 ? '1 cobranza' : `${n} cobranzas`);

/**
 * Reporte de cobranzas (3.3, 7.3.12), con el diseño 8.6.4: los totales por estado arriba, en una tabla con la fila
 * Total, y debajo los filtros y las cobranzas, de la que vence primero a la última.
 */
export default function ReporteCobranzas({ barra, panel }) {
  const { tiene } = useAuth();
  const veCarteraCompleta = tiene(PERMISOS.seguridad.verCarteraCompleta);
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [promotores, setPromotores] = useState([]);
  const [errorPromotores, setErrorPromotores] = useState('');
  const clientes = useOpcionesDeClientes({ incluirInactivos: true });

  useEffect(() => {
    if (!veCarteraCompleta) return;
    api.get('/usuarios/promotores')
      .then(({ data }) => setPromotores(data))
      .catch((err) => setErrorPromotores(mensajeDeError(err, 'No se pudo cargar la lista de promotores.')));
  }, [veCarteraCompleta]);

  const filtrosDeLaApi = useMemo(() => ({
    estado: filtros.estado || undefined,
    promotorId: filtros.promotor || undefined,
    clienteId: filtros.cliente || undefined,
    desde: filtros.desde || undefined,
    hasta: filtros.hasta || undefined,
  }), [filtros]);
  const parametros = useMemo(() => ({ ...filtrosDeLaApi, pagina: numeroPagina, tamano: TAMANO_PAGINA }), [filtrosDeLaApi, numeroPagina]);
  const { datos: reporte, cargando, erroresFiltro, estado: estadoDeCarga } =
    useReporte('/cobranzas/reporte', parametros, 'No se pudo generar el reporte de cobranzas.');
  const { exportar, exportando, errorExportar } = useExportar('/cobranzas/reporte/excel', filtrosDeLaApi, 'cobranzas');

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };
  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const pagina = reporte?.cobranzas;
  const sinCobranzas = pagina?.total === 0;

  let estado = estadoDeCarga;
  if (!estado && sinCobranzas && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay cobranzas" icono={Wallet}>Las ventas a crédito confirmadas aparecen en este reporte.</EstadoVacio>
  );

  return (
    <>
      {barra(<BotonExportar onExportar={exportar} exportando={exportando} disabled={!reporte || sinCobranzas} />)}
      <div {...panel}>
        {(errorExportar || errorPromotores) && <Alert type="error" className="mb-4">{errorExportar || errorPromotores}</Alert>}
        {sinCobranzas && hayFiltros && (
          <Alert type="info" className="mb-4">Ninguna cobranza coincide con los filtros: no hay nada que exportar.</Alert>
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
            {reporte.porEstado.some((e) => FUERA_DEL_TOTAL.includes(e.estado)) && (
              <p className="mt-2 text-sm text-slate-600">La fila Total no suma las cobranzas incobrables ni las anuladas.</p>
            )}
          </div>
        )}

        <Table
          compacta
          headers={[{ texto: 'Cliente', segundo: 'Promotor' }, { texto: 'Emitida', segundo: 'Vence' }, { texto: 'Total', derecha: true },
            { texto: 'Pagado', derecha: true }, { texto: 'Saldo', derecha: true }, 'Estado']}
          empty="Ninguna cobranza coincide con los filtros."
          estado={estado}
          barra={!cargando && (hayFiltros || pagina?.total > 0) && (
            <BarraDeFiltros resultado={pagina ? cobranzas(pagina.total) : ''} hayFiltros={hayFiltros}
              onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}>
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
                  opciones={[{ valor: TODOS, texto: 'Todos' }, ...clientes.opciones]} onBuscar={clientes.buscar}
                  cargando={clientes.cargando} errorBusqueda={clientes.error} limite={LIMITE_DE_CLIENTES}
                  onChange={(cliente) => filtrar({ cliente: cliente === TODOS ? '' : cliente })} />
              </div>
              <div className="w-full sm:w-36">
                <Input label="Emitida desde" type="date" autoComplete="off" value={filtros.desde} error={erroresFiltro.desde}
                  onChange={(e) => filtrar({ desde: e.target.value })} />
              </div>
              <div className="w-full sm:w-36">
                <Input label="Hasta" type="date" autoComplete="off" value={filtros.hasta} error={erroresFiltro.hasta}
                  onChange={(e) => filtrar({ hasta: e.target.value })} />
              </div>
            </BarraDeFiltros>
          )}
          pie={pagina && <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina} />}
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
      </div>
    </>
  );
}
