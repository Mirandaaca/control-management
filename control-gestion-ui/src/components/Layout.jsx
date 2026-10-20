import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import {
  Boxes, ChartColumn, ChevronDown, ClipboardList, House, LogOut, Map as Mapa, MapPinned, Menu, Package, PanelLeftClose,
  PanelLeftOpen, Route, Settings, Shield, ShoppingCart, SlidersHorizontal, Store, Tag, TriangleAlert, Users, Wallet, X,
} from 'lucide-react';
import { useAuth } from '../context/auth';
import { PANTALLAS } from '../context/permisos';

// Los módulos siguen el orden de la tesis (docs/alcance.md). Cada pantalla se ve si el usuario tiene uno de sus
// permisos. Un módulo con una sola pantalla visible se muestra como enlace directo; con dos o más, como un grupo que
// se abre y se cierra.
const modulos = [
  { id: 'inicio', seccion: 'inicio', items: [{ to: '/dashboard', label: 'Inicio', icon: House }] },
  { id: 'pedidos', seccion: 'operacion', nombre: 'Pedidos', icon: Package, items: [
    { to: '/pedidos', label: 'Pedidos', icon: Package, permisos: PANTALLAS.pedidos },
  ] },
  { id: 'ventas', seccion: 'operacion', nombre: 'Ventas', icon: ShoppingCart, items: [
    { to: '/clientes', label: 'Clientes', icon: Store, permisos: PANTALLAS.clientes },
    { to: '/hojas-pedido', label: 'Hojas de pedido', icon: ClipboardList, permisos: PANTALLAS.hojasPedido },
    { to: '/inventario', label: 'Inventario', icon: Boxes, permisos: PANTALLAS.inventario },
  ] },
  { id: 'cobranzas', seccion: 'operacion', nombre: 'Cobranzas', icon: Wallet, items: [
    { to: '/cobranzas', label: 'Cobranzas', icon: Wallet, permisos: PANTALLAS.cobranzas },
  ] },
  { id: 'eventos', seccion: 'operacion', nombre: 'Eventos', icon: TriangleAlert, items: [
    { to: '/eventos', label: 'Eventos', icon: TriangleAlert, permisos: PANTALLAS.eventos },
  ] },
  { id: 'territorio', seccion: 'operacion', nombre: 'Territorio', icon: MapPinned, items: [
    { to: '/rutas', label: 'Rutas', icon: Route, permisos: PANTALLAS.rutas },
  ] },
  { id: 'reportes', seccion: 'operacion', nombre: 'Reportes', icon: ChartColumn, items: [
    { to: '/reportes', label: 'Reportes', icon: ChartColumn, permisos: PANTALLAS.reportes },
  ] },
  { id: 'parametros', seccion: 'administracion', nombre: 'Parámetros', icon: SlidersHorizontal, items: [
    { to: '/zonas', label: 'Zonas', icon: Mapa, permisos: PANTALLAS.zonas },
    { to: '/productos', label: 'Productos', icon: Tag, permisos: PANTALLAS.productos },
    { to: '/configuraciones', label: 'Configuraciones', icon: Settings, permisos: PANTALLAS.configuraciones },
  ] },
  { id: 'seguridad', seccion: 'administracion', nombre: 'Seguridad', icon: Shield, items: [
    { to: '/usuarios', label: 'Usuarios', icon: Users, permisos: PANTALLAS.usuarios },
  ] },
];

const CLAVE_REPLEGADO = 'menu-replegado';
const foco = 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-300';

// localStorage puede fallar (navegación privada, almacenamiento bloqueado): el menú funciona igual, solo no lo recuerda.
function leerReplegado() {
  try { return localStorage.getItem(CLAVE_REPLEGADO) === '1'; } catch { return false; }
}
function guardarReplegado(valor) {
  try { localStorage.setItem(CLAVE_REPLEGADO, valor ? '1' : '0'); } catch { /* sin almacenamiento: no se recuerda */ }
}

function Marca() {
  return (
    <div className="flex items-center gap-3 min-w-0">
      <span className="grid place-items-center w-9 h-9 rounded-lg bg-marca-600 text-white text-sm font-bold shrink-0" aria-hidden="true">
        DC
      </span>
      <div className="min-w-0">
        <p className="text-sm font-semibold text-white leading-tight truncate">Control de Gestión</p>
        <p className="text-xs text-marca-200 leading-tight truncate">Del Campo SRL</p>
      </div>
    </div>
  );
}

function claseDeEnlace(activo) {
  return `relative flex items-center gap-3 h-10 rounded-lg text-sm transition-colors ${foco} ${
    activo ? 'bg-marca-700 text-white font-semibold' : 'text-marca-200 hover:bg-marca-800 hover:text-white'
  }`;
}

function BarraActiva() {
  return <span className="absolute left-0 top-2 bottom-2 w-0.75 rounded-r bg-marca-300" aria-hidden="true" />;
}

function Separador() {
  return <li role="separator" className="my-3 mx-3 border-t border-marca-800" />;
}

function EnlaceDelMenu({ item, ruta, alElegir, sangria = false }) {
  const activo = ruta === item.to;
  const Icono = item.icon;
  return (
    <Link
      to={item.to}
      onClick={alElegir}
      aria-current={activo ? 'page' : undefined}
      className={`${claseDeEnlace(activo)} ${sangria ? 'pl-3 pr-3' : 'px-3'}`}
    >
      {activo && <BarraActiva />}
      <Icono size={sangria ? 16 : 18} aria-hidden="true" className="shrink-0" />
      <span className="truncate">{item.label}</span>
    </Link>
  );
}

function MenuCompleto({ visibles, ruta, alElegir }) {
  const moduloActivo = visibles.find((m) => m.items.some((i) => i.to === ruta))?.id;
  const [abiertos, setAbiertos] = useState(() => new Set(moduloActivo ? [moduloActivo] : []));

  const alternar = (id) => setAbiertos((previos) => {
    const siguientes = new Set(previos);
    if (siguientes.has(id)) siguientes.delete(id); else siguientes.add(id);
    return siguientes;
  });

  return (
    <ul className="px-3">
      {visibles.map((modulo, i) => {
        const separar = i > 0 && modulo.seccion !== visibles[i - 1].seccion;
        if (modulo.items.length === 1) {
          return [
            separar && <Separador key={`sep-${modulo.id}`} />,
            <li key={modulo.id} className="mb-0.5">
              <EnlaceDelMenu item={modulo.items[0]} ruta={ruta} alElegir={alElegir} />
            </li>,
          ];
        }

        const abierto = abiertos.has(modulo.id);
        const contieneActiva = modulo.id === moduloActivo;
        const Icono = modulo.icon;
        return [
          separar && <Separador key={`sep-${modulo.id}`} />,
          <li key={modulo.id} className="mb-0.5">
            <button
              type="button"
              onClick={() => alternar(modulo.id)}
              aria-expanded={abierto}
              aria-controls={`modulo-${modulo.id}`}
              className={`flex items-center gap-3 w-full h-10 px-3 rounded-lg text-sm transition-colors cursor-pointer ${foco} ${
                contieneActiva && !abierto ? 'text-white font-semibold bg-marca-800' : 'text-marca-200 hover:bg-marca-800 hover:text-white'
              }`}
            >
              <Icono size={18} aria-hidden="true" className="shrink-0" />
              <span className="flex-1 text-left truncate">{modulo.nombre}</span>
              <ChevronDown
                size={16}
                aria-hidden="true"
                className={`shrink-0 transition-transform duration-200 ${abierto ? 'rotate-180' : ''}`}
              />
            </button>
            {abierto && (
              <ul id={`modulo-${modulo.id}`} className="mt-0.5 ml-5 pl-2 border-l border-marca-800">
                {modulo.items.map((item) => (
                  <li key={item.to} className="mb-0.5">
                    <EnlaceDelMenu item={item} ruta={ruta} alElegir={alElegir} sangria />
                  </li>
                ))}
              </ul>
            )}
          </li>,
        ];
      })}
    </ul>
  );
}

// Menú replegado: solo los íconos, con el nombre en una etiqueta flotante. La etiqueta va con position: fixed
// porque el menú tiene scroll y recortaría cualquier elemento que sobresalga de su ancho.
function MenuReplegado({ visibles, ruta, mostrarEtiqueta, ocultarEtiqueta }) {
  return (
    <ul className="px-3">
      {visibles.map((modulo, i) => {
        // Sin los nombres de los módulos, una línea entre cada uno muestra dónde empieza el siguiente.
        return [
          i > 0 && <li key={`sep-${modulo.id}`} role="separator" className="my-2 mx-2 border-t border-marca-800" />,
          ...modulo.items.map((item) => {
            const activo = ruta === item.to;
            const Icono = item.icon;
            return (
              <li key={item.to} className="mb-0.5">
                <Link
                  to={item.to}
                  aria-label={item.label}
                  aria-current={activo ? 'page' : undefined}
                  onMouseEnter={(e) => mostrarEtiqueta(e, item.label)}
                  onFocus={(e) => mostrarEtiqueta(e, item.label)}
                  onMouseLeave={ocultarEtiqueta}
                  onBlur={ocultarEtiqueta}
                  className={`${claseDeEnlace(activo)} justify-center w-12`}
                >
                  {activo && <BarraActiva />}
                  <Icono size={18} aria-hidden="true" />
                </Link>
              </li>
            );
          }),
        ];
      })}
    </ul>
  );
}

function Avatar({ usuario }) {
  return (
    <span className="grid place-items-center w-8 h-8 rounded-full bg-marca-600 text-xs font-bold text-white shrink-0" aria-hidden="true">
      {usuario?.nombreCompleto?.[0]?.toUpperCase() ?? 'U'}
    </span>
  );
}

export default function Layout({ children }) {
  const { usuario, tiene, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [menuAbierto, setMenuAbierto] = useState(false);
  const [replegado, setReplegado] = useState(leerReplegado);
  const [etiqueta, setEtiqueta] = useState(null);

  const handleLogout = () => { logout(); navigate('/login'); };
  const cerrarMenu = () => setMenuAbierto(false);
  const alternarReplegado = () => {
    setEtiqueta(null);
    setReplegado((r) => { guardarReplegado(!r); return !r; });
  };
  const mostrarEtiqueta = (e, texto) => {
    const caja = e.currentTarget.getBoundingClientRect();
    setEtiqueta({ texto, y: caja.top + caja.height / 2 });
  };
  const ocultarEtiqueta = () => setEtiqueta(null);

  useEffect(() => {
    if (!menuAbierto) return;
    const alPresionar = (e) => { if (e.key === 'Escape') setMenuAbierto(false); };
    window.addEventListener('keydown', alPresionar);
    return () => window.removeEventListener('keydown', alPresionar);
  }, [menuAbierto]);

  const visibles = modulos
    .map((m) => ({ ...m, items: m.items.filter((i) => !i.permisos || tiene(...i.permisos)) }))
    .filter((m) => m.items.length > 0);

  const pieCompleto = (
    <div className="px-3 py-4 border-t border-marca-800">
      <div className="flex items-center gap-3 px-3 mb-3">
        <Avatar usuario={usuario} />
        <div className="min-w-0">
          <p className="text-sm font-medium text-white truncate">{usuario?.nombreCompleto}</p>
          <p className="text-xs text-marca-200 truncate">{usuario?.rol}</p>
        </div>
      </div>
      <button
        onClick={handleLogout}
        className={`flex items-center gap-3 w-full h-10 px-3 rounded-lg text-sm text-marca-200 hover:bg-marca-800 hover:text-white transition-colors cursor-pointer ${foco}`}
      >
        <LogOut size={18} aria-hidden="true" />
        Cerrar sesión
      </button>
    </div>
  );

  return (
    <div className="min-h-screen bg-slate-50 lg:flex">
      {/* PC: menú fijo, replegable */}
      <aside
        className={`hidden lg:flex lg:flex-col shrink-0 bg-marca-900 sticky top-0 h-screen transition-[width] duration-200 ${
          replegado ? 'w-18' : 'w-64'
        }`}
      >
        <div className={`h-16 flex items-center border-b border-marca-800 ${replegado ? 'justify-center' : 'justify-between pl-6 pr-3'}`}>
          {!replegado && <Marca />}
          <button
            onClick={alternarReplegado}
            aria-label={replegado ? 'Expandir menú' : 'Replegar menú'}
            aria-expanded={!replegado}
            onMouseEnter={(e) => replegado && mostrarEtiqueta(e, 'Expandir menú')}
            onMouseLeave={ocultarEtiqueta}
            className={`grid place-items-center w-10 h-10 rounded-lg text-marca-200 hover:bg-marca-800 hover:text-white cursor-pointer shrink-0 ${foco}`}
          >
            {replegado ? <PanelLeftOpen size={20} aria-hidden="true" /> : <PanelLeftClose size={20} aria-hidden="true" />}
          </button>
        </div>

        <nav aria-label="Menú principal" className="flex-1 py-4 overflow-y-auto overflow-x-hidden" onScroll={ocultarEtiqueta}>
          {replegado
            ? <MenuReplegado visibles={visibles} ruta={location.pathname} mostrarEtiqueta={mostrarEtiqueta} ocultarEtiqueta={ocultarEtiqueta} />
            : <MenuCompleto visibles={visibles} ruta={location.pathname} />}
        </nav>

        {replegado ? (
          <div className="flex flex-col items-center gap-2 py-4 border-t border-marca-800">
            <span
              tabIndex={0}
              aria-label={`${usuario?.nombreCompleto ?? ''}, ${usuario?.rol ?? ''}`}
              onMouseEnter={(e) => mostrarEtiqueta(e, usuario?.nombreCompleto ?? '')}
              onFocus={(e) => mostrarEtiqueta(e, usuario?.nombreCompleto ?? '')}
              onMouseLeave={ocultarEtiqueta}
              onBlur={ocultarEtiqueta}
              className={`rounded-full ${foco}`}
            >
              <Avatar usuario={usuario} />
            </span>
            <button
              onClick={handleLogout}
              aria-label="Cerrar sesión"
              onMouseEnter={(e) => mostrarEtiqueta(e, 'Cerrar sesión')}
              onFocus={(e) => mostrarEtiqueta(e, 'Cerrar sesión')}
              onMouseLeave={ocultarEtiqueta}
              onBlur={ocultarEtiqueta}
              className={`grid place-items-center w-12 h-10 rounded-lg text-marca-200 hover:bg-marca-800 hover:text-white cursor-pointer ${foco}`}
            >
              <LogOut size={18} aria-hidden="true" />
            </button>
          </div>
        ) : pieCompleto}
      </aside>

      {replegado && etiqueta && (
        <div
          role="tooltip"
          className="hidden lg:block fixed left-20 z-50 -translate-y-1/2 px-2.5 py-1.5 rounded-md bg-slate-900 text-white text-xs font-medium shadow-flotante pointer-events-none whitespace-nowrap"
          style={{ top: etiqueta.y }}
        >
          {etiqueta.texto}
        </div>
      )}

      {/* Celular y tablet: encabezado con el botón del menú */}
      <header className="lg:hidden sticky top-0 z-30 flex items-center gap-3 h-14 px-4 bg-marca-900">
        <button
          onClick={() => setMenuAbierto(true)}
          aria-label="Abrir menú"
          aria-expanded={menuAbierto}
          className={`grid place-items-center w-10 h-10 -ml-2 rounded-lg text-white hover:bg-marca-800 cursor-pointer ${foco}`}
        >
          <Menu size={22} aria-hidden="true" />
        </button>
        <Marca />
      </header>

      {menuAbierto && (
        <div className="lg:hidden fixed inset-0 z-40" role="dialog" aria-modal="true" aria-label="Menú">
          <div className="absolute inset-0 bg-slate-900/50" onClick={cerrarMenu} aria-hidden="true" />
          <aside className="absolute inset-y-0 left-0 w-72 max-w-[85vw] flex flex-col bg-marca-900 shadow-flotante">
            <div className="px-4 h-14 flex items-center justify-between border-b border-marca-800">
              <Marca />
              <button
                onClick={cerrarMenu}
                aria-label="Cerrar menú"
                className={`grid place-items-center w-10 h-10 rounded-lg text-white hover:bg-marca-800 cursor-pointer ${foco}`}
              >
                <X size={22} aria-hidden="true" />
              </button>
            </div>
            <nav aria-label="Menú principal" className="flex-1 py-4 overflow-y-auto">
              <MenuCompleto visibles={visibles} ruta={location.pathname} alElegir={cerrarMenu} />
            </nav>
            {pieCompleto}
          </aside>
        </div>
      )}

      <main className="flex-1 min-w-0 p-4 sm:p-6 lg:p-8">
        {children}
      </main>
    </div>
  );
}
