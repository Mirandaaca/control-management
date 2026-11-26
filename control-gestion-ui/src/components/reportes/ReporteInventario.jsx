import { useMemo, useState } from 'react';
import { usePaginaLocal } from '../../hooks/usePaginaLocal';
import { useExportar, useReporte } from '../../hooks/useReporte';
import { formatearCantidad, formatearFecha } from '../../utils/formato';
import { Table, Td, Tr } from '../Table';
import { BarraDeFiltros, CampoFecha, Paginacion, Selector } from '../ui';
import BotonesExportar from './BotonesExportar';
import { atajosDePeriodo } from '../../utils/fechas';
import { TAMANO_PAGINA } from '../../utils/paginas';

const SIN_FILTROS = { categoria: '', desde: '', hasta: '' };
const TIPOS = { 0: 'Entrada', 1: 'Salida', 2: 'Ajuste', 3: 'Devolución' };

// Un arreglo de filas, no un componente: así la tabla ve que está vacía y muestra su mensaje.
const filasDeProductos = (productos, conFalta = false) => productos.map((p) => (
  <Tr key={p.id}>
    <Td mono>{p.codigo}</Td>
    <Td><span className="font-medium">{p.producto}</span></Td>
    <Td>{p.categoria ?? '—'}</Td>
    <Td className="text-right tabular-nums">{formatearCantidad(p.stockActual)}</Td>
    <Td className="text-right tabular-nums">{formatearCantidad(p.stockMinimo)}</Td>
    {conFalta && <Td className="text-right tabular-nums font-semibold">{formatearCantidad(p.falta)}</Td>}
  </Tr>
));

const Titulo = ({ children }) => <h3 className="mb-2 mt-6 text-base font-semibold text-slate-900">{children}</h3>;

/**
 * Reporte de inventario (6.4, 7.3.25): el stock de los productos activos, aparte los que están en su mínimo o debajo
 * con lo que les falta, y los movimientos del período. La categoría filtra las tres partes; las fechas, los movimientos.
 */
export default function ReporteInventario({ barra, panel }) {
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [tamano, setTamano] = useState(TAMANO_PAGINA);
  const filtrosDeLaApi = useMemo(() => ({
    categoriaId: filtros.categoria || undefined, desde: filtros.desde || undefined, hasta: filtros.hasta || undefined,
  }), [filtros]);
  const parametros = useMemo(() => ({ ...filtrosDeLaApi, pagina: numeroPagina, tamano }), [filtrosDeLaApi, numeroPagina, tamano]);
  const { datos: reporte, cargando, recargando, erroresFiltro, estado } =
    useReporte('/reportes/inventario', parametros, 'No se pudo generar el reporte de inventario.');
  const { exportar, exportando } = useExportar('/reportes/inventario', filtrosDeLaApi, 'inventario');

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };
  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const movimientos = reporte?.movimientos;
  // El stock y los que están bajo el mínimo llegan enteros: se paginan aquí, cada tabla con la suya. Las fechas solo
  // filtran los movimientos, así que solo la categoría las vuelve a la página 1.
  const stock = usePaginaLocal(reporte?.productos, filtros.categoria);
  const bajoElMinimo = usePaginaLocal(reporte?.bajoElMinimo, filtros.categoria);
  const columnas =['Código', 'Producto', 'Categoría', { texto: 'Stock', derecha: true }, { texto: 'Mínimo', derecha: true }];

  return (
    <>
      {barra(<BotonesExportar onExportar={exportar} exportando={exportando} disabled={!reporte} />)}
      <div {...panel}>
        <Table
          cargando={cargando && 'Generando el reporte…'} recargando={recargando}
          titulo="Stock"
          headers={columnas}
          empty="No hay productos activos en esta categoría."
          estado={estado}
          pie={<Paginacion {...stock.paginacion} deshabilitada={recargando} />}
          barra={(
            <BarraDeFiltros resultado={reporte ? `${reporte.productos.length} ${reporte.productos.length === 1 ? 'producto' : 'productos'}` : ''}
              hayFiltros={hayFiltros} onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}>
              <div className="w-full sm:w-44">
                <Selector label="Categoría" todos="Todas" value={filtros.categoria}
                  opciones={(reporte?.categorias ?? []).map((c) => ({ valor: String(c.id), texto: c.nombre }))}
                  onChange={(categoria) => filtrar({ categoria })} />
              </div>
              <div className="w-full sm:w-40">
                <CampoFecha label="Movimientos desde" value={filtros.desde} error={erroresFiltro.desde}
                  onChange={(e) => filtrar({ desde: e.target.value })}
                  atajos={atajosDePeriodo()} onAtajo={filtrar} />
              </div>
              <div className="w-full sm:w-40">
                <CampoFecha label="Hasta" value={filtros.hasta} error={erroresFiltro.hasta}
                  onChange={(e) => filtrar({ hasta: e.target.value })} />
              </div>
            </BarraDeFiltros>
          )}
        >
          {filasDeProductos(stock.visibles)}
        </Table>

        {reporte && (
          <>
            <Titulo>Bajo el mínimo</Titulo>
            <Table titulo="Bajo el mínimo" headers={[...columnas, { texto: 'Falta', derecha: true }]}
              empty="Ningún producto está por debajo de su mínimo."
              pie={<Paginacion {...bajoElMinimo.paginacion} deshabilitada={recargando} />}>
              {filasDeProductos(bajoElMinimo.visibles, true)}
            </Table>

            <Titulo>Movimientos</Titulo>
            <Table
              titulo="Movimientos"
              headers={['Fecha', 'Producto', 'Tipo', { texto: 'Cantidad', derecha: true }, { texto: 'Stock resultante', derecha: true }, 'Usuario']}
              empty="No hay movimientos en el período."
              pie={<Paginacion pagina={movimientos.numeroPagina} totalPaginas={movimientos.totalPaginas} onCambiar={setNumeroPagina}
                total={movimientos.total} tamano={movimientos.tamano}
                onCambiarTamano={(nuevo) => { setTamano(nuevo); setNumeroPagina(1); }} deshabilitada={recargando} />}
            >
              {movimientos.items.map((m) => (
                <Tr key={m.id}>
                  <Td className="whitespace-nowrap tabular-nums">{formatearFecha(m.fecha, { hora: true })}</Td>
                  <Td>{m.producto}</Td>
                  <Td>{TIPOS[m.tipo] ?? '—'}</Td>
                  <Td className="text-right tabular-nums">{formatearCantidad(m.cantidad)}</Td>
                  <Td className="text-right tabular-nums">{formatearCantidad(m.stockResultante)}</Td>
                  <Td>{m.usuario.nombreCompleto}</Td>
                </Tr>
              ))}
            </Table>
          </>
        )}
      </div>
    </>
  );
}
