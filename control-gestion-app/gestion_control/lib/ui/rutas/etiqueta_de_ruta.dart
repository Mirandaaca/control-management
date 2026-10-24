import 'package:flutter/material.dart';

import '../../data/models/ruta.dart';
import '../core/formato.dart';
import '../core/widgets/etiqueta_de_estado.dart';

/// El estado de una ruta, con su texto y su ícono (docs/diseno.md, "Etiquetas de estado"): Planificada en ámbar, En
/// curso en azul con desde qué hora y Completada en gris con a qué hora, como el 8.6.9 y el panel.
class EtiquetaDeRuta extends StatelessWidget {
  const EtiquetaDeRuta(this.ruta, {super.key});

  final Ruta ruta;

  @override
  Widget build(BuildContext context) => switch (ruta.estado) {
    EstadoRuta.planificada => const EtiquetaDeEstado('Planificada', icono: Icons.event, color: ColorDeEstado.ambar),
    EstadoRuta.enCurso => EtiquetaDeEstado(
      'En curso desde las ${formatearHora(ruta.inicio)}',
      icono: Icons.navigation,
      color: ColorDeEstado.azul,
    ),
    EstadoRuta.completada => EtiquetaDeEstado(
      'Completada a las ${formatearHora(ruta.fin)}',
      icono: Icons.check_circle,
      color: ColorDeEstado.gris,
    ),
  };
}
