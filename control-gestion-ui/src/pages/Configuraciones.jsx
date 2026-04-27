import { useEffect, useState } from 'react';
import api from '../api/axios';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Btn, Input, Card } from '../components/ui';

const EMPTY = { clave: '', valor: '', descripcion: '' };

export default function Configuraciones() {
  const [configs, setConfigs] = useState([]);
  const [form, setForm] = useState(EMPTY);
  const [saving, setSaving] = useState(false);

  const fetchConfigs = () => api.get('/configuraciones').then((r) => setConfigs(r.data));
  useEffect(() => { fetchConfigs(); }, []);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      await api.put(`/configuraciones/${form.clave}`, form);
      setForm(EMPTY);
      fetchConfigs();
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = (c) => setForm({ clave: c.clave, valor: c.valor, descripcion: c.descripcion || '' });

  const handleDelete = async (clave) => {
    if (!confirm(`¿Eliminar la configuración "${clave}"?`)) return;
    await api.delete(`/configuraciones/${clave}`);
    fetchConfigs();
  };

  return (
    <Layout>
      <PageHeader
        title="Configuraciones"
        subtitle="Parámetros globales del sistema"
      />

      <Card className="mb-6">
        <h3 className="text-sm font-semibold text-slate-700 mb-4">
          {form.clave ? `Editando: ${form.clave}` : 'Nueva configuración'}
        </h3>
        <form onSubmit={handleSubmit} className="flex flex-wrap gap-3 items-end">
          <div className="w-48">
            <Input
              label="Clave"
              placeholder="dias_revisita"
              value={form.clave}
              onChange={(e) => setForm({ ...form, clave: e.target.value })}
              required
            />
          </div>
          <div className="w-36">
            <Input
              label="Valor"
              placeholder="15"
              value={form.valor}
              onChange={(e) => setForm({ ...form, valor: e.target.value })}
              required
            />
          </div>
          <div className="flex-1 min-w-[200px]">
            <Input
              label="Descripción (opcional)"
              placeholder="Días mínimos entre re-visitas"
              value={form.descripcion}
              onChange={(e) => setForm({ ...form, descripcion: e.target.value })}
            />
          </div>
          <div className="flex gap-2">
            <Btn type="submit" variant="primary" size="md" disabled={saving}>
              Guardar
            </Btn>
            {form.clave && (
              <Btn onClick={() => setForm(EMPTY)} variant="ghost" size="md">
                Limpiar
              </Btn>
            )}
          </div>
        </form>
        <p className="text-xs text-slate-400 mt-3">
          Ejemplos de claves: <code className="bg-slate-100 px-1 rounded">dias_revisita</code>,{' '}
          <code className="bg-slate-100 px-1 rounded">iva_porcentaje</code>,{' '}
          <code className="bg-slate-100 px-1 rounded">limite_credito_default</code>
        </p>
      </Card>

      <Table headers={['Clave', 'Valor', 'Descripción', 'Acciones']} empty="Sin configuraciones registradas.">
        {configs.map((c) => (
          <Tr key={c.clave}>
            <Td mono>{c.clave}</Td>
            <Td>
              <span className="font-semibold text-slate-800">{c.valor}</span>
            </Td>
            <Td>{c.descripcion || <span className="text-slate-400">—</span>}</Td>
            <Td>
              <div className="flex gap-2">
                <Btn onClick={() => handleEdit(c)} variant="ghost">Editar</Btn>
                <Btn onClick={() => handleDelete(c.clave)} variant="danger">Eliminar</Btn>
              </div>
            </Td>
          </Tr>
        ))}
      </Table>
    </Layout>
  );
}
