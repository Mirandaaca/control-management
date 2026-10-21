/// El perfil propio (8.3): el `PerfilDTO` de `GET /perfil`.
class Perfil {
  const Perfil({
    required this.id,
    required this.email,
    required this.nombreCompleto,
    required this.rol,
    required this.fechaCreacion,
  });

  factory Perfil.fromJson(Map<String, dynamic> json) => Perfil(
    id: json['id'] as String,
    email: json['email'] as String,
    nombreCompleto: json['nombreCompleto'] as String,
    rol: json['rol'] as String?,
    fechaCreacion: json['fechaCreacion'] as String,
  );

  final String id;
  final String email;
  final String nombreCompleto;
  final String? rol;

  /// En UTC y sin "Z", como la manda la API: se muestra con `formatearFecha`.
  final String fechaCreacion;
}
