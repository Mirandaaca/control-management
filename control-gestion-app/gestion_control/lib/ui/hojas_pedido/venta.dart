import 'package:flutter/material.dart';

import '../../data/models/hoja_pedido.dart';
import '../../domain/credito.dart';
import '../core/formato.dart';
import '../core/tema.dart';
import '../core/widgets/confirmar.dart';
import '../core/widgets/etiqueta_de_estado.dart';

/// El estado de una HP, con su texto y su ícono (docs/diseno.md, "Etiquetas de estado").
class EtiquetaDeHoja extends StatelessWidget {
  const EtiquetaDeHoja(this.estado, {super.key});

  final EstadoHP estado;

  @override
  Widget build(BuildContext context) => switch (estado) {
    EstadoHP.borrador => const EtiquetaDeEstado('Borrador', icono: Icons.edit_note, color: ColorDeEstado.gris),
    EstadoHP.confirmada => const EtiquetaDeEstado('Confirmada', icono: Icons.check_circle, color: ColorDeEstado.verde),
    EstadoHP.anulada => const EtiquetaDeEstado('Anulada', icono: Icons.block, color: ColorDeEstado.rojo),
  };
}

String tipoDeVenta(TipoVenta tipo) => tipo == TipoVenta.credito ? 'Crédito' : 'Contado';

/// El aviso de éxito al guardar o al confirmar una venta, con el número y el total. A crédito, la fecha de vencimiento
/// es lo que el promotor le dice al cliente: la deuda nace al confirmar, así que vence a los 30 días de ahora.
String mensajeDeVenta(HojaPedido hp, {DateTime? ahora}) => switch ((hp.estado, hp.tipoVenta)) {
  (EstadoHP.borrador, _) => '${hp.numero} quedó en borrador: confírmala cuando cierres la venta.',
  (_, TipoVenta.contado) => '${hp.numero} quedó confirmada por ${formatearMonto(hp.total)} al contado.',
  (_, TipoVenta.credito) =>
    '${hp.numero} quedó confirmada por ${formatearMonto(hp.total)} a crédito: la deuda vence el '
        '${formatearMomento(vencimientoDelCredito(ahora ?? DateTime.now()))}.',
};

/// "Confirmar venta" pide confirmar con el resumen de la venta: el cliente, cuántos productos, contado o crédito (con su
/// vencimiento) y el total. El botón es el primario, no el rojo: confirmar no destruye nada.
Future<bool> confirmarVenta(
  BuildContext context, {
  required String cliente,
  required int productos,
  required double unidades,
  required TipoVenta tipo,
  required double total,
}) {
  final credito = tipo == TipoVenta.credito;
  return confirmar(
    context,
    titulo: '¿Confirmar la venta?',
    accion: 'Confirmar venta',
    contenido: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(cliente, style: const TextStyle(fontWeight: FontWeight.w600)),
        Text(
          '${productos == 1 ? '1 producto' : '$productos productos'}, '
          '${unidades == 1 ? '1 unidad' : '${formatearCantidad(unidades)} unidades'}',
        ),
        Text(
          credito
              ? 'A crédito: la deuda vence el ${formatearMomento(vencimientoDelCredito(DateTime.now()))}.'
              : 'Al contado.',
        ),
        const SizedBox(height: 8),
        Text('Total: ${formatearMonto(total)}', style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 8),
        Text(
          credito ? 'Se descuenta el stock y nace la deuda.' : 'Se descuenta el stock.',
          style: const TextStyle(color: textoSecundario),
        ),
      ],
    ),
  );
}
