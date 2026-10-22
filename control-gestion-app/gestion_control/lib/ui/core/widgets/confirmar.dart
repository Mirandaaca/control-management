import 'package:flutter/material.dart';

/// Pide confirmación antes de algo que no se deshace (reemplazar un comprobante) o que cierra algo (confirmar una
/// venta): dice qué va a pasar, con "Cancelar" y la acción. Solo lo que destruye algo va en rojo (8.6.12).
Future<bool> confirmar(
  BuildContext context, {
  required String titulo,
  required Widget contenido,
  required String accion,
  bool peligro = false,
}) async {
  final confirmado = await showDialog<bool>(
    context: context,
    // Como en el 8.6.12: 312 dp en una pantalla de 360 y los botones del ancho de su texto, a la derecha. Si no entran
    // lado a lado ("Cancelar" y "Confirmar venta"), se apilan con 8 dp entre ellos y la acción abajo.
    builder: (contexto) => AlertDialog(
      insetPadding: const EdgeInsets.symmetric(horizontal: 24, vertical: 24),
      // Sin el mínimo, el diálogo toma el ancho de su texto, desde 280 dp.
      constraints: const BoxConstraints(minWidth: 312, maxWidth: 560),
      actionsOverflowButtonSpacing: 8,
      title: Text(titulo),
      content: SingleChildScrollView(child: contenido),
      actions: [
        OutlinedButton(
          style: OutlinedButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: 20)),
          onPressed: () => Navigator.of(contexto).pop(false),
          child: const Text('Cancelar'),
        ),
        FilledButton(
          style: FilledButton.styleFrom(
            // En el diálogo, el botón mide lo que su texto: el del tema ocupa todo el ancho.
            minimumSize: const Size(64, 48),
            padding: const EdgeInsets.symmetric(horizontal: 20),
            backgroundColor: peligro ? const Color(0xFFDC2626) : null,
          ),
          onPressed: () => Navigator.of(contexto).pop(true),
          child: Text(accion),
        ),
      ],
    ),
  );
  return confirmado ?? false;
}
