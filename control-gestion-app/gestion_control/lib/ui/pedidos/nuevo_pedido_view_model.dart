import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cliente.dart';
import '../../data/models/pedido.dart';
import '../../data/models/producto.dart';
import '../../data/services/clave_de_idempotencia.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/lineas/lineas.dart';

const _igual = Object();

/// La hora de Bolivia de ahora, como un momento sin zona (Bolivia está en UTC−4 todo el año).
DateTime ahoraEnBolivia() {
  final utc = DateTime.now().toUtc().subtract(const Duration(hours: 4));
  return DateTime(utc.year, utc.month, utc.day, utc.hour, utc.minute, utc.second);
}

class EstadoNuevoPedido {
  const EstadoNuevoPedido({
    this.productos,
    this.errorDeCarga,
    this.reintentando = false,
    this.cliente,
    this.direccion = '',
    this.direccionPropuesta = '',
    this.fecha,
    this.hora,
    this.lineas = const [LineaDelFormulario(clave: 0)],
    this.descuento = '',
    this.campos = const {},
    this.enviando = false,
    this.errorAlEnviar,
  });

  /// El catálogo con su stock, para elegir el producto de cada línea. Null mientras carga.
  final List<Producto>? productos;
  final ErrorDeApi? errorDeCarga;
  final bool reintentando;
  final ClienteOpcion? cliente;
  final String direccion;

  /// La dirección que se propuso del cliente: si nadie la cambió, se reemplaza al elegir otro cliente.
  final String direccionPropuesta;

  /// El día y la hora de entrega, en la hora de Bolivia.
  final DateTime? fecha;
  final TimeOfDay? hora;
  final List<LineaDelFormulario> lineas;

  /// El descuento sobre el total, tal cual se escribió.
  final String descuento;

  /// Los errores por campo, con las claves de la API: `clienteId`, `fechaEntregaEstimada`, `detalles[1].cantidad`.
  final Map<String, String> campos;
  final bool enviando;
  final ErrorDeApi? errorAlEnviar;

  double get subtotal => Lineas.subtotal(lineas);
  double get descuentoNumero => leerNumero(descuento) ?? 0;
  double get total => subtotal - descuentoNumero;

  /// La fecha y la hora de entrega juntas, o null si falta una.
  DateTime? get entrega {
    final fecha = this.fecha;
    final hora = this.hora;
    return fecha == null || hora == null ? null : DateTime(fecha.year, fecha.month, fecha.day, hora.hour, hora.minute);
  }

  /// El stock no se exige al registrar: se valida al entregar. El aviso dice qué pasaría.
  String? avisoDeStock(LineaDelFormulario linea) =>
      Lineas.avisoDeStock(lineas, linea, consecuencia: 'con esta cantidad no se podrá entregar.');

  EstadoNuevoPedido copiar({
    Object? productos = _igual,
    Object? errorDeCarga = _igual,
    bool? reintentando,
    Object? cliente = _igual,
    String? direccion,
    String? direccionPropuesta,
    Object? fecha = _igual,
    Object? hora = _igual,
    List<LineaDelFormulario>? lineas,
    String? descuento,
    Map<String, String>? campos,
    bool? enviando,
    Object? errorAlEnviar = _igual,
  }) => EstadoNuevoPedido(
    productos: productos == _igual ? this.productos : productos as List<Producto>?,
    errorDeCarga: errorDeCarga == _igual ? this.errorDeCarga : errorDeCarga as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    cliente: cliente == _igual ? this.cliente : cliente as ClienteOpcion?,
    direccion: direccion ?? this.direccion,
    direccionPropuesta: direccionPropuesta ?? this.direccionPropuesta,
    fecha: fecha == _igual ? this.fecha : fecha as DateTime?,
    hora: hora == _igual ? this.hora : hora as TimeOfDay?,
    lineas: lineas ?? this.lineas,
    descuento: descuento ?? this.descuento,
    campos: campos ?? this.campos,
    enviando: enviando ?? this.enviando,
    errorAlEnviar: errorAlEnviar == _igual ? this.errorAlEnviar : errorAlEnviar as ErrorDeApi?,
  );
}

/// El aviso de éxito, con el texto del panel (`Pedidos.jsx`).
String mensajeDePedido(Pedido pedido) =>
    'Se registró ${pedido.numero} por ${formatearMonto(pedido.total)}, con entrega el '
    '${formatearFecha(pedido.fechaEntrega, hora: true)}.';

/// Registrar un pedido (1.1; 7.3.1, paso 2): el cliente, la dirección de entrega (propuesta con la del cliente), la
/// fecha y la hora de entrega, los productos, el descuento y las observaciones. Sin GPS: la 7.3.1 no lo pide.
class NuevoPedidoViewModel extends Notifier<EstadoNuevoPedido> {
  final _clave = ClaveDeIdempotencia();
  var _siguienteClave = 1;

  // Errores de la API que pertenecen a un campo.
  static const _campoDelCodigo = {
    'cliente_inactivo': 'clienteId',
    'cliente_no_existe': 'clienteId',
    'fecha_en_el_pasado': 'fechaEntregaEstimada',
    'total_invalido': 'descuento',
    'producto_no_disponible': 'detalles',
  };

  @override
  EstadoNuevoPedido build() {
    Future.microtask(cargarProductos);
    return const EstadoNuevoPedido();
  }

  Future<void> cargarProductos() async {
    state = state.copiar(reintentando: state.errorDeCarga != null);
    try {
      final productos = await ref.read(productosRepositoryProvider).listar();
      if (ref.mounted) state = state.copiar(productos: productos, errorDeCarga: null, reintentando: false);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(errorDeCarga: e, reintentando: false);
    }
  }

  /// Propone la dirección del cliente como dirección de entrega, salvo que el promotor ya haya escrito otra.
  void elegirCliente(ClienteOpcion cliente) {
    final propuesta = state.direccion.trim().isEmpty || state.direccion == state.direccionPropuesta;
    state = state.copiar(
      cliente: cliente,
      direccion: propuesta ? cliente.direccion : null,
      direccionPropuesta: cliente.direccion,
      campos: _sin(state.campos, ['clienteId', if (propuesta) 'direccionEntrega']),
    );
  }

  void cambiarDireccion(String texto) =>
      state = state.copiar(direccion: texto, campos: _sin(state.campos, ['direccionEntrega']));

  void elegirFecha(DateTime fecha) =>
      state = state.copiar(fecha: fecha, campos: _sin(state.campos, ['fechaEntregaEstimada']));

  void elegirHora(TimeOfDay hora) =>
      state = state.copiar(hora: hora, campos: _sin(state.campos, ['fechaEntregaEstimada']));

  /// Agrega una línea vacía y devuelve su clave, para elegir su producto enseguida.
  int agregarLinea() {
    final clave = _siguienteClave++;
    state = state.copiar(
      lineas: [
        ...state.lineas,
        LineaDelFormulario(clave: clave),
      ],
      campos: Lineas.sinErroresDeLineas(state.campos),
    );
    return clave;
  }

  void quitarLinea(int clave) => state = state.copiar(
    lineas: [
      for (final l in state.lineas)
        if (l.clave != clave) l,
    ],
    campos: Lineas.sinErroresDeLineas(state.campos),
  );

  void elegirProducto(int clave, Producto producto) => _cambiarLinea(clave, (l) => l.con(producto: producto));

  void cambiarCantidad(int clave, String texto) => _cambiarLinea(clave, (l) => l.con(cantidad: texto));

  void cambiarDescuentoDeLinea(int clave, String texto) => _cambiarLinea(clave, (l) => l.con(descuento: texto));

  void cambiarDescuento(String texto) =>
      state = state.copiar(descuento: texto, campos: _sin(state.campos, ['descuento']));

  void _cambiarLinea(int clave, LineaDelFormulario Function(LineaDelFormulario) cambio) {
    final cambiado = Lineas.cambiar(state.lineas, state.campos, clave, cambio);
    state = state.copiar(lineas: cambiado.lineas, campos: cambiado.campos);
  }

  /// Revisa el formulario con las reglas de la API (7.3.1, ext. 2.1b): cada error va en su campo.
  bool revisar() {
    final entrega = state.entrega;
    final errores = <String, String>{
      if (state.cliente == null) 'clienteId': 'Elige el cliente.',
      if (state.direccion.trim().isEmpty) 'direccionEntrega': 'Escribe la dirección de entrega.',
      if (entrega == null)
        'fechaEntregaEstimada': 'Elige la fecha y la hora de entrega.'
      else if (!entrega.isAfter(ahoraEnBolivia()))
        'fechaEntregaEstimada': 'La fecha y la hora de entrega ya pasaron: elige unas futuras.',
      ...Lineas.errores(state.lineas),
    };
    if (!errores.keys.any((c) => c.startsWith('detalles')) &&
        centavos(state.descuentoNumero) >= centavos(state.subtotal)) {
      errores['descuento'] = 'El descuento tiene que ser menor que el subtotal (${formatearMonto(state.subtotal)}).';
    }
    state = state.copiar(campos: errores, errorAlEnviar: null);
    return errores.isEmpty;
  }

  /// Registra el pedido. Devuelve el pedido, o null si algo lo impidió: los errores quedan en el estado y lo escrito no
  /// se pierde.
  Future<Pedido?> registrar({required String observaciones}) async {
    if (state.enviando || !revisar()) return null;
    state = state.copiar(enviando: true);
    final datos = DatosDePedido(
      clienteId: state.cliente!.id,
      entrega: state.entrega!,
      direccion: state.direccion,
      descuento: state.descuentoNumero,
      observaciones: observaciones,
      lineas: Lineas.paraEnviar(state.lineas),
    );
    try {
      final pedido = await ref.read(pedidosRepositoryProvider).registrar(datos, clave: _clave.valor);
      _clave.despuesDelEnvio(huboRespuesta: true);
      if (ref.mounted) state = state.copiar(enviando: false);
      return pedido;
    } on ErrorDeApi catch (e) {
      _clave.despuesDelEnvio(huboRespuesta: !e.sinConexion);
      if (!ref.mounted) return null;
      final campos = e.camposCon(_campoDelCodigo);
      state = state.copiar(enviando: false, campos: campos, errorAlEnviar: campos.isEmpty ? e : null);
      return null;
    }
  }

  static Map<String, String> _sin(Map<String, String> campos, List<String> claves) =>
      {...campos}..removeWhere((clave, _) => claves.contains(clave));
}

final nuevoPedidoProvider = NotifierProvider.autoDispose<NuevoPedidoViewModel, EstadoNuevoPedido>(
  NuevoPedidoViewModel.new,
);
