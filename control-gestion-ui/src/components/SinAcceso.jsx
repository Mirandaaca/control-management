import { Link } from 'react-router-dom';
import { Lock } from 'lucide-react';

export default function SinAcceso() {
  return (
    <div className="flex flex-col items-center justify-center text-center py-20 px-4">
      <span className="grid place-items-center w-14 h-14 rounded-full bg-slate-100 text-slate-500 mb-4" aria-hidden="true">
        <Lock size={26} />
      </span>
      <h2 className="text-xl font-semibold text-slate-900">No tienes acceso a esta pantalla</h2>
      <p className="mt-2 max-w-md text-sm text-slate-600">
        Tu rol no tiene el permiso que se necesita. Si crees que deberías tenerlo, pídeselo al administrador del sistema.
      </p>
      <Link
        to="/dashboard"
        className="mt-6 inline-flex items-center h-10 px-4 rounded-lg bg-marca-600 hover:bg-marca-700 text-white text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 focus-visible:ring-offset-2"
      >
        Volver al inicio
      </Link>
    </div>
  );
}
