import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/producto.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/texto.dart';

class EstadoProductos {
  const EstadoProductos({this.productos, this.error, this.reintentando = false, this.busqueda = '', this.categoria});

  /// Null mientras carga o si no se pudo cargar.
  final List<Producto>? productos;
  final ErrorDeApi? error;
  final bool reintentando;
  final String busqueda;

  /// Null: todas las categorías.
  final String? categoria;

  bool get hayFiltros => busqueda.trim().isNotEmpty || categoria != null;

  List<String> get categorias =>
      {for (final p in productos ?? const <Producto>[]) ?p.categoria}.toList()
        ..sort((a, b) => normalizar(a).compareTo(normalizar(b)));

  List<Producto> get visibles => [
    for (final p in productos ?? const <Producto>[])
      if ((categoria == null || p.categoria == categoria) && coincide(busqueda, [p.sku, p.descripcion, p.categoria])) p,
  ];
}

/// El catálogo (2.3; 7.3.7, pasos 1 y 2): los productos con su código, su categoría, su precio y su stock. Se filtran
/// en la lista que ya llegó.
class ProductosViewModel extends Notifier<EstadoProductos> {
  @override
  EstadoProductos build() {
    Future.microtask(cargar);
    return const EstadoProductos();
  }

  Future<void> cargar() async {
    state = EstadoProductos(
      productos: state.productos,
      error: state.error,
      reintentando: state.error != null,
      busqueda: state.busqueda,
      categoria: state.categoria,
    );
    try {
      final productos = await ref.read(productosRepositoryProvider).listar();
      if (ref.mounted) {
        state = EstadoProductos(productos: productos, busqueda: state.busqueda, categoria: state.categoria);
      }
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = EstadoProductos(error: e, busqueda: state.busqueda, categoria: state.categoria);
    }
  }

  void buscar(String texto) =>
      state = EstadoProductos(productos: state.productos, busqueda: texto, categoria: state.categoria);

  void elegirCategoria(String? categoria) =>
      state = EstadoProductos(productos: state.productos, busqueda: state.busqueda, categoria: categoria);
}

final productosProvider = NotifierProvider.autoDispose<ProductosViewModel, EstadoProductos>(ProductosViewModel.new);
