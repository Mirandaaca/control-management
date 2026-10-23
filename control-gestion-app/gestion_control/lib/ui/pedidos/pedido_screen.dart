import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/hoja_pedido.dart';
import '../../data/models/pedido.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/lineas/linea_de_producto.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../sesion/sesion_notifier.dart';
import 'etiqueta_de_pedido.dart';
import 'pedido_view_model.dart';

/// Cómo se abre el detalle de un pedido: con el aviso de lo que pasó.
class AbrirPedido {
  const AbrirPedido({this.mensaje});

  final String? mensaje;
}

/// Un pedido (`frmPedidoMovil` y `frmBandejaPedidosMovil`): el cliente, la dirección, los productos, el monto y la
/// fecha y hora de entrega (7.3.3, paso 4), la entrega si ya se hizo, y su historial (7.3.2, paso 4). Abierto desde la
/// bandeja, ofrece registrar la entrega (7.3.2, paso 3: "selecciona un pedido de su bandeja").
class PedidoScreen extends ConsumerStatefulWidget {
  const PedidoScreen({super.key, required this.id, this.abrir, this.desdeLaBandeja = false});

  final int id;
  final AbrirPedido? abrir;
  final bool desdeLaBandeja;

  @override
  ConsumerState<PedidoScreen> createState() => _PedidoScreenState();
}

class _PedidoScreenState extends ConsumerState<PedidoScreen> {
  PedidoViewModel get _viewModel => ref.read(pedidoProvider(widget.id).notifier);

  @override
  void initState() {
    super.initState();
    final mensaje = widget.abrir?.mensaje;
    if (mensaje != null) Future.microtask(() => _viewModel.avisar(mensaje));
  }

  // Solo el promotor asignado entrega, y solo un pedido en ruta (7.3.2, ext. 3.1b y 3.1c): la API lo revisa igual.
  bool _puedeEntregar(Pedido pedido) {
    final sesion = ref.read(sesionProvider);
    return widget.desdeLaBandeja &&
        pedido.estado.id == EstadosDePedido.enRuta &&
        sesion is ConSesion &&
        pedido.repartidor?.id == sesion.sesion.id &&
        sesion.sesion.tiene([Permisos.pedidos.registrarEntrega]);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(pedidoProvider(widget.id));
    final pedido = estado.pedido;

    return Scaffold(
      appBar: AppBar(title: Text(pedido?.numero ?? 'Pedido')),
      body: switch (estado) {
        DetalleDePedido(pedido: final pedido?) => RefreshIndicator(
          onRefresh: _viewModel.cargar,
          child: _Contenido(pedido: pedido, mensaje: estado.mensaje, desdeLaBandeja: widget.desdeLaBandeja),
        ),
        DetalleDePedido(error: final error?) => ErrorConReintento.de(
          error,
          reintentando: estado.reintentando,
          alReintentar: _viewModel.cargar,
        ),
        _ => const Cargando('Cargando el pedido…'),
      },
      bottomNavigationBar: pedido != null && _puedeEntregar(pedido)
          ? BarraDeAcciones(principal: Accion('Registrar entrega', () => context.push(Rutas.entrega(pedido.id))))
          : null,
    );
  }
}

class _Contenido extends ConsumerWidget {
  const _Contenido({required this.pedido, this.mensaje, this.desdeLaBandeja = false});

  final Pedido pedido;
  final String? mensaje;
  final bool desdeLaBandeja;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sesion = ref.watch(sesionProvider);
    final puedeVerLaVenta = sesion is ConSesion && sesion.sesion.tiene(PermisosDe.hojasPedido);
    final venta = pedido.venta;
    final titulo = Theme.of(context).textTheme.titleMedium;

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
      children: [
        if (mensaje != null) ...[Aviso(mensaje!, tipo: TipoDeAviso.exito), const SizedBox(height: 16)],
        if (desdeLaBandeja &&
            (pedido.estado.id == EstadosDePedido.pendiente || pedido.estado.id == EstadosDePedido.enProceso)) ...[
          const Aviso('Se entrega cuando salga a ruta.', tipo: TipoDeAviso.informacion),
          const SizedBox(height: 16),
        ],
        Wrap(
          spacing: 12,
          runSpacing: 4,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Semantics(header: true, container: true, child: Text(pedido.numero, style: titulo)),
            EtiquetaDePedido(pedido.estado),
          ],
        ),
        const SizedBox(height: 12),
        _Dato('Cliente', pedido.cliente.nombre),
        _Dato('Dirección de entrega', pedido.direccionEntrega),
        _Dato('Entrega', formatearFecha(pedido.fechaEntrega, hora: true)),
        _Dato('Asignado a', pedido.repartidor?.nombre ?? 'Sin asignar'),
        _Dato('Registrado', '${formatearFecha(pedido.fechaRegistro, hora: true)} por ${pedido.registradoPor}'),
        if (pedido.observaciones != null) _Dato('Observaciones', pedido.observaciones!),
        if (pedido.estado.id == EstadosDePedido.entregado) ...[
          const SizedBox(height: 8),
          Semantics(header: true, container: true, child: Text('Entrega', style: titulo)),
          const SizedBox(height: 8),
          _Dato('Entregado', formatearFecha(pedido.fechaEntregaReal, hora: true)),
          if (pedido.recibidoPor != null) _Dato('Recibió', pedido.recibidoPor!),
          if (venta != null) ...[
            _Dato('Venta', '${venta.numero}, ${venta.tipoVenta == TipoVenta.credito ? 'a crédito' : 'al contado'}'),
            if (puedeVerLaVenta)
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton(
                  onPressed: () => context.push(Rutas.hojaPedido(venta.id)),
                  child: const Text('Ver la venta'),
                ),
              ),
          ],
        ],
        const SizedBox(height: 8),
        Semantics(header: true, container: true, child: Text('Productos', style: titulo)),
        const SizedBox(height: 8),
        for (final linea in pedido.lineas) ...[LineaDelPedido(linea), const SizedBox(height: 8)],
        const SizedBox(height: 8),
        TotalesDelPedido(pedido),
        const SizedBox(height: 20),
        Semantics(header: true, container: true, child: Text('Historial', style: titulo)),
        const SizedBox(height: 8),
        for (final cambio in pedido.historial) _CambioDelHistorial(cambio),
      ],
    );
  }
}

/// Un producto del pedido: la cantidad por el precio, menos el descuento por unidad, y su subtotal. En la entrega,
/// [error] dice que no alcanza el stock (7.3.2, ext. 3.2a).
class LineaDelPedido extends StatelessWidget {
  const LineaDelPedido(this.linea, {super.key, this.error});

  final LineaDePedido linea;
  final String? error;

  @override
  Widget build(BuildContext context) {
    final descuento = linea.descuento > 0 ? ' − ${formatearMonto(linea.descuento)} c/u' : '';
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: MergeSemantics(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(linea.descripcion, style: const TextStyle(fontWeight: FontWeight.w600)),
              Text(
                '${formatearCantidad(linea.cantidad)} × ${formatearMonto(linea.precioUnitario)}$descuento',
                style: const TextStyle(color: textoSecundario),
              ),
              Align(
                alignment: Alignment.centerRight,
                child: Text(formatearMonto(linea.subtotal), style: const TextStyle(fontWeight: FontWeight.w600)),
              ),
              if (error != null) ...[const SizedBox(height: 4), ErrorDeCampo(error!)],
            ],
          ),
        ),
      ),
    );
  }
}

/// El subtotal, el descuento sobre el total y el monto del pedido.
class TotalesDelPedido extends StatelessWidget {
  const TotalesDelPedido(this.pedido, {super.key});

  final Pedido pedido;

  @override
  Widget build(BuildContext context) =>
      ResumenDeTotales(subtotal: pedido.subTotal, descuento: pedido.descuento, total: pedido.total);
}

/// Un cambio de estado (7.3.2, paso 4.2): su estado, su fecha, quién lo hizo y su observación.
class _CambioDelHistorial extends StatelessWidget {
  const _CambioDelHistorial(this.cambio);

  final CambioDelPedido cambio;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: MergeSemantics(
        child: Container(
          padding: const EdgeInsets.only(left: 12),
          decoration: const BoxDecoration(
            border: Border(left: BorderSide(color: Color(0xFFE2E8F0), width: 2)),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              EtiquetaDePedido(cambio.estado),
              const SizedBox(height: 2),
              Text(
                '${formatearFecha(cambio.fecha, hora: true)} · ${cambio.usuario}',
                style: const TextStyle(color: textoSecundario),
              ),
              if (cambio.observacion != null) Text(cambio.observacion!),
            ],
          ),
        ),
      ),
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
