import { useEffect, useState } from 'react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Badge, Btn, Input, Card, Alert } from '../components/ui';

export default function Inventario() {
  const [inventario, setInventario] = useState([]);
  const [movimientos, setMovimientos] = useState([]);
  const [selectedProducto, setSelectedProducto] = useState(null);
  const [ajuste, setAjuste] = useState({ cantidad: '', observacion: '' });
  const [showAjuste, setShowAjuste] = useState(false);
  const [showMovimientos, setShowMovimientos] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  const fetchInventario = () => {
    api.get('/inventario').then(r => { setInventario(r.data); setLoading(false); });
  };

  useEffect(() => { fetchInventario(); }, []);

  const handleVerMovimientos = async (item) => {
    setSelectedProducto(item);
    const r = await api.get(`/inventario/${item.productoId}/movimientos`);
    setMovimientos(r.data);
    setShowMovimientos(true);
    setShowAjuste(false);
  };

  const handleAjustar = (item) => {
    setSelectedProducto(item);
    setAjuste({ cantidad: '', observacion: '' });
    setShowAjuste(true);
    setShowMovimientos(false);
    setError('');
  };

  const handleSubmitAjuste = async (e) => {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      await api.post(`/inventario/${selectedProducto.productoId}/ajuste`, {
        cantidad: parseFloat(ajuste.cantidad),
        observacion: ajuste.observacion,
      });
      setShowAjuste(false);
      fetchInventario();
    } catch (err) {
      setError(mensajeDeError(err, 'Error al ajustar stock.'));
    } finally {
      setSaving(false);
    }
  };

  const bajoMinimo = inventario.filter(i => i.bajoMinimo).length;

  return (
    <Layout>
      <PageHeader
        title="Inventario"
        subtitle="Control de stock y movimientos"
      />

      {bajoMinimo > 0 && (
        <Alert type="warning" className="mb-5">
          ⚠ {bajoMinimo} producto{bajoMinimo > 1 ? 's' : ''} con stock bajo el mínimo configurado.
        </Alert>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5 mb-6">
        <StockStat label="Total productos" value={inventario.length} color="blue" />
        <StockStat label="Bajo mínimo" value={bajoMinimo} color="red" />
        <StockStat label="Stock normal" value={inventario.length - bajoMinimo} color="emerald" />
      </div>

      <div className="flex gap-5">
        {/* Tabla principal */}
        <div className="flex-1 min-w-0">
          {loading ? (
            <div className="flex items-center justify-center h-40 text-slate-400 text-sm">Cargando inventario...</div>
          ) : (
            <Table headers={['Producto', 'SKU', 'Stock actual', 'Mínimo', 'Última actualización', 'Acciones']}>
              {inventario.map(item => (
                <Tr key={item.id}>
                  <Td>
                    <div>
                      <p className="font-medium text-slate-800">{item.producto?.descripcion}</p>
                      {item.producto?.categoria && (
                        <Badge color="blue">{item.producto.categoria}</Badge>
                      )}
                    </div>
                  </Td>
                  <Td mono>{item.producto?.sku}</Td>
                  <Td>
                    <span className={`text-lg font-bold ${item.bajoMinimo ? 'text-red-600' : 'text-slate-800'}`}>
                      {item.stockActual?.toFixed(0)}
                    </span>
                    {item.bajoMinimo && <span className="ml-2 text-xs text-red-500">▼ bajo mínimo</span>}
                  </Td>
                  <Td>{item.stockMinimo?.toFixed(0)}</Td>
                  <Td>{new Date(item.ultimaActualizacion).toLocaleString()}</Td>
                  <Td>
                    <div className="flex gap-2">
                      <Btn onClick={() => handleVerMovimientos(item)} variant="ghost">Movimientos</Btn>
                      <Btn onClick={() => handleAjustar(item)} variant="outline">Ajustar</Btn>
                    </div>
                  </Td>
                </Tr>
              ))}
            </Table>
          )}
        </div>

        {/* Panel lateral */}
        {(showAjuste || showMovimientos) && (
          <div className="w-80 shrink-0">
            <Card>
              <div className="flex items-center justify-between mb-3">
                <h3 className="text-sm font-semibold text-slate-700">
                  {showAjuste ? 'Ajustar stock' : 'Movimientos'}
                </h3>
                <Btn variant="ghost" onClick={() => { setShowAjuste(false); setShowMovimientos(false); }}>✕</Btn>
              </div>
              <p className="text-xs text-slate-500 mb-4 font-medium">{selectedProducto?.producto?.descripcion}</p>

              {showAjuste && (
                <form onSubmit={handleSubmitAjuste} className="space-y-3">
                  {error && <Alert type="error">{error}</Alert>}
                  <Input
                    label="Cantidad (+ entrada / - salida)"
                    type="number"
                    step="0.001"
                    placeholder="Ej: 50 o -10"
                    value={ajuste.cantidad}
                    onChange={e => setAjuste({ ...ajuste, cantidad: e.target.value })}
                    required
                  />
                  <div>
                    <label className="block text-xs font-medium text-slate-600 mb-1">Observación *</label>
                    <textarea
                      value={ajuste.observacion}
                      onChange={e => setAjuste({ ...ajuste, observacion: e.target.value })}
                      placeholder="Motivo del ajuste..."
                      rows={3}
                      required
                      className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
                    />
                  </div>
                  <Btn type="submit" variant="primary" size="md" disabled={saving}>
                    {saving ? 'Guardando...' : 'Aplicar ajuste'}
                  </Btn>
                </form>
              )}

              {showMovimientos && (
                <div className="space-y-2 max-h-[400px] overflow-y-auto">
                  {movimientos.length === 0 && (
                    <p className="text-slate-400 text-sm text-center py-4">Sin movimientos registrados.</p>
                  )}
                  {movimientos.map(m => (
                    <div key={m.id} className="border border-slate-100 rounded-lg p-2.5">
                      <div className="flex items-center justify-between">
                        <Badge color={m.tipoMovimiento === 0 ? 'green' : m.tipoMovimiento === 1 ? 'red' : 'yellow'}>
                          {['Entrada', 'Salida', 'Ajuste', 'Devolución'][m.tipoMovimiento]}
                        </Badge>
                        <span className={`text-sm font-bold ${m.tipoMovimiento === 0 ? 'text-emerald-600' : 'text-red-600'}`}>
                          {m.tipoMovimiento === 0 ? '+' : '-'}{m.cantidad}
                        </span>
                      </div>
                      <p className="text-xs text-slate-400 mt-1">{m.referenciaTabla} #{m.referenciaId}</p>
                      <p className="text-xs text-slate-400">{new Date(m.fecha).toLocaleString()}</p>
                      <p className="text-xs text-slate-500 mt-1">Stock: {m.stockAnterior} → {m.stockResultante}</p>
                    </div>
                  ))}
                </div>
              )}
            </Card>
          </div>
        )}
      </div>
    </Layout>
  );
}

function StockStat({ label, value, color }) {
  const colors = {
    blue: 'bg-blue-50 border-blue-100 text-blue-700',
    red: 'bg-red-50 border-red-100 text-red-600',
    emerald: 'bg-emerald-50 border-emerald-100 text-emerald-700',
  };
  return (
    <div className={`rounded-xl border p-4 ${colors[color]}`}>
      <p className="text-xs font-medium uppercase tracking-wide opacity-60">{label}</p>
      <p className="text-3xl font-bold mt-1">{value}</p>
    </div>
  );
}
