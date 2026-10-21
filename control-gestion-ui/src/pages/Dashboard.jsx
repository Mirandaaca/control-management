import { useEffect, useState } from 'react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Alert } from '../components/ui';

export default function Dashboard() {
  const [kpis, setKpis] = useState(null);
  const [ventas, setVentas] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    const avisarError = (err) =>
      setError(mensajeDeError(err, 'No se pudieron cargar algunos datos del tablero.'));
    api.get('/reportes/dashboard').then(r => setKpis(r.data)).catch(avisarError);
    api.get('/reportes/ventas').then(r => setVentas(r.data)).catch(avisarError);
  }, []);

  return (
    <Layout>
      <div className="mb-6">
        <h2 className="text-2xl font-semibold text-slate-900">Inicio</h2>
        <p className="text-slate-500 text-sm mt-1">Resumen general del sistema</p>
      </div>

      {error && <div className="mb-6"><Alert type="error">{error}</Alert></div>}

      {/* KPIs principales */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3 mb-6">
        <KpiCard label="Clientes activos"   value={kpis?.totalClientes ?? '—'}     color="blue" />
        <KpiCard label="HPs este mes"       value={kpis?.pedidosMes ?? '—'}         color="emerald" />
        <KpiCard label="Ventas del mes"     value={kpis ? `Bs ${kpis.ventasMes?.toFixed(0)}` : '—'} color="amber" />
        <KpiCard label="Por cobrar"         value={kpis ? `Bs ${kpis.cobranzasPendientes?.toFixed(0)}` : '—'} color="red" />
        <KpiCard label="Eventos abiertos"   value={kpis?.eventosAbiertos ?? '—'}    color="purple" />
        <KpiCard label="Stock bajo mínimo"  value={kpis?.stockBajo ?? '—'}          color="orange" />
      </div>

      {/* Gráficos */}
      <div className="grid grid-cols-1 gap-6 mb-6">
        {/* Ventas por promotor */}
        <ChartCard title="Ventas por Promotor (mes actual)">
          {ventas?.porPromotor?.length > 0 ? (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={ventas.porPromotor} margin={{ left: -10 }}>
                <XAxis dataKey="promotor" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip formatter={(v) => `Bs ${v.toFixed(2)}`} />
                <Bar dataKey="total" fill="#3b82f6" radius={[4, 4, 0, 0]} name="Total" />
              </BarChart>
            </ResponsiveContainer>
          ) : <EmptyChart />}
        </ChartCard>
      </div>

      {/* Top productos + ventas por día */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <ChartCard title="Top productos más vendidos">
          {ventas?.porProducto?.length > 0 ? (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={ventas.porProducto.slice(0, 8)} layout="vertical" margin={{ left: 10 }}>
                <XAxis type="number" tick={{ fontSize: 11 }} />
                <YAxis dataKey="producto" type="category" tick={{ fontSize: 10 }} width={110} />
                <Tooltip formatter={(v) => `Bs ${v.toFixed(2)}`} />
                <Bar dataKey="total" fill="#10b981" radius={[0, 4, 4, 0]} name="Total" />
              </BarChart>
            </ResponsiveContainer>
          ) : <EmptyChart />}
        </ChartCard>

        <ChartCard title="Tendencia de ventas (30 días)">
          {ventas?.porDia?.length > 0 ? (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={ventas.porDia} margin={{ left: -10 }}>
                <XAxis dataKey="fecha" tick={{ fontSize: 9 }} interval="preserveStartEnd" />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip formatter={(v) => `Bs ${v.toFixed(2)}`} />
                <Bar dataKey="total" fill="#8b5cf6" radius={[4, 4, 0, 0]} name="Ventas" />
              </BarChart>
            </ResponsiveContainer>
          ) : <EmptyChart />}
        </ChartCard>
      </div>
    </Layout>
  );
}

function KpiCard({ label, value, color }) {
  const styles = {
    blue:   'bg-blue-50   border-blue-100   text-blue-700',
    emerald:'bg-emerald-50 border-emerald-100 text-emerald-700',
    amber:  'bg-amber-50  border-amber-100  text-amber-700',
    red:    'bg-red-50    border-red-100    text-red-700',
    purple: 'bg-purple-50 border-purple-100 text-purple-700',
    orange: 'bg-orange-50 border-orange-100 text-orange-700',
  };
  return (
    <div className={`rounded-xl border p-4 ${styles[color]}`}>
      <p className="text-xs font-medium uppercase tracking-wide opacity-60 leading-tight mb-1">{label}</p>
      <p className="text-2xl font-bold">{value}</p>
    </div>
  );
}

function ChartCard({ title, children }) {
  return (
    <div className="bg-white rounded-xl shadow-sm border border-slate-100 p-5">
      <h3 className="text-sm font-semibold text-slate-700 mb-4">{title}</h3>
      {children}
    </div>
  );
}

function EmptyChart() {
  return (
    <div className="h-[220px] flex items-center justify-center text-slate-400 text-sm">
      Sin datos disponibles
    </div>
  );
}
