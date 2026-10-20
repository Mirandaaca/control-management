import { useEffect, useState } from 'react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Badge, Btn, Input, Card, Alert } from '../components/ui';
import { leerFechaUtc } from '../utils/formato';
import { useAuth } from '../context/auth';
import { ELIGEN_PROMOTOR } from '../context/permisos';

const EMPTY = {
  nombreCompleto: '', razonSocial: '', nit: '', telefono: '', email: '',
  direccionTexto: '', latitud: '', longitud: '', zonaId: '',
  promotorId: '', diasRevisita: 15, tieneCredito: false, limiteCredito: 0,
};

export default function Clientes() {
  const { tiene } = useAuth();
  // La lista de promotores es solo para quien asigna; el promotor registra clientes sin elegir a nadie.
  const eligePromotor = tiene(...ELIGEN_PROMOTOR);
  const [clientes, setClientes] = useState([]);
  const [zonas, setZonas] = useState([]);
  const [estados, setEstados] = useState([]);
  const [promotores, setPromotores] = useState([]);
  const [filtros, setFiltros] = useState({ zonaId: '', estadoId: '', soloRevisita: false });
  const [form, setForm] = useState(EMPTY);
  const [editId, setEditId] = useState(null);
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  const fetchClientes = () => {
    const params = {};
    if (filtros.zonaId) params.zonaId = filtros.zonaId;
    if (filtros.estadoId) params.estadoId = filtros.estadoId;
    if (filtros.soloRevisita) params.soloRevisita = true;
    return api.get('/clientes', { params }).then((r) => {
      setClientes(r.data);
      setLoading(false);
    });
  };

  useEffect(() => {
    Promise.all([
      api.get('/zonas').then(r => setZonas(r.data)),
      api.get('/clientes/estados').then(r => setEstados(r.data)),
      eligePromotor && api.get('/usuarios/promotores').then(r => setPromotores(r.data))
        .catch(err => setError(mensajeDeError(err, 'No se pudo cargar la lista de promotores.'))),
    ]);
    fetchClientes();
  }, [eligePromotor]);

  const handleFiltrar = (e) => { e.preventDefault(); setLoading(true); fetchClientes(); };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      const payload = {
        ...form,
        latitud: parseFloat(form.latitud) || 0,
        longitud: parseFloat(form.longitud) || 0,
        zonaId: parseInt(form.zonaId),
        limiteCredito: parseFloat(form.limiteCredito) || 0,
        diasRevisita: parseInt(form.diasRevisita) || 15,
        estadoClienteId: 1,
      };
      if (editId) {
        await api.put(`/clientes/${editId}`, payload);
      } else {
        await api.post('/clientes', payload);
      }
      setShowForm(false);
      setForm(EMPTY);
      setEditId(null);
      fetchClientes();
    } catch (err) {
      setError(mensajeDeError(err, 'Error al guardar el cliente.'));
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = (c) => {
    setEditId(c.id);
    setForm({
      nombreCompleto: c.nombreCompleto, razonSocial: c.razonSocial ?? '',
      nit: c.nit ?? '', telefono: c.telefono, email: c.email ?? '',
      direccionTexto: c.direccionTexto, latitud: String(c.latitud), longitud: String(c.longitud),
      zonaId: String(c.zonaId), promotorId: c.promotorId ?? '',
      diasRevisita: c.diasRevisita, tieneCredito: c.tieneCredito,
      limiteCredito: c.limiteCredito, estadoClienteId: c.estadoClienteId,
    });
    setShowForm(true);
  };

  const handleDelete = async (id) => {
    if (!confirm('¿Desactivar este cliente?')) return;
    await api.delete(`/clientes/${id}`);
    fetchClientes();
  };

  const proximaRevisitaLabel = (fecha) => {
    if (!fecha) return null;
    const dias = Math.ceil((leerFechaUtc(fecha) - new Date()) / 86400000);
    if (dias < 0) return <Badge color="red">Vencida {Math.abs(dias)}d</Badge>;
    if (dias === 0) return <Badge color="yellow">Hoy</Badge>;
    if (dias <= 3) return <Badge color="yellow">En {dias}d</Badge>;
    return <span className="text-xs text-slate-400">En {dias}d</span>;
  };

  return (
    <Layout>
      <PageHeader
        title="Clientes"
        subtitle={`${clientes.length} clientes registrados`}
        action={
          <Btn variant="primary" size="md" onClick={() => { setShowForm(true); setEditId(null); setForm(EMPTY); }}>
            + Nuevo cliente
          </Btn>
        }
      />

      {/* Filtros */}
      <Card className="mb-5">
        <form onSubmit={handleFiltrar} className="flex flex-wrap gap-3 items-end">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Zona</label>
            <select
              value={filtros.zonaId}
              onChange={(e) => setFiltros({ ...filtros, zonaId: e.target.value })}
              className="px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">Todas</option>
              {zonas.map(z => <option key={z.id} value={z.id}>{z.nombre}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Estado</label>
            <select
              value={filtros.estadoId}
              onChange={(e) => setFiltros({ ...filtros, estadoId: e.target.value })}
              className="px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">Todos</option>
              {estados.map(e => <option key={e.id} value={e.id}>{e.nombre}</option>)}
            </select>
          </div>
          <label className="flex items-center gap-2 text-sm text-slate-600 cursor-pointer pb-1">
            <input
              type="checkbox"
              checked={filtros.soloRevisita}
              onChange={(e) => setFiltros({ ...filtros, soloRevisita: e.target.checked })}
              className="rounded"
            />
            Solo pendientes de re-visita
          </label>
          <Btn type="submit" variant="primary" size="md">Filtrar</Btn>
        </form>
      </Card>

      {/* Formulario */}
      {showForm && (
        <Card className="mb-5 border-blue-100">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold text-slate-700">
              {editId ? 'Editar cliente' : 'Nuevo cliente'}
            </h3>
            <Btn variant="ghost" onClick={() => { setShowForm(false); setError(''); }}>✕</Btn>
          </div>
          {error && <Alert type="error" className="mb-4">{error}</Alert>}
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <Input label="Nombre completo *" value={form.nombreCompleto} onChange={e => setForm({ ...form, nombreCompleto: e.target.value })} required />
              <Input label="Razón social" value={form.razonSocial} onChange={e => setForm({ ...form, razonSocial: e.target.value })} />
              <Input label="NIT / CI" value={form.nit} onChange={e => setForm({ ...form, nit: e.target.value })} />
            </div>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <Input label="Teléfono *" value={form.telefono} onChange={e => setForm({ ...form, telefono: e.target.value })} required />
              <Input label="Email" type="email" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} />
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Zona *</label>
                <select
                  value={form.zonaId}
                  onChange={e => setForm({ ...form, zonaId: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                  required
                >
                  <option value="">Seleccionar...</option>
                  {zonas.map(z => <option key={z.id} value={z.id}>{z.nombre}</option>)}
                </select>
              </div>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {eligePromotor && <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Promotor asignado</label>
                <select value={form.promotorId} onChange={e => setForm({ ...form, promotorId: e.target.value })}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
                  <option value="">Sin asignar</option>
                  {promotores.map(p => <option key={p.id} value={p.id}>{p.nombreCompleto}</option>)}
                </select>
              </div>}
            </div>
            <Input label="Dirección *" value={form.direccionTexto} onChange={e => setForm({ ...form, direccionTexto: e.target.value })} required />
            <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
              <Input label="Latitud" type="number" step="any" placeholder="-17.783" value={form.latitud} onChange={e => setForm({ ...form, latitud: e.target.value })} />
              <Input label="Longitud" type="number" step="any" placeholder="-63.182" value={form.longitud} onChange={e => setForm({ ...form, longitud: e.target.value })} />
              <Input label="Días de re-visita" type="number" min="1" value={form.diasRevisita} onChange={e => setForm({ ...form, diasRevisita: e.target.value })} />
              <div className="flex flex-col gap-1">
                <label className="text-xs font-medium text-slate-600">Crédito</label>
                <label className="flex items-center gap-2 text-sm cursor-pointer mt-2">
                  <input type="checkbox" checked={form.tieneCredito} onChange={e => setForm({ ...form, tieneCredito: e.target.checked })} className="rounded" />
                  Habilitar crédito
                </label>
              </div>
            </div>
            {form.tieneCredito && (
              <div className="w-48">
                <Input label="Límite de crédito (Bs.)" type="number" step="0.01" value={form.limiteCredito} onChange={e => setForm({ ...form, limiteCredito: e.target.value })} />
              </div>
            )}
            <div className="flex gap-2 pt-2">
              <Btn type="submit" variant="primary" size="md" disabled={saving}>
                {saving ? 'Guardando...' : editId ? 'Actualizar' : 'Crear cliente'}
              </Btn>
              <Btn variant="ghost" size="md" onClick={() => { setShowForm(false); setError(''); }}>
                Cancelar
              </Btn>
            </div>
          </form>
        </Card>
      )}

      {/* Tabla */}
      {loading ? (
        <div className="flex items-center justify-center h-40 text-slate-400 text-sm">Cargando clientes...</div>
      ) : (
        <Table headers={['Cliente', 'Zona', 'Estado', 'Teléfono', 'Crédito', 'Próxima visita', 'Acciones']}
          empty="Sin clientes. Agrega el primero.">
          {clientes.map(c => (
            <Tr key={c.id}>
              <Td>
                <div>
                  <p className="font-medium text-slate-800">{c.nombreCompleto}</p>
                  <p className="text-xs text-slate-400 font-mono">{c.codigo}</p>
                </div>
              </Td>
              <Td>{c.zona?.nombre ?? '—'}</Td>
              <Td>
                <span
                  className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium"
                  style={{ backgroundColor: c.estadoCliente?.color + '22', color: c.estadoCliente?.color }}
                >
                  {c.estadoCliente?.nombre}
                </span>
              </Td>
              <Td>{c.telefono}</Td>
              <Td>
                {c.tieneCredito
                  ? <div>
                      <Badge color="blue">Habilitado</Badge>
                      <p className="text-xs text-slate-400 mt-0.5">Bs. {c.saldoPendiente?.toFixed(2)} / {c.limiteCredito?.toFixed(2)}</p>
                    </div>
                  : <span className="text-slate-400 text-xs">Sin crédito</span>
                }
              </Td>
              <Td>{proximaRevisitaLabel(c.proximaVisita)}</Td>
              <Td>
                <div className="flex gap-2">
                  <Btn onClick={() => handleEdit(c)} variant="ghost">Editar</Btn>
                  <Btn onClick={() => handleDelete(c.id)} variant="danger">Desactivar</Btn>
                </div>
              </Td>
            </Tr>
          ))}
        </Table>
      )}
    </Layout>
  );
}
