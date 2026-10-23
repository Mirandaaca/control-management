import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../data/models/hoja_pedido.dart';
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
import 'hojas_pedido_view_model.dart';
import 'nueva_hoja_screen.dart';
import 'venta.dart';

/// Sus hojas de pedido (2.1): buscarlas, abrir una, confirmar un borrador y registrar una nueva.
class HojasPedidoScreen extends ConsumerStatefulWidget {
  const HojasPedidoScreen({super.key});

  @override
  ConsumerState<HojasPedidoScreen> createState() => _HojasPedidoScreenState();
}

class _HojasPedidoScreenState extends ConsumerState<HojasPedidoScreen> {
  final _busqueda = TextEditingController();
  Timer? _demora;

  HojasPedidoViewModel get _viewModel => ref.read(hojasPedidoProvider.notifier);

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
      opciones: [(-1, 'Todos los estados'), for (final e in EstadoHP.values) (e.index, _nombreDelEstado(e))],
      actual: _viewModel.estado?.index ?? -1,
    );
    if (elegido == null) return;
    setState(() => _viewModel.filtrarPorEstado(elegido < 0 ? null : EstadoHP.values[elegido]));
  }

  Future<void> _elegirDia() async {
    final hoy = DateTime.now();
    final dia = await showDatePicker(
      context: context,
      initialDate: _viewModel.dia ?? hoy,
      firstDate: DateTime(2024),
      lastDate: hoy,
      helpText: 'Fecha de la venta',
    );
    if (dia != null) setState(() => _viewModel.filtrarPorDia(dia));
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(hojasPedidoProvider);
    final sesion = ref.watch(sesionProvider);
    final yo = sesion is ConSesion ? sesion.sesion : null;
    final puedeRegistrar = yo?.tiene([Permisos.ventas.registrarHp]) ?? false;
    final puedeConfirmar = yo?.tiene([Permisos.ventas.confirmarHp]) ?? false;
    final dia = _viewModel.dia;

    return Scaffold(
      appBar: AppBar(title: const Text('Hojas de pedido')),
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
              valor: _viewModel.estado == null ? 'Todos los estados' : _nombreDelEstado(_viewModel.estado!),
              alTocar: _elegirEstado,
            ),
            const SizedBox(height: 12),
            CampoDeEleccion(
              etiqueta: 'Fecha',
              valor: dia == null ? 'Cualquier fecha' : DateFormat('dd/MM/yyyy').format(dia),
              icono: Icons.calendar_today_outlined,
              alTocar: _elegirDia,
            ),
            if (dia != null)
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton(
                  onPressed: () => setState(() => _viewModel.filtrarPorDia(null)),
                  child: const Text('Quitar la fecha'),
                ),
              ),
            const SizedBox(height: 12),
            if (estado.items != null)
              Semantics(
                liveRegion: true,
                child: Text(
                  estado.total == 1 ? '1 hoja de pedido' : '${estado.total} hojas de pedido',
                  style: const TextStyle(color: textoSecundario),
                ),
              ),
            const SizedBox(height: 8),
            ...cuerpoDeLista(
              estado: estado,
              textoDeCarga: 'Cargando tus hojas de pedido…',
              alReintentar: _viewModel.cargar,
              alCargarMas: _viewModel.cargarMas,
              hayFiltros: _viewModel.hayFiltros,
              vacio: const Vacio(
                icono: Icons.receipt_long_outlined,
                titulo: 'Todavía no tienes hojas de pedido',
                texto: 'Aquí aparecen las ventas que registras.',
              ),
              sinCoincidencias: const Vacio(
                icono: Icons.search_off,
                titulo: 'Ninguna hoja de pedido coincide con la búsqueda.',
              ),
              item: (hoja) => _TarjetaDeHoja(
                hoja,
                // Un borrador suyo se confirma desde la lista: abre el detalle, que pide la confirmación.
                confirmable: puedeConfirmar && hoja.estado == EstadoHP.borrador && hoja.promotorId == yo?.id,
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: puedeRegistrar
          ? BarraDeAcciones(principal: Accion('Nueva hoja de pedido', () => context.push(Rutas.nuevaHoja)))
          : null,
    );
  }
}

String _nombreDelEstado(EstadoHP estado) => switch (estado) {
  EstadoHP.borrador => 'Borrador',
  EstadoHP.confirmada => 'Confirmada',
  EstadoHP.anulada => 'Anulada',
};

class _TarjetaDeHoja extends StatelessWidget {
  const _TarjetaDeHoja(this.hoja, {required this.confirmable});

  final HojaPedidoResumen hoja;
  final bool confirmable;

  @override
  Widget build(BuildContext context) {
    return TarjetaQueSeToca(
      alTocar: () => context.push(Rutas.hojaPedido(hoja.id)),
      abajo: confirmable
          ? OutlinedButton(
              onPressed: () => context.push(Rutas.hojaPedido(hoja.id), extra: const AbrirHoja(confirmar: true)),
              child: const Text('Confirmar venta'),
            )
          : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 12,
            runSpacing: 4,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(hoja.numero, style: const TextStyle(fontWeight: FontWeight.w600)),
              EtiquetaDeHoja(hoja.estado),
            ],
          ),
          Text(hoja.cliente.nombre),
          Text(
            '${formatearFecha(hoja.fechaRegistro, hora: true)} · ${tipoDeVenta(hoja.tipoVenta)}',
            style: const TextStyle(color: textoSecundario),
          ),
          Align(
            alignment: Alignment.centerRight,
            child: Text(formatearMonto(hoja.total), style: const TextStyle(fontWeight: FontWeight.w600)),
          ),
        ],
      ),
    );
  }
}
