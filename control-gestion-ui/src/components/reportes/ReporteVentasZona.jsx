import { useMemo, useState } from 'react';
import { usePaginaLocal } from '../../hooks/usePaginaLocal';
import { useExportar, useReporte } from '../../hooks/useReporte';
import { formatearMonto } from '../../utils/formato';
import { Table, Td, Tr } from '../Table';
import { BarraDeFiltros, CampoFecha, Paginacion } from '../ui';
import { atajosDePeriodo } from '../../utils/fechas';
import BotonesExportar from './BotonesExportar';

const SIN_FILTROS = { desde: '', hasta: '' };

/**
 * Ventas por zona (6.2, 7.3.23): cada zona con sus HP confirmadas, el total, el contado y el crédito; las que no
 * vendieron, en cero. Sin fechas, todas las ventas. Desde la tarea 33 se exporta a Excel y a PDF; sin ventas, no.
 */
export default function ReporteVentasZona({ barra, panel }) {
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const parametros = useMemo(() => ({ desde: filtros.desde || undefined, hasta: filtros.hasta || undefined }), [filtros]);
  const { datos: reporte, cargando, recargando, erroresFiltro, estado: estadoDeCarga } =
    useReporte('/reportes/ventas-por-zona', parametros, 'No se pudo generar el reporte de ventas por zona.');
  const { exportar, exportando } = useExportar('/reportes/ventas-por-zona', parametros, 'ventas_por_zona');

  // Las zonas llegan todas: se paginan aquí. La fila Total, que es la del período, va en cada página.
  const { visibles, paginacion } = usePaginaLocal(reporte?.zonas, JSON.stringify(filtros));
  const hayFiltros = filtros.desde !== '' || filtros.hasta !== '';
  const sinVentas = reporte?.total.cantidad === 0;
  let estado = estadoDeCarga;
  // Ext. 2.1a: sin ventas en el período, el mensaje en lugar de una tabla de ceros.
  if (!estado && sinVentas) estado = <p className="py-10 text-center text-sm text-slate-600">No hay ventas confirmadas en el período.</p>;

  return (
    <>
      {barra(<BotonesExportar onExportar={exportar} exportando={exportando} disabled={!reporte || sinVentas} />)}
      <div {...panel}>
        <Table
          cargando={cargando && 'Generando el reporte…'} recargando={recargando}
          titulo="Ventas por zona"
          headers={['Zona', { texto: 'HP', derecha: true }, { texto: 'Total', derecha: true }, { texto: 'Contado', derecha: true },
            { texto: 'Crédito', derecha: true }]}
          empty="Todavía no hay zonas."
          estado={estado}
          pie={<Paginacion {...paginacion} deshabilitada={recargando} />}
          barra={(
            <BarraDeFiltros
              resultado={reporte && !sinVentas ? `${reporte.total.cantidad} ${reporte.total.cantidad === 1 ? 'venta' : 'ventas'}` : ''}
              hayFiltros={hayFiltros} onLimpiar={() => setFiltros(SIN_FILTROS)}>
              <div className="w-full sm:w-40">
                <CampoFecha label="Desde" value={filtros.desde} error={erroresFiltro.desde}
                  onChange={(e) => setFiltros((f) => ({ ...f, desde: e.target.value }))}
                  atajos={atajosDePeriodo()} onAtajo={(periodo) => setFiltros((f) => ({ ...f, ...periodo }))} />
              </div>
              <div className="w-full sm:w-40">
                <CampoFecha label="Hasta" value={filtros.hasta} error={erroresFiltro.hasta}
                  onChange={(e) => setFiltros((f) => ({ ...f, hasta: e.target.value }))} />
              </div>
            </BarraDeFiltros>
          )}
        >
          {visibles.map((z) => (
            <Tr key={z.zonaId}>
              <Td><span className="font-medium">{z.zona}</span></Td>
              <Td className="text-right tabular-nums">{z.cantidad}</Td>
              <Td className={`text-right tabular-nums whitespace-nowrap ${z.total > 0 ? 'font-semibold' : 'text-slate-600'}`}>
                {formatearMonto(z.total)}
              </Td>
              <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(z.contado)}</Td>
              <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(z.credito)}</Td>
            </Tr>
          ))}
          {reporte?.zonas.length > 0 && (
            <tr className="font-semibold border-t border-slate-200">
              <Td>Total</Td>
              <Td className="text-right tabular-nums">{reporte.total.cantidad}</Td>
              <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(reporte.total.total)}</Td>
              <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(reporte.total.contado)}</Td>
              <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(reporte.total.credito)}</Td>
            </tr>
          )}
        </Table>
      </div>
    </>
  );
}
