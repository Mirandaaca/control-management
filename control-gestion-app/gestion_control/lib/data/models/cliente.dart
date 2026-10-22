/// El estado de un cliente (5.2 y 7.4): `EstadoClienteDTO`.
class EstadoCliente {
  const EstadoCliente({required this.id, required this.nombre, required this.color, required this.delSistema});

  factory EstadoCliente.fromJson(Map<String, dynamic> json) => EstadoCliente(
    id: json['id'] as int,
    nombre: json['nombre'] as String,
    color: json['color'] as String,
    delSistema: json['delSistema'] as bool,
  );

  // Los ids fijos de la semilla, como ACTIVO y DISPONIBLE en EstadoCliente.jsx del panel.
  static const idActivo = 1;
  static const idDisponible = 2;

  final int id;
  final String nombre;

  /// El color en hexadecimal ("#475569"). En Activo y Disponible su significado es fijo: rojo y verde.
  final String color;

  /// Activo y Disponible para re-visita, que asigna el sistema según las visitas.
  final bool delSistema;
}

class ZonaResumen {
  const ZonaResumen({required this.id, required this.nombre});

  factory ZonaResumen.fromJson(Map<String, dynamic> json) =>
      ZonaResumen(id: json['id'] as int, nombre: json['nombre'] as String);

  final int id;
  final String nombre;
}

/// Un cliente de la lista (`ClienteListaDTO`).
class ClienteResumen {
  const ClienteResumen({
    required this.id,
    required this.codigo,
    required this.nombreCompleto,
    required this.telefono,
    required this.direccion,
    required this.zona,
    required this.estado,
    required this.saldoPendiente,
  });

  factory ClienteResumen.fromJson(Map<String, dynamic> json) => ClienteResumen(
    id: json['id'] as int,
    codigo: json['codigo'] as String,
    nombreCompleto: json['nombreCompleto'] as String,
    telefono: json['telefono'] as String,
    direccion: json['direccionTexto'] as String,
    zona: ZonaResumen.fromJson(json['zona'] as Map<String, dynamic>),
    estado: EstadoCliente.fromJson(json['estado'] as Map<String, dynamic>),
    saldoPendiente: (json['saldoPendiente'] as num).toDouble(),
  );

  final int id;
  final String codigo;
  final String nombreCompleto;
  final String telefono;
  final String direccion;
  final ZonaResumen zona;
  final EstadoCliente estado;
  final double saldoPendiente;
}

/// Un cliente con todos sus datos (`ClienteDTO`).
class Cliente {
  const Cliente({
    required this.id,
    required this.codigo,
    required this.nombreCompleto,
    required this.telefono,
    required this.direccion,
    required this.latitud,
    required this.longitud,
    required this.zona,
    required this.estado,
    required this.saldoPendiente,
    this.razonSocial,
    this.nit,
    this.email,
    this.ultimaVisita,
    this.proximaVisita,
  });

  factory Cliente.fromJson(Map<String, dynamic> json) => Cliente(
    id: json['id'] as int,
    codigo: json['codigo'] as String,
    nombreCompleto: json['nombreCompleto'] as String,
    razonSocial: json['razonSocial'] as String?,
    nit: json['nit'] as String?,
    telefono: json['telefono'] as String,
    email: json['email'] as String?,
    direccion: json['direccionTexto'] as String,
    latitud: (json['latitud'] as num).toDouble(),
    longitud: (json['longitud'] as num).toDouble(),
    zona: ZonaResumen.fromJson(json['zona'] as Map<String, dynamic>),
    estado: EstadoCliente.fromJson(json['estado'] as Map<String, dynamic>),
    saldoPendiente: (json['saldoPendiente'] as num).toDouble(),
    ultimaVisita: json['ultimaVisita'] as String?,
    proximaVisita: json['proximaVisita'] as String?,
  );

  final int id;
  final String codigo;
  final String nombreCompleto;
  final String? razonSocial;
  final String? nit;
  final String telefono;
  final String? email;
  final String direccion;
  final double latitud;
  final double longitud;
  final ZonaResumen zona;
  final EstadoCliente estado;
  final double saldoPendiente;

  /// En UTC y sin "Z", como las manda la API.
  final String? ultimaVisita;
  final String? proximaVisita;

  /// Los clientes viejos se guardaban en 0, 0 cuando no se marcaba el punto: un lugar en el océano (7.3.8, ext. 2.2a).
  bool get tieneUbicacion => latitud != 0 || longitud != 0;

  ClienteOpcion get comoOpcion => ClienteOpcion(
    id: id,
    codigo: codigo,
    nombreCompleto: nombreCompleto,
    direccion: direccion,
    estado: estado,
    inactivo: false,
  );
}

/// Un cliente para elegir en un formulario (`ClienteOpcionDTO`).
class ClienteOpcion {
  const ClienteOpcion({
    required this.id,
    required this.codigo,
    required this.nombreCompleto,
    required this.direccion,
    required this.estado,
    required this.inactivo,
  });

  factory ClienteOpcion.fromJson(Map<String, dynamic> json) => ClienteOpcion(
    id: json['id'] as int,
    codigo: json['codigo'] as String,
    nombreCompleto: json['nombreCompleto'] as String,
    direccion: json['direccionTexto'] as String,
    estado: EstadoCliente.fromJson(json['estado'] as Map<String, dynamic>),
    inactivo: json['inactivo'] as bool,
  );

  final int id;
  final String codigo;
  final String nombreCompleto;
  final String direccion;
  final EstadoCliente estado;

  /// No se le registran HP (7.3.5, ext. 2.2c).
  final bool inactivo;

  /// "Rosa Quispe Mamani · CLI-00042", como en el campo del 8.6.5.
  String get texto => '$nombreCompleto · $codigo';
}

/// Lo que se escribe en el formulario del cliente (8.6.6). La ubicación va aparte: sale del GPS.
class DatosDeCliente {
  const DatosDeCliente({
    required this.nombreCompleto,
    required this.telefono,
    required this.direccion,
    required this.zonaId,
    this.razonSocial = '',
    this.nit = '',
    this.email = '',
  });

  final String nombreCompleto;
  final String telefono;
  final String direccion;
  final int? zonaId;
  final String razonSocial;
  final String nit;
  final String email;

  Map<String, dynamic> aJson({required double latitud, required double longitud}) {
    String? opcional(String texto) => texto.trim().isEmpty ? null : texto.trim();
    return {
      'nombreCompleto': nombreCompleto.trim(),
      'telefono': telefono.trim(),
      'direccionTexto': direccion.trim(),
      'zonaId': zonaId ?? 0,
      'razonSocial': opcional(razonSocial),
      'nit': opcional(nit),
      'email': opcional(email),
      // Seis decimales, unos 10 cm: la columna de la base guarda ocho, pero el GPS no da más.
      'latitud': double.parse(latitud.toStringAsFixed(6)),
      'longitud': double.parse(longitud.toStringAsFixed(6)),
    };
  }
}
