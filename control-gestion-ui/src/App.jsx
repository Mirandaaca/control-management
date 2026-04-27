import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import PrivateRoute from './components/PrivateRoute';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Usuarios from './pages/Usuarios';
import Zonas from './pages/Zonas';
import Productos from './pages/Productos';
import Reportes from './pages/Reportes';
import Configuraciones from './pages/Configuraciones';
import Clientes from './pages/Clientes';
import HojasPedido from './pages/HojasPedido';
import Inventario from './pages/Inventario';
import Pedidos from './pages/Pedidos';
import Eventos from './pages/Eventos';
import Cobranzas from './pages/Cobranzas';
import Rutas from './pages/Rutas';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/dashboard" element={<PrivateRoute><Dashboard /></PrivateRoute>} />
          <Route path="/clientes" element={<PrivateRoute><Clientes /></PrivateRoute>} />
          <Route path="/hojas-pedido" element={<PrivateRoute><HojasPedido /></PrivateRoute>} />
          <Route path="/inventario" element={<PrivateRoute><Inventario /></PrivateRoute>} />
          <Route path="/pedidos" element={<PrivateRoute><Pedidos /></PrivateRoute>} />
          <Route path="/eventos" element={<PrivateRoute><Eventos /></PrivateRoute>} />
          <Route path="/cobranzas" element={<PrivateRoute><Cobranzas /></PrivateRoute>} />
          <Route path="/rutas" element={<PrivateRoute><Rutas /></PrivateRoute>} />
          <Route path="/usuarios" element={<PrivateRoute roles={['Admin']}><Usuarios /></PrivateRoute>} />
          <Route path="/zonas" element={<PrivateRoute><Zonas /></PrivateRoute>} />
          <Route path="/productos" element={<PrivateRoute><Productos /></PrivateRoute>} />
          <Route path="/reportes" element={<PrivateRoute roles={['Admin']}><Reportes /></PrivateRoute>} />
          <Route path="/configuraciones" element={<PrivateRoute roles={['Admin']}><Configuraciones /></PrivateRoute>} />
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
