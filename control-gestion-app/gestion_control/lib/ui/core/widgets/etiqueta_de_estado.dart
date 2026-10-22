import 'package:flutter/material.dart';

/// Los colores de las etiquetas de estado, los mismos del `Badge` del panel: fondo -50 y texto -700 u -800.
enum ColorDeEstado {
  ambar(Color(0xFF92400E), Color(0xFFFFFBEB)),
  azul(Color(0xFF1C3A66), Color(0xFFEFF5FB)),
  verde(Color(0xFF166534), Color(0xFFF0FDF4)),
  rojo(Color(0xFFB91C1C), Color(0xFFFEF2F2)),
  violeta(Color(0xFF6B21A8), Color(0xFFFAF5FF)),
  gris(Color(0xFF334155), Color(0xFFF1F5F9));

  const ColorDeEstado(this.texto, this.fondo);

  final Color texto;
  final Color fondo;
}

/// La etiqueta de un estado, con su texto y su ícono: nunca solo con el color (docs/diseno.md, "Etiquetas de estado").
class EtiquetaDeEstado extends StatelessWidget {
  const EtiquetaDeEstado(this.texto, {super.key, required this.icono, required this.color, this.fondo});

  final String texto;
  final IconData? icono;
  final ColorDeEstado color;

  /// Para los estados del cliente asignados a mano, que traen su propio color (7.4).
  final Color? fondo;

  @override
  Widget build(BuildContext context) {
    final colorDelTexto = fondo == null ? color.texto : Colors.white;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 2),
      decoration: BoxDecoration(color: fondo ?? color.fondo, borderRadius: BorderRadius.circular(999)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icono != null) ...[
            ExcludeSemantics(child: Icon(icono, size: 16, color: colorDelTexto)),
            const SizedBox(width: 4),
          ],
          Flexible(
            child: Text(
              texto,
              style: TextStyle(fontSize: 14, height: 20 / 14, fontWeight: FontWeight.w600, color: colorDelTexto),
            ),
          ),
        ],
      ),
    );
  }
}
