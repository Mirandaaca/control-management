import { useEffect, useState } from 'react';

/**
 * Devuelve el valor cuando dejó de cambiar durante "ms" milisegundos. Sirve para que un buscador pida a la API al
 * terminar de escribir, no con cada tecla.
 */
export function useDemorado(valor, ms = 300) {
  const [demorado, setDemorado] = useState(valor);
  useEffect(() => {
    const espera = setTimeout(() => setDemorado(valor), ms);
    return () => clearTimeout(espera);
  }, [valor, ms]);
  return demorado;
}
