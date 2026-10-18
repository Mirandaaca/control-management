import { createContext, useContext } from 'react';

// Separado de AuthContext.jsx: un archivo con componentes solo debe exportar componentes para que Vite
// pueda recargarlos en caliente.
export const AuthContext = createContext(null);

export const useAuth = () => useContext(AuthContext);
