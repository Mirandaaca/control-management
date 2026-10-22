import '../data/models/zona.dart';

/// Si el punto cae dentro del polígono, con el algoritmo del rayo: se cuentan los lados que cruza una línea que sale del
/// punto hacia el este. Para las pocas zonas de una ciudad, la latitud y la longitud sirven como coordenadas planas. Es
/// `puntoEnPoligono` del panel (`utils/geometria.js`): si cambia allá, cambia aquí.
bool puntoEnPoligono(double latitud, double longitud, List<Vertice> vertices) {
  var dentro = false;
  for (var i = 0, j = vertices.length - 1; i < vertices.length; j = i++) {
    final (yi, xi) = (vertices[i].latitud, vertices[i].longitud);
    final (yj, xj) = (vertices[j].latitud, vertices[j].longitud);
    final cruza = (yi > latitud) != (yj > latitud) && longitud < (xj - xi) * (latitud - yi) / (yj - yi) + xi;
    if (cruza) dentro = !dentro;
  }
  return dentro;
}

/// La primera zona cuyo límite contiene el punto, o null. Los límites pueden superponerse (respuesta 2 de la tarea 12).
Zona? zonaQueContiene(double latitud, double longitud, Iterable<Zona> zonas) {
  for (final zona in zonas) {
    final limite = zona.limite;
    if (limite != null && puntoEnPoligono(latitud, longitud, limite)) return zona;
  }
  return null;
}
