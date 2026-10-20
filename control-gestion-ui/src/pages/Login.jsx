import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Eye, EyeOff, LoaderCircle } from 'lucide-react';
import { useAuth } from '../context/auth';
import { mensajeDeError } from '../api/errores';
import { Alert } from '../components/ui';

const campo = 'w-full h-11 px-3 border border-borde-control rounded-lg text-sm text-slate-900 placeholder:text-slate-500 bg-white transition focus:outline-none focus:ring-2 focus:ring-marca-500 focus:border-marca-500';

// Polígonos de zonas de venta, como los límites que se dibujan en la 5.5.
const zonas = [
  { puntos: '0,0 330,0 290,240 0,300', clase: 'fill-marca-800' },
  { puntos: '330,0 800,0 800,180 560,260 290,240', clase: 'fill-marca-700' },
  { puntos: '0,300 290,240 360,520 120,610 0,560', clase: 'fill-marca-900' },
  { puntos: '290,240 560,260 620,500 360,520', clase: 'fill-marca-600' },
  { puntos: '560,260 800,180 800,560 620,500', clase: 'fill-marca-800' },
  { puntos: '0,560 120,610 360,520 420,900 0,900', clase: 'fill-marca-700' },
  { puntos: '360,520 620,500 800,560 800,900 420,900', clase: 'fill-marca-900' },
];

const clientes = [[150, 150], [245, 330], [430, 370], [520, 300], [690, 380], [610, 470]];
const promotor = [470, 470];

function IlustracionTerritorio() {
  const ruta = [...clientes, promotor].map((p) => p.join(',')).join(' ');
  return (
    <svg viewBox="0 0 800 900" preserveAspectRatio="xMidYMid slice" className="absolute inset-0 w-full h-full" aria-hidden="true">
      {zonas.map((z) => (
        <polygon key={z.puntos} points={z.puntos} className={`${z.clase} stroke-marca-500/55`} strokeWidth="1.5" />
      ))}
      <polyline className="ruta-login fill-none stroke-marca-300" points={ruta} pathLength="1" strokeWidth="3"
        strokeLinecap="round" strokeLinejoin="round" />
      {clientes.map(([x, y]) => (
        <circle key={`${x}-${y}`} cx={x} cy={y} r="7" className="fill-white stroke-marca-600" strokeWidth="3" />
      ))}
      <g className="promotor-login">
        <circle cx={promotor[0]} cy={promotor[1]} r="20" className="fill-none stroke-trigo/60" strokeWidth="2" />
        <circle cx={promotor[0]} cy={promotor[1]} r="10" className="fill-trigo stroke-white" strokeWidth="3" />
      </g>
    </svg>
  );
}

function Marca({ grande = false }) {
  return (
    <div className="flex items-center gap-3">
      <span
        className={`grid place-items-center rounded-xl bg-white text-marca-700 font-bold shrink-0 ${grande ? 'w-12 h-12 text-lg' : 'w-10 h-10 text-sm'}`}
        aria-hidden="true"
      >
        DC
      </span>
      <div>
        <p className={`font-semibold text-white leading-tight ${grande ? 'text-lg' : 'text-base'}`}>Control de Gestión</p>
        <p className="text-sm text-marca-200 leading-tight">Del Campo SRL</p>
      </div>
    </div>
  );
}

export default function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ email: '', password: '' });
  const [verContrasena, setVerContrasena] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await login(form.email, form.password);
      navigate('/dashboard');
    } catch (err) {
      setError(mensajeDeError(err, 'No se pudo iniciar sesión.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-white lg:grid lg:grid-cols-12">
      {/* Territorio: arriba en el celular, a la izquierda en la PC */}
      <section className="relative overflow-hidden bg-marca-900 h-52 sm:h-60 lg:h-auto lg:min-h-screen lg:col-span-7">
        <IlustracionTerritorio />
        <div className="relative h-full flex flex-col items-start justify-between p-6 sm:p-8 lg:p-12">
          {/* El fondo detrás del texto lo separa de la ruta, que en el celular pasa por debajo. */}
          <div className="bg-marca-900/80 rounded-xl p-3 pr-5 backdrop-blur-sm">
            <Marca grande />
          </div>
          <div className="hidden lg:block max-w-md bg-marca-900/80 rounded-xl p-6 backdrop-blur-sm">
            <p className="text-3xl font-semibold text-white leading-tight">
              Pedidos, ventas, cobranzas y rutas de los promotores, en un solo lugar.
            </p>
            <p className="mt-3 text-base text-marca-200">
              Cada venta queda con su ubicación, y cada cliente, con su próxima visita.
            </p>
          </div>
        </div>
      </section>

      <section className="flex items-center justify-center px-4 py-10 sm:px-8 lg:col-span-5 lg:py-12">
        <div className="w-full max-w-sm">
          <h1 className="text-2xl font-semibold text-slate-900">Inicia sesión</h1>
          <p className="mt-1 mb-6 text-sm text-slate-600">Con el correo y la contraseña que te dio el administrador.</p>

          <form onSubmit={handleSubmit} className="space-y-4" noValidate>
            <div>
              <label htmlFor="email" className="block text-sm font-medium text-slate-700 mb-1">
                Correo electrónico
              </label>
              <input
                id="email"
                type="email"
                autoComplete="username"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
                placeholder="nombre@empresa.com"
                className={campo}
                required
              />
            </div>

            <div>
              <label htmlFor="password" className="block text-sm font-medium text-slate-700 mb-1">
                Contraseña
              </label>
              <div className="relative">
                <input
                  id="password"
                  type={verContrasena ? 'text' : 'password'}
                  autoComplete="current-password"
                  value={form.password}
                  onChange={(e) => setForm({ ...form, password: e.target.value })}
                  className={`${campo} pr-11`}
                  required
                />
                <button
                  type="button"
                  onClick={() => setVerContrasena((v) => !v)}
                  aria-label={verContrasena ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                  className="absolute inset-y-0 right-0 grid place-items-center w-11 rounded-r-lg text-slate-600 hover:text-slate-900 cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500"
                >
                  {verContrasena ? <EyeOff size={18} aria-hidden="true" /> : <Eye size={18} aria-hidden="true" />}
                </button>
              </div>
            </div>

            {error && <Alert type="error">{error}</Alert>}

            <button
              type="submit"
              disabled={loading}
              className="w-full h-11 inline-flex items-center justify-center gap-2 bg-marca-600 hover:bg-marca-700 disabled:opacity-50 disabled:cursor-not-allowed text-white font-medium rounded-lg transition-colors text-sm cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 focus-visible:ring-offset-2"
            >
              {loading && <LoaderCircle size={18} className="animate-spin" aria-hidden="true" />}
              {loading ? 'Ingresando…' : 'Ingresar'}
            </button>
          </form>
        </div>
      </section>
    </div>
  );
}
