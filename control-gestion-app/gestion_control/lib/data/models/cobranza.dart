/// Los estados de una cobranza, en el orden del enum `EstadoCobranza` de la API.
enum EstadoCobranza { pendiente, parcial, pagada, vencida, incobrable, anulada }

/// El enum `MetodoPago` de la API.
enum MetodoPago { efectivo, qr }

/// Una cobranza de la lista (`CobranzaResumenDTO`).
class CobranzaResumen {
  const CobranzaResumen({
    required this.id,
    required this.numero,
    required this.estado,
    required this.montoTotal,
    required this.montoPagado,
    required this.saldo,
    required this.fechaEmision,
    required this.fechaVencimiento,
    required this.clienteId,
    required this.clienteNombre,
    this.hojaPedido,
    this.diasDeMora,
  });

  factory CobranzaResumen.fromJson(Map<String, dynamic> json) => CobranzaResumen(
    id: json['id'] as int,
    numero: json['numero'] as String,
    estado: EstadoCobranza.values[json['estado'] as int],
    montoTotal: (json['montoTotal'] as num).toDouble(),
    montoPagado: (json['montoPagado'] as num).toDouble(),
    saldo: (json['saldoPendiente'] as num).toDouble(),
    fechaEmision: json['fechaEmision'] as String,
    fechaVencimiento: json['fechaVencimiento'] as String,
    clienteId: (json['cliente'] as Map<String, dynamic>)['id'] as int,
    clienteNombre: (json['cliente'] as Map<String, dynamic>)['nombreCompleto'] as String,
    hojaPedido: (json['hojaPedido'] as Map<String, dynamic>?)?['numero'] as String?,
    diasDeMora: json['diasDeMora'] as int?,
  );

  final int id;
  final String numero;
  final EstadoCobranza estado;
  final double montoTotal;
  final double montoPagado;
  final double saldo;

  /// En UTC y sin "Z".
  final String fechaEmision;
  final String fechaVencimiento;
  final int clienteId;
  final String clienteNombre;

  /// El número de la HP de la que nació.
  final String? hojaPedido;

  /// Días de calendario de Bolivia desde el vencimiento. Solo en una vencida.
  final int? diasDeMora;

  /// Solo las pendientes, las parciales y las vencidas reciben pagos: una pagada, incobrable o anulada, no.
  bool get recibePagos => recibePagosEn(estado) && saldo > 0;
}

bool recibePagosEn(EstadoCobranza estado) =>
    estado == EstadoCobranza.pendiente || estado == EstadoCobranza.parcial || estado == EstadoCobranza.vencida;

/// Un pago del historial (`PagoDTO`).
class Pago {
  const Pago({
    required this.id,
    required this.monto,
    required this.metodo,
    required this.fecha,
    required this.registradoPor,
    required this.anulado,
    this.referencia,
    this.observaciones,
    this.motivoDeAnulacion,
    this.comprobanteId,
  });

  factory Pago.fromJson(Map<String, dynamic> json) => Pago(
    id: json['id'] as int,
    monto: (json['monto'] as num).toDouble(),
    metodo: MetodoPago.values[json['metodoPago'] as int],
    fecha: json['fechaPago'] as String,
    referencia: json['referencia'] as String?,
    observaciones: json['observaciones'] as String?,
    registradoPor: (json['registradoPor'] as Map<String, dynamic>)['nombreCompleto'] as String,
    anulado: json['anulado'] as bool,
    motivoDeAnulacion: json['motivoAnulacion'] as String?,
    comprobanteId: json['comprobanteId'] as int?,
  );

  final int id;
  final double monto;
  final MetodoPago metodo;
  final String fecha;
  final String? referencia;
  final String? observaciones;
  final String registradoPor;

  /// Un pago anulado queda en el historial con su motivo y deja de contar.
  final bool anulado;
  final String? motivoDeAnulacion;

  /// La foto del comprobante, si tiene.
  final int? comprobanteId;
}

/// Una cobranza con su historial de pagos (`CobranzaDetalleDTO`).
class Cobranza {
  const Cobranza({
    required this.id,
    required this.numero,
    required this.estado,
    required this.montoTotal,
    required this.montoPagado,
    required this.saldo,
    required this.fechaEmision,
    required this.fechaVencimiento,
    required this.clienteNombre,
    required this.pagos,
    this.hojaPedido,
    this.diasDeMora,
  });

  factory Cobranza.fromJson(Map<String, dynamic> json) => Cobranza(
    id: json['id'] as int,
    numero: json['numero'] as String,
    estado: EstadoCobranza.values[json['estado'] as int],
    montoTotal: (json['montoTotal'] as num).toDouble(),
    montoPagado: (json['montoPagado'] as num).toDouble(),
    saldo: (json['saldoPendiente'] as num).toDouble(),
    fechaEmision: json['fechaEmision'] as String,
    fechaVencimiento: json['fechaVencimiento'] as String,
    clienteNombre: (json['cliente'] as Map<String, dynamic>)['nombreCompleto'] as String,
    hojaPedido: (json['hojaPedido'] as Map<String, dynamic>?)?['numero'] as String?,
    diasDeMora: json['diasDeMora'] as int?,
    pagos: [for (final p in json['pagos'] as List) Pago.fromJson(p as Map<String, dynamic>)],
  );

  final int id;
  final String numero;
  final EstadoCobranza estado;
  final double montoTotal;
  final double montoPagado;
  final double saldo;
  final String fechaEmision;
  final String fechaVencimiento;
  final String clienteNombre;
  final String? hojaPedido;
  final int? diasDeMora;
  final List<Pago> pagos;

  bool get recibePagos => recibePagosEn(estado) && saldo > 0;
}

/// Lo que se debe en las cobranzas por cobrar (`ResumenCobranzasDTO`).
class ResumenDeCobranzas {
  const ResumenDeCobranzas({required this.porCobrar, required this.vencidas});

  factory ResumenDeCobranzas.fromJson(Map<String, dynamic> json) =>
      ResumenDeCobranzas(porCobrar: (json['porCobrar'] as num).toDouble(), vencidas: json['vencidas'] as int);

  final double porCobrar;
  final int vencidas;
}

/// El pago recién registrado (`PagoRegistradoDTO`).
class PagoRegistrado {
  const PagoRegistrado({required this.id, required this.estado, required this.saldo});

  factory PagoRegistrado.fromJson(Map<String, dynamic> json) => PagoRegistrado(
    id: json['id'] as int,
    estado: EstadoCobranza.values[json['estado'] as int],
    saldo: (json['saldoPendiente'] as num).toDouble(),
  );

  final int id;
  final EstadoCobranza estado;
  final double saldo;
}

/// Lo que se escribe al registrar un pago (8.6.7). El comprobante va aparte, después del pago.
class DatosDePago {
  const DatosDePago({required this.monto, required this.metodo, this.referencia = '', this.observaciones = ''});

  final double monto;
  final MetodoPago metodo;
  final String referencia;
  final String observaciones;

  Map<String, dynamic> aJson() => {
    'monto': monto,
    'metodoPago': metodo.index,
    // Con efectivo el campo no se muestra: no se manda.
    'referencia': metodo == MetodoPago.qr && referencia.trim().isNotEmpty ? referencia.trim() : null,
    'observaciones': observaciones.trim().isEmpty ? null : observaciones.trim(),
  };
}
