import { useCallback, useEffect, useState } from 'react';
import { CircleCheck, Clock, Download, LoaderCircle, TriangleAlert } from 'lucide-react';
import api from '../api/axios';
import { descargar } from '../api/descargas';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import EstadoEvento from '../components/eventos/EstadoEvento';
import { ESTADOS, PENDIENTE, RESUELTO } from '../components/eventos/estados';
import {
  Alert, BarraDeFiltros, Btn, Cargando, ErrorDeCarga, EstadoVacio, Indicador, Input, PageHeader, Paginacion, Selector,
} from '../components/ui';
import { diaEnBolivia, formatearFecha } from '../utils/formato';

const TAMANO_PAGINA = 25;
const SIN_FILTROS = { tipo: '', estado: '', zona: '', promotor: '', desde: '', hasta: '' };

const cuantos = (n) => (n === 1 ? '1 evento' : `${n} eventos`);

/**
 * Reporte de incidencias (4.4): los eventos con los filtros de tipo, estado, promotor, zona y fechas, cuántos hay de
 * cada tipo y en cada estado, y el Excel con los mismos filtros. Sin eventos no se exporta.
 */
export default function ReporteEventos() {
  const [reporte, setReporte] = useState(null);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [errorAccion, setErrorAccion] = useState('');
  const [exportando, setExportando] = useState(false);
  const [tipos, setTipos] = useState([]);
  const [zonas, setZonas] = useState([]);
  const [promotores, setPromotores] = useState([]);

  useEffect(() => {
    Promise.all([api.get('/eventos/tipos'), api.get('/zonas'), api.get('/usuarios/promotores')])
      .then(([t, z, p]) => { setTipos(t.data); setZonas(z.data); setPromotores(p.data); })
      .catch((err) => setErrorAccion(mensajeDeError(err, 'No se pudieron cargar los filtros.')));
  }, []);

  const parametros = useCallback((pagina) => ({
    pagina,
    tamano: TAMANO_PAGINA,
    tipoEventoId: filtros.tipo || undefined,
    estado: filtros.estado || undefined,
    zonaId: filtros.zona || undefined,
    promotorId: filtros.promotor || undefined,
    desde: filtros.desde || undefined,
    hasta: filtros.hasta || undefined,
  }), [filtros]);

  const cargar = useCallback(() => api.get('/eventos/reporte', { params: parametros(numeroPagina) })
    .then(({ data }) => { setReporte(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo generar el reporte de incidencias.')))
    .finally(() => { setCargando(false); setReintentando(false); }),
  [parametros, numeroPagina]);

  useEffect(() => { cargar(); }, [cargar]);

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };

  const exportar = async () => {
    setExportando(true);
    setErrorAccion('');
    try {
      await descargar('/eventos/reporte/excel', parametros(undefined), `incidencias_${diaEnBolivia().replaceAll('-', '')}.xlsx`);
    } catch (err) {
      setErrorAccion(mensajeDeError(err, 'No se pudo exportar el reporte a Excel.'));
    } finally {
      setExportando(false);
    }
  };

  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const pagina = reporte?.eventos;
  const sinEventos = reporte?.cantidad === 0;

  let estado = null;
  if (cargando) estado = <Cargando texto="Generando el reporte…" />;
  else if (errorCarga && !reporte) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (sinEventos && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay eventos" icono={TriangleAlert}>Las incidencias que se registren aparecen en este reporte.</EstadoVacio>
  );

  const porEstado = (id) => reporte?.porEstado.find((e) => e.estado === id)?.cantidad ?? 0;

  return (
    <Layout>
      <PageHeader
        title="Reporte de incidencias"
        subtitle="Los eventos por tipo, estado, promotor, zona y fecha, con sus totales."
        action={(
          <Btn variant="outline" size="md" onClick={exportar} disabled={exportando || !reporte || sinEventos}>
            {exportando ? <LoaderCircle size={16} className="animate-spin" aria-hidden="true" /> : <Download size={16} aria-hidden="true" />}
            {exportando ? 'Exportando…' : 'Exportar Excel'}
          </Btn>
        )}
      />

      {errorAccion && <Alert type="error" className="mb-4">{errorAccion}</Alert>}
      {errorCarga && reporte && <Alert type="error" className="mb-4">{errorCarga}</Alert>}
      {sinEventos && hayFiltros && (
        <Alert type="info" className="mb-4">Ningún evento coincide con los filtros: no hay nada que exportar.</Alert>
      )}

      {reporte && (
        <div className="mb-5 grid gap-4 lg:grid-cols-[2fr_1fr]">
          <section aria-label="Totales por estado" className="grid content-start gap-4 sm:grid-cols-3">
            <Indicador titulo="Eventos" icono={TriangleAlert} valor={reporte.cantidad} detalle="Con los filtros elegidos." />
            <Indicador titulo="Pendientes" icono={Clock} valor={porEstado(PENDIENTE)} detalle="Todavía sin resolver." />
            <Indicador titulo="Resueltos" icono={CircleCheck} valor={porEstado(RESUELTO)} detalle="Con su solución." />
          </section>
          <section aria-labelledby="titulo-por-tipo">
            <h3 id="titulo-por-tipo" className="sr-only">Totales por tipo</h3>
            <Table headers={['Tipo', { texto: 'Eventos', derecha: true }]}>
              {reporte.porTipo.map((t) => (
                <Tr key={t.tipoEventoId}>
                  <Td>{t.tipo}</Td>
                  <Td className="text-right tabular-nums">{t.cantidad}</Td>
                </Tr>
              ))}
            </Table>
          </section>
        </div>
      )}

      <Table
        headers={['Número', 'Fecha', 'Tipo', 'Cliente', 'Zona', 'Registró', { texto: 'Fotos', derecha: true }, 'Estado']}
        empty="Ningún evento coincide con los filtros."
        estado={estado}
        barra={!cargando && reporte && (pagina.total > 0 || hayFiltros) && (
          <BarraDeFiltros resultado={cuantos(pagina.total)} hayFiltros={hayFiltros}
            onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}>
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
            <div className="w-full sm:w-48">
              <Selector label="Registró" todos="Todos" value={filtros.promotor}
                opciones={promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }))}
                onChange={(promotor) => filtrar({ promotor })} />
            </div>
            <div className="w-full sm:w-40">
              <Input label="Desde" type="date" autoComplete="off" value={filtros.desde} onChange={(e) => filtrar({ desde: e.target.value })} />
            </div>
            <div className="w-full sm:w-40">
              <Input label="Hasta" type="date" autoComplete="off" value={filtros.hasta} onChange={(e) => filtrar({ hasta: e.target.value })} />
            </div>
          </BarraDeFiltros>
        )}
        pie={pagina && <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina} />}
      >
        {(pagina?.items ?? []).map((e) => (
          <Tr key={e.id}>
            <Td mono>{e.numero}</Td>
            <Td className="whitespace-nowrap tabular-nums">{formatearFecha(e.fechaEvento, { hora: true })}</Td>
            <Td>{e.tipo.nombre}</Td>
            <Td>
              <p className="font-medium">{e.cliente?.nombreCompleto ?? '—'}</p>
              {e.pedido && <p className="text-xs text-slate-600 font-mono" translate="no">{e.pedido.numero}</p>}
            </Td>
            <Td>{e.zona?.nombre ?? '—'}</Td>
            <Td>{e.promotor.nombreCompleto}</Td>
            <Td className="text-right tabular-nums">{e.cantidadFotos || '—'}</Td>
            <Td><EstadoEvento estado={e.estado} /></Td>
          </Tr>
        ))}
      </Table>
    </Layout>
  );
}
