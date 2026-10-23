import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/evento.dart';
import '../../data/models/pagina.dart';
import '../../proveedores.dart';
import '../core/lista_paginada.dart';

/// Los eventos que ve (4.1; 7.3.13, paso 1 y ext. 1a): los de su cartera, los de sus pedidos asignados y los que
/// registró, del último al primero, filtrados por texto y por estado.
class EventosViewModel extends ListaPaginada<EventoResumen> {
  var _busqueda = '';
  EstadoEvento? _estado;

  EstadoEvento? get estado => _estado;
  bool get hayFiltros => _busqueda.trim().isNotEmpty || _estado != null;

  @override
  Future<Pagina<EventoResumen>> pedir(int pagina) =>
      ref.read(eventosRepositoryProvider).listar(buscar: _busqueda, estado: _estado, pagina: pagina);

  void buscar(String texto) {
    if (texto.trim() == _busqueda.trim()) return;
    _busqueda = texto;
    cargar();
  }

  void filtrarPorEstado(EstadoEvento? estado) {
    _estado = estado;
    cargar();
  }
}

final eventosProvider = NotifierProvider.autoDispose<EventosViewModel, EstadoDeLista<EventoResumen>>(
  EventosViewModel.new,
);
