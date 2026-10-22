import 'package:flutter/material.dart';

import '../tema.dart';

/// Pocas opciones lado a lado, una elegida: Contado | Crédito, Efectivo | QR (8.6.5 y 8.6.7). La elegida lleva el
/// check, no solo el color. Con la letra grande, el texto pasa de línea dentro de su segmento en vez de cortarse.
class Segmentado<T> extends StatelessWidget {
  const Segmentado({super.key, required this.opciones, required this.valor, required this.alCambiar, this.etiqueta});

  final List<(T, String)> opciones;
  final T valor;
  final ValueChanged<T> alCambiar;
  final String? etiqueta;

  @override
  Widget build(BuildContext context) {
    final segmentos = Container(
      decoration: BoxDecoration(
        border: Border.all(color: bordeControl),
        borderRadius: BorderRadius.circular(8),
      ),
      clipBehavior: Clip.antiAlias,
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (final (indice, (opcion, texto)) in opciones.indexed) ...[
              if (indice > 0) const VerticalDivider(width: 1, thickness: 1, color: bordeControl),
              Expanded(
                child: _Segmento(rotulo: texto, elegido: opcion == valor, alTocar: () => alCambiar(opcion)),
              ),
            ],
          ],
        ),
      ),
    );
    if (etiqueta == null) return segmentos;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(etiqueta!, style: Theme.of(context).textTheme.labelMedium?.copyWith(color: const Color(0xFF334155))),
        const SizedBox(height: 4),
        segmentos,
      ],
    );
  }
}

class _Segmento extends StatelessWidget {
  const _Segmento({required this.rotulo, required this.elegido, required this.alTocar});

  final String rotulo;
  final bool elegido;
  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context) {
    final color = elegido ? const Color(0xFF1C3A66) : texto;
    return Semantics(
      button: true,
      selected: elegido,
      inMutuallyExclusiveGroup: true,
      child: Material(
        color: elegido ? const Color(0xFFDCE8F5) : Colors.white,
        child: InkWell(
          onTap: alTocar,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minHeight: 48),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 10),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  if (elegido) ...[
                    const Icon(Icons.check, size: 18, color: Color(0xFF1C3A66)),
                    const SizedBox(width: 6),
                  ],
                  Flexible(
                    child: Text(
                      rotulo,
                      textAlign: TextAlign.center,
                      style: TextStyle(fontWeight: FontWeight.w600, color: color),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
