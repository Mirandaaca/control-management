import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/cobranza.dart';
import '../core/formato.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/widgets/etiqueta_de_estado.dart';

/// El estado de una cobranza, con su texto y su ícono (docs/diseno.md, "Etiquetas de estado").
class EtiquetaDeCobranza extends StatelessWidget {
  const EtiquetaDeCobranza(this.estado, {super.key});

  final EstadoCobranza estado;

  @override
  Widget build(BuildContext context) => switch (estado) {
    EstadoCobranza.pendiente => const EtiquetaDeEstado(
      'Pendiente',
      icono: Icons.hourglass_empty,
      color: ColorDeEstado.ambar,
    ),
    EstadoCobranza.parcial => const EtiquetaDeEstado('Parcial', icono: Icons.pie_chart, color: ColorDeEstado.azul),
    EstadoCobranza.vencida => const EtiquetaDeEstado('Vencida', icono: Icons.event_busy, color: ColorDeEstado.rojo),
    EstadoCobranza.pagada => const EtiquetaDeEstado('Pagada', icono: Icons.check_circle, color: ColorDeEstado.verde),
    EstadoCobranza.incobrable => const EtiquetaDeEstado(
      'Incobrable',
      icono: Icons.cancel,
      color: ColorDeEstado.violeta,
    ),
    EstadoCobranza.anulada => const EtiquetaDeEstado('Anulada', icono: Icons.block, color: ColorDeEstado.gris),
  };
}

/// "Vence el 07/11/2026" o, si ya venció, "Venció el 15/10/2026 · 14 días de mora" (3.2).
String vencimientoDe(String fechaVencimiento, int? diasDeMora) {
  final fecha = formatearFecha(fechaVencimiento);
  if (diasDeMora == null) return 'Vence el $fecha';
  return 'Venció el $fecha · ${diasDeMora == 1 ? '1 día' : '$diasDeMora días'} de mora';
}

/// Una cobranza en una lista: el cliente, el número, el saldo, el vencimiento o la mora, y su estado.
class TarjetaDeCobranza extends StatelessWidget {
  const TarjetaDeCobranza(this.cobranza, {super.key, this.conCliente = true});

  final CobranzaResumen cobranza;

  /// En el detalle de un cliente, su nombre sobra.
  final bool conCliente;

  @override
  Widget build(BuildContext context) {
    final c = cobranza;
    return MergeSemantics(
      child: Card(
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: () => context.push(Rutas.cobranza(c.id)),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  spacing: 12,
                  runSpacing: 4,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(conCliente ? c.clienteNombre : c.numero, style: const TextStyle(fontWeight: FontWeight.w600)),
                    EtiquetaDeCobranza(c.estado),
                  ],
                ),
                Text(
                  [if (conCliente) c.numero, ?c.hojaPedido].join(' · '),
                  style: const TextStyle(color: textoSecundario),
                ),
                const SizedBox(height: 4),
                Text('Saldo ${formatearMonto(c.saldo)}', style: const TextStyle(fontWeight: FontWeight.w600)),
                Text(vencimientoDe(c.fechaVencimiento, c.diasDeMora), style: const TextStyle(color: textoSecundario)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
