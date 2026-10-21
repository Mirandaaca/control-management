// Para buscar sin importar mayúsculas ni tildes: "maria" encuentra a "María".
export function normalizar(texto) {
  return (texto ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();
}

/** Si alguno de los textos contiene la búsqueda. Una búsqueda vacía coincide con todo. */
export function coincide(busqueda, ...textos) {
  const buscado = normalizar(busqueda);
  return !buscado || textos.some((t) => normalizar(t).includes(buscado));
}
