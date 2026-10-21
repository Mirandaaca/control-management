import 'package:flutter/material.dart';

import 'aviso.dart';

/// El estado de error de una pantalla, como `ErrorDeCarga` del panel: el mensaje de la API y "Reintentar".
class ErrorConReintento extends StatelessWidget {
  const ErrorConReintento({super.key, required this.mensaje, required this.alReintentar, this.reintentando = false});

  final String mensaje;
  final VoidCallback alReintentar;
  final bool reintentando;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Aviso(mensaje),
            const SizedBox(height: 16),
            OutlinedButton.icon(
              onPressed: reintentando ? null : alReintentar,
              icon: reintentando
                  ? const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(Icons.refresh),
              label: Text(reintentando ? 'Reintentando…' : 'Reintentar'),
            ),
          ],
        ),
      ),
    );
  }
}
