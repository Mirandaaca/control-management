import 'package:flutter/material.dart';

import '../../../data/services/error_de_api.dart';
import 'aviso.dart';

/// El estado de error de una pantalla, como `ErrorDeCarga` del panel: el mensaje de la API y "Reintentar".
class ErrorConReintento extends StatelessWidget {
  const ErrorConReintento({
    super.key,
    required this.mensaje,
    required this.alReintentar,
    this.reintentando = false,
    this.detalle,
  });

  /// Sin internet, el mensaje es el título y el detalle dice qué revisar (8.6.12).
  factory ErrorConReintento.de(ErrorDeApi error, {required VoidCallback alReintentar, bool reintentando = false}) =>
      ErrorConReintento(
        mensaje: error.mensaje,
        detalle: error.detalle,
        alReintentar: alReintentar,
        reintentando: reintentando,
      );

  final String mensaje;
  final String? detalle;
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
            if (detalle == null) Aviso(mensaje) else Aviso(detalle!, titulo: mensaje),
            const SizedBox(height: 16),
            BotonReintentar(alReintentar: alReintentar, reintentando: reintentando),
          ],
        ),
      ),
    );
  }
}

/// "Reintentar", con su indicador mientras vuelve a intentar.
class BotonReintentar extends StatelessWidget {
  const BotonReintentar({super.key, required this.alReintentar, this.reintentando = false});

  final VoidCallback? alReintentar;
  final bool reintentando;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      onPressed: reintentando ? null : alReintentar,
      icon: reintentando
          ? const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2))
          : const Icon(Icons.refresh),
      label: Text(reintentando ? 'Reintentando…' : 'Reintentar'),
    );
  }
}
