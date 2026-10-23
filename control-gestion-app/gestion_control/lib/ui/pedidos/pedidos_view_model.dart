import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/pagina.dart';
import '../../data/models/pedido.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/lista_paginada.dart';

/// Los pedidos de su cartera y los que tiene asignados (1.1; 7.3.1, paso 1 y ext. 1a), del último registrado al
/// primero, filtrados por número y cliente y por estado.
class PedidosViewModel extends ListaPaginada<PedidoResumen> {
  var _busqueda = '';
  EstadoPedido? _estado;

  /// Los estados para el filtro, de la API. Vacío hasta que llegan.
  var estados = const <EstadoPedido>[];

  EstadoPedido? get estado => _estado;
  bool get hayFiltros => _busqueda.trim().isNotEmpty || _estado != null;

  @override
  EstadoDeLista<PedidoResumen> build() {
    Future.microtask(_cargarEstados);
    return super.build();
  }

  Future<void> _cargarEstados() async {
    try {
      final estados = await ref.read(pedidosRepositoryProvider).estados();
      if (ref.mounted) this.estados = estados;
    } on ErrorDeApi catch (e) {
      // Sin los estados, la lista funciona igual: solo falta el filtro.
      debugPrint('No se pudieron cargar los estados del pedido. ${e.mensaje}');
    }
  }

  @override
  Future<Pagina<PedidoResumen>> pedir(int pagina) =>
      ref.read(pedidosRepositoryProvider).listar(buscar: _busqueda, estadoId: _estado?.id, pagina: pagina);

  void buscar(String texto) {
    if (texto.trim() == _busqueda.trim()) return;
    _busqueda = texto;
    cargar();
  }

  void filtrarPorEstado(EstadoPedido? estado) {
    _estado = estado;
    cargar();
  }
}

final pedidosProvider = NotifierProvider.autoDispose<PedidosViewModel, EstadoDeLista<PedidoResumen>>(
  PedidosViewModel.new,
);
