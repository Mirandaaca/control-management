import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/pedido.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/lista_paginada.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/selectores.dart';
import '../core/widgets/tarjeta_que_se_toca.dart';
import '../core/widgets/vacio.dart';
import '../sesion/sesion_notifier.dart';
import 'etiqueta_de_pedido.dart';
import 'pedidos_view_model.dart';

/// Los pedidos (1.1; `frmPedidoMovil`): los de sus clientes y los que tiene asignados, con su cliente, su entrega, su
/// monto y su estado (7.3.1, paso 1). Desde aquí se registra uno nuevo.
class PedidosScreen extends ConsumerStatefulWidget {
  const PedidosScreen({super.key});

  @override
  ConsumerState<PedidosScreen> createState() => _PedidosScreenState();
}

class _PedidosScreenState extends ConsumerState<PedidosScreen> {
  final _busqueda = TextEditingController();
  Timer? _demora;

  PedidosViewModel get _viewModel => ref.read(pedidosProvider.notifier);

  @override
  void dispose() {
    _demora?.cancel();
    _busqueda.dispose();
    super.dispose();
  }

  void _buscar(String texto) {
    _demora?.cancel();
    _demora = Timer(const Duration(milliseconds: 300), () => _viewModel.buscar(texto));
  }

  // Lo vacío es "todos": null es que se cerró la lista sin elegir.
  Future<void> _elegirEstado() async {
    final elegido = await elegirDeLaLista<int>(
      context,
      titulo: 'Estado',
      opciones: [(0, 'Todos los estados'), for (final e in _viewModel.estados) (e.id, e.nombre)],
      actual: _viewModel.estado?.id ?? 0,
    );
    if (elegido == null) return;
    final estado = _viewModel.estados.where((e) => e.id == elegido).firstOrNull;
    setState(() => _viewModel.filtrarPorEstado(estado));
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(pedidosProvider);
    final sesion = ref.watch(sesionProvider);
    final puedeRegistrar = sesion is ConSesion && sesion.sesion.tiene([Permisos.pedidos.registrarPedido]);

    return Scaffold(
      appBar: AppBar(title: const Text('Pedidos')),
      body: RefreshIndicator(
        onRefresh: _viewModel.cargar,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: [
            CampoDeTexto(
              controller: _busqueda,
              etiqueta: 'Buscar',
              ejemplo: 'Número o cliente…',
              textInputAction: TextInputAction.search,
              onChanged: _buscar,
            ),
            const SizedBox(height: 12),
            CampoDeEleccion(
              etiqueta: 'Estado',
              valor: _viewModel.estado?.nombre ?? 'Todos los estados',
              alTocar: _elegirEstado,
            ),
            const SizedBox(height: 12),
            if (estado.items != null)
              Semantics(
                liveRegion: true,
                child: Text(
                  estado.total == 1 ? '1 pedido' : '${estado.total} pedidos',
                  style: const TextStyle(color: textoSecundario),
                ),
              ),
            const SizedBox(height: 8),
            ...cuerpoDeLista(
              estado: estado,
              textoDeCarga: 'Cargando los pedidos…',
              alReintentar: _viewModel.cargar,
              alCargarMas: _viewModel.cargarMas,
              hayFiltros: _viewModel.hayFiltros,
              vacio: const Vacio(
                icono: Icons.shopping_bag_outlined,
                titulo: 'Todavía no hay pedidos de tus clientes',
                texto: 'Aquí aparecen los pedidos de tus clientes y los que te asignan para entregar.',
              ),
              sinCoincidencias: const Vacio(icono: Icons.search_off, titulo: 'Ningún pedido coincide con la búsqueda.'),
              item: (pedido) => TarjetaDePedido(pedido, alTocar: () => context.push(Rutas.pedido(pedido.id))),
            ),
          ],
        ),
      ),
      bottomNavigationBar: puedeRegistrar
          ? BarraDeAcciones(principal: Accion('Nuevo pedido', () => context.push(Rutas.nuevoPedido)))
          : null,
    );
  }
}

/// Un pedido en una lista: su número y su estado, su cliente, su entrega y su monto.
class TarjetaDePedido extends StatelessWidget {
  const TarjetaDePedido(this.pedido, {super.key, required this.alTocar});

  final PedidoResumen pedido;
  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context) {
    return TarjetaQueSeToca(
      alTocar: alTocar,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 12,
            runSpacing: 4,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(pedido.numero, style: const TextStyle(fontWeight: FontWeight.w600)),
              EtiquetaDePedido(pedido.estado),
            ],
          ),
          Text(pedido.cliente.nombre),
          Text(
            'Entrega ${formatearFecha(pedido.fechaEntrega, hora: true)}',
            style: const TextStyle(color: textoSecundario),
          ),
          Align(
            alignment: Alignment.centerRight,
            child: Text(formatearMonto(pedido.total), style: const TextStyle(fontWeight: FontWeight.w600)),
          ),
        ],
      ),
    );
  }
}
