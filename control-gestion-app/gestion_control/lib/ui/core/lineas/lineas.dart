import 'package:flutter/widgets.dart';

import '../../../data/models/hoja_pedido.dart';
import '../../../data/models/producto.dart';
import '../formato.dart';
import '../primer_problema.dart';

/// Una línea de productos de un formulario (la venta o el pedido): el producto, la cantidad y el descuento por unidad.
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

/// Lo que se hace con las líneas de un formulario. Los errores van con las claves de la API: `detalles`,
/// `detalles[1].cantidad`.
abstract final class Lineas {
  static double subtotal(List<LineaDelFormulario> lineas) => lineas.fold(0, (suma, l) => suma + l.subtotal);

  /// Lo que se pide de cada producto, sumando sus líneas: el stock se compara con la suma, como en la API.
  static Map<int, double> pedidoPorProducto(List<LineaDelFormulario> lineas) {
    final pedido = <int, double>{};
    for (final linea in lineas) {
      final producto = linea.producto;
      if (producto != null) pedido[producto.id] = (pedido[producto.id] ?? 0) + linea.cantidadNumero;
    }
    return pedido;
  }

  /// El aviso en ámbar de una línea que pide más de lo que hay, que no impide guardar: "Solo hay N en stock.", o con
  /// [consecuencia], "Solo hay N en stock: [consecuencia]".
  static String? avisoDeStock(List<LineaDelFormulario> lineas, LineaDelFormulario linea, {String? consecuencia}) {
    final producto = linea.producto;
    if (producto == null || linea.cantidadNumero <= 0) return null;
    if ((pedidoPorProducto(lineas)[producto.id] ?? 0) <= producto.stock) return null;
    final hay = 'Solo hay ${formatearCantidad(producto.stock)} en stock';
    return consecuencia == null ? '$hay.' : '$hay: $consecuencia';
  }

  /// Las reglas de la API para las líneas, antes de enviar: cada error en su campo.
  static Map<String, String> errores(List<LineaDelFormulario> lineas) {
    final errores = <String, String>{if (lineas.isEmpty) 'detalles': 'Agrega al menos un producto.'};
    for (final (i, linea) in lineas.indexed) {
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
    return errores;
  }

  /// Las líneas como las recibe la API. Solo después de revisarlas: todas tienen su producto.
  static List<LineaParaEnviar> paraEnviar(List<LineaDelFormulario> lineas) => [
    for (final l in lineas) (productoId: l.producto!.id, cantidad: l.cantidadNumero, descuento: l.descuentoNumero),
  ];

  /// Cambia una línea y quita los errores de esa línea, que ya no valen.
  static ({List<LineaDelFormulario> lineas, Map<String, String> campos}) cambiar(
    List<LineaDelFormulario> lineas,
    Map<String, String> campos,
    int clave,
    LineaDelFormulario Function(LineaDelFormulario) cambio,
  ) {
    final indice = lineas.indexWhere((l) => l.clave == clave);
    if (indice < 0) return (lineas: lineas, campos: campos);
    final viejos = [
      'detalles',
      for (final campo in ['productoId', 'cantidad', 'descuento']) 'detalles[$indice].$campo',
    ];
    return (
      lineas: [for (final l in lineas) l.clave == clave ? cambio(l) : l],
      campos: {...campos}..removeWhere((c, _) => viejos.contains(c)),
    );
  }

  /// Al agregar o quitar una línea, los errores por posición ya no corresponden.
  static Map<String, String> sinErroresDeLineas(Map<String, String> campos) => {
    for (final e in campos.entries)
      if (!e.key.startsWith('detalles')) e.key: e.value,
  };
}

typedef LugaresDeLinea = ({LugarDelFormulario producto, LugarDelFormulario cantidad, LugarDelFormulario descuento});

/// Los lugares de cada línea, por su clave, para llevar la vista al primer problema: siguen a la línea aunque se quiten
/// otras. Vive en el `State` de la pantalla, que lo libera en su `dispose`.
class LugaresDeLasLineas {
  final _lugares = <int, LugaresDeLinea>{};

  LugaresDeLinea de(int clave) => _lugares.putIfAbsent(
    clave,
    () => (
      producto: (llave: GlobalKey(), foco: FocusNode()),
      cantidad: (llave: GlobalKey(), foco: FocusNode()),
      descuento: (llave: GlobalKey(), foco: FocusNode()),
    ),
  );

  /// Los lugares con un error, en el orden de las líneas.
  Iterable<LugarDelFormulario> conError(List<LineaDelFormulario> lineas, Map<String, String> campos) sync* {
    for (final (i, linea) in lineas.indexed) {
      final lugares = de(linea.clave);
      if (campos.containsKey('detalles[$i].productoId')) yield lugares.producto;
      if (campos.containsKey('detalles[$i].cantidad')) yield lugares.cantidad;
      if (campos.containsKey('detalles[$i].descuento')) yield lugares.descuento;
    }
  }

  void dispose() {
    for (final lugares in _lugares.values) {
      for (final lugar in [lugares.producto, lugares.cantidad, lugares.descuento]) {
        lugar.foco?.dispose();
      }
    }
  }
}
