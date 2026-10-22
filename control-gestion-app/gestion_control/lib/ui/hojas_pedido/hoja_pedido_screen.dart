import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/hoja_pedido.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/tema.dart';
import '../core/ubicacion/mapa_de_ubicacion.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../sesion/sesion_notifier.dart';
import 'hoja_pedido_view_model.dart';
import 'nueva_hoja_screen.dart';
import 'venta.dart';

/// Una hoja de pedido (2.1; 7.3.5, paso 5): el cliente, la fecha, el tipo de venta, la ubicación en el mapa, los
/// productos (2.3), el total y su cobranza. Un borrador suyo se confirma aquí.
class HojaPedidoScreen extends ConsumerStatefulWidget {
  const HojaPedidoScreen({super.key, required this.id, this.abrir});

  final int id;
  final AbrirHoja? abrir;

  @override
  ConsumerState<HojaPedidoScreen> createState() => _HojaPedidoScreenState();
}

class _HojaPedidoScreenState extends ConsumerState<HojaPedidoScreen> {
  HojaPedidoViewModel get _viewModel => ref.read(hojaPedidoProvider(widget.id).notifier);

  @override
  void initState() {
    super.initState();
    final mensaje = widget.abrir?.mensaje;
    if (mensaje != null) Future.microtask(() => _viewModel.avisar(mensaje));
  }

  bool _puedeConfirmar(HojaPedido hoja) {
    final sesion = ref.read(sesionProvider);
    // Solo el promotor que la registró la confirma: es la regla del dueño, no un permiso (la API la revisa igual).
    return hoja.estado == EstadoHP.borrador &&
        sesion is ConSesion &&
        sesion.sesion.id == hoja.promotorId &&
        sesion.sesion.tiene([Permisos.ventas.confirmarHp]);
  }

  Future<void> _confirmar(HojaPedido hoja) async {
    final confirmado = await confirmarVenta(
      context,
      cliente: hoja.cliente.nombre,
      productos: hoja.lineas.length,
      unidades: hoja.lineas.fold(0, (suma, l) => suma + l.cantidad),
      tipo: hoja.tipoVenta,
      total: hoja.total,
    );
    if (!confirmado) return;
    final confirmada = await _viewModel.confirmar();
    if (confirmada != null) _viewModel.avisar(mensajeDeVenta(confirmada));
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(hojaPedidoProvider(widget.id));
    // Desde la lista, "Confirmar venta" abre el detalle y pide la confirmación apenas llega la HP.
    ref.listen(hojaPedidoProvider(widget.id).select((e) => e.hoja), (anterior, hoja) {
      if (anterior == null && hoja != null && widget.abrir?.confirmar == true && _puedeConfirmar(hoja)) {
        _confirmar(hoja);
      }
    });
    final hoja = estado.hoja;

    return Scaffold(
      appBar: AppBar(title: Text(hoja?.numero ?? 'Hoja de pedido')),
      body: switch (estado) {
        DetalleDeHoja(hoja: final hoja?) => _Contenido(hoja: hoja, estado: estado),
        DetalleDeHoja(error: final error?) => ErrorConReintento.de(
          error,
          reintentando: estado.reintentando,
          alReintentar: _viewModel.cargar,
        ),
        _ => const Cargando('Cargando la hoja de pedido…'),
      },
      bottomNavigationBar: hoja != null && _puedeConfirmar(hoja)
          ? BarraDeAcciones(
              arriba: [
                if (estado.campos.isNotEmpty)
                  const Aviso(
                    'Para cambiar las cantidades, pide a la secretaria que anule esta hoja y registra otra.',
                    tipo: TipoDeAviso.aviso,
                    titulo: 'No alcanza el stock para confirmarla.',
                  )
                else if (estado.errorAlConfirmar != null)
                  Aviso.deError(estado.errorAlConfirmar!, accion: AccionDeAviso('Reintentar', () => _confirmar(hoja))),
              ],
              principal: Accion(
                'Confirmar venta',
                () => _confirmar(hoja),
                enviando: estado.confirmando,
                textoEnviando: 'Confirmando…',
              ),
            )
          : null,
    );
  }
}

class _Contenido extends StatelessWidget {
  const _Contenido({required this.hoja, required this.estado});

  final HojaPedido hoja;
  final DetalleDeHoja estado;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
      children: [
        if (estado.mensaje != null) ...[Aviso(estado.mensaje!, tipo: TipoDeAviso.exito), const SizedBox(height: 16)],
        Wrap(
          spacing: 12,
          runSpacing: 4,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Semantics(
              header: true,
              container: true,
              child: Text(hoja.numero, style: Theme.of(context).textTheme.titleMedium),
            ),
            EtiquetaDeHoja(hoja.estado),
          ],
        ),
        const SizedBox(height: 12),
        _Dato('Cliente', hoja.cliente.nombre),
        _Dato('Fecha', formatearFecha(hoja.fechaRegistro, hora: true)),
        _Dato('Tipo de venta', tipoDeVenta(hoja.tipoVenta)),
        if (hoja.cobranza != null) _Dato('Deuda', hoja.cobranza!.numero),
        if (hoja.estado == EstadoHP.anulada) ...[
          Aviso(
            hoja.motivoDeAnulacion ?? '',
            tipo: TipoDeAviso.informacion,
            titulo: 'Anulada el ${formatearFecha(hoja.fechaAnulacion, hora: true)}',
          ),
          const SizedBox(height: 12),
        ],
        MapaDeUbicacion(latitud: hoja.latitud, longitud: hoja.longitud),
        if (hoja.precisionGps != null) ...[
          const SizedBox(height: 4),
          Text(
            'Ubicación de la venta · ±${formatearCantidad(hoja.precisionGps!.round())} m',
            style: const TextStyle(color: textoSecundario),
          ),
        ],
        const SizedBox(height: 20),
        Semantics(
          header: true,
          container: true,
          child: Text('Productos', style: Theme.of(context).textTheme.titleMedium),
        ),
        const SizedBox(height: 8),
        for (final (i, linea) in hoja.lineas.indexed) ...[
          _LineaVendida(linea, error: estado.campos['detalles[$i].cantidad']),
          const SizedBox(height: 8),
        ],
        const SizedBox(height: 8),
        Card(
          margin: EdgeInsets.zero,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            child: Column(
              children: [
                _Fila('Subtotal', formatearMonto(hoja.subTotal)),
                _Fila('Descuento', formatearMonto(hoja.descuento)),
                const Divider(height: 16),
                _Fila('Total', formatearMonto(hoja.total), fuerte: true),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

/// Un producto vendido (2.3): la cantidad por el precio, menos el descuento por unidad, y su subtotal.
class _LineaVendida extends StatelessWidget {
  const _LineaVendida(this.linea, {this.error});

  final LineaDeHoja linea;

  /// Al confirmar sin stock: qué producto es y cuánto hay.
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

class _Fila extends StatelessWidget {
  const _Fila(this.titulo, this.valor, {this.fuerte = false});

  final String titulo;
  final String valor;
  final bool fuerte;

  @override
  Widget build(BuildContext context) {
    final estilo = fuerte ? Theme.of(context).textTheme.titleMedium : null;
    return MergeSemantics(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Text(titulo, style: estilo ?? const TextStyle(color: textoSecundario)),
          ),
          Text(valor, style: estilo),
        ],
      ),
    );
  }
}
