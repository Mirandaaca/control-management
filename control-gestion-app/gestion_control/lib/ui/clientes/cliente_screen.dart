import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/cliente.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/ubicacion/mapa_de_ubicacion.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../cobranzas/cobranza_ui.dart';
import '../cobranzas/cobranzas_view_model.dart';
import '../../data/services/error_de_api.dart';
import '../sesion/sesion_notifier.dart';
import 'cliente_view_model.dart';
import 'etiqueta_del_cliente.dart';

/// Un cliente (2.2 y 2.4): sus datos, su ubicación en el mapa, su estado y lo que debe.
class ClienteScreen extends ConsumerStatefulWidget {
  const ClienteScreen({super.key, required this.id, this.mensaje});

  final int id;

  /// El aviso de éxito con que llega desde el formulario ("Se registró a …").
  final String? mensaje;

  @override
  ConsumerState<ClienteScreen> createState() => _ClienteScreenState();
}

class _ClienteScreenState extends ConsumerState<ClienteScreen> {
  late String? _mensaje = widget.mensaje;

  // Al volver de editarlo, avisa lo que se guardó y lo trae de nuevo.
  Future<void> _editar({bool capturar = false}) async {
    final mensaje = await context.push<String>(Rutas.editarCliente(widget.id), extra: capturar);
    if (!mounted || mensaje == null) return;
    setState(() => _mensaje = mensaje);
    ref.read(clienteProvider(widget.id).notifier).cargar();
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(clienteProvider(widget.id));
    final sesion = ref.watch(sesionProvider);
    final puedeEditar = sesion is ConSesion && sesion.sesion.tiene([Permisos.ventas.editarCliente]);
    final puedeVender = sesion is ConSesion && sesion.sesion.tiene([Permisos.ventas.registrarHp]);
    final veCobranzas = sesion is ConSesion && sesion.sesion.tiene(PermisosDe.cobranzas);
    final cliente = estado.cliente;

    return Scaffold(
      appBar: AppBar(title: const Text('Cliente')),
      body: switch (estado) {
        DetalleDeCliente(cliente: final cliente?) => ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: [
            if (_mensaje != null) ...[Aviso(_mensaje!, tipo: TipoDeAviso.exito), const SizedBox(height: 16)],
            _Encabezado(cliente),
            const SizedBox(height: 16),
            if (cliente.tieneUbicacion)
              MapaDeUbicacion(latitud: cliente.latitud, longitud: cliente.longitud)
            else
              Aviso(
                'Captúrala cuando estés en su negocio: sin ubicación, el cliente no aparece bien en el mapa.',
                tipo: TipoDeAviso.aviso,
                icono: Icons.location_off_outlined,
                titulo: 'Este cliente no tiene ubicación.',
                accion: puedeEditar ? AccionDeAviso('Capturar ubicación', () => _editar(capturar: true)) : null,
              ),
            const SizedBox(height: 16),
            _Dato('Celular', cliente.telefono),
            _Dato('Dirección', cliente.direccion),
            _Dato('Zona', cliente.zona.nombre),
            if (cliente.razonSocial != null) _Dato('Razón social', cliente.razonSocial!),
            if (cliente.nit != null) _Dato('NIT', cliente.nit!),
            if (cliente.email != null) _Dato('Correo', cliente.email!),
            _Dato('Última visita', formatearFecha(cliente.ultimaVisita)),
            _Dato('Próxima visita', formatearFecha(cliente.proximaVisita)),
            if (veCobranzas) _PorCobrar(cliente),
          ],
        ),
        DetalleDeCliente(error: final error?) => ErrorConReintento.de(
          error,
          reintentando: estado.reintentando,
          alReintentar: ref.read(clienteProvider(widget.id).notifier).cargar,
        ),
        _ => const Cargando('Cargando el cliente…'),
      },
      // Una venta abierta desde el cliente lo trae elegido.
      bottomNavigationBar: switch ((cliente, puedeVender, puedeEditar)) {
        (final c?, true, true) => BarraDeAcciones(
          secundaria: Accion('Editar', _editar),
          principal: Accion('Nueva hoja de pedido', () => context.push(Rutas.nuevaHoja, extra: c.comoOpcion)),
        ),
        (final c?, true, false) => BarraDeAcciones(
          principal: Accion('Nueva hoja de pedido', () => context.push(Rutas.nuevaHoja, extra: c.comoOpcion)),
        ),
        (_?, false, true) => BarraDeAcciones(principal: Accion('Editar', _editar)),
        _ => null,
      },
    );
  }
}

class _Encabezado extends StatelessWidget {
  const _Encabezado(this.cliente);

  final Cliente cliente;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Semantics(
          header: true,
          container: true,
          child: Text(cliente.nombreCompleto, style: Theme.of(context).textTheme.titleMedium),
        ),
        Text(cliente.codigo, style: const TextStyle(color: textoSecundario)),
        const SizedBox(height: 8),
        Wrap(
          spacing: 12,
          runSpacing: 4,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            EtiquetaDelCliente(cliente.estado),
            Text(
              cliente.saldoPendiente > 0 ? 'Debe ${formatearMonto(cliente.saldoPendiente)}' : 'No tiene deudas',
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ],
        ),
      ],
    );
  }
}

class _Dato extends StatelessWidget {
  const _Dato(this.titulo, this.valor);

  final String titulo;
  final String valor;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: MergeSemantics(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(titulo, style: Theme.of(context).textTheme.labelMedium?.copyWith(color: const Color(0xFF334155))),
            Text(valor),
          ],
        ),
      ),
    );
  }
}

/// Sus cobranzas por cobrar (3.2), y el camino a todas.
class _PorCobrar extends ConsumerWidget {
  const _PorCobrar(this.cliente);

  final Cliente cliente;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cobranzas = ref.watch(porCobrarDelClienteProvider(cliente.id));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SizedBox(height: 8),
        Semantics(
          header: true,
          container: true,
          child: Text('Por cobrar', style: Theme.of(context).textTheme.titleMedium),
        ),
        const SizedBox(height: 8),
        ...switch (cobranzas) {
          AsyncData(:final value) when value.isEmpty => [
            const Text('No tiene deudas por cobrar.', style: TextStyle(color: textoSecundario)),
          ],
          AsyncData(:final value) => [
            for (final cobranza in value) ...[
              TarjetaDeCobranza(cobranza, conCliente: false),
              const SizedBox(height: 8),
            ],
          ],
          AsyncError(:final error) => [
            Aviso.deError(
              ErrorDeApi.desde(error),
              accion: AccionDeAviso('Reintentar', () => ref.invalidate(porCobrarDelClienteProvider(cliente.id))),
            ),
          ],
          _ => [const LinearProgressIndicator()],
        },
        Align(
          alignment: Alignment.centerLeft,
          child: TextButton(
            onPressed: () => context.push(Rutas.cobranzasDe(cliente.id), extra: cliente.nombreCompleto),
            child: const Text('Ver todas sus cobranzas'),
          ),
        ),
      ],
    );
  }
}
