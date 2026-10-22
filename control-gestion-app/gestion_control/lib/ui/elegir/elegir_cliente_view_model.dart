import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cliente.dart';
import '../../data/repositories/clientes_repository.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';

class BusquedaDeClientes {
  const BusquedaDeClientes({this.resultados, this.buscando = false, this.error});

  /// Null hasta que llega la primera respuesta.
  final List<ClienteOpcion>? resultados;
  final bool buscando;
  final ErrorDeApi? error;

  /// Llegaron tantos como se pidieron: puede haber más.
  bool get hayMas => (resultados?.length ?? 0) >= ClientesRepository.limiteDeOpciones;
}

/// La búsqueda de un cliente para una venta, en la API, como el `Combobox` del panel: de a 50.
class ElegirClienteViewModel extends Notifier<BusquedaDeClientes> {
  var _pedido = 0;
  var _texto = '';

  @override
  BusquedaDeClientes build() {
    Future.microtask(() => buscar(''));
    return const BusquedaDeClientes(buscando: true);
  }

  Future<void> buscar(String texto) async {
    _texto = texto;
    final pedido = ++_pedido;
    state = BusquedaDeClientes(resultados: state.resultados, buscando: true);
    try {
      final resultados = await ref.read(clientesRepositoryProvider).opciones(texto);
      if (ref.mounted && pedido == _pedido) state = BusquedaDeClientes(resultados: resultados);
    } on ErrorDeApi catch (e) {
      if (ref.mounted && pedido == _pedido) state = BusquedaDeClientes(resultados: state.resultados, error: e);
    }
  }

  Future<void> reintentar() => buscar(_texto);
}

final elegirClienteProvider = NotifierProvider.autoDispose<ElegirClienteViewModel, BusquedaDeClientes>(
  ElegirClienteViewModel.new,
);
