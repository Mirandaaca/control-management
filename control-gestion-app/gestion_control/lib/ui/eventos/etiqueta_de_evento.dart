import 'package:flutter/material.dart';

import '../../data/models/evento.dart';
import '../core/widgets/etiqueta_de_estado.dart';

/// El estado de un evento, con su texto y su ícono (docs/diseno.md, "Etiquetas de estado"): ámbar y azul, como en el
/// panel; el rojo y el verde son de los estados del cliente.
class EtiquetaDeEvento extends StatelessWidget {
  const EtiquetaDeEvento(this.estado, {super.key});

  final EstadoEvento estado;

  @override
  Widget build(BuildContext context) => switch (estado) {
    EstadoEvento.pendiente => const EtiquetaDeEstado(
      'Pendiente',
      icono: Icons.hourglass_empty,
      color: ColorDeEstado.ambar,
    ),
    EstadoEvento.resuelto => const EtiquetaDeEstado('Resuelto', icono: Icons.check_circle, color: ColorDeEstado.azul),
  };
}
