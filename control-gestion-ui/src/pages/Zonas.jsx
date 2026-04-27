import { useEffect, useState } from 'react';
import api from '../api/axios';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Btn, Input, Card } from '../components/ui';

const EMPTY = { nombre: '', codigo: '' };

export default function Zonas() {
  const [zonas, setZonas] = useState([]);
  const [form, setForm] = useState(EMPTY);
  const [editId, setEditId] = useState(null);
  const [saving, setSaving] = useState(false);

  const fetchZonas = () => api.get('/zonas').then((r) => setZonas(r.data));
  useEffect(() => { fetchZonas(); }, []);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (editId) {
        await api.put(`/zonas/${editId}`, { id: editId, ...form });
        setEditId(null);
      } else {
        await api.post('/zonas', form);
      }
      setForm(EMPTY);
      fetchZonas();
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = (z) => { setEditId(z.id); setForm({ nombre: z.nombre, codigo: z.codigo }); };
  const handleCancel = () => { setEditId(null); setForm(EMPTY); };
  const handleDelete = async (id) => {
    if (!confirm('¿Eliminar esta zona?')) return;
    await api.delete(`/zonas/${id}`);
    fetchZonas();
  };

  return (
    <Layout>
      <PageHeader title="Zonas" subtitle="Perímetros y áreas de trabajo" />

      <Card className="mb-6">
        <h3 className="text-sm font-semibold text-slate-700 mb-4">
          {editId ? 'Editar zona' : 'Nueva zona'}
        </h3>
        <form onSubmit={handleSubmit} className="flex flex-wrap gap-3 items-end">
          <div className="flex-1 min-w-[180px]">
            <Input
              label="Nombre"
              placeholder="Zona Norte"
              value={form.nombre}
              onChange={(e) => setForm({ ...form, nombre: e.target.value })}
              required
            />
          </div>
          <div className="flex-1 min-w-[140px]">
            <Input
              label="Código"
              placeholder="ZN-01"
              value={form.codigo}
              onChange={(e) => setForm({ ...form, codigo: e.target.value })}
              required
            />
          </div>
          <div className="flex gap-2">
            <Btn type="submit" variant="primary" size="md" disabled={saving}>
              {editId ? 'Actualizar' : 'Agregar'}
            </Btn>
            {editId && (
              <Btn onClick={handleCancel} variant="ghost" size="md">
                Cancelar
              </Btn>
            )}
          </div>
        </form>
      </Card>

      <Table headers={['ID', 'Nombre', 'Código', 'Supervisor', 'Acciones']}>
        {zonas.map((z) => (
          <Tr key={z.id}>
            <Td mono>{z.id}</Td>
            <Td><span className="font-medium text-slate-800">{z.nombre}</span></Td>
            <Td mono>{z.codigo}</Td>
            <Td>{z.supervisor?.nombreCompleto ?? <span className="text-slate-400">Sin asignar</span>}</Td>
            <Td>
              <div className="flex gap-2">
                <Btn onClick={() => handleEdit(z)} variant="ghost">Editar</Btn>
                <Btn onClick={() => handleDelete(z.id)} variant="danger">Eliminar</Btn>
              </div>
            </Td>
          </Tr>
        ))}
      </Table>
    </Layout>
  );
}
