import { useEffect, useState } from 'react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Badge, Btn, Input, Card, Alert } from '../components/ui';
import { formatearFecha } from '../utils/formato';

const ESTADO_COLOR = { 0: 'gray', 1: 'green', 2: 'red' };
const ESTADO_LABEL = { 0: 'Borrador', 1: 'Confirmada', 2: 'Anulada' };
const TIPO_COLOR = { 0: 'blue', 1: 'yellow' };
const TIPO_LABEL = { 0: 'Contado', 1: 'Crédito' };

const EMPTY_FORM = { clienteId: '', zonaId: '', latitud: '', longitud: '', tipoVenta: 0, descuento: 0 };

export default function HojasPedido() {
  const [hps, setHps] = useState([]);
  const [clientes, setClientes] = useState([]);
  const [zonas, setZonas] = useState([]);
  const [productos, setProductos] = useState([]);
  const [form, setForm] = useState(EMPTY_FORM);
  const [detalles, setDetalles] = useState([{ productoId: '', cantidad: 1, descuento: 0 }]);
  const [showForm, setShowForm] = useState(false);
  const [showDetalle, setShowDetalle] = useState(null);
  const [anulandoId, setAnulandoId] = useState(null);
  const [motivoAnulacion, setMotivoAnulacion] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [filtros, setFiltros] = useState({ estado: '', desde: '', hasta: '' });

  const fetchHPs = () => {
    const params = {};
    if (filtros.estado !== '') params.estado = filtros.estado;
    if (filtros.desde) params.desde = filtros.desde;
    if (filtros.hasta) params.hasta = filtros.hasta;
    return api.get('/hojas-pedido', { params }).then((r) => {
      setHps(r.data);
      setLoading(false);
    });
  };

  useEffect(() => {
    Promise.all([
      api.get('/clientes').then(r => setClientes(r.data)),
      api.get('/zonas').then(r => setZonas(r.data)),
      api.get('/productos').then(r => setProductos(r.data)),
    ]);
    fetchHPs();
  }, []);

  const precioProducto = (id) => productos.find(p => p.id === parseInt(id))?.precio ?? 0;

  const calcSubtotal = (d) => {
    const precio = precioProducto(d.productoId);
    return ((precio - (parseFloat(d.descuento) || 0)) * (parseFloat(d.cantidad) || 0)).toFixed(2);
  };

  const totalHP = detalles.reduce((acc, d) => acc + parseFloat(calcSubtotal(d)), 0) - (parseFloat(form.descuento) || 0);

  const addDetalle = () => setDetalles([...detalles, { productoId: '', cantidad: 1, descuento: 0 }]);
  const removeDetalle = (i) => setDetalles(detalles.filter((_, idx) => idx !== i));
  const updateDetalle = (i, field, val) => {
    const next = [...detalles];
    next[i] = { ...next[i], [field]: val };
    setDetalles(next);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (detalles.some(d => !d.productoId)) { setError('Todos los detalles deben tener un producto.'); return; }
    setSaving(true);
    setError('');
    try {
      await api.post('/hojas-pedido', {
        clienteId: parseInt(form.clienteId),
        zonaId: parseInt(form.zonaId),
        latitud: parseFloat(form.latitud) || 0,
        longitud: parseFloat(form.longitud) || 0,
        tipoVenta: parseInt(form.tipoVenta),
        descuento: parseFloat(form.descuento) || 0,
        detalles: detalles.map(d => ({
          productoId: parseInt(d.productoId),
          cantidad: parseFloat(d.cantidad),
          descuento: parseFloat(d.descuento) || 0,
        })),
      });
      setShowForm(false);
      setForm(EMPTY_FORM);
      setDetalles([{ productoId: '', cantidad: 1, descuento: 0 }]);
      fetchHPs();
    } catch (err) {
      setError(mensajeDeError(err, 'Error al crear la HP.'));
    } finally {
      setSaving(false);
    }
  };

  const handleConfirmar = async (id) => {
    try {
      await api.post(`/hojas-pedido/${id}/confirmar`);
      fetchHPs();
    } catch (err) {
      alert(mensajeDeError(err, 'Error al confirmar HP.'));
    }
  };

  const handleAnular = async () => {
    if (!motivoAnulacion.trim()) return;
    try {
      await api.post(`/hojas-pedido/${anulandoId}/anular`, { motivo: motivoAnulacion });
      setAnulandoId(null);
      setMotivoAnulacion('');
      fetchHPs();
    } catch (err) {
      alert(mensajeDeError(err, 'Error al anular HP.'));
    }
  };

  const handleVerDetalle = async (id) => {
    const r = await api.get(`/hojas-pedido/${id}`);
    setShowDetalle(r.data);
  };

  return (
    <Layout>
      <PageHeader
        title="Hojas de Pedido"
        subtitle="Ventas geolocalizadas en campo"
        action={
          <Btn variant="primary" size="md" onClick={() => { setShowForm(true); setError(''); }}>
            + Nueva HP
          </Btn>
        }
      />

      {/* Filtros */}
      <Card className="mb-5">
        <div className="flex flex-wrap gap-3 items-end">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Estado</label>
            <select
              value={filtros.estado}
              onChange={e => setFiltros({ ...filtros, estado: e.target.value })}
              className="px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">Todos</option>
              <option value="0">Borrador</option>
              <option value="1">Confirmada</option>
              <option value="2">Anulada</option>
            </select>
          </div>
          <Input label="Desde" type="date" value={filtros.desde} onChange={e => setFiltros({ ...filtros, desde: e.target.value })} />
          <Input label="Hasta" type="date" value={filtros.hasta} onChange={e => setFiltros({ ...filtros, hasta: e.target.value })} />
          <Btn onClick={() => { setLoading(true); fetchHPs(); }} variant="primary" size="md">Filtrar</Btn>
        </div>
      </Card>

      {/* Formulario nueva HP */}
      {showForm && (
        <Card className="mb-5 border-blue-100">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold text-slate-700">Nueva Hoja de Pedido</h3>
            <Btn variant="ghost" onClick={() => setShowForm(false)}>✕</Btn>
          </div>
          {error && <Alert type="error" className="mb-4">{error}</Alert>}
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Cliente *</label>
                <select value={form.clienteId} onChange={e => setForm({ ...form, clienteId: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500" required>
                  <option value="">Seleccionar cliente...</option>
                  {clientes.map(c => <option key={c.id} value={c.id}>{c.nombreCompleto} — {c.codigo}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Zona *</label>
                <select value={form.zonaId} onChange={e => setForm({ ...form, zonaId: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500" required>
                  <option value="">Seleccionar zona...</option>
                  {zonas.map(z => <option key={z.id} value={z.id}>{z.nombre}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Tipo de venta</label>
                <select value={form.tipoVenta} onChange={e => setForm({ ...form, tipoVenta: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
                  <option value={0}>Contado</option>
                  <option value={1}>Crédito</option>
                </select>
              </div>
            </div>
            <div className="grid grid-cols-3 gap-3">
              <Input label="Latitud" type="number" step="any" placeholder="-17.783" value={form.latitud} onChange={e => setForm({ ...form, latitud: e.target.value })} />
              <Input label="Longitud" type="number" step="any" placeholder="-63.182" value={form.longitud} onChange={e => setForm({ ...form, longitud: e.target.value })} />
              <Input label="Descuento global (Bs.)" type="number" step="0.01" value={form.descuento} onChange={e => setForm({ ...form, descuento: e.target.value })} />
            </div>

            {/* Líneas de productos */}
            <div>
              <div className="flex items-center justify-between mb-2">
                <label className="text-xs font-medium text-slate-600">Productos *</label>
                <Btn type="button" variant="ghost" onClick={addDetalle}>+ Agregar línea</Btn>
              </div>
              <div className="space-y-2">
                {detalles.map((d, i) => (
                  <div key={i} className="grid grid-cols-12 gap-2 items-end">
                    <div className="col-span-5">
                      <select value={d.productoId} onChange={e => updateDetalle(i, 'productoId', e.target.value)}
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500" required>
                        <option value="">Producto...</option>
                        {productos.map(p => <option key={p.id} value={p.id}>{p.descripcion} — Bs.{p.precio}</option>)}
                      </select>
                    </div>
                    <div className="col-span-2">
                      <input type="number" min="0.001" step="0.001" placeholder="Cant." value={d.cantidad}
                        onChange={e => updateDetalle(i, 'cantidad', e.target.value)}
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" required />
                    </div>
                    <div className="col-span-2">
                      <input type="number" min="0" step="0.01" placeholder="Desc." value={d.descuento}
                        onChange={e => updateDetalle(i, 'descuento', e.target.value)}
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                    </div>
                    <div className="col-span-2 text-right">
                      <span className="text-sm font-semibold text-slate-700">Bs. {calcSubtotal(d)}</span>
                    </div>
                    <div className="col-span-1">
                      {detalles.length > 1 && (
                        <Btn type="button" variant="danger" onClick={() => removeDetalle(i)}>✕</Btn>
                      )}
                    </div>
                  </div>
                ))}
              </div>
              <div className="flex justify-end mt-3 pt-3 border-t border-slate-100">
                <span className="text-base font-bold text-slate-800">Total: Bs. {totalHP.toFixed(2)}</span>
              </div>
            </div>

            <div className="flex gap-2 pt-2">
              <Btn type="submit" variant="primary" size="md" disabled={saving}>
                {saving ? 'Creando...' : 'Crear HP (Borrador)'}
              </Btn>
              <Btn variant="ghost" size="md" onClick={() => setShowForm(false)}>Cancelar</Btn>
            </div>
          </form>
        </Card>
      )}

      {/* Modal anulación */}
      {anulandoId && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
          <Card className="w-96">
            <h3 className="text-sm font-semibold text-slate-700 mb-3">Anular Hoja de Pedido</h3>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Motivo de anulación *</label>
              <textarea rows={3} value={motivoAnulacion} onChange={e => setMotivoAnulacion(e.target.value)}
                className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
                placeholder="Describe el motivo..." />
            </div>
            <div className="flex gap-2 mt-3">
              <Btn variant="danger" size="md" onClick={handleAnular}>Confirmar anulación</Btn>
              <Btn variant="ghost" size="md" onClick={() => setAnulandoId(null)}>Cancelar</Btn>
            </div>
          </Card>
        </div>
      )}

      {/* Modal detalle */}
      {showDetalle && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <Card className="w-full max-w-lg">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="font-semibold text-slate-800">{showDetalle.numero}</h3>
                <p className="text-xs text-slate-400">{showDetalle.cliente?.nombreCompleto}</p>
              </div>
              <Btn variant="ghost" onClick={() => setShowDetalle(null)}>✕</Btn>
            </div>
            <div className="space-y-1 mb-4">
              {showDetalle.detalles?.map(d => (
                <div key={d.id} className="flex justify-between text-sm py-1 border-b border-slate-50">
                  <span>{d.producto?.descripcion} × {d.cantidad}</span>
                  <span className="font-medium">Bs. {d.subtotal?.toFixed(2)}</span>
                </div>
              ))}
            </div>
            <div className="flex justify-between font-bold text-slate-800 pt-2 border-t border-slate-200">
              <span>Total</span>
              <span>Bs. {showDetalle.total?.toFixed(2)}</span>
            </div>
          </Card>
        </div>
      )}

      {/* Tabla */}
      {loading ? (
        <div className="flex items-center justify-center h-40 text-slate-400 text-sm">Cargando...</div>
      ) : (
        <Table headers={['Número', 'Cliente', 'Promotor', 'Tipo', 'Total', 'Estado', 'Fecha', 'Acciones']}>
          {hps.map(hp => (
            <Tr key={hp.id}>
              <Td mono>{hp.numero}</Td>
              <Td><span className="font-medium">{hp.cliente?.nombreCompleto}</span></Td>
              <Td>{hp.promotor?.nombreCompleto}</Td>
              <Td><Badge color={TIPO_COLOR[hp.tipoVenta]}>{TIPO_LABEL[hp.tipoVenta]}</Badge></Td>
              <Td><span className="font-semibold text-slate-800">Bs. {hp.total?.toFixed(2)}</span></Td>
              <Td><Badge color={ESTADO_COLOR[hp.estadoHP]}>{ESTADO_LABEL[hp.estadoHP]}</Badge></Td>
              <Td>{formatearFecha(hp.fechaRegistro)}</Td>
              <Td>
                <div className="flex gap-1.5 flex-wrap">
                  <Btn onClick={() => handleVerDetalle(hp.id)} variant="ghost">Ver</Btn>
                  {hp.estadoHP === 0 && (
                    <Btn onClick={() => handleConfirmar(hp.id)} variant="primary">Confirmar</Btn>
                  )}
                  {hp.estadoHP !== 2 && (
                    <Btn onClick={() => { setAnulandoId(hp.id); setMotivoAnulacion(''); }} variant="danger">Anular</Btn>
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
