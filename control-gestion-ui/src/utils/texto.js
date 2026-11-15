// Para buscar sin importar mayúsculas ni tildes: "maria" encuentra a "María".
export function normalizar(texto) {
  return (texto ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();
}

/** Si alguno de los textos contiene la búsqueda. Una búsqueda vacía coincide con todo. */
export function coincide(busqueda, ...textos) {
  const buscado = normalizar(busqueda);
  return !buscado || textos.some((t) => normalizar(t).includes(buscado));
}

/**
 * Si cada palabra de la búsqueda está en los textos, en cualquier orden y sin importar mayúsculas ni tildes:
 * "eliminar producto" encuentra "Eliminar productos", y "credito", "crédito". Una búsqueda vacía coincide con todo.
 */
export function coincidenLasPalabras(busqueda, ...textos) {
  const palabras = normalizar(busqueda).split(/\s+/).filter(Boolean);
  const donde = normalizar(textos.join(' '));
  return palabras.every((p) => donde.includes(p));
}
