import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/auth';
import Layout from './Layout';
import SinAcceso from './SinAcceso';

/** Exige sesión y, si se indican, al menos uno de los permisos. Sin permiso muestra "Sin acceso", no una pantalla vacía. */
export default function PrivateRoute({ children, permisos }) {
  const { usuario, tiene } = useAuth();
  if (!usuario) return <Navigate to="/login" replace />;
  if (permisos && !tiene(...permisos)) return <Layout><SinAcceso /></Layout>;
  return children;
}
