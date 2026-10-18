import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/auth';

const navItems = [
  { to: '/dashboard', label: 'Dashboard', roles: [], group: null },
  { to: '/clientes', label: 'Clientes', roles: [], group: 'Campo' },
  { to: '/hojas-pedido', label: 'Hojas de Pedido', roles: [], group: 'Campo' },
  { to: '/inventario', label: 'Inventario', roles: [], group: 'Campo' },
  { to: '/pedidos', label: 'Pedidos', roles: [], group: 'Campo' },
  { to: '/eventos', label: 'Eventos', roles: [], group: 'Campo' },
  { to: '/cobranzas', label: 'Cobranzas', roles: [], group: 'Campo' },
  { to: '/rutas', label: 'Rutas', roles: [], group: 'Campo' },
  { to: '/zonas', label: 'Zonas', roles: [], group: 'Configuración' },
  { to: '/productos', label: 'Productos', roles: [], group: 'Configuración' },
  { to: '/configuraciones', label: 'Configuraciones', roles: ['Admin'], group: 'Configuración' },
  { to: '/usuarios', label: 'Usuarios', roles: ['Admin'], group: 'Sistema' },
  { to: '/reportes', label: 'Reportes', roles: ['Admin'], group: 'Sistema' },
];

export default function Layout({ children }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const handleLogout = () => { logout(); navigate('/login'); };

  const visibleItems = navItems.filter(
    (item) => item.roles.length === 0 || item.roles.some((r) => user?.roles?.includes(r))
  );

  return (
    <div className="flex min-h-screen bg-slate-100">
      {/* Sidebar */}
      <aside className="w-64 bg-slate-900 text-white flex flex-col shrink-0">
        {/* Logo */}
        <div className="px-6 py-5 border-b border-slate-700">
          <h1 className="text-lg font-bold text-white tracking-tight">Control Gestión</h1>
          <p className="text-xs text-slate-400 mt-0.5">Panel de Administración</p>
        </div>

        {/* Nav */}
        <nav className="flex-1 px-3 py-4 overflow-y-auto">
          {['', 'Campo', 'Configuración', 'Sistema'].map(group => {
            const groupItems = visibleItems.filter(i => i.group === group || (group === '' && !i.group));
            if (groupItems.length === 0) return null;
            return (
              <div key={group} className="mb-3">
                {group && (
                  <p className="px-3 text-[10px] font-semibold uppercase tracking-widest text-slate-500 mb-1">{group}</p>
                )}
                {groupItems.map(item => {
                  const active = location.pathname === item.to;
                  return (
                    <Link
                      key={item.to}
                      to={item.to}
                      className={`flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm font-medium transition-colors mb-0.5 ${
                        active ? 'bg-blue-600 text-white' : 'text-slate-400 hover:bg-slate-800 hover:text-white'
                      }`}
                    >
                      <span className="w-1.5 h-1.5 rounded-full bg-current opacity-60 shrink-0" />
                      {item.label}
                    </Link>
                  );
                })}
              </div>
            );
          })}
        </nav>

        {/* User info */}
        <div className="px-4 py-4 border-t border-slate-700">
          <div className="flex items-center gap-3 mb-3">
            <div className="w-8 h-8 rounded-full bg-blue-600 flex items-center justify-center text-xs font-bold text-white shrink-0">
              {user?.nombreCompleto?.[0]?.toUpperCase() ?? 'U'}
            </div>
            <div className="min-w-0">
              <p className="text-sm font-medium text-white truncate">{user?.nombreCompleto}</p>
              <p className="text-xs text-slate-400 truncate">{user?.roles?.[0]}</p>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="w-full text-sm text-slate-400 hover:text-red-400 hover:bg-slate-800 px-3 py-2 rounded-lg transition-colors text-left"
          >
            Cerrar sesión
          </button>
        </div>
      </aside>

      {/* Main */}
      <main className="flex-1 flex flex-col min-w-0">
        <div className="p-6 lg:p-8 flex-1">
          {children}
        </div>
      </main>
    </div>
  );
}
