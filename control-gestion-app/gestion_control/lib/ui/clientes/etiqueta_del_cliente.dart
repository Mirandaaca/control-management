import 'package:flutter/material.dart';

import '../../data/models/cliente.dart';
import '../core/widgets/etiqueta_de_estado.dart';

// La paleta de los estados asignados a mano, la tercera copia: la primera está en
// ControlGestion.API/Models/PaletaDeEstados.cs y la segunda en control-gestion-ui/src/components/paletaDeEstados.js. Si
// cambia allá, cambia aquí. Rojo y verde quedan para Activo y Disponible; cada color lleva texto blanco con 4,5:1 o más.
const _paleta = {'#475569', '#1E293B', '#1D4ED8', '#0369A1', '#4338CA', '#6D28D9'};

/// El color de un estado asignado a mano, si es de la paleta; si no, null (la API solo acepta los de la paleta).
Color? colorDePaleta(String valor) {
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
    _ => EtiquetaDeEstado(estado.nombre, icono: null, color: ColorDeEstado.gris, fondo: colorDePaleta(estado.color)),
  };
}

/// El color de un cliente en el mapa (docs/diseno.md, sección 2): rojo-600 activo, verde-700 disponible y los manuales
/// con el de la paleta (Pizarra si llegara otro).
Color colorDelEstado(EstadoCliente estado) => switch (estado.id) {
  EstadoCliente.idActivo => const Color(0xFFDC2626),
  EstadoCliente.idDisponible => const Color(0xFF15803D),
  _ => colorDePaleta(estado.color) ?? const Color(0xFF475569),
};

/// El ícono de un estado del cliente: el reloj para Activo y el pin para Disponible. Los manuales no llevan.
IconData? iconoDelEstado(EstadoCliente estado) => switch (estado.id) {
  EstadoCliente.idActivo => Icons.schedule,
  EstadoCliente.idDisponible => Icons.place_outlined,
  _ => null,
};
