/**
 * Si el punto cae dentro del polígono, con el algoritmo del rayo: se cuentan los lados que cruza una línea que sale del
 * punto hacia el este. Para las pocas zonas de una ciudad, la latitud y la longitud sirven como coordenadas planas.
 * vertices: [{ latitud, longitud }], como los devuelve la API.
 */
export function puntoEnPoligono(latitud, longitud, vertices) {
  let dentro = false;
  for (let i = 0, j = vertices.length - 1; i < vertices.length; j = i++) {
    const [yi, xi] = [Number(vertices[i].latitud), Number(vertices[i].longitud)];
    const [yj, xj] = [Number(vertices[j].latitud), Number(vertices[j].longitud)];
    const cruza = (yi > latitud) !== (yj > latitud) && longitud < ((xj - xi) * (latitud - yi)) / (yj - yi) + xi;
    if (cruza) dentro = !dentro;
  }
  return dentro;
}

/** La primera zona cuyo límite contiene el punto, o undefined. Los límites pueden superponerse (respuesta 2 de la tarea 12). */
export function zonaQueContiene(latitud, longitud, zonas) {
  return zonas.find((z) => z.limite && puntoEnPoligono(latitud, longitud, z.limite));
}
