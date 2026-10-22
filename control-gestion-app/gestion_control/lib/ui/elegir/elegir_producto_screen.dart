import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/producto.dart';
import '../core/formato.dart';
import '../core/tema.dart';
import '../core/texto.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/etiqueta_de_estado.dart';
import '../core/widgets/vacio.dart';

/// Elegir el producto de una línea (⇅): busca en la lista que ya llegó, con su precio y su stock. Uno sin stock se
/// puede elegir igual: la venta se guarda; lo que no se puede es confirmarla.
class ElegirProductoScreen extends StatefulWidget {
  const ElegirProductoScreen({super.key, required this.productos});

  final List<Producto> productos;

  @override
  State<ElegirProductoScreen> createState() => _ElegirProductoScreenState();
}

class _ElegirProductoScreenState extends State<ElegirProductoScreen> {
  final _busqueda = TextEditingController();

  @override
  void dispose() {
    _busqueda.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final visibles = [
      for (final p in widget.productos)
        if (coincide(_busqueda.text, [p.sku, p.descripcion, p.categoria])) p,
    ];
    return Scaffold(
      appBar: AppBar(title: const Text('Elegir producto')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          CampoDeTexto(
            controller: _busqueda,
            etiqueta: 'Buscar producto',
            ejemplo: 'Código, nombre o categoría…',
            autofocus: true,
            textInputAction: TextInputAction.search,
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: 8),
          if (visibles.isEmpty)
            const Vacio(icono: Icons.search_off, titulo: 'Ningún producto coincide con la búsqueda.')
          else
            for (final producto in visibles)
              ListTile(
                contentPadding: EdgeInsets.zero,
                minTileHeight: 56,
                title: Text(producto.descripcion, style: const TextStyle(fontWeight: FontWeight.w600)),
                subtitle: Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(
                      producto.sinStock
                          ? formatearMonto(producto.precio)
                          : '${formatearMonto(producto.precio)} · Stock: ${formatearCantidad(producto.stock)}',
                      style: const TextStyle(color: textoSecundario),
                    ),
                    if (producto.sinStock)
                      const EtiquetaDeEstado(
                        'Sin stock',
                        icono: Icons.remove_shopping_cart_outlined,
                        color: ColorDeEstado.ambar,
                      ),
                  ],
                ),
                onTap: () => context.pop(producto),
              ),
        ],
      ),
    );
  }
}
