import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { PANTALLAS } from './context/permisos';
import Layout from './components/Layout';
import PrivateRoute from './components/PrivateRoute';
import { EsqueletoDePagina } from './components/ui/basicos';
import { Avisos } from './components/ui/sonner';
import Login from './pages/Login';

// Cada pantalla se baja cuando se abre (tarea 21): el inicio de sesión no espera a las demás, ni a Leaflet ni a
// Recharts. Login va en el archivo principal: es la primera que se ve.
const Dashboard = lazy(() => import('./pages/Dashboard'));
const Usuarios = lazy(() => import('./pages/Usuarios'));
const Zonas = lazy(() => import('./pages/Zonas'));
const Productos = lazy(() => import('./pages/Productos'));
const Reportes = lazy(() => import('./pages/Reportes'));
const ConfiguracionGeneral = lazy(() => import('./pages/ConfiguracionGeneral'));
const EstadosCliente = lazy(() => import('./pages/EstadosCliente'));
const MapaZonas = lazy(() => import('./pages/MapaZonas'));
const Clientes = lazy(() => import('./pages/Clientes'));
const HojasPedido = lazy(() => import('./pages/HojasPedido'));
const Inventario = lazy(() => import('./pages/Inventario'));
const Pedidos = lazy(() => import('./pages/Pedidos'));
const ReportePedidos = lazy(() => import('./pages/ReportePedidos'));
const ReporteEventos = lazy(() => import('./pages/ReporteEventos'));
const Eventos = lazy(() => import('./pages/Eventos'));
const MapaEventos = lazy(() => import('./pages/MapaEventos'));
const Cobranzas = lazy(() => import('./pages/Cobranzas'));
const Rutas = lazy(() => import('./pages/Rutas'));
const MapaClientes = lazy(() => import('./pages/MapaClientes'));
const ClientesPorVisitar = lazy(() => import('./pages/ClientesPorVisitar'));
const Roles = lazy(() => import('./pages/Roles'));
const Perfil = lazy(() => import('./pages/Perfil'));
const Bitacora = lazy(() => import('./pages/Bitacora'));
const HojaParaImprimir = lazy(() => import('./pages/HojaParaImprimir'));

export default function App() {
  return (
    <AuthProvider>
      <Avisos />
      <BrowserRouter>
        {/* Mientras se baja una pantalla, el menú y su forma. Solo se ve con sesión: sin ella, PrivateRoute manda al login. */}
        <Suspense fallback={<Layout><EsqueletoDePagina /></Layout>}>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route path="/dashboard" element={<PrivateRoute><Dashboard /></PrivateRoute>} />
            <Route path="/clientes" element={<PrivateRoute permisos={PANTALLAS.clientes}><Clientes /></PrivateRoute>} />
            <Route path="/hojas-pedido" element={<PrivateRoute permisos={PANTALLAS.hojasPedido}><HojasPedido /></PrivateRoute>} />
            {/* La hoja para imprimir no lleva el menú, tampoco mientras se baja. */}
            <Route path="/hojas-pedido/:id/imprimir" element={(
              <PrivateRoute permisos={PANTALLAS.imprimirHp}>
                <Suspense fallback={<main className="mx-auto max-w-3xl p-6"><EsqueletoDePagina /></main>}><HojaParaImprimir /></Suspense>
              </PrivateRoute>
            )} />
            <Route path="/inventario" element={<PrivateRoute permisos={PANTALLAS.inventario}><Inventario /></PrivateRoute>} />
            <Route path="/pedidos" element={<PrivateRoute permisos={PANTALLAS.pedidos}><Pedidos /></PrivateRoute>} />
            <Route path="/eventos" element={<PrivateRoute permisos={PANTALLAS.eventos}><Eventos /></PrivateRoute>} />
            <Route path="/eventos/mapa" element={<PrivateRoute permisos={PANTALLAS.mapaEventos}><MapaEventos /></PrivateRoute>} />
            <Route path="/cobranzas" element={<PrivateRoute permisos={PANTALLAS.cobranzas}><Cobranzas /></PrivateRoute>} />
            <Route path="/clientes/mapa" element={<PrivateRoute permisos={PANTALLAS.mapaClientes}><MapaClientes /></PrivateRoute>} />
            <Route path="/clientes/por-visitar" element={<PrivateRoute permisos={PANTALLAS.porVisitar}><ClientesPorVisitar /></PrivateRoute>} />
            <Route path="/rutas" element={<PrivateRoute permisos={PANTALLAS.rutas}><Rutas /></PrivateRoute>} />
            <Route path="/usuarios" element={<PrivateRoute permisos={PANTALLAS.usuarios}><Usuarios /></PrivateRoute>} />
            <Route path="/roles" element={<PrivateRoute permisos={PANTALLAS.roles}><Roles /></PrivateRoute>} />
            <Route path="/bitacora" element={<PrivateRoute permisos={PANTALLAS.bitacora}><Bitacora /></PrivateRoute>} />
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
        </Suspense>
      </BrowserRouter>
    </AuthProvider>
  );
}
