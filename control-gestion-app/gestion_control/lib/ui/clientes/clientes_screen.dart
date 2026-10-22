import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/cliente.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/lista_paginada.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/vacio.dart';
import '../sesion/sesion_notifier.dart';
import 'clientes_view_model.dart';
import 'etiqueta_del_cliente.dart';

/// Los clientes de la cartera (2.2): buscarlos, abrir uno y registrar uno nuevo.
class ClientesScreen extends ConsumerStatefulWidget {
  const ClientesScreen({super.key});

  @override
  ConsumerState<ClientesScreen> createState() => _ClientesScreenState();
}

class _ClientesScreenState extends ConsumerState<ClientesScreen> {
  final _busqueda = TextEditingController();
  Timer? _demora;

  @override
  void dispose() {
    _demora?.cancel();
    _busqueda.dispose();
    super.dispose();
  }

  // Como el buscador del panel: espera 300 ms sin teclear antes de pedir.
  void _buscar(String texto) {
    _demora?.cancel();
    _demora = Timer(const Duration(milliseconds: 300), () => ref.read(clientesProvider.notifier).buscar(texto));
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(clientesProvider);
    final viewModel = ref.read(clientesProvider.notifier);
    final sesion = ref.watch(sesionProvider);
    final puedeRegistrar = sesion is ConSesion && sesion.sesion.tiene([Permisos.ventas.registrarCliente]);

    return Scaffold(
      appBar: AppBar(title: const Text('Clientes')),
      body: RefreshIndicator(
        onRefresh: viewModel.cargar,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: [
            CampoDeTexto(
              controller: _busqueda,
              etiqueta: 'Buscar',
              ejemplo: 'Nombre, código o celular…',
              textInputAction: TextInputAction.search,
              onChanged: _buscar,
            ),
            const SizedBox(height: 12),
            if (estado.items != null)
              Semantics(
                liveRegion: true,
                child: Text(_clientes(estado.total), style: const TextStyle(color: textoSecundario)),
              ),
            const SizedBox(height: 8),
            ...cuerpoDeLista(
              estado: estado,
              textoDeCarga: 'Cargando los clientes…',
              alReintentar: viewModel.cargar,
              alCargarMas: viewModel.cargarMas,
              hayFiltros: _busqueda.text.trim().isNotEmpty,
              vacio: const Vacio(
                icono: Icons.storefront_outlined,
                titulo: 'Todavía no tienes clientes',
                texto: 'Aquí aparecen los clientes de tus zonas.',
              ),
              sinCoincidencias: const Vacio(
                icono: Icons.search_off,
                titulo: 'Ningún cliente coincide con la búsqueda.',
              ),
              item: (cliente) => TarjetaDeCliente(cliente, alTocar: () => context.push(Rutas.cliente(cliente.id))),
            ),
          ],
        ),
      ),
      bottomNavigationBar: puedeRegistrar
          ? BarraDeAcciones(principal: Accion('Nuevo cliente', () => context.push(Rutas.nuevoCliente)))
          : null,
    );
  }
}

String _clientes(int cantidad) => cantidad == 1 ? '1 cliente' : '$cantidad clientes';

/// Un cliente en una lista: su nombre, su código y su zona, su dirección, su estado y lo que debe.
class TarjetaDeCliente extends StatelessWidget {
  const TarjetaDeCliente(this.cliente, {super.key, required this.alTocar});

  final ClienteResumen cliente;
  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context) {
    // MergeSemantics por fuera del InkWell: así el toque y los textos son un solo elemento, con su etiqueta.
    return MergeSemantics(
      child: Card(
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: alTocar,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(cliente.nombreCompleto, style: const TextStyle(fontWeight: FontWeight.w600)),
                Text('${cliente.codigo} · ${cliente.zona.nombre}', style: const TextStyle(color: textoSecundario)),
                Text(cliente.direccion, style: const TextStyle(color: textoSecundario)),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 12,
                  runSpacing: 4,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    EtiquetaDelCliente(cliente.estado),
                    if (cliente.saldoPendiente > 0) Text('Debe ${formatearMonto(cliente.saldoPendiente)}'),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
