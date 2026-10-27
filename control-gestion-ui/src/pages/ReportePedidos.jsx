import { useCallback, useEffect, useState } from 'react';
import { CalendarX, Download, LoaderCircle, Package } from 'lucide-react';
import api from '../api/axios';
import { descargar } from '../api/descargas';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import EstadoPedido from '../components/pedidos/EstadoPedido';
import {
  Alert, BarraDeFiltros, Btn, CampoFecha, Cargando, ErrorDeCarga, EstadoVacio, Indicador, PageHeader, Paginacion, Selector,
} from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { diaEnBolivia, formatearFecha, formatearMonto } from '../utils/formato';
import { useFiltrosEnLaUrl } from '../hooks/useFiltrosEnLaUrl';
import { atajosDePeriodo } from '../utils/fechas';

const TAMANO_PAGINA = 25;
const SIN_FILTROS = { estado: '', zona: '', promotor: '', desde: '', hasta: '' };

/**
 * Reporte de pedidos (1.4): los pedidos con los filtros de estado, zona, promotor y días de entrega, los totales por
 * estado y el Excel con los mismos filtros. El total general no suma los anulados.
 */
export default function ReportePedidos() {
  const [reporte, setReporte] = useState(null);
  const { filtros, setFiltros, numeroPagina, setNumeroPagina } = useFiltrosEnLaUrl(SIN_FILTROS);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [erroresFiltro, setErroresFiltro] = useState({});
  const [exportando, setExportando] = useState(false);
  const [estados, setEstados] = useState([]);
  const [zonas, setZonas] = useState([]);
  const [promotores, setPromotores] = useState([]);

  useEffect(() => {
    Promise.all([api.get('/pedidos/estados'), api.get('/zonas'), api.get('/usuarios/promotores')])
      .then(([e, z, p]) => { setEstados(e.data); setZonas(z.data); setPromotores(p.data); })
      .catch((err) => avisar.error(mensajeDeError(err, 'No se pudieron cargar los filtros.')));
  }, []);

  const parametros = useCallback((pagina) => ({
    pagina,
    tamano: TAMANO_PAGINA,
    estadoId: filtros.estado || undefined,
    zonaId: filtros.zona || undefined,
    promotorId: filtros.promotor || undefined,
    desde: filtros.desde || undefined,
    hasta: filtros.hasta || undefined,
  }), [filtros]);

  // Si la API rechaza los filtros, el reporte anterior no queda a la vista: ya no corresponde a lo que se pidió.
  const cargar = useCallback(() => api.get('/pedidos/reporte', { params: parametros(numeroPagina) })
    .then(({ data }) => { setReporte(data); setErrorCarga(''); setErroresFiltro({}); })
    .catch((err) => {
      setReporte(null);
      setErroresFiltro(erroresPorCampo(err));
      setErrorCarga(mensajeDeError(err, 'No se pudo generar el reporte de pedidos.'));
    })
    .finally(() => { setCargando(false); setReintentando(false); }),
  [parametros, numeroPagina]);

  useEffect(() => { cargar(); }, [cargar]);

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };

  const exportar = async () => {
    setExportando(true);
    avisar.quitarError();
    try {
      await descargar('/pedidos/reporte/excel', parametros(undefined), `pedidos_${diaEnBolivia().replaceAll('-', '')}.xlsx`);
    } catch (err) {
      avisar.error(mensajeDeError(err, 'No se pudo exportar el reporte a Excel.'));
    } finally {
      setExportando(false);
    }
  };

  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const pagina = reporte?.pedidos;
  // Los anulados también cuentan: son filas del Excel.
  const sinPedidos = pagina?.total === 0;

  let estado = null;
  if (cargando) estado = <Cargando texto="Generando el reporte…" />;
  else if (Object.keys(erroresFiltro).length > 0) estado = (
    <EstadoVacio titulo="Revisa los filtros" icono={CalendarX}>El error está marcado en su campo.</EstadoVacio>
  );
  else if (errorCarga) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (sinPedidos && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay pedidos" icono={Package}>Los pedidos que se registren aparecen en este reporte.</EstadoVacio>
  );

  return (
    <Layout>
      <PageHeader
        title="Reporte de pedidos"
        subtitle="Los pedidos por estado, zona, promotor y fecha de entrega. El total no suma los anulados."
        action={(
          <Btn variant="outline" size="md" onClick={exportar} disabled={exportando || !reporte || sinPedidos}>
            {exportando ? <LoaderCircle size={16} className="animate-spin" aria-hidden="true" /> : <Download size={16} aria-hidden="true" />}
            {exportando ? 'Exportando…' : 'Exportar Excel'}
          </Btn>
        )}
      />

      {sinPedidos && hayFiltros && (
        <Alert type="info" className="mb-4">Ningún pedido coincide con los filtros: no hay nada que exportar.</Alert>
      )}

      {reporte && (
        <section aria-label="Totales por estado" className="mb-5 grid gap-4 sm:grid-cols-3">
          <Indicador titulo="Total sin anulados" valor={formatearMonto(reporte.monto)}
            detalle={reporte.cantidad === 1 ? '1 pedido' : `${reporte.cantidad} pedidos`} />
          {reporte.porEstado.map((e) => (
            <Indicador key={e.estadoId} titulo={e.estado} valor={formatearMonto(e.monto)}
              detalle={e.cantidad === 1 ? '1 pedido' : `${e.cantidad} pedidos`} />
          ))}
        </section>
      )}

      <Table
        headers={['Número', 'Cliente', 'Zona', 'Promotor', 'Entrega', 'Asignado a', { texto: 'Total', derecha: true }, 'Estado']}
        empty="Ningún pedido coincide con los filtros."
        estado={estado}
        barra={!cargando && (hayFiltros || pagina?.total > 0) && (
          <BarraDeFiltros
            resultado={pagina ? `${pagina.total} ${pagina.total === 1 ? 'pedido' : 'pedidos'}` : ''}
            hayFiltros={hayFiltros}
            onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}
          >
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
            <div className="w-full sm:w-48">
              <Selector label="Promotor" todos="Todos" value={filtros.promotor}
                opciones={promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }))}
                onChange={(promotor) => filtrar({ promotor })} />
            </div>
            <div className="w-full sm:w-40">
              <CampoFecha label="Entrega desde" value={filtros.desde} error={erroresFiltro.desde}
                onChange={(e) => filtrar({ desde: e.target.value })}
                atajos={atajosDePeriodo()} onAtajo={filtrar} />
            </div>
            <div className="w-full sm:w-40">
              <CampoFecha label="Entrega hasta" value={filtros.hasta} error={erroresFiltro.hasta}
                onChange={(e) => filtrar({ hasta: e.target.value })} />
            </div>
          </BarraDeFiltros>
        )}
        pie={pagina && <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina} />}
      >
        {(pagina?.items ?? []).map((p) => (
          <Tr key={p.id}>
            <Td mono>{p.numero}</Td>
            <Td><span className="font-medium">{p.cliente.nombreCompleto}</span></Td>
            <Td>{p.zona.nombre}</Td>
            <Td>{p.promotor?.nombreCompleto ?? '—'}</Td>
            <Td className="whitespace-nowrap tabular-nums">{formatearFecha(p.fechaEntregaEstimada, { hora: true })}</Td>
            <Td>{p.repartidor?.nombreCompleto ?? <span className="text-slate-600">Sin asignar</span>}</Td>
            <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(p.total)}</Td>
            <Td><EstadoPedido estado={p.estado} /></Td>
          </Tr>
        ))}
      </Table>
    </Layout>
  );
}
