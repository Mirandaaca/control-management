import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { PANTALLAS } from './context/permisos';
import PrivateRoute from './components/PrivateRoute';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Usuarios from './pages/Usuarios';
import Zonas from './pages/Zonas';
import Productos from './pages/Productos';
import Reportes from './pages/Reportes';
import ConfiguracionGeneral from './pages/ConfiguracionGeneral';
import EstadosCliente from './pages/EstadosCliente';
import MapaZonas from './pages/MapaZonas';
import Clientes from './pages/Clientes';
import HojasPedido from './pages/HojasPedido';
import Inventario from './pages/Inventario';
import Pedidos from './pages/Pedidos';
import BandejaPedidos from './pages/BandejaPedidos';
import ReportePedidos from './pages/ReportePedidos';
import ReporteEventos from './pages/ReporteEventos';
import Eventos from './pages/Eventos';
import MapaEventos from './pages/MapaEventos';
import Cobranzas from './pages/Cobranzas';
import Rutas from './pages/Rutas';
import Roles from './pages/Roles';
import Perfil from './pages/Perfil';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/dashboard" element={<PrivateRoute><Dashboard /></PrivateRoute>} />
          <Route path="/clientes" element={<PrivateRoute permisos={PANTALLAS.clientes}><Clientes /></PrivateRoute>} />
          <Route path="/hojas-pedido" element={<PrivateRoute permisos={PANTALLAS.hojasPedido}><HojasPedido /></PrivateRoute>} />
          <Route path="/inventario" element={<PrivateRoute permisos={PANTALLAS.inventario}><Inventario /></PrivateRoute>} />
          <Route path="/pedidos" element={<PrivateRoute permisos={PANTALLAS.pedidos}><Pedidos /></PrivateRoute>} />
          <Route path="/pedidos/bandeja" element={<PrivateRoute permisos={PANTALLAS.bandeja}><BandejaPedidos /></PrivateRoute>} />
          <Route path="/eventos" element={<PrivateRoute permisos={PANTALLAS.eventos}><Eventos /></PrivateRoute>} />
          <Route path="/eventos/mapa" element={<PrivateRoute permisos={PANTALLAS.mapaEventos}><MapaEventos /></PrivateRoute>} />
          <Route path="/cobranzas" element={<PrivateRoute permisos={PANTALLAS.cobranzas}><Cobranzas /></PrivateRoute>} />
          <Route path="/rutas" element={<PrivateRoute permisos={PANTALLAS.rutas}><Rutas /></PrivateRoute>} />
          <Route path="/usuarios" element={<PrivateRoute permisos={PANTALLAS.usuarios}><Usuarios /></PrivateRoute>} />
          <Route path="/roles" element={<PrivateRoute permisos={PANTALLAS.roles}><Roles /></PrivateRoute>} />
          <Route path="/perfil" element={<PrivateRoute><Perfil /></PrivateRoute>} />
          <Route path="/zonas/mapa" element={<PrivateRoute permisos={PANTALLAS.mapaZonas}><MapaZonas /></PrivateRoute>} />
          <Route path="/zonas" element={<PrivateRoute permisos={PANTALLAS.zonas}><Zonas /></PrivateRoute>} />
          <Route path="/productos" element={<PrivateRoute permisos={PANTALLAS.productos}><Productos /></PrivateRoute>} />
          <Route path="/reportes" element={<PrivateRoute permisos={PANTALLAS.reportes}><Reportes /></PrivateRoute>} />
          <Route path="/reportes/pedidos" element={<PrivateRoute permisos={PANTALLAS.reportePedidos}><ReportePedidos /></PrivateRoute>} />
          <Route path="/reportes/eventos" element={<PrivateRoute permisos={PANTALLAS.reporteEventos}><ReporteEventos /></PrivateRoute>} />
          <Route path="/estados-cliente" element={<PrivateRoute permisos={PANTALLAS.estadosCliente}><EstadosCliente /></PrivateRoute>} />
          <Route path="/configuracion-general" element={<PrivateRoute permisos={PANTALLAS.configuracionGeneral}><ConfiguracionGeneral /></PrivateRoute>} />
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
