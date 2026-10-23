import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/cliente.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../clientes/etiqueta_del_cliente.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/vacio.dart';
import '../sesion/sesion_notifier.dart';
import 'elegir_cliente_view_model.dart';

/// Elegir el cliente de una venta, un pedido o un evento (⇅): una pantalla de búsqueda con el campo enfocado. Busca en
/// su cartera, en la API, 300 ms después de la última tecla. Vuelve con el cliente elegido.
class ElegirClienteScreen extends ConsumerStatefulWidget {
  const ElegirClienteScreen({super.key, this.aceptaInactivos = false});

  /// Un evento acepta un cliente inactivo: puede explicar justo por qué quedó inactivo. Una venta o un pedido, no.
  final bool aceptaInactivos;

  @override
  ConsumerState<ElegirClienteScreen> createState() => _ElegirClienteScreenState();
}

class _ElegirClienteScreenState extends ConsumerState<ElegirClienteScreen> {
  final _busqueda = TextEditingController();
  Timer? _demora;

  @override
  void dispose() {
    _demora?.cancel();
    _busqueda.dispose();
    super.dispose();
  }

  void _buscar(String texto) {
    _demora?.cancel();
    _demora = Timer(const Duration(milliseconds: 300), () => ref.read(elegirClienteProvider.notifier).buscar(texto));
  }

  // Si el cliente no aparece, se registra aquí mismo y la venta sigue con él (7.3.5, ext. 2.2a).
  Future<void> _registrarNuevo() async {
    final nuevo = await context.push<ClienteOpcion>(Rutas.nuevoCliente, extra: true);
    if (nuevo != null && mounted) context.pop(nuevo);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(elegirClienteProvider);
    final sesion = ref.watch(sesionProvider);
    final puedeRegistrar = sesion is ConSesion && sesion.sesion.tiene([Permisos.ventas.registrarCliente]);
    final resultados = estado.resultados;

    return Scaffold(
      appBar: AppBar(title: const Text('Elegir cliente')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          CampoDeTexto(
            controller: _busqueda,
            etiqueta: 'Buscar cliente',
            ejemplo: 'Nombre, código o celular…',
            autofocus: true,
            textInputAction: TextInputAction.search,
            onChanged: _buscar,
          ),
          const SizedBox(height: 8),
          Semantics(
            liveRegion: true,
            child: Text(switch (estado) {
              BusquedaDeClientes(buscando: true) => 'Buscando…',
              BusquedaDeClientes(resultados: final r?) when estado.hayMas =>
                '${r.length} resultados. Hay más: escribe para afinar la búsqueda.',
              BusquedaDeClientes(resultados: final r?) => r.length == 1 ? '1 resultado' : '${r.length} resultados',
              _ => '',
            }, style: const TextStyle(color: textoSecundario)),
          ),
          if (estado.buscando) const LinearProgressIndicator(),
          const SizedBox(height: 8),
          if (estado.error != null) ...[
            Aviso.deError(estado.error!),
            const SizedBox(height: 12),
            BotonReintentar(alReintentar: ref.read(elegirClienteProvider.notifier).reintentar),
          ] else if (resultados != null && resultados.isEmpty && !estado.buscando)
            const Vacio(
              icono: Icons.search_off,
              titulo: 'Ningún cliente coincide con la búsqueda.',
              texto: 'Si es un cliente nuevo, regístralo.',
            )
          else
            for (final cliente in resultados ?? const <ClienteOpcion>[])
              _OpcionDeCliente(cliente, aceptaInactivo: widget.aceptaInactivos),
        ],
      ),
      bottomNavigationBar: puedeRegistrar
          ? BarraDeAcciones(principal: Accion('Registrar cliente nuevo', _registrarNuevo))
          : null,
    );
  }
}

class _OpcionDeCliente extends StatelessWidget {
  const _OpcionDeCliente(this.cliente, {required this.aceptaInactivo});

  final ClienteOpcion cliente;
  final bool aceptaInactivo;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      contentPadding: EdgeInsets.zero,
      minTileHeight: 56,
      enabled: !cliente.inactivo || aceptaInactivo,
      title: Text(cliente.nombreCompleto, style: const TextStyle(fontWeight: FontWeight.w600)),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('${cliente.codigo} · ${cliente.direccion}'),
          // Un cliente inactivo no recibe ventas ni pedidos (7.3.5, ext. 2.2c; 7.3.1, ext. 2.1c): sale deshabilitado,
          // con su estado.
          if (cliente.inactivo) ...[const SizedBox(height: 4), EtiquetaDelCliente(cliente.estado)],
        ],
      ),
      onTap: () => context.pop(cliente),
    );
  }
}
