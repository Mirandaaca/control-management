import 'package:flutter/material.dart';

/// Los cuatro tipos de mensaje de docs/diseno.md (sección 2, "Mensajes"), con su ícono: nunca van solo con el color.
enum TipoDeAviso {
  error(Color(0xFFB91C1C), Color(0xFFFEF2F2), Color(0xFFFECACA), Icons.error_outline),
  exito(Color(0xFF166534), Color(0xFFF0FDF4), Color(0xFFBBF7D0), Icons.check_circle_outline),
  aviso(Color(0xFF92400E), Color(0xFFFFFBEB), Color(0xFFFDE68A), Icons.warning_amber_outlined),
  informacion(Color(0xFF1C3A66), Color(0xFFEFF5FB), Color(0xFFB9D1EB), Icons.info_outline);

  const TipoDeAviso(this.texto, this.fondo, this.borde, this.icono);

  final Color texto;
  final Color fondo;
  final Color borde;
  final IconData icono;
}

/// Un recuadro con un mensaje, como `Alert` del panel. El lector de pantalla lo lee apenas aparece.
class Aviso extends StatelessWidget {
  const Aviso(this.texto, {super.key, this.tipo = TipoDeAviso.error});

  final String texto;
  final TipoDeAviso tipo;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      container: true,
      liveRegion: true,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        decoration: BoxDecoration(
          color: tipo.fondo,
          border: Border.all(color: tipo.borde),
          borderRadius: BorderRadius.circular(8),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Icon(tipo.icono, color: tipo.texto, size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(texto, style: TextStyle(color: tipo.texto)),
            ),
          ],
        ),
      ),
    );
  }
}
