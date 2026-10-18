import { useEffect, useState } from 'react';
import api from '../api/axios';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Badge, Btn, Input, Card } from '../components/ui';

const METHOD_COLOR = { POST: 'blue', PUT: 'yellow', DELETE: 'red' };
const TABS = ['Ventas', 'Cobranzas', 'Inventario', 'Promotores', 'Auditoría'];

export default function Reportes() {
  const [tab, setTab] = useState('Ventas');
  const [ventas, setVentas] = useState(null);
  const [cobranzas, setCobranzas] = useState(null);
  const [inventario, setInventario] = useState(null);
  const [promotores, setPromotores] = useState([]);
  const [auditoria, setAuditoria] = useState([]);
  const [loading, setLoading] = useState(true);

  // La fecha se calcula una sola vez, al montar: calcularla en cada render rompe la regla de pureza de React.
  const [filtros, setFiltros] = useState(() => ({
    desde: new Date(Date.now() - 30 * 86400000).toISOString().split('T')[0],
    hasta: new Date().toISOString().split('T')[0],
    modulo: '',
  }));

  const fetchTab = async (t) => {
    try {
      const p = { desde: filtros.desde, hasta: filtros.hasta };
      if (t === 'Ventas') {
        const r = await api.get('/reportes/ventas', { params: p });
        setVentas(r.data);
      } else if (t === 'Cobranzas') {
        const r = await api.get('/reportes/cobranzas', { params: p });
        setCobranzas(r.data);
      } else if (t === 'Inventario') {
        const r = await api.get('/reportes/inventario');
        setInventario(r.data);
      } else if (t === 'Promotores') {
        const r = await api.get('/reportes/promotores', { params: p });
        setPromotores(r.data);
      } else if (t === 'Auditoría') {
        const params = { ...p };
        if (filtros.modulo) params.modulo = filtros.modulo;
        const r = await api.get('/reportes/auditoria', { params });
        setAuditoria(r.data);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchTab(tab); }, [tab]);

  const handleExport = async (tipo) => {
    const p = new URLSearchParams({ desde: filtros.desde, hasta: filtros.hasta });
    const token = localStorage.getItem('token');
    const res = await fetch(`http://localhost:5124/api/reportes/export/${tipo}?${p}`, {
      headers: { Authorization: `Bearer ${token}` }
    });
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${tipo}_${filtros.desde}_${filtros.hasta}.xlsx`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <Layout>
      <PageHeader title="Reportes" subtitle="Análisis y exportación de datos" />

      {/* Filtro global de fechas */}
      <Card className="mb-5">
        <div className="flex flex-wrap gap-3 items-end">
          <Input label="Desde" type="date" value={filtros.desde} onChange={e => setFiltros({ ...filtros, desde: e.target.value })} />
          <Input label="Hasta" type="date" value={filtros.hasta} onChange={e => setFiltros({ ...filtros, hasta: e.target.value })} />
          {tab === 'Auditoría' && (
            <Input label="Módulo" placeholder="Clientes, Pedidos..." value={filtros.modulo}
              onChange={e => setFiltros({ ...filtros, modulo: e.target.value })} />
          )}
          <Btn variant="primary" size="md" onClick={() => { setLoading(true); fetchTab(tab); }} disabled={loading}>
            {loading ? 'Cargando...' : 'Aplicar'}
          </Btn>
          {(tab === 'Ventas' || tab === 'Cobranzas') && (
            <Btn variant="outline" size="md" onClick={() => handleExport(tab === 'Ventas' ? 'ventas' : 'cobranzas')}>
              ↓ Exportar Excel
            </Btn>
          )}
        </div>
      </Card>

      {/* Tabs */}
      <div className="flex gap-1 mb-5 border-b border-slate-200">
        {TABS.map(t => (
          <button key={t} onClick={() => { if (t !== tab) { setLoading(true); setTab(t); } }}
            className={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px ${
              tab === t ? 'border-blue-600 text-blue-600' : 'border-transparent text-slate-500 hover:text-slate-700'
            }`}>
            {t}
          </button>
        ))}
      </div>

      {/* TAB: Ventas */}
      {tab === 'Ventas' && ventas && (
        <div className="space-y-5">
          {/* KPIs */}
          <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
            {[
              { label: 'Total ventas', value: `Bs ${ventas.resumen.totalVentas?.toLocaleString('es-BO', { minimumFractionDigits: 2 })}`, color: 'text-slate-800' },
              { label: 'HPs confirmadas', value: ventas.resumen.cantidadHPs, color: 'text-blue-600' },
              { label: 'Ticket promedio', value: `Bs ${ventas.resumen.ticketPromedio?.toFixed(2)}`, color: 'text-slate-600' },
              { label: 'Ventas crédito', value: `Bs ${ventas.resumen.ventasCredito?.toFixed(2)}`, color: 'text-amber-600' },
              { label: 'Ventas contado', value: `Bs ${ventas.resumen.ventasContado?.toFixed(2)}`, color: 'text-green-600' },
            ].map(k => (
              <Card key={k.label}>
                <p className="text-xs text-slate-500 mb-1">{k.label}</p>
                <p className={`text-xl font-bold ${k.color}`}>{k.value}</p>
              </Card>
            ))}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
            {/* Por promotor */}
            <Card>
              <h3 className="text-sm font-semibold text-slate-700 mb-3">Ventas por promotor</h3>
              <div className="space-y-2">
                {ventas.porPromotor?.map((p, i) => (
                  <div key={i} className="flex items-center justify-between text-sm">
                    <span className="text-slate-700 truncate flex-1">{p.promotor}</span>
                    <span className="text-slate-500 text-xs mr-3">{p.cantidad} HPs</span>
                    <span className="font-semibold text-slate-800 w-28 text-right">Bs {p.total?.toFixed(2)}</span>
                  </div>
                ))}
                {ventas.porPromotor?.length === 0 && <p className="text-slate-400 text-sm">Sin datos</p>}
              </div>
            </Card>

            {/* Top productos */}
            <Card>
              <h3 className="text-sm font-semibold text-slate-700 mb-3">Top 10 productos</h3>
              <div className="space-y-2">
                {ventas.porProducto?.map((p, i) => (
                  <div key={i} className="flex items-center gap-2 text-sm">
                    <span className="w-5 text-xs text-slate-400 font-mono">{i + 1}</span>
                    <span className="text-slate-700 truncate flex-1">{p.producto}</span>
                    <span className="text-slate-500 text-xs">{p.cantidad} un.</span>
                    <span className="font-semibold text-slate-800 w-24 text-right">Bs {p.total?.toFixed(2)}</span>
                  </div>
                ))}
              </div>
            </Card>
          </div>

          {/* Por día */}
          <Card>
            <h3 className="text-sm font-semibold text-slate-700 mb-3">Ventas por día</h3>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-100">
                    <th className="text-left py-2 text-xs font-medium text-slate-500">Fecha</th>
                    <th className="text-right py-2 text-xs font-medium text-slate-500">HPs</th>
                    <th className="text-right py-2 text-xs font-medium text-slate-500">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {ventas.porDia?.map((d, i) => (
                    <tr key={i} className="border-b border-slate-50 hover:bg-slate-50">
                      <td className="py-1.5 text-slate-700">{d.fecha}</td>
                      <td className="py-1.5 text-right text-slate-500">{d.cantidad}</td>
                      <td className="py-1.5 text-right font-medium">Bs {d.total?.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      )}

      {/* TAB: Cobranzas */}
      {tab === 'Cobranzas' && cobranzas && (
        <div className="space-y-5">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <Card>
              <p className="text-xs text-slate-500 mb-1">Total emitido</p>
              <p className="text-2xl font-bold text-slate-800">Bs {cobranzas.totalEmitido?.toFixed(2)}</p>
            </Card>
            <Card>
              <p className="text-xs text-slate-500 mb-1">Total cobrado</p>
              <p className="text-2xl font-bold text-green-600">Bs {cobranzas.totalCobrado?.toFixed(2)}</p>
            </Card>
            <Card>
              <p className="text-xs text-slate-500 mb-1">Saldo pendiente</p>
              <p className="text-2xl font-bold text-red-600">Bs {cobranzas.totalPendiente?.toFixed(2)}</p>
            </Card>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
            <Card>
              <h3 className="text-sm font-semibold text-slate-700 mb-3">Por estado</h3>
              <div className="space-y-2">
                {cobranzas.porEstado?.map((e, i) => (
                  <div key={i} className="flex items-center justify-between text-sm">
                    <span className="text-slate-700">{e.estado}</span>
                    <span className="text-slate-500 text-xs mr-3">{e.cantidad} cobranzas</span>
                    <span className="font-semibold w-28 text-right">Bs {e.monto?.toFixed(2)}</span>
                  </div>
                ))}
              </div>
            </Card>

            <Card>
              <h3 className="text-sm font-semibold text-slate-700 mb-3">Vencidas — requieren acción</h3>
              {cobranzas.vencidas?.length === 0 && <p className="text-sm text-slate-400">Sin cobranzas vencidas.</p>}
              <div className="space-y-2 max-h-60 overflow-y-auto">
                {cobranzas.vencidas?.map((v, i) => (
                  <div key={i} className="bg-red-50 rounded-lg px-3 py-2 text-sm">
                    <div className="flex items-center justify-between">
                      <span className="font-medium text-slate-800">{v.cliente}</span>
                      <span className="text-red-600 font-bold">Bs {v.saldoPendiente?.toFixed(2)}</span>
                    </div>
                    <p className="text-xs text-red-500 mt-0.5">{v.diasVencida} días vencida — {v.numero}</p>
                  </div>
                ))}
              </div>
            </Card>
          </div>
        </div>
      )}

      {/* TAB: Inventario */}
      {tab === 'Inventario' && inventario && (
        <div className="space-y-5">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <Card>
              <p className="text-xs text-slate-500 mb-1">Stock total</p>
              <p className="text-2xl font-bold text-slate-800">{inventario.stockTotal?.toFixed(0)} un.</p>
            </Card>
            <Card>
              <p className="text-xs text-slate-500 mb-1">Valor inventario</p>
              <p className="text-2xl font-bold text-blue-600">Bs {inventario.valorTotal?.toFixed(2)}</p>
            </Card>
            <Card>
              <p className="text-xs text-slate-500 mb-1">Productos bajo mínimo</p>
              <p className="text-2xl font-bold text-red-600">{inventario.stockBajo?.length ?? 0}</p>
            </Card>
          </div>

          {inventario.stockBajo?.length > 0 && (
            <Card>
              <h3 className="text-sm font-semibold text-red-600 mb-3">⚠ Productos bajo stock mínimo</h3>
              <Table headers={['Producto', 'Stock Actual', 'Mínimo', 'Déficit']} empty="">
                {inventario.stockBajo.map((s, i) => (
                  <Tr key={i}>
                    <Td>{s.producto}</Td>
                    <Td><span className="text-red-600 font-semibold">{s.stockActual}</span></Td>
                    <Td>{s.stockMinimo}</Td>
                    <Td><span className="text-red-600">{s.deficit}</span></Td>
                  </Tr>
                ))}
              </Table>
            </Card>
          )}

          <Card>
            <h3 className="text-sm font-semibold text-slate-700 mb-3">Últimos 100 movimientos (30 días)</h3>
            <Table headers={['Producto', 'Tipo', 'Cantidad', 'Stock result.', 'Usuario', 'Fecha']} empty="Sin movimientos.">
              {inventario.movimientos30Dias?.map((m, i) => (
                <Tr key={i}>
                  <Td>{m.producto}</Td>
                  <Td><Badge color={m.tipoMovimiento === 0 ? 'green' : m.tipoMovimiento === 1 ? 'red' : 'yellow'}>
                    {['Entrada', 'Salida', 'Ajuste', 'Devolución'][m.tipoMovimiento] ?? '—'}
                  </Badge></Td>
                  <Td>{m.cantidad}</Td>
                  <Td>{m.stockResultante}</Td>
                  <Td>{m.usuario}</Td>
                  <Td>{new Date(m.fecha).toLocaleDateString()}</Td>
                </Tr>
              ))}
            </Table>
          </Card>
        </div>
      )}

      {/* TAB: Promotores */}
      {tab === 'Promotores' && (
        <Card>
          <h3 className="text-sm font-semibold text-slate-700 mb-3">Rendimiento por promotor</h3>
          <Table headers={['Promotor', 'HPs', 'Total ventas', 'Ticket prom.', 'Clientes asign.', 'Eventos']} empty="Sin datos.">
            {promotores.map((p, i) => (
              <Tr key={i}>
                <Td><span className="font-medium text-slate-800">{p.promotor}</span></Td>
                <Td>{p.cantidadHPs}</Td>
                <Td><span className="font-semibold">Bs {p.totalVentas?.toFixed(2)}</span></Td>
                <Td>Bs {p.ticketPromedio?.toFixed(2)}</Td>
                <Td>{p.clientesAsignados}</Td>
                <Td>{p.eventosRegistrados}</Td>
              </Tr>
            ))}
          </Table>
        </Card>
      )}

      {/* TAB: Auditoría */}
      {tab === 'Auditoría' && (
        <Card>
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-sm font-semibold text-slate-700">Bitácora del sistema</h3>
            <span className="text-xs text-slate-400">{auditoria.length} registros</span>
          </div>
          <Table headers={['Usuario', 'Acción', 'Módulo', 'IP', 'Fecha']} empty="Sin registros.">
            {auditoria.map(a => (
              <Tr key={a.id}>
                <Td mono>{a.usuarioId?.slice(0, 8)}…</Td>
                <Td><Badge color={METHOD_COLOR[a.accion] ?? 'gray'}>{a.accion}</Badge></Td>
                <Td>{a.modulo}</Td>
                <Td mono>{a.direccionIp ?? '—'}</Td>
                <Td>{new Date(a.fecha).toLocaleString()}</Td>
              </Tr>
            ))}
          </Table>
        </Card>
      )}
    </Layout>
  );
}
