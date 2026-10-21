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
    // En el login, un 401 significa "credenciales incorrectas": lo muestra esa pantalla, sin recargarla.
    const esElLogin = err.config?.url?.endsWith('/auth/login');
    if (err.response?.status === 401 && !esElLogin) {
      borrarSesion();
      window.location.href = '/login';
    }
    return Promise.reject(err);
  }
);

export default api;
