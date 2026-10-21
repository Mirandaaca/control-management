import { useEffect, useState } from 'react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Btn, Input, Card, Alert } from '../components/ui';
import { formatearFecha } from '../utils/formato';

export default function Pedidos() {
  const [pedidos, setPedidos] = useState([]);
  const [estados, setEstados] = useState([]);
  const [clientes, setClientes] = useState([]);
  const [productos, setProductos] = useState([]);
  const [filtros, setFiltros] = useState({ estadoId: '', desde: '', hasta: '' });
  const [showForm, setShowForm] = useState(false);
  const [detalle, setDetalle] = useState(null);
  const [cambioEstado, setCambioEstado] = useState(null);
  const [errorEstado, setErrorEstado] = useState('');
  const [form, setForm] = useState({ clienteId: '', fechaEntregaEstimada: '', direccionEntrega: '', descuento: 0, observaciones: '' });
  const [lineas, setLineas] = useState([{ productoId: '', cantidad: 1, descuento: 0 }]);
  const [estadoForm, setEstadoForm] = useState({ nuevoEstadoId: '', observacion: '', latitud: '', longitud: '' });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  const fetchPedidos = () => {
    const params = {};
    if (filtros.estadoId) params.estadoId = filtros.estadoId;
    if (filtros.desde) params.desde = filtros.desde;
    if (filtros.hasta) params.hasta = filtros.hasta;
    return api.get('/pedidos', { params }).then((r) => {
      setPedidos(r.data.items);
      setLoading(false);
    });
  };

  useEffect(() => {
    Promise.all([
      api.get('/pedidos/estados').then(r => setEstados(r.data)),
      api.get('/clientes/opciones').then(r => setClientes(r.data)),
      api.get('/productos/opciones').then(r => setProductos(r.data)),
    ]);
    fetchPedidos();
  }, []);

  const precioProducto = (id) => productos.find(p => p.id === parseInt(id))?.precio ?? 0;
  const calcSubtotal = (l) => ((precioProducto(l.productoId) - (parseFloat(l.descuento) || 0)) * (parseFloat(l.cantidad) || 0)).toFixed(2);
  const totalPedido = lineas.reduce((a, l) => a + parseFloat(calcSubtotal(l)), 0) - (parseFloat(form.descuento) || 0);

  const addLinea = () => setLineas([...lineas, { productoId: '', cantidad: 1, descuento: 0 }]);
  const removeLinea = (i) => setLineas(lineas.filter((_, idx) => idx !== i));
  const updateLinea = (i, f, v) => { const n = [...lineas]; n[i] = { ...n[i], [f]: v }; setLineas(n); };

  const handleCreate = async (e) => {
    e.preventDefault();
    setSaving(true); setError('');
    try {
      await api.post('/pedidos', {
        clienteId: parseInt(form.clienteId),
        fechaEntregaEstimada: form.fechaEntregaEstimada || null,
        direccionEntrega: form.direccionEntrega,
        descuento: parseFloat(form.descuento) || 0,
        observaciones: form.observaciones,
        detalles: lineas.map(l => ({ productoId: parseInt(l.productoId), cantidad: parseFloat(l.cantidad), descuento: parseFloat(l.descuento) || 0 })),
      });
      setShowForm(false);
      fetchPedidos();
    } catch (err) { setError(mensajeDeError(err, 'Error al crear pedido.')); }
    finally { setSaving(false); }
  };

  const handleCambiarEstado = async () => {
    if (!estadoForm.nuevoEstadoId) return;
    setErrorEstado('');
    try {
      if (parseInt(estadoForm.nuevoEstadoId) === 5) {
        await api.post(`/pedidos/${cambioEstado.id}/anular`, { motivo: estadoForm.observacion });
      } else {
        await api.post(`/pedidos/${cambioEstado.id}/avanzar`, { observacion: estadoForm.observacion });
      }
      setCambioEstado(null);
      fetchPedidos();
    } catch (err) { setErrorEstado(mensajeDeError(err, 'Transición no permitida.')); }
  };

  const cerrarCambioEstado = () => { setCambioEstado(null); setErrorEstado(''); };

  const handleVerDetalle = async (id) => {
    const r = await api.get(`/pedidos/${id}`);
    setDetalle(r.data);
  };

  const TRANSICIONES = { 1: [2, 5], 2: [3, 5], 3: [5] };
  const transicionesDisponibles = (pedido) => {
    const ids = TRANSICIONES[pedido?.estado?.id] ?? [];
    return estados.filter(e => ids.includes(e.id));
  };

  return (
    <Layout>
      <PageHeader
        title="Pedidos"
        subtitle={`${pedidos.length} pedidos registrados`}
        action={
          <Btn variant="primary" size="md" onClick={() => { setShowForm(true); setError(''); }}>
            + Nuevo pedido
          </Btn>
        }
      />

      {/* Filtros */}
      <Card className="mb-5">
        <div className="flex flex-wrap gap-3 items-end">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Estado</label>
            <select value={filtros.estadoId} onChange={e => setFiltros({ ...filtros, estadoId: e.target.value })}
              className="px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="">Todos</option>
              {estados.map(e => <option key={e.id} value={e.id}>{e.nombre}</option>)}
            </select>
          </div>
          <Input label="Desde" type="date" value={filtros.desde} onChange={e => setFiltros({ ...filtros, desde: e.target.value })} />
          <Input label="Hasta" type="date" value={filtros.hasta} onChange={e => setFiltros({ ...filtros, hasta: e.target.value })} />
          <Btn onClick={() => { setLoading(true); fetchPedidos(); }} variant="primary" size="md">Filtrar</Btn>
        </div>
      </Card>

      {/* Formulario */}
      {showForm && (
        <Card className="mb-5 border-blue-100">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold text-slate-700">Nuevo Pedido</h3>
            <Btn variant="ghost" onClick={() => setShowForm(false)}>✕</Btn>
          </div>
          {error && <Alert type="error" className="mb-4">{error}</Alert>}
          <form onSubmit={handleCreate} className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Cliente *</label>
                <select value={form.clienteId} onChange={e => setForm({ ...form, clienteId: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500" required>
                  <option value="">Seleccionar...</option>
                  {clientes.map(c => <option key={c.id} value={c.id}>{c.nombreCompleto}</option>)}
                </select>
              </div>
              <Input label="Dirección de entrega" value={form.direccionEntrega} onChange={e => setForm({ ...form, direccionEntrega: e.target.value })} />
              <Input label="Entrega" type="datetime-local" value={form.fechaEntregaEstimada} onChange={e => setForm({ ...form, fechaEntregaEstimada: e.target.value })} />
            </div>
            <div>
              <div className="flex items-center justify-between mb-2">
                <label className="text-xs font-medium text-slate-600">Productos *</label>
                <Btn type="button" variant="ghost" onClick={addLinea}>+ Línea</Btn>
              </div>
              {lineas.map((l, i) => (
                <div key={i} className="grid grid-cols-12 gap-2 mb-2 items-end">
                  <div className="col-span-5">
                    <select value={l.productoId} onChange={e => updateLinea(i, 'productoId', e.target.value)}
                      className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500" required>
                      <option value="">Producto...</option>
                      {productos.map(p => <option key={p.id} value={p.id}>{p.descripcion} — Bs.{p.precio}</option>)}
                    </select>
                  </div>
                  <div className="col-span-2">
                    <input type="number" min="0.001" step="0.001" placeholder="Cant." value={l.cantidad} onChange={e => updateLinea(i, 'cantidad', e.target.value)}
                      className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" required />
                  </div>
                  <div className="col-span-2">
                    <input type="number" min="0" step="0.01" placeholder="Desc." value={l.descuento} onChange={e => updateLinea(i, 'descuento', e.target.value)}
                      className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                  </div>
                  <div className="col-span-2 text-right text-sm font-semibold text-slate-700">Bs. {calcSubtotal(l)}</div>
                  <div className="col-span-1">{lineas.length > 1 && <Btn type="button" variant="danger" onClick={() => removeLinea(i)}>✕</Btn>}</div>
                </div>
              ))}
              <div className="flex justify-end mt-2 pt-2 border-t border-slate-100">
                <span className="font-bold text-slate-800">Total: Bs. {totalPedido.toFixed(2)}</span>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <Input label="Descuento global" type="number" step="0.01" value={form.descuento} onChange={e => setForm({ ...form, descuento: e.target.value })} />
              <Input label="Observaciones" value={form.observaciones} onChange={e => setForm({ ...form, observaciones: e.target.value })} />
            </div>
            <div className="flex gap-2 pt-2">
              <Btn type="submit" variant="primary" size="md" disabled={saving}>{saving ? 'Creando...' : 'Crear pedido'}</Btn>
              <Btn variant="ghost" size="md" onClick={() => setShowForm(false)}>Cancelar</Btn>
            </div>
          </form>
        </Card>
      )}

      {/* Modal cambio de estado */}
      {cambioEstado && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <Card className="w-full max-w-md">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-semibold text-slate-700">Cambiar estado — {cambioEstado.numero}</h3>
              <Btn variant="ghost" onClick={cerrarCambioEstado}>✕</Btn>
            </div>
            <p className="text-xs text-slate-500 mb-3">
              Estado actual: <span className="font-semibold">{cambioEstado.estado?.nombre}</span>
            </p>
            <div className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Nuevo estado *</label>
                <select value={estadoForm.nuevoEstadoId} onChange={e => setEstadoForm({ ...estadoForm, nuevoEstadoId: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
                  <option value="">Seleccionar...</option>
                  {transicionesDisponibles(cambioEstado).map(e => (
                    <option key={e.id} value={e.id}>{e.nombre}</option>
                  ))}
                </select>
              </div>
              <Input label="Observación" value={estadoForm.observacion} onChange={e => setEstadoForm({ ...estadoForm, observacion: e.target.value })} placeholder="Motivo del cambio..." />
            </div>
            {errorEstado && <Alert type="error" className="mt-4">{errorEstado}</Alert>}
            <div className="flex gap-2 mt-4">
              <Btn variant="primary" size="md" onClick={handleCambiarEstado}>Confirmar cambio</Btn>
              <Btn variant="ghost" size="md" onClick={cerrarCambioEstado}>Cancelar</Btn>
            </div>
          </Card>
        </div>
      )}

      {/* Modal detalle */}
      {detalle && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <Card className="w-full max-w-lg max-h-[80vh] overflow-y-auto">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="font-semibold text-slate-800">{detalle.numero}</h3>
                <p className="text-xs text-slate-400">{detalle.cliente?.nombreCompleto}</p>
              </div>
              <Btn variant="ghost" onClick={() => setDetalle(null)}>✕</Btn>
            </div>
            <div className="space-y-1 mb-4">
              {detalle.detalles?.map(d => (
                <div key={d.id} className="flex justify-between text-sm py-1.5 border-b border-slate-50">
                  <span>{d.descripcion} × {d.cantidad}</span>
                  <span className="font-medium">Bs. {d.subtotal?.toFixed(2)}</span>
                </div>
              ))}
            </div>
            <div className="flex justify-between font-bold text-slate-800 py-2 border-t border-slate-200 mb-4">
              <span>Total</span><span>Bs. {detalle.total?.toFixed(2)}</span>
            </div>
            <h4 className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-2">Historial de estados</h4>
            <div className="space-y-2">
              {detalle.historial?.map(h => (
                <div key={h.id} className="flex items-start gap-2 text-xs">
                  <span className="w-2 h-2 rounded-full mt-1 shrink-0 bg-slate-400" />
                  <div>
                    <span className="font-medium">{h.estado?.nombre}</span>
                    <span className="text-slate-400 ml-2">{formatearFecha(h.fecha, { hora: true })}</span>
                    {h.observacion && <p className="text-slate-500">{h.observacion}</p>}
                  </div>
                </div>
              ))}
            </div>
          </Card>
        </div>
      )}

      {/* Tabla */}
      {loading ? (
        <div className="flex items-center justify-center h-40 text-slate-400 text-sm">Cargando pedidos...</div>
      ) : (
        <Table headers={['Número', 'Cliente', 'Zona', 'Total', 'Estado', 'Fecha', 'Acciones']}>
          {pedidos.map(p => (
            <Tr key={p.id}>
              <Td mono>{p.numero}</Td>
              <Td><span className="font-medium text-slate-800">{p.cliente?.nombreCompleto}</span></Td>
              <Td>{p.zona?.nombre}</Td>
              <Td><span className="font-semibold">Bs. {p.total?.toFixed(2)}</span></Td>
              <Td>
                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-slate-100 text-slate-700">
                  {p.estado?.nombre}
                </span>
              </Td>
              <Td>{formatearFecha(p.fechaRegistro)}</Td>
              <Td>
                <div className="flex gap-1.5">
                  <Btn onClick={() => handleVerDetalle(p.id)} variant="ghost">Ver</Btn>
                  {![4, 5].includes(p.estado?.id) && (
                    <Btn onClick={() => { setCambioEstado(p); setEstadoForm({ nuevoEstadoId: '', observacion: '', latitud: '', longitud: '' }); }} variant="outline">
                      Cambiar estado
                    </Btn>
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
