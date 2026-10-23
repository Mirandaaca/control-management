import 'hoja_pedido.dart';

/// El enum `EstadoEvento` de la API: pendiente mientras se atiende y resuelto con su solución.
enum EstadoEvento { pendiente, resuelto }

/// Un tipo de evento (`TipoEventoDTO`). Los que piden evidencia se resuelven con al menos una foto.
class TipoDeEvento {
  const TipoDeEvento({required this.id, required this.nombre, required this.requiereEvidencia});

  factory TipoDeEvento.fromJson(Map<String, dynamic> json) => TipoDeEvento(
    id: json['id'] as int,
    nombre: json['nombre'] as String,
    requiereEvidencia: json['requiereEvidencia'] as bool,
  );

  final int id;
  final String nombre;
  final bool requiereEvidencia;

  /// "Cliente ausente · pide foto", como en el 8.6.8 y en el panel.
  String get texto => requiereEvidencia ? '$nombre · pide foto' : nombre;
}

/// El pedido de un evento (`PedidoResumenDTO`).
class PedidoDelEvento {
  const PedidoDelEvento({required this.id, required this.numero});

  factory PedidoDelEvento.fromJson(Map<String, dynamic> json) =>
      PedidoDelEvento(id: json['id'] as int, numero: json['numero'] as String);

  final int id;
  final String numero;
}

PedidoDelEvento? _pedidoOpcional(Object? json) =>
    json == null ? null : PedidoDelEvento.fromJson(json as Map<String, dynamic>);

Referencia? _referenciaOpcional(Object? json) =>
    json == null ? null : Referencia.fromJson(json as Map<String, dynamic>);

/// Un evento de la lista (`EventoListaDTO`).
class EventoResumen {
  const EventoResumen({
    required this.id,
    required this.numero,
    required this.tipo,
    required this.estado,
    required this.fecha,
    required this.promotor,
    required this.fotos,
    this.cliente,
    this.pedido,
  });

  factory EventoResumen.fromJson(Map<String, dynamic> json) => EventoResumen(
    id: json['id'] as int,
    numero: json['numero'] as String,
    tipo: TipoDeEvento.fromJson(json['tipo'] as Map<String, dynamic>),
    estado: EstadoEvento.values[json['estado'] as int],
    fecha: json['fechaEvento'] as String,
    cliente: _referenciaOpcional(json['cliente']),
    pedido: _pedidoOpcional(json['pedido']),
    promotor: Referencia.fromJson(json['promotor'] as Map<String, dynamic>),
    fotos: json['cantidadFotos'] as int,
  );

  final int id;
  final String numero;
  final TipoDeEvento tipo;
  final EstadoEvento estado;

  /// En UTC y sin "Z".
  final String fecha;
  final Referencia? cliente;
  final PedidoDelEvento? pedido;

  /// Quien lo registró.
  final Referencia promotor;
  final int fotos;
}

/// Una foto de un evento (`FotoDTO`).
class FotoDeEvento {
  const FotoDeEvento({required this.id, required this.fechaSubida, required this.subidoPor, this.fechaCaptura});

  factory FotoDeEvento.fromJson(Map<String, dynamic> json) => FotoDeEvento(
    id: json['id'] as int,
    fechaSubida: json['fechaSubida'] as String,
    subidoPor: Referencia.fromJson(json['subidoPor'] as Map<String, dynamic>).nombre,
    fechaCaptura: json['fechaCaptura'] as String?,
  );

  final int id;
  final String fechaSubida;
  final String subidoPor;

  /// Cuándo se tomó, si la foto salió de la cámara de la app.
  final String? fechaCaptura;
}

/// Un evento con sus fotos (`EventoDTO`).
class Evento {
  const Evento({
    required this.id,
    required this.numero,
    required this.tipo,
    required this.estado,
    required this.fecha,
    required this.latitud,
    required this.longitud,
    required this.descripcion,
    required this.promotor,
    required this.fotos,
    this.cliente,
    this.pedido,
    this.solucion,
    this.fechaResolucion,
    this.resueltoPor,
  });

  factory Evento.fromJson(Map<String, dynamic> json) => Evento(
    id: json['id'] as int,
    numero: json['numero'] as String,
    tipo: TipoDeEvento.fromJson(json['tipo'] as Map<String, dynamic>),
    estado: EstadoEvento.values[json['estado'] as int],
    fecha: json['fechaEvento'] as String,
    latitud: (json['latitud'] as num).toDouble(),
    longitud: (json['longitud'] as num).toDouble(),
    descripcion: json['descripcion'] as String,
    cliente: _referenciaOpcional(json['cliente']),
    pedido: _pedidoOpcional(json['pedido']),
    promotor: Referencia.fromJson(json['promotor'] as Map<String, dynamic>),
    solucion: json['solucion'] as String?,
    fechaResolucion: json['fechaResolucion'] as String?,
    resueltoPor: _referenciaOpcional(json['resueltoPor'])?.nombre,
    fotos: [for (final f in json['fotos'] as List) FotoDeEvento.fromJson(f as Map<String, dynamic>)],
  );

  final int id;
  final String numero;
  final TipoDeEvento tipo;
  final EstadoEvento estado;
  final String fecha;
  final double latitud;
  final double longitud;
  final String descripcion;
  final Referencia? cliente;
  final PedidoDelEvento? pedido;

  /// Quien lo registró: solo él lo modifica y administra sus fotos (7.3.13, ext. 3b; 7.3.14, ext. *a).
  final Referencia promotor;
  final String? solucion;
  final String? fechaResolucion;
  final String? resueltoPor;
  final List<FotoDeEvento> fotos;
}

/// Lo que se envía de un evento (7.3.13, paso 2.2). La ubicación va aparte: sale del GPS, y al modificarlo no cambia.
class DatosDeEvento {
  const DatosDeEvento({required this.tipoEventoId, required this.descripcion, this.clienteId, this.pedidoId});

  final int tipoEventoId;
  final int? clienteId;
  final int? pedidoId;
  final String descripcion;

  Map<String, dynamic> aJson() => {
    'tipoEventoId': tipoEventoId,
    'clienteId': clienteId,
    'pedidoId': pedidoId,
    'descripcion': descripcion.trim(),
  };
}
