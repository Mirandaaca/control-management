import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cobranza.dart';
import '../../data/models/pagina.dart';
import '../../data/repositories/cobranzas_repository.dart';
import '../../proveedores.dart';
import '../core/lista_paginada.dart';

/// Las cobranzas de sus clientes (3.2; 7.3.11): por cobrar al abrir, las vencidas o todas. Abierta desde un cliente,
/// solo las de él.
class CobranzasViewModel extends ListaPaginada<CobranzaResumen> {
  CobranzasViewModel(this.clienteId);

  final int? clienteId;
  var _filtro = FiltroDeCobranzas.porCobrar;
  var _busqueda = '';

  FiltroDeCobranzas get filtro => _filtro;
  bool get hayBusqueda => _busqueda.trim().isNotEmpty;

  @override
  Future<Pagina<CobranzaResumen>> pedir(int pagina) => ref
      .read(cobranzasRepositoryProvider)
      .listar(filtro: _filtro, clienteId: clienteId, buscar: _busqueda, pagina: pagina);

  void filtrar(FiltroDeCobranzas filtro) {
    if (filtro == _filtro) return;
    _filtro = filtro;
    cargar();
  }

  void buscar(String texto) {
    if (texto.trim() == _busqueda.trim()) return;
    _busqueda = texto;
    cargar();
  }
}

final cobranzasProvider = NotifierProvider.autoDispose.family<CobranzasViewModel, EstadoDeLista<CobranzaResumen>, int?>(
  CobranzasViewModel.new,
);

/// Cuánto hay por cobrar y cuántas están vencidas, arriba de la lista.
final resumenDeCobranzasProvider = FutureProvider.autoDispose.family<ResumenDeCobranzas, int?>(
  (ref, clienteId) => ref.read(cobranzasRepositoryProvider).resumen(clienteId: clienteId),
);

/// Las cobranzas por cobrar de un cliente, en su detalle.
final porCobrarDelClienteProvider = FutureProvider.autoDispose.family<List<CobranzaResumen>, int>(
  (ref, clienteId) async => (await ref.read(cobranzasRepositoryProvider).listar(clienteId: clienteId)).items,
);
