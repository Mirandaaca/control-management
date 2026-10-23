import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/pedido.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';

const _igual = Object();

class DetalleDePedido {
  const DetalleDePedido({this.pedido, this.error, this.reintentando = false, this.mensaje});

  /// Null mientras carga o si no se pudo cargar.
  final Pedido? pedido;
  final ErrorDeApi? error;
  final bool reintentando;

  /// El aviso de éxito de lo último que pasó.
  final String? mensaje;

  DetalleDePedido copiar({
    Object? pedido = _igual,
    Object? error = _igual,
    bool? reintentando,
    Object? mensaje = _igual,
  }) => DetalleDePedido(
    pedido: pedido == _igual ? this.pedido : pedido as Pedido?,
    error: error == _igual ? this.error : error as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    mensaje: mensaje == _igual ? this.mensaje : mensaje as String?,
  );
}

/// Un pedido (7.3.3, paso 4; 7.3.2, paso 4): sus datos, sus productos y su historial.
class PedidoViewModel extends Notifier<DetalleDePedido> {
  PedidoViewModel(this.id);

  final int id;

  @override
  DetalleDePedido build() {
    Future.microtask(cargar);
    return const DetalleDePedido();
  }

  Future<void> cargar() async {
    state = state.copiar(reintentando: state.error != null);
    try {
      final pedido = await ref.read(pedidosRepositoryProvider).obtener(id);
      if (ref.mounted) state = state.copiar(pedido: pedido, error: null, reintentando: false);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(error: e, reintentando: false);
    }
  }

  void avisar(String mensaje) => state = state.copiar(mensaje: mensaje);
}

final pedidoProvider = NotifierProvider.autoDispose.family<PedidoViewModel, DetalleDePedido, int>(PedidoViewModel.new);
