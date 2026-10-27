import { useEffect, useMemo, useState } from 'react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { ChartColumn, Table2 } from 'lucide-react';
import api from '../../api/axios';
import { mensajeDeError } from '../../api/errores';
import { useReporte } from '../../hooks/useReporte';
import { formatearMonto } from '../../utils/formato';
import { Table, Td, Tr } from '../Table';
import { Alert, BarraDeFiltros, Btn, CampoFecha, Selector } from '../ui';
import { atajosDePeriodo } from '../../utils/fechas';

const SIN_FILTROS = { desde: '', hasta: '', promotor: '' };
// Los colores del gráfico, de docs/diseno.md: la barra en marca-600 y la grilla y los ejes en gris, para que no compitan.
const COLOR_BARRA = '#2a5899';
const COLOR_EJE = '#475569';
const COLOR_GRILLA = '#e2e8f0';

const montoCorto = new Intl.NumberFormat('es-BO', { notation: 'compact', maximumFractionDigits: 1 });

function Globo({ active, payload }) {
  if (!active || !payload?.length) return null;
  const p = payload[0].payload;
  return (
    <div className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm shadow-tarjeta">
      <p className="font-medium text-slate-900">{p.promotor}</p>
      <p className="tabular-nums text-slate-700">{formatearMonto(p.total)} · {p.cantidad} HP</p>
    </div>
  );
}

/**
 * Ventas por promotor (6.1, 7.3.22): cada promotor con sus HP confirmadas y el total vendido, y el total del período.
 * "Ver gráfico" cambia la tabla por barras (paso 3) y "Ver tabla" vuelve (ext. 3a). No se exporta.
 */
export default function ReporteVentasPromotor({ barra, panel }) {
  const [filtros, setFiltros] = useState(SIN_FILTROS);
  const [grafico, setGrafico] = useState(false);
  const [promotores, setPromotores] = useState([]);
  const [errorPromotores, setErrorPromotores] = useState('');

  useEffect(() => {
    api.get('/usuarios/promotores')
      .then(({ data }) => setPromotores(data))
      .catch((err) => setErrorPromotores(mensajeDeError(err, 'No se pudo cargar la lista de promotores.')));
  }, []);

  const parametros = useMemo(() => ({
    desde: filtros.desde || undefined, hasta: filtros.hasta || undefined, promotorId: filtros.promotor || undefined,
  }), [filtros]);
  const { datos: reporte, cargando, erroresFiltro, estado: estadoDeCarga } =
    useReporte('/reportes/ventas-por-promotor', parametros, 'No se pudo generar el reporte de ventas por promotor.');

  const hayFiltros = Object.values(filtros).some((v) => v !== '');
  const sinVentas = reporte?.promotores.length === 0;
  let estado = estadoDeCarga;
  if (!estado && sinVentas) estado = (
    <p className="py-10 text-center text-sm text-slate-600">Ninguna hoja de pedido confirmada coincide con los filtros.</p>
  );
  else if (!estado && grafico) estado = (
    <figure className="p-4" aria-label="Total vendido por promotor">
      <ResponsiveContainer width="100%" height={Math.max(240, reporte.promotores.length * 48)}>
        <BarChart data={reporte.promotores} layout="vertical" margin={{ left: 8, right: 24, top: 4, bottom: 4 }} barCategoryGap={8}>
          <CartesianGrid horizontal={false} stroke={COLOR_GRILLA} />
          <XAxis type="number" tickFormatter={(v) => `Bs ${montoCorto.format(v)}`} tick={{ fontSize: 12, fill: COLOR_EJE }}
            axisLine={false} tickLine={false} />
          <YAxis type="category" dataKey="promotor" width={150} tick={{ fontSize: 12, fill: COLOR_EJE }} axisLine={false} tickLine={false} />
          <Tooltip content={<Globo />} cursor={{ fill: '#f1f5f9' }} />
          <Bar dataKey="total" name="Total vendido" fill={COLOR_BARRA} radius={[0, 4, 4, 0]} maxBarSize={28} />
        </BarChart>
      </ResponsiveContainer>
      <figcaption className="mt-2 text-sm text-slate-600">
        Total del período: <span className="font-semibold text-slate-900 tabular-nums">{formatearMonto(reporte.total)}</span>
      </figcaption>
    </figure>
  );

  return (
    <>
      {barra(null)}
      <div {...panel}>
        {errorPromotores && <Alert type="error" className="mb-4">{errorPromotores}</Alert>}
        <Table
          titulo="Ventas por promotor"
          headers={['Promotor', { texto: 'HP', derecha: true }, { texto: 'Total vendido', derecha: true }]}
          estado={estado}
          barra={!cargando && (
            <BarraDeFiltros resultado={reporte && !sinVentas ? `${reporte.cantidad} ${reporte.cantidad === 1 ? 'venta' : 'ventas'}` : ''}
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
              <div className="w-full sm:w-48">
                <Selector label="Promotor" todos="Todos" value={filtros.promotor}
                  opciones={promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }))}
                  onChange={(promotor) => setFiltros((f) => ({ ...f, promotor }))} />
              </div>
              {reporte && !sinVentas && (
                <Btn variant="outline" size="md" onClick={() => setGrafico((g) => !g)}>
                  {grafico ? <Table2 size={16} aria-hidden="true" /> : <ChartColumn size={16} aria-hidden="true" />}
                  {grafico ? 'Ver tabla' : 'Ver gráfico'}
                </Btn>
              )}
            </BarraDeFiltros>
          )}
        >
          {(reporte?.promotores ?? []).map((p) => (
            <Tr key={p.promotorId}>
              <Td><span className="font-medium">{p.promotor}</span></Td>
              <Td className="text-right tabular-nums">{p.cantidad}</Td>
              <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(p.total)}</Td>
            </Tr>
          ))}
          {reporte && !sinVentas && (
            <tr className="font-semibold border-t border-slate-200">
              <Td>Total</Td>
              <Td className="text-right tabular-nums">{reporte.cantidad}</Td>
              <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(reporte.total)}</Td>
            </tr>
          )}
        </Table>
      </div>
    </>
  );
}
