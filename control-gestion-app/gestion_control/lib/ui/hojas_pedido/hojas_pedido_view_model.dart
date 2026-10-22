import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../data/models/hoja_pedido.dart';
import '../../data/models/pagina.dart';
import '../../proveedores.dart';
import '../core/lista_paginada.dart';

/// Sus hojas de pedido (2.1; 7.3.5, paso 4), de la última a la primera, filtradas por número y cliente, estado y fecha.
class HojasPedidoViewModel extends ListaPaginada<HojaPedidoResumen> {
  var _busqueda = '';
  EstadoHP? _estado;
  DateTime? _dia;

  EstadoHP? get estado => _estado;
  DateTime? get dia => _dia;
  bool get hayFiltros => _busqueda.trim().isNotEmpty || _estado != null || _dia != null;

  @override
  Future<Pagina<HojaPedidoResumen>> pedir(int pagina) => ref
      .read(hojasPedidoRepositoryProvider)
      .listar(
        buscar: _busqueda,
        estado: _estado,
        // La fecha que se eligió es un día de Bolivia: la API cuenta el día en esa hora.
        dia: _dia == null ? null : DateFormat('yyyy-MM-dd').format(_dia!),
        pagina: pagina,
      );

  void buscar(String texto) {
    if (texto.trim() == _busqueda.trim()) return;
    _busqueda = texto;
    cargar();
  }

  void filtrarPorEstado(EstadoHP? estado) {
    _estado = estado;
    cargar();
  }

  void filtrarPorDia(DateTime? dia) {
    _dia = dia;
    cargar();
  }
}

final hojasPedidoProvider = NotifierProvider.autoDispose<HojasPedidoViewModel, EstadoDeLista<HojaPedidoResumen>>(
  HojasPedidoViewModel.new,
);
