import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/auth';

export default function PrivateRoute({ children, roles }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  if (roles && !roles.some((r) => user.roles?.includes(r)))
    return <Navigate to="/dashboard" replace />;
  return children;
}
