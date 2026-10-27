import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Si una lista está pidiendo otros datos que los que muestra, para la barra de recarga de Table (tarea 21).
 * - "pedido" cambia con lo que se pide: la función cargar de un useCallback, que cambia con los filtros y la página.
 * - Al llegar la respuesta, terminar(pedido) baja la marca. La respuesta de un pedido viejo no cuenta: si los filtros
 *   cambiaron dos veces, la barra sigue hasta que llega la del último.
 *
 * Se usa así, con el cargar() que devuelve la promesa:
 *   const { recargando, terminar } = useRecarga(cargar);
 *   useEffect(() => { cargar().finally(() => terminar(cargar)); }, [cargar, terminar]);
 */
export function useRecarga(pedido) {
  // Envuelto en una función: el pedido suele ser una función, y useState y setState llamarían a una función suelta.
  const [mostrado, setMostrado] = useState(() => pedido);
  const ultimo = useRef(pedido);
  useEffect(() => { ultimo.current = pedido; }, [pedido]);
  const terminar = useCallback((de) => { if (de === ultimo.current) setMostrado(() => de); }, []);
  return { recargando: mostrado !== pedido, terminar };
}
