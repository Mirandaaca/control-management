/// Quién inició sesión y qué puede hacer: el `SesionDTO` de la API (login y `GET /auth/yo`).
class Sesion {
  const Sesion({
    required this.id,
    required this.email,
    required this.nombreCompleto,
    required this.rol,
    required this.permisos,
  });

  factory Sesion.fromJson(Map<String, dynamic> json) => Sesion(
    id: json['id'] as String,
    email: json['email'] as String,
    nombreCompleto: json['nombreCompleto'] as String,
    rol: json['rol'] as String?,
    permisos: Set.unmodifiable((json['permisos'] as List).cast<String>()),
  );

  final String id;
  final String email;
  final String nombreCompleto;

  /// Solo para mostrarlo: la app nunca decide nada por el nombre del rol.
  final String? rol;

  final Set<String> permisos;

  /// Si tiene alguno de esos permisos: basta con uno, igual que `[Permiso(...)]` en la API.
  bool tiene(Iterable<String> claves) => claves.any(permisos.contains);
}
