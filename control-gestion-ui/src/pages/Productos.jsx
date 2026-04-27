import { useEffect, useState } from 'react';
import api from '../api/axios';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Badge, Btn, Input, Card } from '../components/ui';

const EMPTY = { sku: '', descripcion: '', precio: '', categoria: '' };

export default function Productos() {
  const [productos, setProductos] = useState([]);
  const [form, setForm] = useState(EMPTY);
  const [editId, setEditId] = useState(null);
  const [saving, setSaving] = useState(false);

  const fetchProductos = () => api.get('/productos').then((r) => setProductos(r.data));
  useEffect(() => { fetchProductos(); }, []);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    const payload = { ...form, precio: parseFloat(form.precio), activo: true };
    try {
      if (editId) {
        await api.put(`/productos/${editId}`, { id: editId, ...payload });
        setEditId(null);
      } else {
        await api.post('/productos', payload);
      }
      setForm(EMPTY);
      fetchProductos();
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = (p) => {
    setEditId(p.id);
    setForm({ sku: p.sku, descripcion: p.descripcion, precio: String(p.precio), categoria: p.categoria || '' });
  };
  const handleCancel = () => { setEditId(null); setForm(EMPTY); };
  const handleDelete = async (id) => {
    if (!confirm('¿Desactivar este producto?')) return;
    await api.delete(`/productos/${id}`);
    fetchProductos();
  };

  return (
    <Layout>
      <PageHeader title="Productos" subtitle="Catálogo maestro de inventario" />

      <Card className="mb-6">
        <h3 className="text-sm font-semibold text-slate-700 mb-4">
          {editId ? 'Editar producto' : 'Nuevo producto'}
        </h3>
        <form onSubmit={handleSubmit} className="grid grid-cols-2 md:grid-cols-4 gap-3 items-end">
          <Input label="SKU" placeholder="PROD-001" value={form.sku} onChange={(e) => setForm({ ...form, sku: e.target.value })} required />
          <Input label="Descripción" placeholder="Nombre del producto" value={form.descripcion} onChange={(e) => setForm({ ...form, descripcion: e.target.value })} required />
          <Input label="Precio" type="number" step="0.01" placeholder="0.00" value={form.precio} onChange={(e) => setForm({ ...form, precio: e.target.value })} required />
          <Input label="Categoría" placeholder="Bebidas, Snacks..." value={form.categoria} onChange={(e) => setForm({ ...form, categoria: e.target.value })} />
          <div className="col-span-2 md:col-span-4 flex gap-2">
            <Btn type="submit" variant="primary" size="md" disabled={saving}>
              {editId ? 'Actualizar' : 'Agregar producto'}
            </Btn>
            {editId && <Btn onClick={handleCancel} variant="ghost" size="md">Cancelar</Btn>}
          </div>
        </form>
      </Card>

      <Table headers={['SKU', 'Descripción', 'Precio', 'Categoría', 'Acciones']}>
        {productos.map((p) => (
          <Tr key={p.id}>
            <Td mono>{p.sku}</Td>
            <Td><span className="font-medium text-slate-800">{p.descripcion}</span></Td>
            <Td>
              <span className="font-semibold text-slate-800">
                Bs. {p.precio?.toFixed(2)}
              </span>
            </Td>
            <Td>
              {p.categoria
                ? <Badge color="blue">{p.categoria}</Badge>
                : <span className="text-slate-400 text-xs">Sin categoría</span>
              }
            </Td>
            <Td>
              <div className="flex gap-2">
                <Btn onClick={() => handleEdit(p)} variant="ghost">Editar</Btn>
                <Btn onClick={() => handleDelete(p.id)} variant="danger">Desactivar</Btn>
              </div>
            </Td>
          </Tr>
        ))}
      </Table>
    </Layout>
  );
}
