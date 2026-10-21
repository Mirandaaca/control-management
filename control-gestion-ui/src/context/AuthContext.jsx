import { useCallback, useEffect, useState } from 'react';
import api from '../api/axios';
import { borrarSesion, guardarSesion, guardarUsuario, leerToken, leerUsuario } from '../api/sesion';
import { AuthContext } from './auth';

// La API guarda desde dónde se abrió cada sesión: 0 es el panel y 1, la app.
const ORIGEN_PANEL = 0;

export function AuthProvider({ children }) {
  // Sin token no hay sesión, aunque haya un usuario guardado de antes.
  const [usuario, setUsuario] = useState(() => (leerToken() ? leerUsuario() : null));

  const refrescar = useCallback(() => api.get('/auth/yo').then(({ data }) => {
    guardarUsuario(data);
    setUsuario(data);
    return data;
  }), []);

  // Los permisos pueden haber cambiado desde el último inicio de sesión: al abrir el panel se piden de nuevo.
  useEffect(() => {
    if (!leerToken()) return;
    refrescar().catch((err) => {
      // Un 401 ya cierra la sesión (interceptor de axios). Si la API no responde, se sigue con los permisos guardados:
      // la API igual rechaza lo que no corresponda.
      console.warn('No se pudo actualizar la sesión; se usan los permisos guardados.', err);
    });
  }, [refrescar]);

  const login = async (email, password) => {
    const { data } = await api.post('/auth/login', { email, password, origen: ORIGEN_PANEL });
    guardarSesion(data.token, data.usuario);
    setUsuario(data.usuario);
    return data.usuario;
  };

  // Cierra la sesión en la API, así ese token deja de servir aunque alguien lo haya copiado. La sesión local se borra
  // antes: una petición de la pantalla que responda 401 por este cierre ya no se toma como una sesión terminada. Si la
  // API no responde, la sesión del servidor vence sola por inactividad.
  const logout = async () => {
    const token = leerToken();
    borrarSesion();
    try {
      if (token) await api.post('/auth/logout', null, { headers: { Authorization: `Bearer ${token}` } });
    } catch (err) {
      console.warn('No se pudo cerrar la sesión en el servidor; vencerá por inactividad.', err);
    } finally {
      setUsuario(null);
    }
  };

  // Basta con uno de los permisos, igual que [Permiso(...)] en la API.
  const tiene = useCallback(
    (...claves) => claves.some((clave) => usuario?.permisos?.includes(clave)),
    [usuario],
  );

  return (
    <AuthContext.Provider value={{ usuario, tiene, login, logout, refrescar }}>
      {children}
    </AuthContext.Provider>
  );
}
