import 'package:flutter/material.dart';

import '../tema.dart';

/// Pocas opciones lado a lado, una elegida: Contado | Crédito, Efectivo | QR (8.6.5 y 8.6.7). La elegida lleva el
/// check, no solo el color. Con la letra grande, si una palabra no entra en su segmento ("Por cobrar" en un tercio), las
/// opciones se apilan, cada una a lo ancho: así ninguna palabra se parte.
class Segmentado<T> extends StatelessWidget {
  const Segmentado({super.key, required this.opciones, required this.valor, required this.alCambiar, this.etiqueta});

  final List<(T, String)> opciones;
  final T valor;
  final ValueChanged<T> alCambiar;
  final String? etiqueta;

  // La palabra más larga de las opciones, con la letra del celular.
  double _palabraMasLarga(BuildContext context) {
    final estilo = Theme.of(context).textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600);
    var ancho = 0.0;
    for (final (_, texto) in opciones) {
      for (final palabra in texto.split(' ')) {
        final pintor = TextPainter(
          text: TextSpan(text: palabra, style: estilo),
          textScaler: MediaQuery.textScalerOf(context),
          textDirection: Directionality.of(context),
          maxLines: 1,
        )..layout();
        if (pintor.width > ancho) ancho = pintor.width;
      }
    }
    return ancho;
  }

  @override
  Widget build(BuildContext context) {
    final segmentos = LayoutBuilder(
      builder: (context, limites) {
        final segmento = (limites.maxWidth - 2 - (opciones.length - 1)) / opciones.length;
        final entran = _palabraMasLarga(context) <= segmento - _relleno * 2 - _check;
        Widget segmentoDe(T opcion, String texto) =>
            _Segmento(rotulo: texto, elegido: opcion == valor, alTocar: () => alCambiar(opcion));
        return Container(
          decoration: BoxDecoration(
            border: Border.all(color: bordeControl),
            borderRadius: BorderRadius.circular(8),
          ),
          clipBehavior: Clip.antiAlias,
          child: entran
              ? IntrinsicHeight(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      for (final (indice, (opcion, texto)) in opciones.indexed) ...[
                        if (indice > 0) const VerticalDivider(width: 1, thickness: 1, color: bordeControl),
                        Expanded(child: segmentoDe(opcion, texto)),
                      ],
                    ],
                  ),
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    for (final (indice, (opcion, texto)) in opciones.indexed) ...[
                      if (indice > 0) const Divider(height: 1, thickness: 1, color: bordeControl),
                      segmentoDe(opcion, texto),
                    ],
                  ],
                ),
        );
      },
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

// Ajustados para que "Vencidas" con su check entre en un tercio de un celular de 360 dp: con 8 de relleno y un check
// de 18, la elegida partía la palabra ("Vencida-s").
const _relleno = 6.0;
const _tamanoDelCheck = 16.0;
const _separacionDelCheck = 4.0;

// El check se cuenta en todas las opciones, porque la elegida cambia.
const _check = _tamanoDelCheck + _separacionDelCheck;

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
              padding: const EdgeInsets.symmetric(horizontal: _relleno, vertical: 10),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  if (elegido) ...[
                    const Icon(Icons.check, size: _tamanoDelCheck, color: Color(0xFF1C3A66)),
                    const SizedBox(width: _separacionDelCheck),
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
