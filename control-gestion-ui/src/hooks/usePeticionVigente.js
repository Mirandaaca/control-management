import { useCallback, useRef } from 'react';

/**
 * Para que una respuesta vieja no pise a una nueva: si los filtros cambian dos veces y la primera respuesta llega
 * última, la lista mostraría lo de antes. Cada petición toma su número con nuevaPeticion(), y siVigente(fn) corre fn
 * solo si esa petición sigue siendo la última. Si no, la descarta, también su error: ya no corresponde a lo que se pidió.
 *
 *   const nuevaPeticion = usePeticionVigente();
 *   const cargar = useCallback(() => {
 *     const siVigente = nuevaPeticion();
 *     return api.get(ruta, { params })
 *       .then(siVigente(({ data }) => setPagina(data)))
 *       .catch(siVigente((err) => setErrorCarga(mensajeDeError(err, '…'))))
 *       .finally(siVigente(() => setCargando(false)));
 *   }, [ruta, params, nuevaPeticion]);
 */
export function usePeticionVigente() {
  const ultima = useRef(0);
  return useCallback(() => {
    const numero = ++ultima.current;
    return (fn) => (...args) => (numero === ultima.current ? fn(...args) : undefined);
  }, []);
}
