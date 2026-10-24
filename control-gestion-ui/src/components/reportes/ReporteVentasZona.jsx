import { useMemo, useState } from 'react';
import { useReporte } from '../../hooks/useReporte';
import { formatearMonto } from '../../utils/formato';
import { Table, Td, Tr } from '../Table';
import { BarraDeFiltros, Input } from '../ui';

const SIN_FILTROS = { desde: '', hasta: '' };

/**
 * Ventas por zona (6.2, 7.3.23): cada zona con sus HP confirmadas, el total, el contado y el crédito; las que no
 * vendieron, en cero. Sin fechas, todas las ventas. No se exporta: la especificación no lo pide.
 */
export default function ReporteVentasZona({ barra, panel }) {
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const parametros = useMemo(() => ({ desde: filtros.desde || undefined, hasta: filtros.hasta || undefined }), [filtros]);
  const { datos: reporte, cargando, erroresFiltro, estado: estadoDeCarga } =
    useReporte('/reportes/ventas-por-zona', parametros, 'No se pudo generar el reporte de ventas por zona.');

  const hayFiltros = filtros.desde !== '' || filtros.hasta !== '';
  const sinVentas = reporte?.total.cantidad === 0;
  let estado = estadoDeCarga;
  // Ext. 2.1a: sin ventas en el período, el mensaje en lugar de una tabla de ceros.
  if (!estado && sinVentas) estado = <p className="py-10 text-center text-sm text-slate-600">No hay ventas confirmadas en el período.</p>;

  return (
    <>
      {barra(null)}
      <div {...panel}>
        <Table
          titulo="Ventas por zona"
          headers={['Zona', { texto: 'HP', derecha: true }, { texto: 'Total', derecha: true }, { texto: 'Contado', derecha: true },
            { texto: 'Crédito', derecha: true }]}
          empty="Todavía no hay zonas."
          estado={estado}
          barra={!cargando && (
            <BarraDeFiltros
              resultado={reporte && !sinVentas ? `${reporte.total.cantidad} ${reporte.total.cantidad === 1 ? 'venta' : 'ventas'}` : ''}
              hayFiltros={hayFiltros} onLimpiar={() => setFiltros(SIN_FILTROS)}>
              <div className="w-full sm:w-40">
                <Input label="Desde" type="date" autoComplete="off" value={filtros.desde} error={erroresFiltro.desde}
                  onChange={(e) => setFiltros((f) => ({ ...f, desde: e.target.value }))} />
              </div>
              <div className="w-full sm:w-40">
                <Input label="Hasta" type="date" autoComplete="off" value={filtros.hasta} error={erroresFiltro.hasta}
                  onChange={(e) => setFiltros((f) => ({ ...f, hasta: e.target.value }))} />
              </div>
            </BarraDeFiltros>
          )}
        >
          {(reporte?.zonas ?? []).map((z) => (
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
