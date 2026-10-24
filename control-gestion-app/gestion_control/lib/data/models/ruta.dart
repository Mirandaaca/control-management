/// Los estados de una ruta, con los valores de `EstadoRuta` de la API.
enum EstadoRuta {
  planificada,
  enCurso,
  completada;

  static EstadoRuta desde(int valor) => EstadoRuta.values[valor];
}

/// El resultado de una visita, con los valores de `EstadoVisita` de la API. Ausente no cuenta como visita.
enum EstadoVisita {
  pendiente,
  visitado,
  ausente;

  static EstadoVisita desde(int valor) => EstadoVisita.values[valor];

  /// El número que se manda al marcar: 1 o 2.
  int get valor => index;
}

/// El cliente de una visita (`ClienteDeRutaDTO`).
class ClienteDeRuta {
  const ClienteDeRuta({
    required this.id,
    required this.codigo,
    required this.nombreCompleto,
    required this.direccion,
    required this.latitud,
    required this.longitud,
  });

  factory ClienteDeRuta.fromJson(Map<String, dynamic> json) => ClienteDeRuta(
    id: json['id'] as int,
    codigo: json['codigo'] as String,
    nombreCompleto: json['nombreCompleto'] as String,
    direccion: json['direccionTexto'] as String,
    latitud: (json['latitud'] as num).toDouble(),
    longitud: (json['longitud'] as num).toDouble(),
  );

  final int id;
  final String codigo;
  final String nombreCompleto;
  final String direccion;
  final double latitud;
  final double longitud;
}

/// Una visita de la ruta (`VisitaDTO`), en su orden.
class VisitaDeRuta {
  const VisitaDeRuta({
    required this.id,
    required this.orden,
    required this.resultado,
    required this.cliente,
    this.hora,
    this.observacion,
    this.diasDeAtraso,
  });

  factory VisitaDeRuta.fromJson(Map<String, dynamic> json) => VisitaDeRuta(
    id: json['id'] as int,
    orden: json['orden'] as int,
    resultado: EstadoVisita.desde(json['resultado'] as int),
    hora: json['horaVisita'] as String?,
    observacion: json['observacion'] as String?,
    cliente: ClienteDeRuta.fromJson(json['cliente'] as Map<String, dynamic>),
    diasDeAtraso: json['diasDeAtraso'] as int?,
  );

  final int id;
  final int orden;
  final EstadoVisita resultado;

  /// Cuándo se marcó, en UTC y sin "Z".
  final String? hora;
  final String? observacion;
  final ClienteDeRuta cliente;

  /// El atraso del cliente el día en que se pidió la ruta; null si nunca se visitó. Solo vale en una pendiente.
  final int? diasDeAtraso;
}

/// La ruta de un promotor (`RutaDTO`), con sus visitas en orden.
class Ruta {
  const Ruta({
    required this.id,
    required this.numero,
    required this.fecha,
    required this.estado,
    required this.zonaId,
    required this.zonaNombre,
    required this.marcadas,
    required this.pendientes,
    required this.visitas,
    this.inicio,
    this.fin,
  });

  factory Ruta.fromJson(Map<String, dynamic> json) {
    final zona = json['zona'] as Map<String, dynamic>;
    return Ruta(
      id: json['id'] as int,
      numero: json['numero'] as String,
      fecha: json['fecha'] as String,
      estado: EstadoRuta.desde(json['estado'] as int),
      zonaId: zona['id'] as int,
      zonaNombre: zona['nombre'] as String,
      inicio: json['fechaInicio'] as String?,
      fin: json['fechaFin'] as String?,
      marcadas: json['marcadas'] as int,
      pendientes: json['pendientes'] as int,
      visitas: [for (final v in json['visitas'] as List) VisitaDeRuta.fromJson(v as Map<String, dynamic>)],
    );
  }

  final int id;
  final String numero;

  /// El día de la ruta, "aaaa-mm-dd" (sin hora).
  final String fecha;
  final EstadoRuta estado;
  final int zonaId;
  final String zonaNombre;

  /// Cuándo se inició y cuándo se terminó, en UTC y sin "Z".
  final String? inicio;
  final String? fin;

  /// Las visitadas y las ausentes: el avance ("3 de 5 marcadas").
  final int marcadas;

  /// Las que siguen sin marcar: lo que avisa "Terminar ruta" (ext. 7.1a).
  final int pendientes;
  final List<VisitaDeRuta> visitas;

  int get total => visitas.length;

  /// La siguiente que toca: la primera pendiente, en orden. Lleva la acción principal (8.6.9).
  VisitaDeRuta? get siguiente {
    for (final v in visitas) {
      if (v.resultado == EstadoVisita.pendiente) return v;
    }
    return null;
  }
}
