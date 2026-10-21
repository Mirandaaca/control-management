import axios from 'axios';
import { borrarSesion, leerToken } from './sesion';

const api = axios.create({
  baseURL: 'http://localhost:5124/api',
});

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
