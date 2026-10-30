import 'hoja_pedido.dart';

/// Los estados de un pedido, por su id: la app nunca compara nombres (el texto sale de la API). El 2 era "En proceso",
/// que ya no existe (tarea 27); los demás no se renumeraron.
abstract final class EstadosDePedido {
  static const pendiente = 1;
  static const enRuta = 3;
  static const entregado = 4;
  static const anulado = 5;
}

class EstadoPedido {
  const EstadoPedido({required this.id, required this.nombre});

  factory EstadoPedido.fromJson(Map<String, dynamic> json) =>
      EstadoPedido(id: json['id'] as int, nombre: json['nombre'] as String);

  final int id;
  final String nombre;
}

Referencia? _referenciaOpcional(Object? json) =>
    json == null ? null : Referencia.fromJson(json as Map<String, dynamic>);

/// Un pedido de una lista o de la bandeja (`PedidoListaDTO`).
class PedidoResumen {
  const PedidoResumen({
    required this.id,
    required this.numero,
    required this.estado,
    required this.total,
    required this.direccionEntrega,
    required this.cliente,
    this.fechaEntrega,
    this.repartidor,
  });

  factory PedidoResumen.fromJson(Map<String, dynamic> json) => PedidoResumen(
    id: json['id'] as int,
    numero: json['numero'] as String,
    estado: EstadoPedido.fromJson(json['estado'] as Map<String, dynamic>),
    total: (json['total'] as num).toDouble(),
    fechaEntrega: json['fechaEntregaEstimada'] as String?,
    direccionEntrega: json['direccionEntrega'] as String,
    cliente: Referencia.fromJson(json['cliente'] as Map<String, dynamic>),
    repartidor: _referenciaOpcional(json['repartidor']),
  );

  final int id;
  final String numero;
  final EstadoPedido estado;
  final double total;

  /// En UTC y sin "Z".
  final String? fechaEntrega;
  final String direccionEntrega;
  final Referencia cliente;

  /// El promotor que tiene asignado el pedido para entregarlo.
  final Referencia? repartidor;
}

/// Un producto del pedido (`DetallePedidoDTO`), en el orden en que se guardó.
class LineaDePedido {
  const LineaDePedido({
    required this.descripcion,
    required this.cantidad,
    required this.precioUnitario,
    required this.descuento,
    required this.subtotal,
  });

  factory LineaDePedido.fromJson(Map<String, dynamic> json) => LineaDePedido(
    descripcion: json['descripcion'] as String,
    cantidad: (json['cantidad'] as num).toDouble(),
    precioUnitario: (json['precioUnitario'] as num).toDouble(),
    descuento: (json['descuento'] as num).toDouble(),
    subtotal: (json['subtotal'] as num).toDouble(),
  );

  final String descripcion;
  final double cantidad;
  final double precioUnitario;

  /// Por unidad, en Bs.
  final double descuento;
  final double subtotal;
}

/// Un cambio del historial del pedido (`HistorialPedidoDTO`): el estado, cuándo, quién y su observación.
class CambioDelPedido {
  const CambioDelPedido({required this.estado, required this.fecha, required this.usuario, this.observacion});

  factory CambioDelPedido.fromJson(Map<String, dynamic> json) => CambioDelPedido(
    estado: EstadoPedido.fromJson(json['estado'] as Map<String, dynamic>),
    fecha: json['fecha'] as String,
    usuario: Referencia.fromJson(json['usuario'] as Map<String, dynamic>).nombre,
    observacion: json['observacion'] as String?,
  );

  final EstadoPedido estado;
  final String fecha;
  final String usuario;
  final String? observacion;
}

/// La venta que nació de la entrega (`HojaDeLaEntregaDTO`).
class VentaDeLaEntrega {
  const VentaDeLaEntrega({required this.id, required this.numero, required this.tipoVenta, this.cobranza});

  factory VentaDeLaEntrega.fromJson(Map<String, dynamic> json) => VentaDeLaEntrega(
    id: json['id'] as int,
    numero: json['numero'] as String,
    tipoVenta: TipoVenta.values[json['tipoVenta'] as int],
    cobranza: json['cobranza'] == null ? null : CobranzaDeHoja.fromJson(json['cobranza'] as Map<String, dynamic>),
  );

  final int id;
  final String numero;
  final TipoVenta tipoVenta;
  final CobranzaDeHoja? cobranza;
}

/// Un pedido con sus productos, su historial y, si se entregó, su venta (`PedidoDTO`).
class Pedido {
  const Pedido({
    required this.id,
    required this.numero,
    required this.estado,
    required this.subTotal,
    required this.descuento,
    required this.total,
    required this.fechaRegistro,
    required this.direccionEntrega,
    required this.cliente,
    required this.registradoPor,
    required this.lineas,
    required this.historial,
    this.fechaEntrega,
    this.fechaEntregaReal,
    this.observaciones,
    this.recibidoPor,
    this.repartidor,
    this.venta,
  });

  factory Pedido.fromJson(Map<String, dynamic> json) => Pedido(
    id: json['id'] as int,
    numero: json['numero'] as String,
    estado: EstadoPedido.fromJson(json['estado'] as Map<String, dynamic>),
    subTotal: (json['subTotal'] as num).toDouble(),
    descuento: (json['descuento'] as num).toDouble(),
    total: (json['total'] as num).toDouble(),
    observaciones: json['observaciones'] as String?,
    fechaRegistro: json['fechaRegistro'] as String,
    fechaEntrega: json['fechaEntregaEstimada'] as String?,
    fechaEntregaReal: json['fechaEntregaReal'] as String?,
    direccionEntrega: json['direccionEntrega'] as String,
    recibidoPor: json['recibidoPor'] as String?,
    cliente: Referencia.fromJson(json['cliente'] as Map<String, dynamic>),
    registradoPor: Referencia.fromJson(json['registradoPor'] as Map<String, dynamic>).nombre,
    repartidor: _referenciaOpcional(json['repartidor']),
    venta: json['hojaPedido'] == null ? null : VentaDeLaEntrega.fromJson(json['hojaPedido'] as Map<String, dynamic>),
    lineas: [for (final l in json['detalles'] as List) LineaDePedido.fromJson(l as Map<String, dynamic>)],
    historial: [for (final h in json['historial'] as List) CambioDelPedido.fromJson(h as Map<String, dynamic>)],
  );

  final int id;
  final String numero;
  final EstadoPedido estado;
  final double subTotal;

  /// Sobre el total, en Bs.
  final double descuento;
  final double total;
  final String? observaciones;
  final String fechaRegistro;
  final String? fechaEntrega;
  final String? fechaEntregaReal;
  final String direccionEntrega;
  final String? recibidoPor;
  final Referencia cliente;
  final String registradoPor;
  final Referencia? repartidor;
  final VentaDeLaEntrega? venta;

  /// En el orden en que se guardaron: el error de stock de la entrega (`detalles[i].cantidad`) es de la línea `i`.
  final List<LineaDePedido> lineas;
  final List<CambioDelPedido> historial;
}

/// Lo que se envía de un pedido nuevo (1.1; 7.3.1, paso 2.1).
class DatosDePedido {
  const DatosDePedido({
    required this.clienteId,
    required this.entrega,
    required this.direccion,
    required this.descuento,
    required this.observaciones,
    required this.lineas,
  });

  final int clienteId;

  /// La fecha y la hora de entrega en la hora de Bolivia, tal cual las eligió el promotor: van sin zona, y la API las
  /// entiende como hora de Bolivia.
  final DateTime entrega;
  final String direccion;
  final double descuento;
  final String observaciones;
  final List<LineaParaEnviar> lineas;

  Map<String, dynamic> aJson({required String clave}) => {
    'clienteId': clienteId,
    'fechaEntregaEstimada': _sinZona(entrega),
    'direccionEntrega': direccion.trim(),
    'descuento': descuento,
    'observaciones': observaciones.trim().isEmpty ? null : observaciones.trim(),
    'detalles': [
      for (final l in lineas) {'productoId': l.productoId, 'cantidad': l.cantidad, 'descuento': l.descuento},
    ],
    'claveIdempotencia': clave,
  };

  static String _sinZona(DateTime momento) {
    String dos(int n) => n.toString().padLeft(2, '0');
    return '${momento.year}-${dos(momento.month)}-${dos(momento.day)}T${dos(momento.hour)}:${dos(momento.minute)}:00';
  }
}

/// Lo que se envía al registrar la entrega (1.2; 7.3.2, paso 3.1). La ubicación va aparte: sale del GPS.
class DatosDeEntrega {
  const DatosDeEntrega({required this.recibidoPor, required this.tipoVenta, required this.observacion});

  final String recibidoPor;
  final TipoVenta tipoVenta;
  final String observacion;

  Map<String, dynamic> aJson({required double latitud, required double longitud, double? precision}) => {
    'recibidoPor': recibidoPor.trim(),
    'tipoVenta': tipoVenta.index,
    'latitud': double.parse(latitud.toStringAsFixed(6)),
    'longitud': double.parse(longitud.toStringAsFixed(6)),
    'precisionGPS': precision,
    'observacion': observacion.trim().isEmpty ? null : observacion.trim(),
  };
}
