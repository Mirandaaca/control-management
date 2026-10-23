import 'package:flutter/material.dart';

import '../formato.dart';
import '../tema.dart';
import '../widgets/campo_de_texto.dart';
import '../widgets/campos_numericos.dart';
import '../widgets/selectores.dart';
import 'lineas.dart';

/// Una línea de productos (8.6.5): el producto con su precio y su stock, la cantidad, el descuento por unidad y el
/// subtotal. Sus campos viven aquí: siguen a la línea aunque se quiten otras. La usan la venta y el pedido.
class LineaDeProducto extends StatefulWidget {
  const LineaDeProducto({
    super.key,
    required this.indice,
    required this.linea,
    required this.campos,
    required this.lugares,
    required this.alElegirProducto,
    required this.alQuitar,
    required this.alCambiarCantidad,
    required this.alCambiarDescuento,
    this.avisoDeStock,
  });

  final int indice;
  final LineaDelFormulario linea;

  /// Los errores del formulario, con las claves de la API (`detalles[i].cantidad`).
  final Map<String, String> campos;
  final LugaresDeLinea lugares;
  final VoidCallback? alElegirProducto;
  final VoidCallback? alQuitar;
  final ValueChanged<String> alCambiarCantidad;
  final ValueChanged<String> alCambiarDescuento;

  /// "Solo hay N en stock…", en ámbar.
  final String? avisoDeStock;

  @override
  State<LineaDeProducto> createState() => _LineaDeProductoState();
}

class _LineaDeProductoState extends State<LineaDeProducto> {
  late final _cantidad = TextEditingController(text: widget.linea.cantidad);
  late final _descuento = TextEditingController(text: widget.linea.descuento);

  @override
  void dispose() {
    _cantidad.dispose();
    _descuento.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final linea = widget.linea;
    final producto = linea.producto;
    final campos = widget.campos;
    final prefijo = 'detalles[${widget.indice}]';
    final numero = widget.indice + 1;
    final avisoDeStock = widget.avisoDeStock;
    // Con la letra grande, la cantidad y el descuento van uno debajo del otro: lado a lado, sus etiquetas se cortarían.
    final apilados = MediaQuery.textScalerOf(context).scale(16) > 22;

    final cantidad = KeyedSubtree(
      key: widget.lugares.cantidad.llave,
      child: CampoDeTexto(
        controller: _cantidad,
        etiqueta: 'Cantidad',
        keyboardType: tecladoDecimal,
        inputFormatters: [FormatoDecimal(decimales: 3)],
        textAlign: TextAlign.end,
        focusNode: widget.lugares.cantidad.foco,
        error: campos['$prefijo.cantidad'],
        onChanged: widget.alCambiarCantidad,
      ),
    );
    final descuento = KeyedSubtree(
      key: widget.lugares.descuento.llave,
      child: CampoDeTexto(
        controller: _descuento,
        etiqueta: 'Descuento c/u (Bs)',
        keyboardType: tecladoDecimal,
        inputFormatters: [FormatoDecimal()],
        textAlign: TextAlign.end,
        focusNode: widget.lugares.descuento.foco,
        error: campos['$prefijo.descuento'],
        onChanged: widget.alCambiarDescuento,
      ),
    );

    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Stack(
              clipBehavior: Clip.none,
              children: [
                KeyedSubtree(
                  key: widget.lugares.producto.llave,
                  child: CampoDeEleccion(
                    etiqueta: 'Producto $numero',
                    valor: producto?.descripcion,
                    ejemplo: 'Busca el producto…',
                    buscando: true,
                    focusNode: widget.lugares.producto.foco,
                    error: campos['$prefijo.productoId'],
                    ayuda: producto == null
                        ? null
                        : '${formatearMonto(producto.precio)} c/u · '
                              '${producto.sinStock ? 'Sin stock' : 'Stock: ${formatearCantidad(producto.stock)}'}',
                    alTocar: widget.alElegirProducto,
                  ),
                ),
                // El tacho va a la altura de la etiqueta. El ícono mide 24 dp, pero se toca en 48 dp.
                Positioned(
                  top: -14,
                  right: -12,
                  child: IconButton(
                    onPressed: widget.alQuitar,
                    tooltip: 'Quitar el producto $numero',
                    icon: const Icon(Icons.delete_outline, color: textoSecundario),
                  ),
                ),
              ],
            ),
            if (avisoDeStock != null) ...[const SizedBox(height: 4), AvisoDeStock(avisoDeStock)],
            const SizedBox(height: 12),
            if (apilados) ...[
              cantidad,
              const SizedBox(height: 12),
              descuento,
            ] else
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(child: cantidad),
                  const SizedBox(width: 12),
                  Expanded(child: descuento),
                ],
              ),
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerRight,
              child: MergeSemantics(
                child: Wrap(
                  spacing: 8,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    const Text('Subtotal', style: TextStyle(color: textoSecundario)),
                    Text(
                      formatearMonto(linea.subtotal),
                      style: const TextStyle(fontWeight: FontWeight.w600, color: texto),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// "Solo hay N en stock.", en ámbar: avisa antes de confirmar, sin impedir guardar.
class AvisoDeStock extends StatelessWidget {
  const AvisoDeStock(this.texto, {super.key});

  final String texto;

  @override
  Widget build(BuildContext context) {
    const color = Color(0xFF92400E);
    return Semantics(
      liveRegion: true,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const ExcludeSemantics(child: Icon(Icons.warning_amber_outlined, size: 18, color: color)),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              texto,
              style: const TextStyle(color: color, fontSize: 14, height: 20 / 14),
            ),
          ),
        ],
      ),
    );
  }
}

/// El subtotal, el descuento sobre el total y el total, en una tarjeta.
class ResumenDeTotales extends StatelessWidget {
  const ResumenDeTotales({super.key, required this.subtotal, required this.descuento, required this.total});

  final double subtotal;
  final double descuento;
  final double total;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Column(
          children: [
            FilaDeTotal('Subtotal', formatearMonto(subtotal)),
            FilaDeTotal('Descuento', formatearMonto(descuento)),
            const Divider(height: 16),
            FilaDeTotal('Total', formatearMonto(total), fuerte: true),
          ],
        ),
      ),
    );
  }
}

/// Una fila de un resumen: el título a la izquierda y el monto a la derecha.
class FilaDeTotal extends StatelessWidget {
  const FilaDeTotal(this.titulo, this.valor, {super.key, this.fuerte = false});

  final String titulo;
  final String valor;
  final bool fuerte;

  @override
  Widget build(BuildContext context) {
    final estilo = fuerte ? Theme.of(context).textTheme.titleMedium : null;
    return MergeSemantics(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 2),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Flexible(
              child: Text(titulo, style: estilo ?? const TextStyle(color: textoSecundario)),
            ),
            const SizedBox(width: 8),
            // Con la letra grande, un monto largo pasa de línea en vez de salirse.
            Flexible(
              child: Text(valor, textAlign: TextAlign.end, style: estilo),
            ),
          ],
        ),
      ),
    );
  }
}

/// El total en la barra de abajo: se ve mientras se edita cualquier cantidad o descuento.
class TotalDeLaBarra extends StatelessWidget {
  const TotalDeLaBarra(this.total, {super.key});

  final double total;

  @override
  Widget build(BuildContext context) {
    final estilo = Theme.of(context).textTheme.titleMedium;
    return Semantics(
      liveRegion: true,
      child: MergeSemantics(
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Flexible(child: Text('Total', style: estilo)),
            const SizedBox(width: 8),
            Flexible(
              child: Text(formatearMonto(total), textAlign: TextAlign.end, style: estilo),
            ),
          ],
        ),
      ),
    );
  }
}
