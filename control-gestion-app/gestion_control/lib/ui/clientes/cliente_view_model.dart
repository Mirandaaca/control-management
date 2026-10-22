import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cliente.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';

class DetalleDeCliente {
  const DetalleDeCliente({this.cliente, this.error, this.reintentando = false});

  /// Null mientras carga o si no se pudo cargar.
  final Cliente? cliente;
  final ErrorDeApi? error;
  final bool reintentando;
}

class ClienteViewModel extends Notifier<DetalleDeCliente> {
  ClienteViewModel(this.id);

  final int id;

  @override
  DetalleDeCliente build() {
    Future.microtask(cargar);
    return const DetalleDeCliente();
  }

  Future<void> cargar() async {
    state = DetalleDeCliente(cliente: state.cliente, error: state.error, reintentando: state.error != null);
    try {
      final cliente = await ref.read(clientesRepositoryProvider).obtener(id);
      if (ref.mounted) state = DetalleDeCliente(cliente: cliente);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = DetalleDeCliente(error: e);
    }
  }
}

final clienteProvider = NotifierProvider.autoDispose.family<ClienteViewModel, DetalleDeCliente, int>(
  ClienteViewModel.new,
);
