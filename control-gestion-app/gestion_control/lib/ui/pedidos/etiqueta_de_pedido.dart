import 'package:flutter/material.dart';

import '../../data/models/pedido.dart';
import '../core/widgets/etiqueta_de_estado.dart';

/// El estado de un pedido, con el texto de la API y el ícono y el color de su id (docs/diseno.md, "Etiquetas de
/// estado"): la app nunca compara nombres.
class EtiquetaDePedido extends StatelessWidget {
  const EtiquetaDePedido(this.estado, {super.key});

  final EstadoPedido estado;

  @override
  Widget build(BuildContext context) {
    final (icono, color) = switch (estado.id) {
      EstadosDePedido.pendiente => (Icons.hourglass_empty, ColorDeEstado.ambar),
      EstadosDePedido.enProceso => (Icons.inventory_2, ColorDeEstado.azul),
      EstadosDePedido.enRuta => (Icons.route, ColorDeEstado.violeta),
      EstadosDePedido.entregado => (Icons.check_circle, ColorDeEstado.verde),
      EstadosDePedido.anulado => (Icons.block, ColorDeEstado.rojo),
      _ => (null, ColorDeEstado.gris),
    };
    return EtiquetaDeEstado(estado.nombre, icono: icono, color: color);
  }
}
