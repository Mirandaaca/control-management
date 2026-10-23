import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/pedido.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';

const _igual = Object();

class EstadoDeLaBandeja {
  const EstadoDeLaBandeja({this.pedidos, this.error, this.reintentando = false});

  /// Null mientras carga o si no se pudo cargar.
  final List<PedidoResumen>? pedidos;
  final ErrorDeApi? error;
  final bool reintentando;

  EstadoDeLaBandeja copiar({Object? pedidos = _igual, Object? error = _igual, bool? reintentando}) => EstadoDeLaBandeja(
    pedidos: pedidos == _igual ? this.pedidos : pedidos as List<PedidoResumen>?,
    error: error == _igual ? this.error : error as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
  );
}

/// La bandeja del día (1.3; 7.3.3, pasos 3 a 3.2): los pedidos que tiene asignados para hoy, sin los entregados ni los
/// anulados, por hora. No se pagina: son los de un promotor en un día.
class BandejaViewModel extends Notifier<EstadoDeLaBandeja> {
  @override
  EstadoDeLaBandeja build() {
    Future.microtask(cargar);
    return const EstadoDeLaBandeja();
  }

  Future<void> cargar() async {
    state = state.copiar(reintentando: state.error != null);
    try {
      final pedidos = await ref.read(pedidosRepositoryProvider).bandeja();
      if (ref.mounted) state = state.copiar(pedidos: pedidos, error: null, reintentando: false);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(error: e, reintentando: false);
    }
  }
}

final bandejaProvider = NotifierProvider.autoDispose<BandejaViewModel, EstadoDeLaBandeja>(BandejaViewModel.new);
