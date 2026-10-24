import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cliente.dart';
import '../core/formato.dart';
import '../core/lista_paginada.dart';
import '../core/tema.dart';
import '../core/ubicacion/mapa_de_ubicacion.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/selectores.dart';
import '../core/widgets/tarjeta_que_se_toca.dart';
import '../core/widgets/vacio.dart';
import 'por_visitar_view_model.dart';
import 'visitas.dart';

/// Los clientes por visitar (5.3; `frmRevisitaMovil`; sin diseño en el 8.6, sigue el panel): la fecha del día, los
/// filtros y los clientes cuya próxima visita ya llegó, del más atrasado al más reciente (7.3.19, pasos 1 y 2). Al tocar
/// uno, su ubicación y sus visitas (paso 3).
class PorVisitarScreen extends ConsumerStatefulWidget {
  const PorVisitarScreen({super.key});

  @override
  ConsumerState<PorVisitarScreen> createState() => _PorVisitarScreenState();
}

class _PorVisitarScreenState extends ConsumerState<PorVisitarScreen> {
  final _busqueda = TextEditingController();
  Timer? _demora;

  PorVisitarViewModel get _viewModel => ref.read(porVisitarProvider.notifier);

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

  // 0 (o '') es "todas": null es que se cerró la lista sin elegir.
  Future<void> _elegirZona() async {
    final elegida = await elegirDeLaLista<int>(
      context,
      titulo: 'Zona',
      opciones: [(0, 'Todas las zonas'), for (final z in _viewModel.zonas) (z.id, z.nombre)],
      actual: _viewModel.zona?.id ?? 0,
    );
    if (elegida == null) return;
    setState(() => _viewModel.filtrarPorZona(_viewModel.zonas.where((z) => z.id == elegida).firstOrNull));
  }

  Future<void> _elegirPromotor() async {
    final elegido = await elegirDeLaLista<String>(
      context,
      titulo: 'Promotor',
      opciones: [('', 'Todos los promotores'), for (final p in _viewModel.promotores) (p.id, p.nombre)],
      actual: _viewModel.promotor?.id ?? '',
    );
    if (elegido == null) return;
    setState(() => _viewModel.filtrarPorPromotor(_viewModel.promotores.where((p) => p.id == elegido).firstOrNull));
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(porVisitarProvider);
    final veTodos = _viewModel.veTodos;

    return Scaffold(
      appBar: AppBar(title: const Text('Clientes por visitar')),
      body: RefreshIndicator(
        onRefresh: _viewModel.cargar,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: [
            Text(
              'Hoy, ${formatearMomento(DateTime.now().toUtc())}: los clientes cuya próxima visita ya llegó, del más '
              'atrasado al más reciente.',
              style: const TextStyle(color: textoSecundario),
            ),
            const SizedBox(height: 16),
            CampoDeTexto(
              controller: _busqueda,
              etiqueta: 'Buscar',
              ejemplo: 'Nombre, código o celular…',
              textInputAction: TextInputAction.search,
              onChanged: _buscar,
            ),
            const SizedBox(height: 12),
            CampoDeEleccion(
              etiqueta: 'Zona',
              valor: _viewModel.zona?.nombre ?? 'Todas las zonas',
              alTocar: _elegirZona,
            ),
            if (_viewModel.eligePromotor) ...[
              const SizedBox(height: 12),
              CampoDeEleccion(
                etiqueta: 'Promotor',
                valor: _viewModel.promotor?.nombre ?? 'Todos los promotores',
                alTocar: _elegirPromotor,
              ),
            ],
            const SizedBox(height: 12),
            if (estado.items != null)
              Semantics(
                liveRegion: true,
                child: Text(
                  estado.total == 1 ? '1 cliente' : '${estado.total} clientes',
                  style: const TextStyle(color: textoSecundario),
                ),
              ),
            const SizedBox(height: 8),
            ...cuerpoDeLista(
              estado: estado,
              textoDeCarga: 'Cargando los clientes por visitar…',
              alReintentar: _viewModel.cargar,
              alCargarMas: _viewModel.cargarMas,
              hayFiltros: _viewModel.hayFiltros,
              // 7.3.19, ext. 1.4a, con los textos del panel.
              vacio: const Vacio(
                icono: Icons.event_available_outlined,
                titulo: 'Ningún cliente debe ser visitado todavía.',
                texto: 'Aquí aparecen los clientes cuando llega el día de su próxima visita.',
              ),
              sinCoincidencias: const Vacio(
                icono: Icons.search_off,
                titulo: 'Ningún cliente por visitar coincide con la búsqueda.',
              ),
              item: (cliente) =>
                  _TarjetaPorVisitar(cliente, conPromotor: veTodos, alTocar: () => _verCliente(context, cliente)),
            ),
          ],
        ),
      ),
    );
  }
}

/// Un cliente por visitar: nombre y código, zona, última visita y atraso.
class _TarjetaPorVisitar extends StatelessWidget {
  const _TarjetaPorVisitar(this.cliente, {required this.conPromotor, required this.alTocar});

  final ClientePorVisitar cliente;
  final bool conPromotor;
  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context) {
    final atraso = textoDelAtraso(cliente.diasDeAtraso);
    return TarjetaQueSeToca(
      alTocar: alTocar,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(cliente.nombreCompleto, style: const TextStyle(fontWeight: FontWeight.w600)),
          Text(cliente.codigo, style: const TextStyle(color: textoSecundario)),
          Text(
            [cliente.zona.nombre, if (conPromotor && cliente.promotor != null) cliente.promotor!].join(' · '),
            style: const TextStyle(color: textoSecundario),
          ),
          const SizedBox(height: 4),
          Text(cliente.ultimaVisita == null ? 'Sin visitas' : 'Última visita ${formatearFecha(cliente.ultimaVisita)}'),
          if (atraso != null) Text('Atraso: $atraso', style: const TextStyle(fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}

/// 7.3.19, paso 3: su ubicación en el mapa, su última visita y su próxima visita, como el modal del panel.
Future<void> _verCliente(BuildContext context, ClientePorVisitar cliente) => showModalBottomSheet<void>(
  context: context,
  isScrollControlled: true,
  useSafeArea: true,
  showDragHandle: true,
  backgroundColor: Colors.white,
  builder: (contexto) {
    final falta = cuantoFalta(cliente.proximaVisita);
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Semantics(
            header: true,
            container: true,
            child: Text(cliente.nombreCompleto, style: Theme.of(contexto).textTheme.titleLarge),
          ),
          Text('${cliente.codigo} · ${cliente.zona.nombre}', style: const TextStyle(color: textoSecundario)),
          Text(cliente.direccion, style: const TextStyle(color: textoSecundario)),
          const SizedBox(height: 12),
          MapaDeUbicacion(latitud: cliente.latitud, longitud: cliente.longitud),
          const SizedBox(height: 12),
          const Text(
            'Última visita',
            style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: textoSecundario),
          ),
          Text(cliente.ultimaVisita == null ? 'Nunca' : formatearFecha(cliente.ultimaVisita, hora: true)),
          const SizedBox(height: 8),
          const Text(
            'Próxima visita',
            style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: textoSecundario),
          ),
          Text(
            cliente.proximaVisita == null
                ? 'Sin visitas: está disponible para re-visita.'
                : '${formatearFecha(cliente.proximaVisita)} · $falta',
          ),
          const SizedBox(height: 16),
          OutlinedButton(onPressed: () => Navigator.of(contexto).pop(), child: const Text('Cerrar')),
        ],
      ),
    );
  },
);
