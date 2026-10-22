/// Un vértice del límite de una zona (5.5).
typedef Vertice = ({double latitud, double longitud});

/// Una zona de venta (`ZonaDTO`), con su promotor y su límite.
class Zona {
  const Zona({required this.id, required this.codigo, required this.nombre, this.promotorId, this.limite});

  factory Zona.fromJson(Map<String, dynamic> json) => Zona(
    id: json['id'] as int,
    codigo: json['codigo'] as String,
    nombre: json['nombre'] as String,
    promotorId: (json['promotor'] as Map<String, dynamic>?)?['id'] as String?,
    limite: (json['limite'] as List?)
        ?.map((v) => (v as Map<String, dynamic>))
        .map<Vertice>((v) => (latitud: (v['latitud'] as num).toDouble(), longitud: (v['longitud'] as num).toDouble()))
        .toList(),
  );

  final int id;
  final String codigo;
  final String nombre;
  final String? promotorId;

  /// Null si todavía no se dibujó.
  final List<Vertice>? limite;
}
