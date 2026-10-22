import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/repositories/cobranzas_repository.dart';
import '../../data/services/error_de_api.dart';
import '../core/formato.dart';
import '../core/lista_paginada.dart';
import '../core/tema.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/segmentado.dart';
import '../core/widgets/vacio.dart';
import 'cobranza_ui.dart';
import 'cobranzas_view_model.dart';

/// Las cobranzas de sus clientes (3.2): lo que hay por cobrar, las vencidas con su mora y todas.
class CobranzasScreen extends ConsumerStatefulWidget {
  const CobranzasScreen({super.key, this.clienteId, this.clienteNombre});

  /// Abierta desde un cliente, solo las de él.
  final int? clienteId;
  final String? clienteNombre;

  @override
  ConsumerState<CobranzasScreen> createState() => _CobranzasScreenState();
}

class _CobranzasScreenState extends ConsumerState<CobranzasScreen> {
  final _busqueda = TextEditingController();
  Timer? _demora;

  CobranzasViewModel get _viewModel => ref.read(cobranzasProvider(widget.clienteId).notifier);

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

  Future<void> _recargar() async {
    ref.invalidate(resumenDeCobranzasProvider(widget.clienteId));
    await _viewModel.cargar();
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(cobranzasProvider(widget.clienteId));
    final resumen = ref.watch(resumenDeCobranzasProvider(widget.clienteId));
    final filtro = _viewModel.filtro;

    return Scaffold(
      appBar: AppBar(title: const Text('Cobranzas')),
      body: RefreshIndicator(
        onRefresh: _recargar,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: [
            if (widget.clienteNombre != null) ...[
              Semantics(
                header: true,
                container: true,
                child: Text('De ${widget.clienteNombre}', style: Theme.of(context).textTheme.titleMedium),
              ),
              const SizedBox(height: 12),
            ],
            switch (resumen) {
              AsyncData(:final value) => _Resumen(porCobrar: value.porCobrar, vencidas: value.vencidas),
              AsyncError(:final error) => Text(
                ErrorDeApi.desde(error).mensaje,
                style: const TextStyle(color: textoSecundario),
              ),
              _ => const SizedBox.shrink(),
            },
            const SizedBox(height: 16),
            Segmentado(
              opciones: const [
                (FiltroDeCobranzas.porCobrar, 'Por cobrar'),
                (FiltroDeCobranzas.vencidas, 'Vencidas'),
                (FiltroDeCobranzas.todas, 'Todas'),
              ],
              valor: filtro,
              alCambiar: (f) => setState(() => _viewModel.filtrar(f)),
            ),
            const SizedBox(height: 12),
            CampoDeTexto(
              controller: _busqueda,
              etiqueta: 'Buscar',
              ejemplo: widget.clienteId == null ? 'Número o cliente…' : 'Número…',
              textInputAction: TextInputAction.search,
              onChanged: _buscar,
            ),
            const SizedBox(height: 12),
            ...cuerpoDeLista(
              estado: estado,
              textoDeCarga: 'Cargando las cobranzas…',
              alReintentar: _viewModel.cargar,
              alCargarMas: _viewModel.cargarMas,
              hayFiltros: _viewModel.hayBusqueda,
              // Sin deudas, o sin vencidas, un mensaje (7.3.11, ext. 2.1a y 3a).
              vacio: switch (filtro) {
                FiltroDeCobranzas.porCobrar => const Vacio(
                  icono: Icons.task_alt,
                  titulo: 'No hay deudas por cobrar',
                  texto: 'Aquí aparecen las cobranzas de tus clientes que todavía tienen saldo.',
                ),
                FiltroDeCobranzas.vencidas => const Vacio(
                  icono: Icons.event_available,
                  titulo: 'No hay deudas vencidas',
                  texto: 'Ninguna cobranza de tus clientes pasó su vencimiento.',
                ),
                FiltroDeCobranzas.todas => const Vacio(
                  icono: Icons.request_quote_outlined,
                  titulo: 'Todavía no hay cobranzas',
                  texto: 'Aquí aparecen las deudas de las ventas a crédito de tus clientes.',
                ),
              },
              sinCoincidencias: const Vacio(
                icono: Icons.search_off,
                titulo: 'Ninguna cobranza coincide con la búsqueda.',
              ),
              item: (cobranza) => TarjetaDeCobranza(cobranza, conCliente: widget.clienteId == null),
            ),
          ],
        ),
      ),
    );
  }
}

class _Resumen extends StatelessWidget {
  const _Resumen({required this.porCobrar, required this.vencidas});

  final double porCobrar;
  final int vencidas;

  @override
  Widget build(BuildContext context) {
    final estilo = Theme.of(context).textTheme.titleMedium;
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Wrap(
          spacing: 32,
          runSpacing: 12,
          children: [
            MergeSemantics(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Por cobrar', style: TextStyle(color: textoSecundario)),
                  Text(formatearMonto(porCobrar), style: estilo),
                ],
              ),
            ),
            MergeSemantics(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Vencidas', style: TextStyle(color: textoSecundario)),
                  Text('$vencidas', style: estilo),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
