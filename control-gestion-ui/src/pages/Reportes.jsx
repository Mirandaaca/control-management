import { useState } from 'react';
import Layout from '../components/Layout';
import Pestanas from '../components/reportes/Pestanas';
import ReporteClientes from '../components/reportes/ReporteClientes';
import ReporteCobranzas from '../components/reportes/ReporteCobranzas';
import ReporteInventario from '../components/reportes/ReporteInventario';
import ReporteVentasPromotor from '../components/reportes/ReporteVentasPromotor';
import ReporteVentasZona from '../components/reportes/ReporteVentasZona';
import { PageHeader } from '../components/ui';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';

const { reportes, cobranzas } = PERMISOS;

// Los atributos del contenido de una pestaña, enlazado con su botón.
const propsDelPanel = (id) => ({ role: 'tabpanel', id: `panel-${id}`, 'aria-labelledby': `pestana-${id}` });

// Las pestañas del 8.6.4, cada una con el permiso de su reporte: así nadie abre una que la API le va a negar. Ventas es
// la 6.2 (ventas por zona); Promotores, la 6.1; Clientes, la 6.3.
const PESTANAS = [
  { id: 'ventas', nombre: 'Ventas', permiso: reportes.ventasPorZona, Reporte: ReporteVentasZona },
  { id: 'cobranzas', nombre: 'Cobranzas', permiso: cobranzas.generarReporte, Reporte: ReporteCobranzas },
  { id: 'inventario', nombre: 'Inventario', permiso: reportes.inventario, Reporte: ReporteInventario },
  { id: 'promotores', nombre: 'Promotores', permiso: reportes.ventasPorPromotor, Reporte: ReporteVentasPromotor },
  { id: 'clientes', nombre: 'Clientes', permiso: reportes.clientesNuevosRecurrentes, Reporte: ReporteClientes },
];

/**
 * Reportes (8.6.4). Cada pestaña se carga la primera vez que se abre y después queda montada, escondida: al volver a
 * ella, sus filtros siguen como estaban.
 */
export default function Reportes() {
  const { tiene } = useAuth();
  const visibles = PESTANAS.filter((p) => tiene(p.permiso));
  const [activa, setActiva] = useState(() => visibles[0]?.id);
  const [abiertas, setAbiertas] = useState(() => [visibles[0]?.id]);

  const cambiar = (id) => {
    setActiva(id);
    setAbiertas((a) => (a.includes(id) ? a : [...a, id]));
  };
  const barra = (accion) => <Pestanas pestanas={visibles} activa={activa} onCambiar={cambiar} accion={accion} />;

  return (
    <Layout>
      <PageHeader title="Reportes" subtitle="Análisis y exportación de datos" />
      {visibles.filter((p) => abiertas.includes(p.id)).map(({ id, Reporte }) => (
        <div key={id} hidden={id !== activa}>
          {/* Solo la pestaña activa dibuja la barra: los botones llevan id. */}
          <Reporte barra={id === activa ? barra : () => null} panel={propsDelPanel(id)} />
        </div>
      ))}
    </Layout>
  );
}
