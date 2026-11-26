// Cuántas filas muestra cada página de una lista (tarea 33). La API acepta hasta 100 (`tamano` de sus filtros).
export const TAMANO_PAGINA = 25;
export const TAMANOS_DE_PAGINA = [25, 50, 100];

/** El tamaño de la URL o de un selector, si es uno de los que se ofrecen; si no, el de siempre. */
export function tamanoValido(valor) {
  const numero = Number(valor);
  return TAMANOS_DE_PAGINA.includes(numero) ? numero : TAMANO_PAGINA;
}
