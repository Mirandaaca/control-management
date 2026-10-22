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
    builder: (contexto) => AlertDialog(
      title: Text(titulo),
      content: SingleChildScrollView(child: contenido),
      actions: [
        OutlinedButton(onPressed: () => Navigator.of(contexto).pop(false), child: const Text('Cancelar')),
        FilledButton(
          style: FilledButton.styleFrom(
            // En el diálogo, el botón mide lo que su texto: el del tema ocupa todo el ancho.
            minimumSize: const Size(64, 48),
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
