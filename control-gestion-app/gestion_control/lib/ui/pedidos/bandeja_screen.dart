import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/pedido.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/tarjeta_que_se_toca.dart';
import '../core/widgets/vacio.dart';
import '../sesion/sesion_notifier.dart';
import 'bandeja_view_model.dart';
import 'etiqueta_de_pedido.dart';

/// La bandeja del día (1.3; `frmBandejaPedidosMovil`; 8.6.10): los pedidos que tiene asignados para hoy, por hora, con
/// su cliente, su dirección y su monto (7.3.3, paso 3.2). Los que ya están en ruta tienen "Registrar entrega".
class BandejaScreen extends ConsumerWidget {
  const BandejaScreen({super.key, this.mensaje});

  /// El aviso de la entrega que se acaba de registrar.
  final String? mensaje;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estado = ref.watch(bandejaProvider);
    final viewModel = ref.read(bandejaProvider.notifier);
    final sesion = ref.watch(sesionProvider);
    final puedeEntregar = sesion is ConSesion && sesion.sesion.tiene([Permisos.pedidos.registrarEntrega]);

    return Scaffold(
      appBar: AppBar(title: const Text('Bandeja del día')),
      body: switch (estado) {
        EstadoDeLaBandeja(pedidos: final pedidos?) => RefreshIndicator(
          onRefresh: viewModel.cargar,
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
            children: [
              if (mensaje != null) ...[Aviso(mensaje!, tipo: TipoDeAviso.exito), const SizedBox(height: 16)],
              _Cabeza(cantidad: pedidos.length),
              const SizedBox(height: 12),
              if (pedidos.isEmpty)
                // 7.3.3, ext. 3.1a, con el texto del 8.6.12.
                const Vacio(
                  icono: Icons.inbox_outlined,
                  titulo: 'No tienes entregas para hoy',
                  texto:
                      'Aquí aparecen los pedidos que la secretaria te asigna para hoy, menos los entregados y los '
                      'anulados.',
                )
              else
                for (final pedido in pedidos) ...[
                  _TarjetaDeEntrega(pedido, puedeEntregar: puedeEntregar),
                  const SizedBox(height: 12),
                ],
            ],
          ),
        ),
        EstadoDeLaBandeja(error: final error?) => ErrorConReintento.de(
          error,
          reintentando: estado.reintentando,
          alReintentar: viewModel.cargar,
        ),
        _ => const Cargando('Cargando la bandeja…'),
      },
    );
  }
}

/// "Hoy, 30/10/2026" y cuántas entregas hay (7.3.3, paso 3).
class _Cabeza extends StatelessWidget {
  const _Cabeza({required this.cantidad});

  final int cantidad;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      alignment: WrapAlignment.spaceBetween,
      crossAxisAlignment: WrapCrossAlignment.end,
      spacing: 12,
      runSpacing: 4,
      children: [
        Semantics(
          header: true,
          container: true,
          child: Text(
            'Hoy, ${formatearMomento(DateTime.now().toUtc())}',
            style: Theme.of(context).textTheme.titleMedium,
          ),
        ),
        Text(cantidad == 1 ? '1 entrega' : '$cantidad entregas', style: const TextStyle(color: textoSecundario)),
      ],
    );
  }
}

/// Un pedido de la bandeja (8.6.10): la hora, el estado, el cliente, la dirección, el número y el monto. Uno que todavía
/// no está en ruta no se entrega (7.3.2, ext. 3.1b): no lleva el botón y dice por qué.
class _TarjetaDeEntrega extends StatelessWidget {
  const _TarjetaDeEntrega(this.pedido, {required this.puedeEntregar});

  final PedidoResumen pedido;
  final bool puedeEntregar;

  @override
  Widget build(BuildContext context) {
    final enRuta = pedido.estado.id == EstadosDePedido.enRuta;
    return TarjetaQueSeToca(
      alTocar: () => context.push(Rutas.pedidoDeLaBandeja(pedido.id)),
      abajo: enRuta && puedeEntregar
          ? OutlinedButton.icon(
              onPressed: () => context.push(Rutas.entrega(pedido.id)),
              icon: const Icon(Icons.local_shipping),
              label: const Text('Registrar entrega'),
            )
          : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(
                      formatearHora(pedido.fechaEntrega),
                      style: const TextStyle(fontSize: 18, height: 24 / 18, fontWeight: FontWeight.w600),
                    ),
                    EtiquetaDePedido(pedido.estado),
                  ],
                ),
              ),
              const ExcludeSemantics(child: Icon(Icons.chevron_right, color: textoSecundario)),
            ],
          ),
          const SizedBox(height: 4),
          Text(pedido.cliente.nombre, style: const TextStyle(fontWeight: FontWeight.w600)),
          Text(pedido.direccionEntrega, style: const TextStyle(color: textoSecundario)),
          const SizedBox(height: 4),
          Wrap(
            alignment: WrapAlignment.spaceBetween,
            spacing: 12,
            children: [
              Text(pedido.numero, style: const TextStyle(color: textoSecundario)),
              Text(formatearMonto(pedido.total), style: const TextStyle(fontWeight: FontWeight.w600)),
            ],
          ),
          if (!enRuta) ...[
            const SizedBox(height: 4),
            const Text('Se entrega cuando salga a ruta.', style: TextStyle(color: textoSecundario)),
          ],
        ],
      ),
    );
  }
}
