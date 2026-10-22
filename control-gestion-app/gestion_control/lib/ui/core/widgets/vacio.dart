import 'package:flutter/material.dart';

/// Una lista sin nada que mostrar (docs/diseno.md, "Estados de una pantalla"): un ícono, un título y una línea que
/// explica, como "No tienes entregas para hoy" del 8.6.12.
class Vacio extends StatelessWidget {
  const Vacio({super.key, required this.icono, required this.titulo, this.texto});

  final IconData icono;
  final String titulo;
  final String? texto;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 32),
      child: Semantics(
        container: true,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ExcludeSemantics(child: Icon(icono, size: 32, color: const Color(0xFF94A3B8))),
            const SizedBox(height: 12),
            Text(titulo, textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium),
            if (texto != null) ...[
              const SizedBox(height: 4),
              Text(
                texto!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: Color(0xFF475569)),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
