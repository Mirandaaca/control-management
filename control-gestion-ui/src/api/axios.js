import axios from 'axios';
import { borrarSesion, leerToken } from './sesion';

/** La raíz de la API, sin /api ni barra final: sale de VITE_API_URL (`.env.local` o el build de CI). */
export const URL_DE_LA_API = import.meta.env.VITE_API_URL.replace(/\/+$/, '').replace(/\/api$/, '');

const api = axios.create({
  baseURL: `${URL_DE_LA_API}/api`,
});

/**
 * En el plan gratis de Azure la API se duerme, y despertarla tarda. El inicio de sesión la llama al abrirse, sin esperar
 * la respuesta: despierta mientras el usuario escribe.
 */
export function despertarLaApi() {
  api.get('/health', { baseURL: URL_DE_LA_API })
    .catch((err) => console.info('No se pudo despertar la API; el inicio de sesión lo intentará igual.', err));
}

api.interceptors.request.use((config) => {
  const token = leerToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (err) => {
    // En el login, un 401 significa "credenciales incorrectas", y en el logout, que la sesión ya estaba cerrada: lo
    // resuelve esa pantalla, sin recargarla.
    const url = err.config?.url ?? '';
    const esDeLaSesion = url.endsWith('/auth/login') || url.endsWith('/auth/logout');
    // Solo cuenta un 401 que trajo la sesión guardada ahora: el de una petición de antes de cerrar sesión, o de una
    // sesión anterior, no debe sacar a la actual.
    const token = leerToken();
    const conLaSesionActual = token && err.config?.headers?.Authorization === `Bearer ${token}`;
    if (err.response?.status === 401 && !esDeLaSesion && conLaSesionActual) {
      borrarSesion();
      // Una sesión cerrada, vencida o de un usuario desactivado: el inicio de sesión lo avisa.
      const terminada = err.response.data?.codigo === 'sesion_terminada';
      window.location.href = terminada ? '/login?sesion=terminada' : '/login';
    }
    return Promise.reject(err);
  }
);

export default api;
