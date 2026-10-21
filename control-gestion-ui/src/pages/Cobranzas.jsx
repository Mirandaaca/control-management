import { useEffect, useState } from 'react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Badge, Btn, Input, Card, Alert } from '../components/ui';
import { formatearFecha, leerFechaUtc } from '../utils/formato';

const ESTADO_COLOR = { 0: 'yellow', 1: 'blue', 2: 'green', 3: 'red', 4: 'gray' };
const ESTADO_LABEL = { 0: 'Pendiente', 1: 'Parcial', 2: 'Pagada', 3: 'Vencida', 4: 'Incobrable' };
const METODO_LABEL = { 0: 'Efectivo', 1: 'Transferencia', 2: 'Cheque', 3: 'Débito', 4: 'Crédito' };

export default function Cobranzas() {
  const [cobranzas, setCobranzas] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filtros, setFiltros] = useState({ estado: '', desde: '', hasta: '' });
  const [detalle, setDetalle] = useState(null);
  const [pagando, setPagando] = useState(null);
  const [formPago, setFormPago] = useState({ monto: '', metodoPago: '0', referencia: '', observaciones: '' });
  const [claveIdempotencia, setClaveIdempotencia] = useState(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const fetchCobranzas = () => {
    const params = {};
    if (filtros.estado !== '') params.estado = filtros.estado;
    if (filtros.desde) params.desde = filtros.desde;
    if (filtros.hasta) params.hasta = filtros.hasta;
    return api.get('/cobranzas', { params }).then((r) => {
      setCobranzas(r.data);
      setLoading(false);
    });
  };

  const fetchDetalle = async (id) => {
    const r = await api.get(`/cobranzas/${id}`);
    setDetalle(r.data);
  };

  useEffect(() => { fetchCobranzas(); }, []);

  // Una clave nueva cada vez que se abre el modal. Si el mismo pago se envía dos veces, la API no lo duplica.
  // crypto.randomUUID solo existe en localhost o con HTTPS; si falta, el pago se envía sin clave.
  const abrirPago = (cobranza) => {
    setPagando(cobranza);
    setClaveIdempotencia(crypto.randomUUID?.() ?? null);
  };

  const handlePago = async (e) => {
    e.preventDefault();
    setSaving(true); setError('');
    try {
      await api.post(`/cobranzas/${pagando.id}/pagos`, {
        monto: parseFloat(formPago.monto),
        metodoPago: parseInt(formPago.metodoPago),
        referencia: formPago.referencia || null,
        observaciones: formPago.observaciones || null,
        claveIdempotencia,
      });
      setPagando(null);
      setFormPago({ monto: '', metodoPago: '0', referencia: '', observaciones: '' });
      fetchCobranzas();
      if (detalle?.id === pagando.id) fetchDetalle(pagando.id);
    } catch (err) {
      setError(mensajeDeError(err, 'Error al registrar el pago.'));
    } finally { setSaving(false); }
  };

  const vencidas = cobranzas.filter(c => c.estado === 3).length;
  const pendientes = cobranzas.filter(c => c.estado === 0 || c.estado === 1).length;
  const totalPendiente = cobranzas
    .filter(c => c.estado !== 2 && c.estado !== 4)
    .reduce((s, c) => s + c.saldoPendiente, 0);

  return (
    <Layout>
      <PageHeader
        title="Cobranzas"
        subtitle="Gestión de cobros y seguimiento de deudas"
      />

      {/* KPIs */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-5">
        <Card>
          <p className="text-xs font-medium text-slate-500 uppercase tracking-wide mb-1">Total por cobrar</p>
          <p className="text-2xl font-bold text-slate-800">
            Bs {totalPendiente.toLocaleString('es-BO', { minimumFractionDigits: 2 })}
          </p>
        </Card>
        <Card>
          <p className="text-xs font-medium text-slate-500 uppercase tracking-wide mb-1">Pendientes</p>
          <p className="text-2xl font-bold text-amber-600">{pendientes}</p>
        </Card>
        <Card>
          <p className="text-xs font-medium text-slate-500 uppercase tracking-wide mb-1">Vencidas</p>
          <p className="text-2xl font-bold text-red-600">{vencidas}</p>
        </Card>
      </div>

      {vencidas > 0 && (
        <Alert type="error" className="mb-5">
          ⚠ {vencidas} cobranza{vencidas > 1 ? 's' : ''} vencida{vencidas > 1 ? 's' : ''} — requiere{vencidas > 1 ? 'n' : ''} atención inmediata.
        </Alert>
      )}

      {/* Filtros */}
      <Card className="mb-5">
        <div className="flex flex-wrap gap-3 items-end">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Estado</label>
            <select value={filtros.estado} onChange={e => setFiltros({ ...filtros, estado: e.target.value })}
              className="px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="">Todos</option>
              <option value="0">Pendiente</option>
              <option value="1">Parcial</option>
              <option value="2">Pagada</option>
              <option value="3">Vencida</option>
              <option value="4">Incobrable</option>
            </select>
          </div>
          <Input label="Desde" type="date" value={filtros.desde} onChange={e => setFiltros({ ...filtros, desde: e.target.value })} />
          <Input label="Hasta" type="date" value={filtros.hasta} onChange={e => setFiltros({ ...filtros, hasta: e.target.value })} />
          <Btn onClick={() => { setLoading(true); fetchCobranzas(); }} variant="primary" size="md">Filtrar</Btn>
        </div>
      </Card>

      {/* Modal detalle */}
      {detalle && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <Card className="w-full max-w-lg max-h-[80vh] overflow-y-auto">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="text-sm font-bold text-slate-800">{detalle.numero}</h3>
                <p className="text-xs text-slate-500">{detalle.cliente?.nombreCompleto}</p>
              </div>
              <Btn variant="ghost" onClick={() => setDetalle(null)}>✕</Btn>
            </div>

            <div className="grid grid-cols-2 gap-3 text-sm mb-4">
              <div>
                <p className="text-xs text-slate-500">Total</p>
                <p className="font-semibold">Bs {detalle.montoTotal?.toFixed(2)}</p>
              </div>
              <div>
                <p className="text-xs text-slate-500">Pagado</p>
                <p className="font-semibold text-green-600">Bs {detalle.montoPagado?.toFixed(2)}</p>
              </div>
              <div>
                <p className="text-xs text-slate-500">Saldo pendiente</p>
                <p className="font-bold text-red-600">Bs {detalle.saldoPendiente?.toFixed(2)}</p>
              </div>
              <div>
                <p className="text-xs text-slate-500">Vencimiento</p>
                <p className="font-medium">{formatearFecha(detalle.fechaVencimiento)}</p>
              </div>
            </div>

            {detalle.pagos?.length > 0 && (
              <>
                <p className="text-xs font-semibold text-slate-600 uppercase tracking-wide mb-2">Historial de pagos</p>
                <div className="space-y-2">
                  {detalle.pagos.map(p => (
                    <div key={p.id} className="flex items-center justify-between bg-slate-50 rounded-lg px-3 py-2 text-sm">
                      <div>
                        <span className="font-medium">Bs {p.monto?.toFixed(2)}</span>
                        <span className="text-slate-400 text-xs ml-2">— {METODO_LABEL[p.metodoPago]}</span>
                        {p.referencia && <span className="text-slate-400 text-xs ml-2">Ref: {p.referencia}</span>}
                      </div>
                      <span className="text-xs text-slate-400">{formatearFecha(p.fechaPago, { hora: true })}</span>
                    </div>
                  ))}
                </div>
              </>
            )}

            {detalle.estado !== 2 && detalle.estado !== 4 && (
              <div className="mt-4">
                <Btn variant="primary" size="md" onClick={() => { abrirPago(detalle); setDetalle(null); }}>
                  Registrar pago
                </Btn>
              </div>
            )}
          </Card>
        </div>
      )}

      {/* Modal pago */}
      {pagando && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <Card className="w-full max-w-md">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-semibold text-slate-700">Registrar Pago — {pagando.numero}</h3>
              <Btn variant="ghost" onClick={() => setPagando(null)}>✕</Btn>
            </div>
            {error && <Alert type="error" className="mb-3">{error}</Alert>}
            <p className="text-xs text-slate-500 mb-3">
              Saldo pendiente: <span className="font-bold text-red-600">Bs {Math.max(0, pagando.saldoPendiente ?? 0).toFixed(2)}</span>
            </p>
            <form onSubmit={handlePago} className="space-y-3">
              <Input
                label="Monto a pagar *"
                type="number"
                step="0.01"
                min="0.01"
                max={Math.max(0.01, pagando.saldoPendiente ?? 0)}
                value={formPago.monto}
                onChange={e => setFormPago({ ...formPago, monto: e.target.value })}
                required
              />
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Método de pago</label>
                <select value={formPago.metodoPago} onChange={e => setFormPago({ ...formPago, metodoPago: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
                  <option value="0">Efectivo</option>
                  <option value="1">Transferencia</option>
                  <option value="2">Cheque</option>
                  <option value="3">Tarjeta Débito</option>
                  <option value="4">Tarjeta Crédito</option>
                </select>
              </div>
              <Input
                label="Referencia / Nro. comprobante"
                value={formPago.referencia}
                onChange={e => setFormPago({ ...formPago, referencia: e.target.value })}
              />
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Observaciones</label>
                <textarea value={formPago.observaciones} onChange={e => setFormPago({ ...formPago, observaciones: e.target.value })}
                  rows={2} className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
              </div>
              <div className="flex gap-2 pt-1">
                <Btn type="submit" variant="primary" size="md" disabled={saving}>
                  {saving ? 'Registrando...' : 'Confirmar pago'}
                </Btn>
                <Btn variant="ghost" size="md" onClick={() => setPagando(null)}>Cancelar</Btn>
              </div>
            </form>
          </Card>
        </div>
      )}

      {/* Tabla */}
      {loading ? (
        <div className="flex items-center justify-center h-40 text-slate-400 text-sm">Cargando cobranzas...</div>
      ) : (
        <Table headers={['Número', 'Cliente', 'HP Origen', 'Total', 'Pagado', 'Saldo', 'Vencimiento', 'Estado', 'Acciones']}
          empty="Sin cobranzas registradas.">
          {cobranzas.map(c => (
            <Tr key={c.id}>
              <Td mono>{c.numero}</Td>
              <Td><span className="font-medium text-slate-800">{c.cliente?.nombreCompleto}</span></Td>
              <Td mono>{c.hojaPedido?.numero ?? <span className="text-slate-400">—</span>}</Td>
              <Td>Bs {c.montoTotal?.toFixed(2)}</Td>
              <Td><span className="text-green-600">Bs {c.montoPagado?.toFixed(2)}</span></Td>
              <Td><span className={c.saldoPendiente > 0 ? 'text-red-600 font-semibold' : 'text-slate-400'}>
                Bs {c.saldoPendiente?.toFixed(2)}
              </span></Td>
              <Td>
                <span className={leerFechaUtc(c.fechaVencimiento) < new Date() && c.estado !== 2 ? 'text-red-600 font-medium' : ''}>
                  {formatearFecha(c.fechaVencimiento)}
                </span>
              </Td>
              <Td><Badge color={ESTADO_COLOR[c.estado]}>{ESTADO_LABEL[c.estado]}</Badge></Td>
              <Td>
                <div className="flex gap-1.5">
                  <Btn variant="ghost" onClick={() => fetchDetalle(c.id)}>Ver</Btn>
                  {c.estado !== 2 && c.estado !== 4 && (
                    <Btn variant="primary" onClick={() => { abrirPago(c); setError(''); }}>Pagar</Btn>
                  )}
                </div>
              </Td>
            </Tr>
          ))}
        </Table>
      )}
    </Layout>
  );
}
