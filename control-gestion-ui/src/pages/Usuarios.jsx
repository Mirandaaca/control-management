import { useEffect, useState } from 'react';
import api from '../api/axios';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PageHeader, Badge, Btn } from '../components/ui';

export default function Usuarios() {
  const [usuarios, setUsuarios] = useState([]);
  const [loading, setLoading] = useState(true);

  const fetchUsuarios = () => {
    api.get('/usuarios')
      .then((r) => setUsuarios(r.data))
      .finally(() => setLoading(false));
  };

  useEffect(() => { fetchUsuarios(); }, []);

  const toggleActivo = async (id) => {
    await api.put(`/usuarios/${id}/activar`);
    fetchUsuarios();
  };

  const eliminar = async (id) => {
    if (!confirm('¿Eliminar este usuario? Esta acción no se puede deshacer.')) return;
    await api.delete(`/usuarios/${id}`);
    fetchUsuarios();
  };

  return (
    <Layout>
      <PageHeader
        title="Usuarios"
        subtitle="Gestión de acceso y roles del sistema"
      />

      {loading ? (
        <div className="flex items-center justify-center h-40 text-slate-400 text-sm">Cargando usuarios...</div>
      ) : (
        <Table headers={['Usuario', 'Email', 'Roles', 'Estado', 'Registro', 'Acciones']}>
          {usuarios.map((u) => (
            <Tr key={u.id}>
              <Td>
                <div className="flex items-center gap-2.5">
                  <div className="w-8 h-8 rounded-full bg-slate-200 flex items-center justify-center text-xs font-bold text-slate-600 shrink-0">
                    {u.nombreCompleto?.[0]?.toUpperCase()}
                  </div>
                  <span className="font-medium text-slate-800">{u.nombreCompleto}</span>
                </div>
              </Td>
              <Td>{u.email}</Td>
              <Td>
                <div className="flex gap-1 flex-wrap">
                  {u.roles?.map((r) => (
                    <Badge key={r} color={r === 'Admin' ? 'purple' : r === 'Supervisor' ? 'blue' : 'gray'}>
                      {r}
                    </Badge>
                  ))}
                </div>
              </Td>
              <Td>
                <Badge color={u.activo ? 'green' : 'red'}>
                  {u.activo ? 'Activo' : 'Inactivo'}
                </Badge>
              </Td>
              <Td>{new Date(u.fechaCreacion).toLocaleDateString()}</Td>
              <Td>
                <div className="flex items-center gap-2">
                  <Btn onClick={() => toggleActivo(u.id)} variant="ghost">
                    {u.activo ? 'Desactivar' : 'Activar'}
                  </Btn>
                  <Btn onClick={() => eliminar(u.id)} variant="danger">
                    Eliminar
                  </Btn>
                </div>
              </Td>
            </Tr>
          ))}
        </Table>
      )}
    </Layout>
  );
}
