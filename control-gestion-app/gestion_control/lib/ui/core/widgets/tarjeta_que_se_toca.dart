import 'package:flutter/material.dart';

/// Una tarjeta de una lista que abre su registro. El toque y los textos son un solo elemento, que el lector de pantalla
/// anuncia como botón: `MergeSemantics` va por fuera del `InkWell` (al revés, la guía `labeledTapTargetGuideline`
/// falla). [abajo] es una acción aparte, como "Registrar entrega", que queda fuera del toque de la tarjeta.
class TarjetaQueSeToca extends StatelessWidget {
  const TarjetaQueSeToca({super.key, required this.alTocar, required this.child, this.abajo});

  final VoidCallback alTocar;
  final Widget child;
  final Widget? abajo;

  @override
  Widget build(BuildContext context) {
    final tocable = MergeSemantics(
      child: Semantics(
        button: true,
        child: InkWell(
          onTap: alTocar,
          child: Padding(padding: const EdgeInsets.all(16), child: child),
        ),
      ),
    );
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: abajo == null
          ? tocable
          : Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                tocable,
                Padding(padding: const EdgeInsets.fromLTRB(16, 0, 16, 16), child: abajo),
              ],
            ),
    );
  }
}
