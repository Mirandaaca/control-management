import { useEffect, useState } from 'react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Badge, Btn, Input, Card, Alert } from '../components/ui';
import { formatearFecha } from '../utils/formato';

const ESTADO_COLOR = { 0: 'blue', 1: 'yellow', 2: 'green', 3: 'red' };
const ESTADO_LABEL = { 0: 'Registrado', 1: 'Revisado', 2: 'Resuelto', 3: 'Escalado' };
const PRIORIDAD_COLOR = { 0: 'gray', 1: 'yellow', 2: 'red' };
const PRIORIDAD_LABEL = { 0: 'Bajo', 1: 'Medio', 2: 'Alto' };

export default function Eventos() {
  const [eventos, setEventos] = useState([]);
  const [tipos, setTipos] = useState([]);
  const [clientes, setClientes] = useState([]);
  const [filtros, setFiltros] = useState({ tipoEventoId: '', estado: '', desde: '', hasta: '' });
  const [showForm, setShowForm] = useState(false);
  const [resolviendoId, setResolviendoId] = useState(null);
  const [motivoResolucion, setMotivoResolucion] = useState('');
  const [form, setForm] = useState({ tipoEventoId: '', clienteId: '', pedidoId: '', latitud: '', longitud: '', descripcion: '' });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  const fetchEventos = () => {
    const params = {};
    if (filtros.tipoEventoId) params.tipoEventoId = filtros.tipoEventoId;
    if (filtros.estado !== '') params.estado = filtros.estado;
    if (filtros.desde) params.desde = filtros.desde;
    if (filtros.hasta) params.hasta = filtros.hasta;
    return api.get('/eventos', { params }).then((r) => {
      setEventos(r.data.items);
      setLoading(false);
    });
  };

  useEffect(() => {
    Promise.all([
      api.get('/eventos/tipos').then(r => setTipos(r.data)),
      api.get('/clientes/opciones').then(r => setClientes(r.data)),
    ]);
    fetchEventos();
  }, []);

  const tipoSeleccionado = tipos.find(t => t.id === parseInt(form.tipoEventoId));

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true); setError('');
    try {
      await api.post('/eventos', {
        tipoEventoId: parseInt(form.tipoEventoId),
        clienteId: form.clienteId ? parseInt(form.clienteId) : null,
        pedidoId: form.pedidoId ? parseInt(form.pedidoId) : null,
        latitud: parseFloat(form.latitud) || 0,
        longitud: parseFloat(form.longitud) || 0,
        descripcion: form.descripcion,
      });

      setShowForm(false);
      setForm({ tipoEventoId: '', clienteId: '', pedidoId: '', latitud: '', longitud: '', descripcion: '' });
      fetchEventos();
    } catch (err) { setError(mensajeDeError(err, 'Error al registrar el evento.')); }
    finally { setSaving(false); }
  };

  const handleResolver = async () => {
    if (!motivoResolucion.trim()) return;
    await api.post(`/eventos/${resolviendoId}/resolver`, { observacion: motivoResolucion });
    setResolviendoId(null);
    setMotivoResolucion('');
    fetchEventos();
  };

  const pendientes = eventos.filter(e => e.estado === 0 || e.estado === 3).length;

  return (
    <Layout>
      <PageHeader
        title="Eventos y Evidencias"
        subtitle="Registro de incidencias en campo"
        action={
          <Btn variant="primary" size="md" onClick={() => { setShowForm(true); setError(''); }}>
            + Registrar evento
          </Btn>
        }
      />

      {pendientes > 0 && (
        <Alert type="warning" className="mb-5">
          ⚠ {pendientes} evento{pendientes > 1 ? 's' : ''} pendiente{pendientes > 1 ? 's' : ''} de revisión.
        </Alert>
      )}

      {/* Filtros */}
      <Card className="mb-5">
        <div className="flex flex-wrap gap-3 items-end">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Tipo</label>
            <select value={filtros.tipoEventoId} onChange={e => setFiltros({ ...filtros, tipoEventoId: e.target.value })}
              className="px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="">Todos</option>
              {tipos.map(t => <option key={t.id} value={t.id}>{t.nombre}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Estado</label>
            <select value={filtros.estado} onChange={e => setFiltros({ ...filtros, estado: e.target.value })}
              className="px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="">Todos</option>
              <option value="0">Registrado</option>
              <option value="1">Revisado</option>
              <option value="2">Resuelto</option>
              <option value="3">Escalado</option>
            </select>
          </div>
          <Input label="Desde" type="date" value={filtros.desde} onChange={e => setFiltros({ ...filtros, desde: e.target.value })} />
          <Input label="Hasta" type="date" value={filtros.hasta} onChange={e => setFiltros({ ...filtros, hasta: e.target.value })} />
          <Btn onClick={() => { setLoading(true); fetchEventos(); }} variant="primary" size="md">Filtrar</Btn>
        </div>
      </Card>

      {/* Formulario nuevo evento */}
      {showForm && (
        <Card className="mb-5 border-amber-100">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold text-slate-700">Registrar Incidencia</h3>
            <Btn variant="ghost" onClick={() => setShowForm(false)}>✕</Btn>
          </div>
          {error && <Alert type="error" className="mb-4">{error}</Alert>}
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Tipo de evento *</label>
                <select value={form.tipoEventoId} onChange={e => setForm({ ...form, tipoEventoId: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500" required>
                  <option value="">Seleccionar tipo...</option>
                  {tipos.map(t => (
                    <option key={t.id} value={t.id}>
                      {t.nombre} — {PRIORIDAD_LABEL[t.nivelPrioridad]}
                    </option>
                  ))}
                </select>
                {tipoSeleccionado && (
                  <p className="text-xs mt-1 flex gap-2">
                    <Badge color={PRIORIDAD_COLOR[tipoSeleccionado.nivelPrioridad]}>{PRIORIDAD_LABEL[tipoSeleccionado.nivelPrioridad]}</Badge>
                    {tipoSeleccionado.requiereEvidencia && <Badge color="yellow">Requiere foto</Badge>}
                  </p>
                )}
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Cliente</label>
                <select value={form.clienteId} onChange={e => setForm({ ...form, clienteId: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
                  <option value="">Sin asociar</option>
                  {clientes.map(c => <option key={c.id} value={c.id}>{c.nombreCompleto}</option>)}
                </select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <Input label="Latitud" type="number" step="any" placeholder="-17.783" value={form.latitud} onChange={e => setForm({ ...form, latitud: e.target.value })} />
              <Input label="Longitud" type="number" step="any" placeholder="-63.182" value={form.longitud} onChange={e => setForm({ ...form, longitud: e.target.value })} />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Descripción *</label>
              <textarea value={form.descripcion} onChange={e => setForm({ ...form, descripcion: e.target.value })} rows={3} required
                placeholder="Describe la incidencia con detalle..."
                className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
            </div>
            <div className="flex gap-2 pt-2">
              <Btn type="submit" variant="primary" size="md" disabled={saving}>
                {saving ? 'Registrando...' : 'Registrar evento'}
              </Btn>
              <Btn variant="ghost" size="md" onClick={() => setShowForm(false)}>Cancelar</Btn>
            </div>
          </form>
        </Card>
      )}

      {/* Modal resolución */}
      {resolviendoId && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
          <Card className="w-full max-w-md">
            <h3 className="text-sm font-semibold text-slate-700 mb-3">Marcar como Resuelto</h3>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Observación de resolución *</label>
              <textarea rows={3} value={motivoResolucion} onChange={e => setMotivoResolucion(e.target.value)}
                className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
                placeholder="¿Cómo se resolvió la incidencia?" />
            </div>
            <div className="flex gap-2 mt-3">
              <Btn variant="primary" size="md" onClick={handleResolver}>Confirmar resolución</Btn>
              <Btn variant="ghost" size="md" onClick={() => setResolviendoId(null)}>Cancelar</Btn>
            </div>
          </Card>
        </div>
      )}

      {/* Tabla */}
      {loading ? (
        <div className="flex items-center justify-center h-40 text-slate-400 text-sm">Cargando eventos...</div>
      ) : (
        <Table headers={['Número', 'Tipo', 'Prioridad', 'Cliente', 'Promotor', 'Estado', 'Evidencias', 'Fecha', 'Acciones']}
          empty="Sin eventos registrados.">
          {eventos.map(ev => (
            <Tr key={ev.id}>
              <Td mono>{ev.numero}</Td>
              <Td><span className="font-medium text-slate-800">{ev.tipo.nombre}</span></Td>
              <Td><Badge color={PRIORIDAD_COLOR[ev.tipo.nivelPrioridad]}>{PRIORIDAD_LABEL[ev.tipo.nivelPrioridad]}</Badge></Td>
              <Td>{ev.cliente?.nombreCompleto ?? <span className="text-slate-400">—</span>}</Td>
              <Td>{ev.promotor?.nombreCompleto}</Td>
              <Td><Badge color={ESTADO_COLOR[ev.estado]}>{ESTADO_LABEL[ev.estado]}</Badge></Td>
              <Td>
                {ev.cantidadFotos > 0
                  ? <Badge color="blue">{ev.cantidadFotos} archivo{ev.cantidadFotos > 1 ? 's' : ''}</Badge>
                  : <span className="text-slate-400 text-xs">Sin archivos</span>
                }
              </Td>
              <Td>{formatearFecha(ev.fechaEvento)}</Td>
              <Td>
                <div className="flex gap-1.5">
                  {ev.estado !== 2 && (
                    <Btn onClick={() => { setResolviendoId(ev.id); setMotivoResolucion(''); }} variant="ghost">
                      Resolver
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
