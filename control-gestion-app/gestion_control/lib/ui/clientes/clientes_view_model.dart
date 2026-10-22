import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cliente.dart';
import '../../data/models/pagina.dart';
import '../../proveedores.dart';
import '../core/lista_paginada.dart';

/// Los clientes de la cartera (2.2), de a 25, con la búsqueda en la API: en producción son más de 2.000.
class ClientesViewModel extends ListaPaginada<ClienteResumen> {
  var _busqueda = '';

  @override
  Future<Pagina<ClienteResumen>> pedir(int pagina) =>
      ref.read(clientesRepositoryProvider).listar(buscar: _busqueda, pagina: pagina);

  void buscar(String texto) {
    if (texto.trim() == _busqueda.trim()) return;
    _busqueda = texto;
    cargar();
  }
}

final clientesProvider = NotifierProvider.autoDispose<ClientesViewModel, EstadoDeLista<ClienteResumen>>(
  ClientesViewModel.new,
);
