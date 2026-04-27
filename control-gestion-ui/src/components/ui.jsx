export function PageHeader({ title, subtitle, action }) {
  return (
    <div className="flex items-start justify-between mb-6">
      <div>
        <h2 className="text-2xl font-bold text-slate-800">{title}</h2>
        {subtitle && <p className="text-slate-500 text-sm mt-1">{subtitle}</p>}
      </div>
      {action && <div>{action}</div>}
    </div>
  );
}

export function Badge({ children, color = 'gray' }) {
  const colors = {
    green: 'bg-emerald-100 text-emerald-700',
    red: 'bg-red-100 text-red-600',
    blue: 'bg-blue-100 text-blue-700',
    yellow: 'bg-amber-100 text-amber-700',
    gray: 'bg-slate-100 text-slate-600',
    purple: 'bg-purple-100 text-purple-700',
  };
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${colors[color]}`}>
      {children}
    </span>
  );
}

export function Btn({ children, onClick, variant = 'primary', size = 'sm', type = 'button', disabled = false }) {
  const base = 'inline-flex items-center gap-1.5 font-medium rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed';
  const sizes = {
    sm: 'px-3 py-1.5 text-xs',
    md: 'px-4 py-2 text-sm',
    lg: 'px-5 py-2.5 text-sm',
  };
  const variants = {
    primary: 'bg-blue-600 hover:bg-blue-700 text-white',
    danger: 'bg-red-500 hover:bg-red-600 text-white',
    ghost: 'bg-slate-100 hover:bg-slate-200 text-slate-700',
    outline: 'border border-slate-200 hover:bg-slate-50 text-slate-700',
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
      {label && <label className="block text-xs font-medium text-slate-600 mb-1">{label}</label>}
      <input
        {...props}
        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition bg-white"
      />
    </div>
  );
}

export function Card({ children, className = '' }) {
  return (
    <div className={`bg-white rounded-xl shadow-sm border border-slate-100 p-5 ${className}`}>
      {children}
    </div>
  );
}

export function Alert({ children, type = 'error' }) {
  const styles = {
    error: 'bg-red-50 border-red-200 text-red-600',
    success: 'bg-emerald-50 border-emerald-200 text-emerald-700',
    warning: 'bg-amber-50 border-amber-200 text-amber-700',
  };
  return (
    <div className={`border rounded-lg px-4 py-3 text-sm ${styles[type]}`}>
      {children}
    </div>
  );
}
