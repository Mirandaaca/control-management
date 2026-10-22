/// Los estados de una HP, en el orden del enum `EstadoHP` de la API.
enum EstadoHP { borrador, confirmada, anulada }

/// El enum `TipoVenta` de la API.
enum TipoVenta { contado, credito }

class Referencia {
  const Referencia({required this.id, required this.nombre});

  factory Referencia.fromJson(Map<String, dynamic> json) =>
      Referencia(id: json['id'].toString(), nombre: (json['nombreCompleto'] ?? json['nombre']) as String);

  final String id;
  final String nombre;
}

/// Una HP de la lista (`HojaPedidoListaDTO`).
class HojaPedidoResumen {
  const HojaPedidoResumen({
    required this.id,
    required this.numero,
    required this.estado,
    required this.tipoVenta,
    required this.total,
    required this.fechaRegistro,
    required this.cliente,
    required this.promotorId,
  });

  factory HojaPedidoResumen.fromJson(Map<String, dynamic> json) => HojaPedidoResumen(
    id: json['id'] as int,
    numero: json['numero'] as String,
    estado: EstadoHP.values[json['estadoHP'] as int],
    tipoVenta: TipoVenta.values[json['tipoVenta'] as int],
    total: (json['total'] as num).toDouble(),
    fechaRegistro: json['fechaRegistro'] as String,
    cliente: Referencia.fromJson(json['cliente'] as Map<String, dynamic>),
    promotorId: (json['promotor'] as Map<String, dynamic>)['id'] as String,
  );

  final int id;
  final String numero;
  final EstadoHP estado;
  final TipoVenta tipoVenta;
  final double total;

  /// En UTC y sin "Z".
  final String fechaRegistro;
  final Referencia cliente;
  final String promotorId;
}

/// Un producto vendido en una HP (2.3): `DetalleHojaPedidoDTO`.
class LineaDeHoja {
  const LineaDeHoja({
    required this.productoId,
    required this.sku,
    required this.descripcion,
    required this.cantidad,
    required this.precioUnitario,
    required this.descuento,
    required this.subtotal,
  });

  factory LineaDeHoja.fromJson(Map<String, dynamic> json) => LineaDeHoja(
    productoId: json['productoId'] as int,
    sku: json['sku'] as String,
    descripcion: json['descripcion'] as String,
    cantidad: (json['cantidad'] as num).toDouble(),
    precioUnitario: (json['precioUnitario'] as num).toDouble(),
    descuento: (json['descuento'] as num).toDouble(),
    subtotal: (json['subtotal'] as num).toDouble(),
  );

  final int productoId;
  final String sku;
  final String descripcion;
  final double cantidad;
  final double precioUnitario;

  /// Por unidad, en Bs.
  final double descuento;
  final double subtotal;
}

/// La cobranza que nació de una HP a crédito.
class CobranzaDeHoja {
  const CobranzaDeHoja({required this.id, required this.numero});

  factory CobranzaDeHoja.fromJson(Map<String, dynamic> json) =>
      CobranzaDeHoja(id: json['id'] as int, numero: json['numero'] as String);

  final int id;
  final String numero;
}

/// Una HP con sus líneas, su ubicación y su cobranza (`HojaPedidoDTO`).
class HojaPedido {
  const HojaPedido({
    required this.id,
    required this.numero,
    required this.estado,
    required this.tipoVenta,
    required this.subTotal,
    required this.descuento,
    required this.total,
    required this.fechaRegistro,
    required this.latitud,
    required this.longitud,
    required this.cliente,
    required this.promotorId,
    required this.lineas,
    this.precisionGps,
    this.cobranza,
    this.motivoDeAnulacion,
    this.fechaAnulacion,
  });

  factory HojaPedido.fromJson(Map<String, dynamic> json) => HojaPedido(
    id: json['id'] as int,
    numero: json['numero'] as String,
    estado: EstadoHP.values[json['estadoHP'] as int],
    tipoVenta: TipoVenta.values[json['tipoVenta'] as int],
    subTotal: (json['subTotal'] as num).toDouble(),
    descuento: (json['descuento'] as num).toDouble(),
    total: (json['total'] as num).toDouble(),
    fechaRegistro: json['fechaRegistro'] as String,
    latitud: (json['latitud'] as num).toDouble(),
    longitud: (json['longitud'] as num).toDouble(),
    precisionGps: (json['precisionGPS'] as num?)?.toDouble(),
    cliente: Referencia.fromJson(json['cliente'] as Map<String, dynamic>),
    promotorId: (json['promotor'] as Map<String, dynamic>)['id'] as String,
    cobranza: json['cobranza'] == null ? null : CobranzaDeHoja.fromJson(json['cobranza'] as Map<String, dynamic>),
    motivoDeAnulacion: json['observacionesAnulacion'] as String?,
    fechaAnulacion: json['fechaAnulacion'] as String?,
    lineas: [for (final l in json['detalles'] as List) LineaDeHoja.fromJson(l as Map<String, dynamic>)],
  );

  final int id;
  final String numero;
  final EstadoHP estado;
  final TipoVenta tipoVenta;
  final double subTotal;

  /// Sobre el total, en Bs.
  final double descuento;
  final double total;
  final String fechaRegistro;
  final double latitud;
  final double longitud;
  final double? precisionGps;
  final Referencia cliente;
  final String promotorId;
  final CobranzaDeHoja? cobranza;
  final String? motivoDeAnulacion;
  final String? fechaAnulacion;
  final List<LineaDeHoja> lineas;
}

/// Una línea del formulario, como la recibe la API.
typedef LineaParaEnviar = ({int productoId, double cantidad, double descuento});

/// Lo que se envía de una HP (8.6.5). La ubicación va aparte: sale del GPS.
class DatosDeHoja {
  const DatosDeHoja({required this.clienteId, required this.tipoVenta, required this.descuento, required this.lineas});

  final int clienteId;
  final TipoVenta tipoVenta;
  final double descuento;
  final List<LineaParaEnviar> lineas;

  Map<String, dynamic> aJson({required double latitud, required double longitud, double? precision}) => {
    'clienteId': clienteId,
    'latitud': double.parse(latitud.toStringAsFixed(6)),
    'longitud': double.parse(longitud.toStringAsFixed(6)),
    'precisionGPS': precision,
    'tipoVenta': tipoVenta.index,
    'descuento': descuento,
    'detalles': [
      for (final l in lineas) {'productoId': l.productoId, 'cantidad': l.cantidad, 'descuento': l.descuento},
    ],
  };
}
