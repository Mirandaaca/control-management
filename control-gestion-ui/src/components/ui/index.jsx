import { useId } from 'react';
import {
  ChevronLeft, ChevronRight, CircleAlert, CircleCheck, Inbox, Info, LoaderCircle, RefreshCw, Search, TriangleAlert, X,
} from 'lucide-react';
import { Checkbox } from './checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from './dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from './select';

// Componentes del panel con el diseño de docs/diseno.md. Los archivos en minúscula de esta carpeta (dialog, select,
// checkbox) son de shadcn/ui: las pantallas usan los de aquí, que ya traen los textos y el estilo del sistema.

export function PageHeader({ title, subtitle, action }) {
  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between mb-6">
      <div className="min-w-0">
        <h2 className="text-2xl font-semibold text-slate-900">{title}</h2>
        {subtitle && <p className="text-slate-600 text-sm mt-1">{subtitle}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

export function Badge({ children, color = 'gray' }) {
  const colors = {
    green: 'bg-green-50 text-green-800',
    red: 'bg-red-50 text-red-700',
    blue: 'bg-marca-50 text-marca-800',
    yellow: 'bg-amber-50 text-amber-800',
    gray: 'bg-slate-100 text-slate-700',
    purple: 'bg-purple-50 text-purple-800',
  };
  return (
    <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold whitespace-nowrap ${colors[color]}`}>
      {children}
    </span>
  );
}

const foco = 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 focus-visible:ring-offset-2';

export function Btn({ children, onClick, variant = 'primary', size = 'sm', type = 'button', disabled = false, ...props }) {
  const base = `inline-flex items-center justify-center gap-1.5 font-medium rounded-lg transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed ${foco}`;
  const sizes = {
    sm: 'h-8 px-3 text-xs',
    md: 'h-10 px-4 text-sm',
    lg: 'h-11 px-5 text-sm',
  };
  const variants = {
    primary: 'bg-marca-600 hover:bg-marca-700 text-white',
    danger: 'bg-red-600 hover:bg-red-700 text-white',
    ghost: 'bg-transparent hover:bg-slate-100 text-slate-700',
    outline: 'bg-white border border-borde-control hover:bg-slate-50 text-slate-800',
  };
  return (
    <button {...props} type={type} onClick={onClick} disabled={disabled} className={`${base} ${sizes[size]} ${variants[variant]}`}>
      {children}
    </button>
  );
}

/** Etiqueta arriba, el control, y debajo la ayuda o el error del campo (que viene en errors del ProblemDetails). */
function Campo({ id, label, ayuda, error, children }) {
  return (
    <div>
      {label && <label htmlFor={id} className="block text-sm font-medium text-slate-700 mb-1">{label}</label>}
      {children}
      {error ? (
        <p id={`${id}-error`} className="mt-1 flex items-start gap-1 text-sm text-red-700">
          <CircleAlert size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
          {error}
        </p>
      ) : ayuda && <p id={`${id}-ayuda`} className="mt-1 text-sm text-slate-600">{ayuda}</p>}
    </div>
  );
}

function describirCon(id, error, ayuda) {
  if (error) return `${id}-error`;
  return ayuda ? `${id}-ayuda` : undefined;
}

export function Input({ label, ayuda, error, id, ...props }) {
  const generado = useId();
  const campoId = id ?? generado;
  return (
    <Campo id={campoId} label={label} ayuda={ayuda} error={error}>
      <input
        {...props}
        id={campoId}
        aria-invalid={error ? true : undefined}
        aria-describedby={describirCon(campoId, error, ayuda)}
        className={`w-full h-10 px-3 border rounded-lg text-sm text-slate-900 placeholder:text-slate-500 bg-white transition focus:outline-none focus:ring-2 focus:ring-marca-500 focus:border-marca-500 disabled:bg-slate-50 disabled:text-slate-600 ${
          error ? 'border-red-600' : 'border-borde-control'
        }`}
      />
    </Campo>
  );
}

const SIN_FILTRO = '__todos';

/**
 * Lista desplegable. opciones: [{ valor, texto }]. Con "todos" agrega una primera opción que vale '' (para filtros:
 * "Todos los roles"); Radix no acepta '' como valor de una opción, por eso se traduce aquí.
 */
export function Selector({ label, value, onChange, opciones, placeholder = 'Elegir…', todos, ayuda, error, id, disabled }) {
  const generado = useId();
  const campoId = id ?? generado;
  const valor = value === '' ? (todos ? SIN_FILTRO : undefined) : value;
  return (
    <Campo id={campoId} label={label} ayuda={ayuda} error={error}>
      <Select value={valor} onValueChange={(v) => onChange(v === SIN_FILTRO ? '' : v)} disabled={disabled}>
        <SelectTrigger
          id={campoId}
          className={`w-full ${error ? 'border-red-600' : ''}`}
          aria-invalid={error ? true : undefined}
          aria-describedby={describirCon(campoId, error, ayuda)}
        >
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {todos && <SelectItem value={SIN_FILTRO}>{todos}</SelectItem>}
          {opciones.map((o) => <SelectItem key={o.valor} value={o.valor}>{o.texto}</SelectItem>)}
        </SelectContent>
      </Select>
    </Campo>
  );
}

export function Casilla({ checked, onCheckedChange, disabled, children, id }) {
  const generado = useId();
  const casillaId = id ?? generado;
  return (
    <div className="flex items-start gap-2.5">
      <Checkbox id={casillaId} checked={checked} onCheckedChange={onCheckedChange} disabled={disabled} className="mt-0.5" />
      <label htmlFor={casillaId} className={`text-sm leading-snug ${disabled ? 'text-slate-600' : 'text-slate-900 cursor-pointer'}`}>
        {children}
      </label>
    </div>
  );
}

export function Card({ children, className = '' }) {
  return (
    <div className={`bg-white rounded-xl shadow-tarjeta border border-slate-200 p-5 ${className}`}>
      {children}
    </div>
  );
}

export function Alert({ children, type = 'error', className = '' }) {
  const styles = {
    error: { clase: 'bg-red-50 border-red-200 text-red-700', Icono: CircleAlert },
    success: { clase: 'bg-green-50 border-green-200 text-green-800', Icono: CircleCheck },
    warning: { clase: 'bg-amber-50 border-amber-200 text-amber-800', Icono: TriangleAlert },
    info: { clase: 'bg-marca-50 border-marca-200 text-marca-800', Icono: Info },
  };
  const { clase, Icono } = styles[type];
  return (
    <div role={type === 'error' ? 'alert' : 'status'} className={`flex items-start gap-2.5 border rounded-lg px-4 py-3 text-sm ${clase} ${className}`}>
      <Icono size={18} className="shrink-0 mt-px" aria-hidden="true" />
      <div className="min-w-0">{children}</div>
    </div>
  );
}

const anchos = { md: 'sm:max-w-lg', lg: 'sm:max-w-3xl' };

/**
 * Ventana modal: atrapa el foco, se cierra con Escape o con la X, y devuelve el foco al botón que la abrió. Un clic
 * fuera no la cierra: perdería lo que se escribió en un formulario.
 */
export function Modal({ abierto, onCerrar, titulo, descripcion, children, pie, ancho = 'md' }) {
  return (
    <Dialog open={abierto} onOpenChange={(abre) => { if (!abre) onCerrar(); }}>
      <DialogContent
        className={`${anchos[ancho]} max-h-[90vh] grid-rows-[auto_minmax(0,1fr)_auto]`}
        onPointerDownOutside={(e) => e.preventDefault()}
      >
        <DialogHeader>
          <DialogTitle>{titulo}</DialogTitle>
          {descripcion
            ? <DialogDescription>{descripcion}</DialogDescription>
            : <DialogDescription className="sr-only">{titulo}</DialogDescription>}
        </DialogHeader>
        <div className="min-h-0 overflow-y-auto -mx-6 px-6">{children}</div>
        {pie && <DialogFooter>{pie}</DialogFooter>}
      </DialogContent>
    </Dialog>
  );
}

/** Pide confirmación antes de desactivar, anular o eliminar (docs/diseno.md, Mensajes). */
export function Confirmar({ abierto, titulo, children, textoAccion, enviando, error, onConfirmar, onCancelar }) {
  return (
    <Modal
      abierto={abierto}
      onCerrar={onCancelar}
      titulo={titulo}
      pie={(
        <>
          <Btn variant="outline" size="md" onClick={onCancelar} disabled={enviando}>Cancelar</Btn>
          <Btn variant="danger" size="md" onClick={onConfirmar} disabled={enviando}>
            {enviando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
            {textoAccion}
          </Btn>
        </>
      )}
    >
      <div className="space-y-3 text-sm text-slate-700">
        {children}
        {error && <Alert type="error">{error}</Alert>}
      </div>
    </Modal>
  );
}

export function Buscador({ value, onChange, placeholder = 'Buscar…', label = 'Buscar' }) {
  const id = useId();
  return (
    <div className="w-full sm:w-72">
      <label htmlFor={id} className="block text-sm font-medium text-slate-700 mb-1">{label}</label>
      <div className="relative">
        <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-600 pointer-events-none" aria-hidden="true" />
        <input
          id={id}
          type="search"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className="w-full h-10 pl-9 pr-3 border border-borde-control rounded-lg text-sm text-slate-900 placeholder:text-slate-500 bg-white transition focus:outline-none focus:ring-2 focus:ring-marca-500 focus:border-marca-500"
        />
      </div>
    </div>
  );
}

/**
 * Barra de búsqueda y filtros, dentro de la tarjeta de la tabla. Los filtros se aplican al cambiar, sin botón
 * "Filtrar"; "Limpiar filtros" aparece cuando hay alguno activo.
 */
export function BarraDeFiltros({ children, resultado, hayFiltros, onLimpiar }) {
  return (
    <div className="flex flex-col gap-3 p-4 border-b border-slate-200 lg:flex-row lg:items-end lg:justify-between">
      <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-end">{children}</div>
      <div className="flex items-center gap-3 text-sm text-slate-600 lg:h-10">
        <span aria-live="polite">{resultado}</span>
        {hayFiltros && (
          <button
            type="button"
            onClick={onLimpiar}
            className={`inline-flex items-center gap-1 rounded-md font-medium text-marca-700 hover:text-marca-800 hover:underline cursor-pointer ${foco}`}
          >
            <X size={14} aria-hidden="true" />
            Limpiar filtros
          </button>
        )}
      </div>
    </div>
  );
}

/** Página anterior y siguiente de una lista paginada en la API. */
export function Paginacion({ pagina, totalPaginas, onCambiar }) {
  if (totalPaginas <= 1) return null;
  return (
    <nav aria-label="Paginación" className="flex items-center justify-between gap-3 px-4 py-3 border-t border-slate-200 text-sm text-slate-600">
      <span>Página {pagina} de {totalPaginas}</span>
      <div className="flex gap-2">
        <Btn variant="outline" onClick={() => onCambiar(pagina - 1)} disabled={pagina <= 1}>
          <ChevronLeft size={14} aria-hidden="true" />
          Anterior
        </Btn>
        <Btn variant="outline" onClick={() => onCambiar(pagina + 1)} disabled={pagina >= totalPaginas}>
          Siguiente
          <ChevronRight size={14} aria-hidden="true" />
        </Btn>
      </div>
    </nav>
  );
}

export function Cargando({ texto = 'Cargando…' }) {
  return (
    <div role="status" className="flex items-center justify-center gap-2 py-16 text-sm text-slate-600">
      <LoaderCircle size={18} className="animate-spin" aria-hidden="true" />
      {texto}
    </div>
  );
}

export function EstadoVacio({ titulo, children, accion, icono: Icono = Inbox }) {
  return (
    <div className="flex flex-col items-center text-center py-14 px-4">
      <Icono size={32} className="text-slate-400 mb-3" aria-hidden="true" />
      <p className="text-base font-semibold text-slate-900">{titulo}</p>
      {children && <p className="mt-1 max-w-sm text-sm text-slate-600">{children}</p>}
      {accion && <div className="mt-4">{accion}</div>}
    </div>
  );
}

export function ErrorDeCarga({ mensaje, onReintentar, reintentando }) {
  return (
    <div className="p-4">
      <Alert type="error">
        <p>{mensaje}</p>
        <button
          type="button"
          onClick={onReintentar}
          disabled={reintentando}
          className={`mt-2 inline-flex items-center gap-1.5 font-medium underline cursor-pointer disabled:opacity-50 ${foco}`}
        >
          <RefreshCw size={14} className={reintentando ? 'animate-spin' : ''} aria-hidden="true" />
          Reintentar
        </button>
      </Alert>
    </div>
  );
}
