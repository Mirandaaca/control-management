import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cliente.dart';
import '../../data/models/hoja_pedido.dart';
import '../../data/models/pagina.dart';
import '../../data/models/zona.dart';
import '../../data/services/error_de_api.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../core/lista_paginada.dart';
import '../sesion/sesion_notifier.dart';

/// Los clientes por visitar (5.3; 7.3.19): los que nunca se visitaron y después del más atrasado al más reciente (el
/// orden lo pone la API), filtrados por texto, zona y promotor.
class PorVisitarViewModel extends ListaPaginada<ClientePorVisitar> {
  var _busqueda = '';
  Zona? _zona;
  Referencia? _promotor;

  /// Las zonas del filtro: sin cartera completa, solo las suyas (ext. 1a). Vacío hasta que llegan.
  var zonas = const <Zona>[];

  /// Los promotores del filtro: solo con cartera completa y permiso para listarlos, como el panel.
  var promotores = const <Referencia>[];

  Zona? get zona => _zona;
  Referencia? get promotor => _promotor;
  bool get hayFiltros => _busqueda.trim().isNotEmpty || _zona != null || _promotor != null;

  bool get veTodos {
    final sesion = ref.read(sesionProvider);
    return sesion is ConSesion && sesion.sesion.tiene([Permisos.seguridad.verCarteraCompleta]);
  }

  bool get eligePromotor {
    final sesion = ref.read(sesionProvider);
    return veTodos && sesion is ConSesion && sesion.sesion.tiene(eligenPromotor);
  }

  @override
  EstadoDeLista<ClientePorVisitar> build() {
    Future.microtask(_cargarFiltros);
    return super.build();
  }

  Future<void> _cargarFiltros() async {
    try {
      final todas = await ref.read(zonasRepositoryProvider).listar();
      if (!ref.mounted) return;
      final sesion = ref.read(sesionProvider);
      final suyas = sesion is ConSesion ? sesion.sesion.id : null;
      zonas = veTodos ? todas : todas.where((z) => z.promotorId == suyas).toList();
      if (eligePromotor) promotores = await ref.read(usuariosRepositoryProvider).promotores();
    } on ErrorDeApi catch (e) {
      // Sin las zonas o los promotores, la lista funciona igual: solo falta el filtro.
      debugPrint('No se pudieron cargar los filtros de clientes por visitar. ${e.mensaje}');
    }
  }

  @override
  Future<Pagina<ClientePorVisitar>> pedir(int pagina) => ref
      .read(clientesRepositoryProvider)
      .porVisitar(buscar: _busqueda, zonaId: _zona?.id, promotorId: _promotor?.id, pagina: pagina);

  void buscar(String texto) {
    if (texto.trim() == _busqueda.trim()) return;
    _busqueda = texto;
    cargar();
  }

  void filtrarPorZona(Zona? zona) {
    _zona = zona;
    cargar();
  }

  void filtrarPorPromotor(Referencia? promotor) {
    _promotor = promotor;
    cargar();
  }
}

final porVisitarProvider = NotifierProvider.autoDispose<PorVisitarViewModel, EstadoDeLista<ClientePorVisitar>>(
  PorVisitarViewModel.new,
);
