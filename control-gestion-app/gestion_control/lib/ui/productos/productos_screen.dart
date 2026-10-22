import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/producto.dart';
import '../core/formato.dart';
import '../core/tema.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/etiqueta_de_estado.dart';
import '../core/widgets/selectores.dart';
import '../core/widgets/vacio.dart';
import 'productos_view_model.dart';

/// El catálogo de productos con su stock (2.3).
class ProductosScreen extends ConsumerStatefulWidget {
  const ProductosScreen({super.key});

  @override
  ConsumerState<ProductosScreen> createState() => _ProductosScreenState();
}

class _ProductosScreenState extends ConsumerState<ProductosScreen> {
  final _busqueda = TextEditingController();

  @override
  void dispose() {
    _busqueda.dispose();
    super.dispose();
  }

  // Lo vacío de la lista de categorías es "todas": null es que se cerró la lista sin elegir.
  Future<void> _elegirCategoria(EstadoProductos estado) async {
    final elegida = await elegirDeLaLista<String>(
      context,
      titulo: 'Categoría',
      opciones: [('', 'Todas las categorías'), for (final c in estado.categorias) (c, c)],
      actual: estado.categoria ?? '',
    );
    if (elegida == null) return;
    ref.read(productosProvider.notifier).elegirCategoria(elegida.isEmpty ? null : elegida);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(productosProvider);
    final viewModel = ref.read(productosProvider.notifier);

    return Scaffold(
      appBar: AppBar(title: const Text('Productos')),
      body: switch (estado) {
        EstadoProductos(error: final error?) => ErrorConReintento.de(
          error,
          reintentando: estado.reintentando,
          alReintentar: viewModel.cargar,
        ),
        EstadoProductos(productos: null) => const Cargando('Cargando los productos…'),
        EstadoProductos(productos: final productos?) when productos.isEmpty => const Vacio(
          icono: Icons.inventory_outlined,
          titulo: 'Todavía no hay productos',
          texto: 'Aquí aparecen los productos que se venden, con su precio y su stock.',
        ),
        EstadoProductos(productos: final productos?) => RefreshIndicator(
          onRefresh: viewModel.cargar,
          child: ListView(
            padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.paddingOf(context).bottom + 24),
            children: [
              CampoDeTexto(
                controller: _busqueda,
                etiqueta: 'Buscar',
                ejemplo: 'Código, nombre o categoría…',
                textInputAction: TextInputAction.search,
                onChanged: viewModel.buscar,
              ),
              const SizedBox(height: 12),
              CampoDeEleccion(
                etiqueta: 'Categoría',
                valor: estado.categoria ?? 'Todas las categorías',
                alTocar: () => _elegirCategoria(estado),
              ),
              const SizedBox(height: 16),
              Semantics(
                liveRegion: true,
                child: Text(
                  estado.hayFiltros
                      ? '${estado.visibles.length} de ${_productos(productos.length)}'
                      : _productos(productos.length),
                  style: const TextStyle(color: textoSecundario),
                ),
              ),
              const SizedBox(height: 8),
              if (estado.visibles.isEmpty)
                const Vacio(icono: Icons.search_off, titulo: 'Ningún producto coincide con la búsqueda.')
              else
                for (final producto in estado.visibles) ...[_TarjetaDeProducto(producto), const SizedBox(height: 12)],
            ],
          ),
        ),
      },
    );
  }
}

String _productos(int cantidad) => cantidad == 1 ? '1 producto' : '$cantidad productos';

class _TarjetaDeProducto extends StatelessWidget {
  const _TarjetaDeProducto(this.producto);

  final Producto producto;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: MergeSemantics(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(producto.descripcion, style: const TextStyle(fontWeight: FontWeight.w600)),
              Text([producto.sku, ?producto.categoria].join(' · '), style: const TextStyle(color: textoSecundario)),
              const SizedBox(height: 8),
              Wrap(
                spacing: 16,
                runSpacing: 4,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(formatearMonto(producto.precio), style: const TextStyle(fontWeight: FontWeight.w600)),
                  if (producto.sinStock)
                    const EtiquetaDeEstado(
                      'Sin stock',
                      icono: Icons.remove_shopping_cart_outlined,
                      color: ColorDeEstado.ambar,
                    )
                  else
                    Text('Stock: ${formatearCantidad(producto.stock)}'),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
