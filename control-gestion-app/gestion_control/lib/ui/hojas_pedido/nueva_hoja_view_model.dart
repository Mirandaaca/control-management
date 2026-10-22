import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cliente.dart';
import '../../data/models/hoja_pedido.dart';
import '../../data/models/producto.dart';
import '../../data/services/clave_de_idempotencia.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';

/// Lo que hace el botón que se tocó: "Guardar" deja el borrador y "Confirmar venta" la cierra.
enum QueSeEnvia { guardar, confirmar }

class LineaDelFormulario {
  const LineaDelFormulario({required this.clave, this.producto, this.cantidad = '', this.descuento = ''});

  /// Distingue la línea aunque se quiten otras: sus campos la siguen.
  final int clave;
  final Producto? producto;

  /// Lo escrito, tal cual: así se puede mostrar el error de "1,,".
  final String cantidad;
  final String descuento;

  double get cantidadNumero => leerNumero(cantidad) ?? 0;
  double get descuentoNumero => leerNumero(descuento) ?? 0;
  double get subtotal => producto == null ? 0 : (producto!.precio - descuentoNumero) * cantidadNumero;

  LineaDelFormulario con({Producto? producto, String? cantidad, String? descuento}) => LineaDelFormulario(
    clave: clave,
    producto: producto ?? this.producto,
    cantidad: cantidad ?? this.cantidad,
    descuento: descuento ?? this.descuento,
  );
}

const _igual = Object();

class EstadoNuevaHoja {
  const EstadoNuevaHoja({
    this.productos,
    this.errorDeCarga,
    this.reintentando = false,
    this.cliente,
    this.lineas = const [LineaDelFormulario(clave: 0)],
    this.tipoVenta = TipoVenta.contado,
    this.descuento = '',
    this.campos = const {},
    this.enviando,
    this.esperandoUbicacion = false,
    this.errorAlEnviar,
    this.sinUbicacion = false,
  });

  /// El catálogo con su stock, para elegir el producto de cada línea. Null mientras carga.
  final List<Producto>? productos;
  final ErrorDeApi? errorDeCarga;
  final bool reintentando;
  final ClienteOpcion? cliente;
  final List<LineaDelFormulario> lineas;
  final TipoVenta tipoVenta;

  /// El descuento sobre el total, tal cual se escribió.
  final String descuento;

  /// Los errores por campo, con las claves de la API: `clienteId`, `detalles[1].cantidad`, `descuento`.
  final Map<String, String> campos;

  /// Qué se está enviando, o null.
  final QueSeEnvia? enviando;

  /// Se tocó el botón antes de tener una ubicación: espera la primera lectura.
  final bool esperandoUbicacion;
  final ErrorDeApi? errorAlEnviar;
  final bool sinUbicacion;

  double get subtotal => lineas.fold(0, (suma, l) => suma + l.subtotal);
  double get descuentoNumero => leerNumero(descuento) ?? 0;
  double get total => subtotal - descuentoNumero;

  /// Lo que pide la venta de cada producto, sumando sus líneas: el stock se compara con la suma, como en la API.
  Map<int, double> get pedidoPorProducto {
    final pedido = <int, double>{};
    for (final linea in lineas) {
      final producto = linea.producto;
      if (producto != null) pedido[producto.id] = (pedido[producto.id] ?? 0) + linea.cantidadNumero;
    }
    return pedido;
  }

  /// El aviso en ámbar de una línea que pide más de lo que hay, antes de confirmar.
  String? avisoDeStock(LineaDelFormulario linea) {
    final producto = linea.producto;
    if (producto == null || linea.cantidadNumero <= 0) return null;
    return (pedidoPorProducto[producto.id] ?? 0) > producto.stock
        ? 'Solo hay ${formatearCantidad(producto.stock)} en stock.'
        : null;
  }

  EstadoNuevaHoja copiar({
    Object? productos = _igual,
    Object? errorDeCarga = _igual,
    bool? reintentando,
    Object? cliente = _igual,
    List<LineaDelFormulario>? lineas,
    TipoVenta? tipoVenta,
    String? descuento,
    Map<String, String>? campos,
    Object? enviando = _igual,
    bool? esperandoUbicacion,
    Object? errorAlEnviar = _igual,
    bool? sinUbicacion,
  }) => EstadoNuevaHoja(
    productos: productos == _igual ? this.productos : productos as List<Producto>?,
    errorDeCarga: errorDeCarga == _igual ? this.errorDeCarga : errorDeCarga as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    cliente: cliente == _igual ? this.cliente : cliente as ClienteOpcion?,
    lineas: lineas ?? this.lineas,
    tipoVenta: tipoVenta ?? this.tipoVenta,
    descuento: descuento ?? this.descuento,
    campos: campos ?? this.campos,
    enviando: enviando == _igual ? this.enviando : enviando as QueSeEnvia?,
    esperandoUbicacion: esperandoUbicacion ?? this.esperandoUbicacion,
    errorAlEnviar: errorAlEnviar == _igual ? this.errorAlEnviar : errorAlEnviar as ErrorDeApi?,
    sinUbicacion: sinUbicacion ?? this.sinUbicacion,
  );
}

/// Registrar una venta (2.1; 7.3.5, 8.6.5): la ubicación, el cliente, los productos, contado o crédito y el total.
class NuevaHojaViewModel extends Notifier<EstadoNuevaHoja> {
  final _clave = ClaveDeIdempotencia();
  var _siguienteClave = 1;

  // Errores de la API que pertenecen a un campo.
  static const _campoDelCodigo = {
    'cliente_inactivo': 'clienteId',
    'cliente_no_existe': 'clienteId',
    'total_invalido': 'descuento',
    'producto_no_disponible': 'detalles',
  };

  @override
  EstadoNuevaHoja build() {
    Future.microtask(cargarProductos);
    return const EstadoNuevaHoja();
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

  /// Si la venta se abre desde un cliente, el cliente ya viene elegido.
  void elegirCliente(ClienteOpcion cliente) =>
      state = state.copiar(cliente: cliente, campos: _sin(state.campos, ['clienteId']));

  /// Agrega una línea vacía y devuelve su clave, para elegir su producto enseguida.
  int agregarLinea() {
    final clave = _siguienteClave++;
    state = state.copiar(
      lineas: [
        ...state.lineas,
        LineaDelFormulario(clave: clave),
      ],
      campos: _sinLineas(),
    );
    return clave;
  }

  void quitarLinea(int clave) => state = state.copiar(
    lineas: [
      for (final l in state.lineas)
        if (l.clave != clave) l,
    ],
    campos: _sinLineas(),
  );

  void elegirProducto(int clave, Producto producto) => _cambiarLinea(clave, (l) => l.con(producto: producto));

  void cambiarCantidad(int clave, String texto) => _cambiarLinea(clave, (l) => l.con(cantidad: texto));

  void cambiarDescuentoDeLinea(int clave, String texto) => _cambiarLinea(clave, (l) => l.con(descuento: texto));

  void elegirTipo(TipoVenta tipo) => state = state.copiar(tipoVenta: tipo);

  void cambiarDescuento(String texto) =>
      state = state.copiar(descuento: texto, campos: _sin(state.campos, ['descuento']));

  void _cambiarLinea(int clave, LineaDelFormulario Function(LineaDelFormulario) cambio) {
    final indice = state.lineas.indexWhere((l) => l.clave == clave);
    if (indice < 0) return;
    state = state.copiar(
      lineas: [for (final l in state.lineas) l.clave == clave ? cambio(l) : l],
      campos: _sin(state.campos, [
        'detalles',
        for (final campo in ['productoId', 'cantidad', 'descuento']) 'detalles[$indice].$campo',
      ]),
    );
  }

  /// Revisa el formulario con las reglas de la API, antes de pedir la confirmación: cada error va en su campo.
  bool revisar() {
    final errores = <String, String>{
      if (state.cliente == null) 'clienteId': 'Elige el cliente.',
      if (state.lineas.isEmpty) 'detalles': 'Agrega al menos un producto.',
    };
    for (final (i, linea) in state.lineas.indexed) {
      final producto = linea.producto;
      final cantidad = leerNumero(linea.cantidad);
      final descuento = leerNumero(linea.descuento) ?? 0;
      if (producto == null) errores['detalles[$i].productoId'] = 'Elige el producto.';
      if (cantidad == null || cantidad <= 0) errores['detalles[$i].cantidad'] = 'La cantidad debe ser mayor que cero.';
      // Igual al precio se acepta: un producto de regalo (tarea 11).
      if (producto != null && centavos(descuento) > centavos(producto.precio)) {
        errores['detalles[$i].descuento'] =
            'El descuento no puede superar el precio (${formatearMonto(producto.precio)}).';
      }
    }
    if (errores.isEmpty && centavos(state.descuentoNumero) >= centavos(state.subtotal)) {
      errores['descuento'] = 'El descuento tiene que ser menor que el subtotal (${formatearMonto(state.subtotal)}).';
    }
    state = state.copiar(campos: errores, errorAlEnviar: null, sinUbicacion: false);
    return errores.isEmpty;
  }

  /// "Guardar" o "Confirmar venta", con la ubicación de la tarjeta. Devuelve la HP, o null si algo lo impidió: los errores
  /// quedan en el estado. Sin stock, la API marca cada línea que no alcanza y no guarda nada.
  Future<HojaPedido?> enviar(QueSeEnvia que) async {
    if (state.enviando != null || !revisar()) return null;

    state = state.copiar(enviando: que, esperandoUbicacion: true);
    final ubicacion = await ref.read(capturaDeUbicacionProvider(QueSeRegistra.venta).notifier).paraGuardar();
    if (!ref.mounted) return null;
    if (ubicacion == null) {
      state = state.copiar(enviando: null, esperandoUbicacion: false, sinUbicacion: true);
      return null;
    }

    state = state.copiar(esperandoUbicacion: false);
    final datos = DatosDeHoja(
      clienteId: state.cliente!.id,
      tipoVenta: state.tipoVenta,
      descuento: state.descuentoNumero,
      lineas: [
        for (final l in state.lineas)
          (productoId: l.producto!.id, cantidad: l.cantidadNumero, descuento: l.descuentoNumero),
      ],
    );
    final repositorio = ref.read(hojasPedidoRepositoryProvider);
    try {
      final hoja = que == QueSeEnvia.guardar
          ? await repositorio.guardar(datos, ubicacion, clave: _clave.valor)
          : await repositorio.confirmarNueva(datos, ubicacion, clave: _clave.valor);
      _clave.despuesDelEnvio(huboRespuesta: true);
      if (ref.mounted) state = state.copiar(enviando: null);
      return hoja;
    } on ErrorDeApi catch (e) {
      _clave.despuesDelEnvio(huboRespuesta: !e.sinConexion);
      if (!ref.mounted) return null;
      final campos = e.camposCon(_campoDelCodigo);
      state = state.copiar(enviando: null, campos: campos, errorAlEnviar: campos.isEmpty ? e : null);
      // El stock cambió desde que se abrió el formulario: se trae de nuevo para los avisos de cada línea.
      if (e.codigo == 'stock_insuficiente') _refrescarStock();
      return null;
    }
  }

  Future<void> _refrescarStock() async {
    try {
      final productos = await ref.read(productosRepositoryProvider).listar();
      if (!ref.mounted) return;
      final porId = {for (final p in productos) p.id: p};
      state = state.copiar(
        productos: productos,
        lineas: [
          for (final l in state.lineas)
            LineaDelFormulario(
              clave: l.clave,
              producto: porId[l.producto?.id] ?? l.producto,
              cantidad: l.cantidad,
              descuento: l.descuento,
            ),
        ],
      );
    } on ErrorDeApi catch (e) {
      debugPrint('No se pudo traer el stock de nuevo. ${e.mensaje}');
    }
  }

  // Al agregar o quitar una línea, los errores por posición ya no corresponden.
  Map<String, String> _sinLineas() => {
    for (final e in state.campos.entries)
      if (!e.key.startsWith('detalles')) e.key: e.value,
  };

  static Map<String, String> _sin(Map<String, String> campos, List<String> claves) =>
      {...campos}..removeWhere((clave, _) => claves.contains(clave));
}

final nuevaHojaProvider = NotifierProvider.autoDispose<NuevaHojaViewModel, EstadoNuevaHoja>(NuevaHojaViewModel.new);
