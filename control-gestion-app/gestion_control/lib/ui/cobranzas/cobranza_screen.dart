import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/cobranza.dart';
import '../../domain/permisos.dart';
import '../core/formato.dart';
import '../core/lista_paginada.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/mensaje_de_exito.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/confirmar.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/etiqueta_de_estado.dart';
import '../core/widgets/visor_de_foto.dart';
import '../sesion/sesion_notifier.dart';
import '../../proveedores.dart';
import 'cobranza_ui.dart';
import 'cobranza_view_model.dart';
import 'cobranzas_view_model.dart';

/// Una cobranza (3.1 y 3.2): el total, lo pagado, el saldo, el vencimiento, la mora y el historial de pagos.
class CobranzaScreen extends ConsumerStatefulWidget {
  const CobranzaScreen({super.key, required this.id, this.mensaje});

  final int id;

  /// El aviso de éxito con que llega después de registrar un pago.
  final String? mensaje;

  @override
  ConsumerState<CobranzaScreen> createState() => _CobranzaScreenState();
}

class _CobranzaScreenState extends ConsumerState<CobranzaScreen> {
  CobranzaViewModel get _viewModel => ref.read(cobranzaProvider(widget.id).notifier);

  @override
  void initState() {
    super.initState();
    final mensaje = widget.mensaje;
    if (mensaje != null) Future.microtask(() => _viewModel.avisar(mensaje));
  }

  // Al volver con el pago registrado, avisa lo que pasó y trae la cobranza de nuevo. Las listas que siguen abiertas
  // debajo (las cobranzas, las del cliente y lo que debe en su ficha) también: si no, mostraban el saldo de antes.
  Future<void> _registrarPago() async {
    final clienteId = ref.read(cobranzaProvider(widget.id)).cobranza?.clienteId;
    final mensaje = await context.push<String>(Rutas.registrarPago(widget.id));
    if (mensaje == null || !mounted) return;
    _viewModel.avisar(mensaje);
    for (final id in [null, ?clienteId]) {
      recargarSiEstaAbierta(ref, cobranzasProvider(id));
      ref.invalidate(resumenDeCobranzasProvider(id));
    }
    if (clienteId != null) ref.invalidate(porCobrarDelClienteProvider(clienteId));
    await _viewModel.cargar();
  }

  // Reemplazar borra la foto anterior: se confirma antes de tomar la nueva (7.3.10, ext. 3.2a).
  Future<void> _adjuntar(Pago pago) async {
    if (pago.comprobanteId != null) {
      final reemplazar = await confirmar(
        context,
        titulo: '¿Reemplazar el comprobante?',
        contenido: const Text('La foto anterior se borra: no se puede deshacer.'),
        accion: 'Reemplazar comprobante',
        peligro: true,
      );
      if (!reemplazar) return;
    }
    await _viewModel.adjuntar(pago);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(cobranzaProvider(widget.id));
    ref.listen(cobranzaProvider(widget.id).select((e) => e.mensaje), (anterior, mensaje) {
      if (mensaje != null && mensaje != anterior) avisarExito(context, mensaje);
    });
    final sesion = ref.watch(sesionProvider);
    final puedeRegistrar = sesion is ConSesion && sesion.sesion.tiene([Permisos.cobranzas.registrarPago]);
    final cobranza = estado.cobranza;

    return Scaffold(
      appBar: AppBar(title: Text(cobranza?.numero ?? 'Cobranza')),
      body: switch (estado) {
        DetalleDeCobranza(cobranza: final cobranza?) => ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: [
            if (estado.errorAlSubir != null) ...[
              Aviso.deError(
                estado.errorAlSubir!,
                accion: AccionDeAviso('Reintentar', estado.subiendo == null ? _viewModel.reintentar : null),
              ),
              const SizedBox(height: 16),
            ],
            TarjetaDeSaldo(cobranza),
            const SizedBox(height: 20),
            Semantics(
              header: true,
              container: true,
              child: Text('Pagos', style: Theme.of(context).textTheme.titleMedium),
            ),
            const SizedBox(height: 8),
            if (cobranza.pagos.isEmpty)
              const Text('Todavía no tiene pagos.', style: TextStyle(color: textoSecundario))
            else
              for (final pago in cobranza.pagos) ...[
                _TarjetaDePago(
                  pago,
                  subiendo: estado.subiendo == pago.id,
                  alAdjuntar: puedeRegistrar && !pago.anulado && estado.subiendo == null ? () => _adjuntar(pago) : null,
                ),
                const SizedBox(height: 12),
              ],
          ],
        ),
        DetalleDeCobranza(error: final error?) => ErrorConReintento.de(
          error,
          reintentando: estado.reintentando,
          alReintentar: _viewModel.cargar,
        ),
        _ => const Cargando('Cargando la cobranza…'),
      },
      // Solo las que reciben pagos: una pagada, incobrable o anulada, no.
      bottomNavigationBar: cobranza != null && cobranza.recibePagos && puedeRegistrar
          ? BarraDeAcciones(principal: Accion('Registrar pago', _registrarPago))
          : null,
    );
  }
}

/// La cobranza y su saldo, como en el 8.6.7: el número, la HP, el cliente, el total, lo pagado, el saldo y el
/// vencimiento.
class TarjetaDeSaldo extends StatelessWidget {
  const TarjetaDeSaldo(this.cobranza, {super.key});

  final Cobranza cobranza;

  @override
  Widget build(BuildContext context) {
    final c = cobranza;
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text([c.numero, ?c.hojaPedido].join(' · '), style: const TextStyle(color: textoSecundario)),
            Semantics(
              header: true,
              container: true,
              child: Text(c.clienteNombre, style: Theme.of(context).textTheme.titleMedium),
            ),
            const SizedBox(height: 4),
            EtiquetaDeCobranza(c.estado),
            const Divider(height: 24),
            _Fila('Total', formatearMonto(c.montoTotal)),
            _Fila('Pagado', formatearMonto(c.montoPagado)),
            _Fila('Saldo', formatearMonto(c.saldo), fuerte: true),
            const SizedBox(height: 8),
            Text('${vencimientoDe(c.fechaVencimiento, c.diasDeMora)}.', style: const TextStyle(color: textoSecundario)),
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
    return MergeSemantics(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 2),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Flexible(
              child: Text(titulo, style: const TextStyle(color: textoSecundario)),
            ),
            const SizedBox(width: 8),
            // Con la letra grande, un monto largo pasa de línea en vez de salirse.
            Flexible(
              child: Text(
                valor,
                textAlign: TextAlign.end,
                style: fuerte ? Theme.of(context).textTheme.titleMedium : null,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Un pago del historial: el monto, el método, cuándo y quién lo registró, si está anulado y su comprobante.
class _TarjetaDePago extends StatelessWidget {
  const _TarjetaDePago(this.pago, {required this.subiendo, required this.alAdjuntar});

  final Pago pago;
  final bool subiendo;
  final VoidCallback? alAdjuntar;

  @override
  Widget build(BuildContext context) {
    final tieneComprobante = pago.comprobanteId != null;
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            MergeSemantics(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Wrap(
                    spacing: 12,
                    runSpacing: 4,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Text(formatearMonto(pago.monto), style: const TextStyle(fontWeight: FontWeight.w600)),
                      if (pago.anulado)
                        const EtiquetaDeEstado('Anulado', icono: Icons.block, color: ColorDeEstado.rojo),
                    ],
                  ),
                  Text(pago.metodo == MetodoPago.qr ? 'QR · ref. ${pago.referencia ?? '—'}' : 'Efectivo'),
                  Text(formatearFecha(pago.fecha, hora: true), style: const TextStyle(color: textoSecundario)),
                  Text('Lo registró ${pago.registradoPor}', style: const TextStyle(color: textoSecundario)),
                  if (pago.observaciones != null) Text(pago.observaciones!),
                  if (pago.anulado && pago.motivoDeAnulacion != null)
                    Text(
                      'Motivo de la anulación: ${pago.motivoDeAnulacion}',
                      style: const TextStyle(color: textoSecundario),
                    ),
                  if (!tieneComprobante) const Text('Sin comprobante', style: TextStyle(color: textoSecundario)),
                ],
              ),
            ),
            Wrap(
              spacing: 8,
              children: [
                if (tieneComprobante)
                  TextButton.icon(
                    onPressed: () => verComprobante(context, pago.comprobanteId!),
                    icon: const Icon(Icons.image_outlined),
                    label: const Text('Ver comprobante'),
                  ),
                if (alAdjuntar != null || subiendo)
                  TextButton.icon(
                    onPressed: subiendo ? null : alAdjuntar,
                    icon: subiendo
                        ? const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Icon(Icons.photo_camera_outlined),
                    label: Text(
                      subiendo ? 'Subiendo…' : (tieneComprobante ? 'Reemplazar comprobante' : 'Adjuntar comprobante'),
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// La foto de un comprobante a pantalla completa, con zoom.
Future<void> verComprobante(BuildContext context, int evidenciaId) => verFoto(
  context,
  titulo: 'Comprobante',
  etiqueta: 'Foto del comprobante',
  foto: fotoDeComprobanteProvider(evidenciaId),
);
