import 'package:flutter/material.dart';

import '../../data/models/cliente.dart';
import '../core/widgets/etiqueta_de_estado.dart';

// La paleta de los estados asignados a mano, la tercera copia: la primera está en
// ControlGestion.API/Models/PaletaDeEstados.cs y la segunda en control-gestion-ui/src/components/paletaDeEstados.js. Si
// cambia allá, cambia aquí. Rojo y verde quedan para Activo y Disponible; cada color lleva texto blanco con 4,5:1 o más.
const _paleta = {'#475569', '#1E293B', '#1D4ED8', '#0369A1', '#4338CA', '#6D28D9'};

Color? _colorDePaleta(String valor) {
  final hex = valor.trim().toUpperCase();
  if (!_paleta.contains(hex)) return null;
  return Color(int.parse('FF${hex.substring(1)}', radix: 16));
}

/// El estado de un cliente (5.2 y 7.4), como `EstadoCliente.jsx` del panel: Activo en rojo con el reloj, Disponible
/// para re-visita en verde con el pin, y los asignados a mano con su color y su nombre.
class EtiquetaDelCliente extends StatelessWidget {
  const EtiquetaDelCliente(this.estado, {super.key});

  final EstadoCliente estado;

  @override
  Widget build(BuildContext context) => switch (estado.id) {
    EstadoCliente.idActivo => EtiquetaDeEstado(estado.nombre, icono: Icons.schedule, color: ColorDeEstado.rojo),
    EstadoCliente.idDisponible => EtiquetaDeEstado(
      estado.nombre,
      icono: Icons.place_outlined,
      color: ColorDeEstado.verde,
    ),
    _ => EtiquetaDeEstado(estado.nombre, icono: null, color: ColorDeEstado.gris, fondo: _colorDePaleta(estado.color)),
  };
}
