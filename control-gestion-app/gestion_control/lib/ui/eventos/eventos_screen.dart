import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/evento.dart';
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
import 'etiqueta_de_evento.dart';
import 'eventos_view_model.dart';

/// Los eventos (4.1; `frmEventoMovil`): cada uno con su tipo, su fecha, su cliente o su pedido y su estado (7.3.13,
/// paso 1). Desde aquí se registra uno nuevo.
class EventosScreen extends ConsumerStatefulWidget {
  const EventosScreen({super.key});

  @override
  ConsumerState<EventosScreen> createState() => _EventosScreenState();
}

class _EventosScreenState extends ConsumerState<EventosScreen> {
  final _busqueda = TextEditingController();
  Timer? _demora;

  EventosViewModel get _viewModel => ref.read(eventosProvider.notifier);

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
      opciones: [(-1, 'Todos los estados'), for (final e in EstadoEvento.values) (e.index, _nombreDelEstado(e))],
      actual: _viewModel.estado?.index ?? -1,
    );
    if (elegido == null) return;
    setState(() => _viewModel.filtrarPorEstado(elegido < 0 ? null : EstadoEvento.values[elegido]));
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(eventosProvider);
    final sesion = ref.watch(sesionProvider);
    final puedeRegistrar = sesion is ConSesion && sesion.sesion.tiene([Permisos.eventos.registrarEvento]);

    return Scaffold(
      appBar: AppBar(title: const Text('Eventos')),
      body: RefreshIndicator(
        onRefresh: _viewModel.cargar,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: [
            CampoDeTexto(
              controller: _busqueda,
              etiqueta: 'Buscar',
              ejemplo: 'Número, cliente o qué pasó…',
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
            if (estado.items != null)
              Semantics(
                liveRegion: true,
                child: Text(
                  estado.total == 1 ? '1 evento' : '${estado.total} eventos',
                  style: const TextStyle(color: textoSecundario),
                ),
              ),
            const SizedBox(height: 8),
            ...cuerpoDeLista(
              estado: estado,
              textoDeCarga: 'Cargando los eventos…',
              alReintentar: _viewModel.cargar,
              alCargarMas: _viewModel.cargarMas,
              hayFiltros: _viewModel.hayFiltros,
              vacio: const Vacio(
                icono: Icons.report_outlined,
                titulo: 'Todavía no hay eventos',
                texto: 'Aquí aparecen los de tus clientes, los de tus pedidos y los que registras.',
              ),
              sinCoincidencias: const Vacio(icono: Icons.search_off, titulo: 'Ningún evento coincide con la búsqueda.'),
              item: (evento) => _TarjetaDeEvento(evento),
            ),
          ],
        ),
      ),
      bottomNavigationBar: puedeRegistrar
          ? BarraDeAcciones(principal: Accion('Nuevo evento', () => context.push(Rutas.nuevoEvento)))
          : null,
    );
  }
}

String _nombreDelEstado(EstadoEvento estado) => switch (estado) {
  EstadoEvento.pendiente => 'Pendientes',
  EstadoEvento.resuelto => 'Resueltos',
};

String _fotos(int cantidad) => switch (cantidad) {
  0 => 'Sin fotos',
  1 => '1 foto',
  _ => '$cantidad fotos',
};

/// Un evento en la lista: su número y su estado, su tipo, su cliente o su pedido, su fecha y cuántas fotos tiene.
class _TarjetaDeEvento extends StatelessWidget {
  const _TarjetaDeEvento(this.evento);

  final EventoResumen evento;

  @override
  Widget build(BuildContext context) {
    final aQuien = [?evento.cliente?.nombre, ?evento.pedido?.numero].join(' · ');
    return TarjetaQueSeToca(
      alTocar: () => context.push(Rutas.evento(evento.id)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 12,
            runSpacing: 4,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(evento.numero, style: const TextStyle(fontWeight: FontWeight.w600)),
              EtiquetaDeEvento(evento.estado),
            ],
          ),
          Text(evento.tipo.nombre),
          if (aQuien.isNotEmpty) Text(aQuien),
          Text(
            '${formatearFecha(evento.fecha, hora: true)} · ${_fotos(evento.fotos)}',
            style: const TextStyle(color: textoSecundario),
          ),
        ],
      ),
    );
  }
}
