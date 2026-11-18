/// Lo que la API sigue del promotor (tarea 31): su ruta de hoy, si está en curso, y cuántas de sus entregas de hoy están
/// en ruta. Con alguna entrega en ruta, la app manda su ubicación cada 10 s; con la ruta sola, cada 30 s.
class Seguimiento {
  const Seguimiento({this.rutaEnCurso, this.entregasEnRuta = 0});

  factory Seguimiento.fromJson(Map<String, dynamic> json) =>
      Seguimiento(rutaEnCurso: json['rutaEnCurso'] as int?, entregasEnRuta: json['entregasEnRuta'] as int);

  final int? rutaEnCurso;
  final int entregasEnRuta;

  /// Si hay algo que seguir.
  bool get hayAlgo => rutaEnCurso != null || entregasEnRuta > 0;
}
