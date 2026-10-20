import { CircleAlert, CircleCheck, Info, TriangleAlert } from 'lucide-react';

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

export function Btn({ children, onClick, variant = 'primary', size = 'sm', type = 'button', disabled = false }) {
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
    <button type={type} onClick={onClick} disabled={disabled} className={`${base} ${sizes[size]} ${variants[variant]}`}>
      {children}
    </button>
  );
}

export function Input({ label, ...props }) {
  return (
    <div>
      {label && <label className="block text-sm font-medium text-slate-700 mb-1">{label}</label>}
      <input
        {...props}
        className="w-full h-10 px-3 border border-borde-control rounded-lg text-sm text-slate-900 placeholder:text-slate-500 bg-white transition focus:outline-none focus:ring-2 focus:ring-marca-500 focus:border-marca-500"
      />
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

export function Alert({ children, type = 'error' }) {
  const styles = {
    error: { clase: 'bg-red-50 border-red-200 text-red-700', Icono: CircleAlert },
    success: { clase: 'bg-green-50 border-green-200 text-green-800', Icono: CircleCheck },
    warning: { clase: 'bg-amber-50 border-amber-200 text-amber-800', Icono: TriangleAlert },
    info: { clase: 'bg-marca-50 border-marca-200 text-marca-800', Icono: Info },
  };
  const { clase, Icono } = styles[type];
  return (
    <div role={type === 'error' ? 'alert' : 'status'} className={`flex items-start gap-2.5 border rounded-lg px-4 py-3 text-sm ${clase}`}>
      <Icono size={18} className="shrink-0 mt-px" aria-hidden="true" />
      <div className="min-w-0">{children}</div>
    </div>
  );
}
