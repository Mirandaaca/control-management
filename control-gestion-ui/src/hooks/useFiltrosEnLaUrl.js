import { useMemo, useRef, useState } from 'react';
import { useLocation, useSearchParams } from 'react-router-dom';

const FECHA = /^\d{4}-\d{2}-\d{2}$/;

// Un valor de la URL con el tipo de su valor por defecto. Lo que no es válido (una fecha mal escrita, una casilla con
// otra cosa que "true") vuelve al valor por defecto.
function leer(texto, porDefecto, clave) {
  if (texto === null) return porDefecto;
  if (typeof porDefecto === 'boolean') return texto === 'true';
  if ((clave === 'desde' || clave === 'hasta') && !FECHA.test(texto)) return porDefecto;
  return texto;
}

const filtrosDe = (parametros, sinFiltros) => Object.fromEntries(
  Object.entries(sinFiltros).map(([clave, porDefecto]) => [clave, leer(parametros.get(clave), porDefecto, clave)]),
);

/**
 * Los filtros y la página de una lista, guardados en la URL (docs/diseno.md, sección 8): al recargar, al volver atrás o
 * al abrir un enlace, la lista queda como estaba. Devuelve lo mismo que dos useState, así cada pantalla cambia solo esa
 * línea. La URL lleva solo lo que no es el valor por defecto.
 *
 * - Cambiar un filtro reemplaza la entrada del historial (si no, "Atrás" recorrería cada tecla del buscador) y vuelve a
 *   la página 1; cambiar de página agrega una.
 * - Dos cambios en el mismo clic ("Limpiar" pone los filtros y la página 1) se suman: el segundo parte de lo que dejó el
 *   primero, no de la URL que todavía no cambió.
 * - React Router cambia la URL dentro de una transición: mientras tanto, los filtros salen de una copia local. Sin ella,
 *   el buscador perdería letras al teclear rápido.
 */
export function useFiltrosEnLaUrl(sinFiltros) {
  const [parametros, setParametros] = useSearchParams();
  const { search } = useLocation();
  const pendiente = useRef(null);
  const [local, setLocal] = useState(null);

  const deLaUrl = useMemo(() => filtrosDe(parametros, sinFiltros), [parametros, sinFiltros]);
  const filtros = local?.desde === search ? local.filtros : deLaUrl;
  const pagina = Number.parseInt(parametros.get('pagina') ?? '', 10);
  const numeroPagina = Number.isInteger(pagina) && pagina > 1 ? pagina : 1;

  // Solo se llama desde los manejadores de eventos: ahí se puede leer el ref.
  const escribir = (cambiar, reemplazar) => {
    const base = new URLSearchParams(pendiente.current?.desde === search ? pendiente.current.valor : search);
    const nuevos = new URLSearchParams(base);
    cambiar(nuevos);
    if (nuevos.toString() === base.toString()) return;
    pendiente.current = { desde: search, valor: nuevos.toString() };
    setParametros(nuevos, { replace: reemplazar });
  };

  const setFiltros = (cambio) => escribir((nuevos) => {
    const actuales = filtrosDe(nuevos, sinFiltros);
    const siguientes = { ...sinFiltros, ...(typeof cambio === 'function' ? cambio(actuales) : cambio) };
    for (const [clave, porDefecto] of Object.entries(sinFiltros)) {
      if (siguientes[clave] === porDefecto || siguientes[clave] === '') nuevos.delete(clave);
      else nuevos.set(clave, String(siguientes[clave]));
    }
    nuevos.delete('pagina');
    setLocal({ desde: search, filtros: filtrosDe(nuevos, sinFiltros) });
  }, true);

  const setNumeroPagina = (numero) => escribir((nuevos) => {
    if (numero > 1) nuevos.set('pagina', String(numero));
    else nuevos.delete('pagina');
  }, false);

  return { filtros, setFiltros, numeroPagina, setNumeroPagina };
}
