import { useState } from 'react';
import { TAMANO_PAGINA } from '../utils/paginas';

/**
 * La paginación de una lista que llega entera de la API (los catálogos, Usuarios y los reportes agregados; tarea 33):
 * se pagina en el navegador, con la misma `Paginacion` de las listas paginadas en la API.
 *
 * - `clave` resume la búsqueda y los filtros: si cambia, la lista vuelve a la página 1, como las paginadas en la API.
 *   Se ajusta durante el render, como recomienda React para un estado que depende de una prop, y no en un efecto.
 * - Si la lista se achica (se eliminó el único registro de la última página), muestra la última página que tiene filas.
 * - Las filas por página se conservan al buscar o filtrar.
 *
 * Devuelve las filas de la página (`visibles`) y las props de `<Paginacion {...paginacion} />`.
 */
export function usePaginaLocal(lista, clave = '') {
  const [estado, setEstado] = useState({ clave, pagina: 1, tamano: TAMANO_PAGINA });
  if (estado.clave !== clave) setEstado({ ...estado, clave, pagina: 1 });

  const filas = lista ?? [];
  const { tamano } = estado;
  const totalPaginas = Math.max(1, Math.ceil(filas.length / tamano));
  const pagina = Math.min(estado.clave === clave ? estado.pagina : 1, totalPaginas);
  return {
    visibles: filas.slice((pagina - 1) * tamano, pagina * tamano),
    paginacion: {
      pagina,
      totalPaginas,
      total: filas.length,
      tamano,
      onCambiar: (numero) => setEstado((e) => ({ ...e, pagina: numero })),
      onCambiarTamano: (nuevo) => setEstado((e) => ({ ...e, pagina: 1, tamano: nuevo })),
    },
  };
}
