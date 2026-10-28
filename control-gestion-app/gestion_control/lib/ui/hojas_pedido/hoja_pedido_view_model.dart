import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/hoja_pedido.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';

const _igual = Object();

class DetalleDeHoja {
  const DetalleDeHoja({
    this.hoja,
    this.error,
    this.reintentando = false,
    this.confirmando = false,
    this.campos = const {},
    this.errorAlConfirmar,
    this.mensaje,
  });

  /// Null mientras carga o si no se pudo cargar.
  final HojaPedido? hoja;
  final ErrorDeApi? error;
  final bool reintentando;
  final bool confirmando;

  /// Los errores de stock de cada línea (`detalles[i].cantidad`), en el orden de sus productos.
  final Map<String, String> campos;
  final ErrorDeApi? errorAlConfirmar;

  /// El aviso de éxito de lo último que pasó.
  final String? mensaje;

  DetalleDeHoja copiar({
    Object? hoja = _igual,
    Object? error = _igual,
    bool? reintentando,
    bool? confirmando,
    Map<String, String>? campos,
    Object? errorAlConfirmar = _igual,
    Object? mensaje = _igual,
  }) => DetalleDeHoja(
    hoja: hoja == _igual ? this.hoja : hoja as HojaPedido?,
    error: error == _igual ? this.error : error as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    confirmando: confirmando ?? this.confirmando,
    campos: campos ?? this.campos,
    errorAlConfirmar: errorAlConfirmar == _igual ? this.errorAlConfirmar : errorAlConfirmar as ErrorDeApi?,
    mensaje: mensaje == _igual ? this.mensaje : mensaje as String?,
  );
}

/// Una HP (2.1; 7.3.5, paso 5) y confirmar un borrador.
class HojaPedidoViewModel extends Notifier<DetalleDeHoja> {
  HojaPedidoViewModel(this.id);

  final int id;

  @override
  DetalleDeHoja build() {
    Future.microtask(cargar);
    return const DetalleDeHoja();
  }

  Future<void> cargar() async {
    state = state.copiar(reintentando: state.error != null);
    try {
      final hoja = await ref.read(hojasPedidoRepositoryProvider).obtener(id);
      if (ref.mounted) state = state.copiar(hoja: hoja, error: null, reintentando: false);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(error: e, reintentando: false);
    }
  }

  /// Confirma el borrador. Sin stock, cada línea que no alcanza muestra cuánto hay, y la HP sigue en borrador.
  Future<HojaPedido?> confirmar() async {
    if (state.confirmando) return null;
    state = state.copiar(confirmando: true, campos: const {}, errorAlConfirmar: null, mensaje: null);
    try {
      final hoja = await ref.read(hojasPedidoRepositoryProvider).confirmar(id);
      if (ref.mounted) state = state.copiar(hoja: hoja, confirmando: false);
      return hoja;
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(confirmando: false, campos: e.campos, errorAlConfirmar: e);
      return null;
    }
  }

  // Primero sin aviso: así el mismo texto dos veces seguidas (dos ediciones iguales) se muestra las dos veces.
  void avisar(String mensaje) {
    state = state.copiar(mensaje: null);
    state = state.copiar(mensaje: mensaje);
  }
}

final hojaPedidoProvider = NotifierProvider.autoDispose.family<HojaPedidoViewModel, DetalleDeHoja, int>(
  HojaPedidoViewModel.new,
);
