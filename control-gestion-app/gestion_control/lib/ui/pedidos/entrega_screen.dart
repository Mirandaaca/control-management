import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/hoja_pedido.dart';
import '../../data/models/pedido.dart';
import '../../domain/credito.dart';
import '../core/formato.dart';
import '../core/primer_problema.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';
import '../core/ubicacion/tarjeta_de_ubicacion.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/confirmar.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/segmentado.dart';
import 'bandeja_view_model.dart';
import 'entrega_view_model.dart';
import 'etiqueta_de_pedido.dart';
import 'pedido_screen.dart';

/// Registrar la entrega (1.2; `frmBandejaPedidosMovil`; 7.3.2, paso 3): los productos y el monto del pedido, quién lo
/// recibe, contado o crédito y la ubicación del GPS, que se captura sola. No tiene diseño en el 8.6: sigue el de la
/// venta (8.6.5), con los textos de la entrega del panel.
class EntregaScreen extends ConsumerStatefulWidget {
  const EntregaScreen({super.key, required this.id});

  final int id;

  @override
  ConsumerState<EntregaScreen> createState() => _EntregaScreenState();
}

class _EntregaScreenState extends ConsumerState<EntregaScreen> {
  final _recibidoPor = TextEditingController();
  final _observacion = TextEditingController();
  final _ubicacion = (llave: GlobalKey(), foco: null);
  final _lugarDeQuienRecibio = (llave: GlobalKey(), foco: FocusNode());
  final _lugarDelTipo = (llave: GlobalKey(), foco: null);

  EntregaViewModel get _viewModel => ref.read(entregaProvider(widget.id).notifier);

  @override
  void initState() {
    super.initState();
    Future.microtask(() => ref.read(capturaDeUbicacionProvider(QueSeRegistra.entrega).notifier).capturar());
  }

  @override
  void dispose() {
    _recibidoPor.dispose();
    _observacion.dispose();
    _lugarDeQuienRecibio.foco.dispose();
    super.dispose();
  }

  Future<void> _registrar() async {
    FocusScope.of(context).unfocus();
    final pedido = ref.read(entregaProvider(widget.id)).pedido;
    if (pedido == null) return;
    if (!_viewModel.revisar(_recibidoPor.text)) return _mostrarPrimerProblema();
    // 7.3.2, paso 3.1: confirma la entrega. No se deshace: la venta queda confirmada.
    final confirmado = await _confirmarEntrega(pedido);
    if (confirmado) await _enviar();
  }

  Future<void> _enviar() async {
    final mensaje = await _viewModel.entregar(recibidoPor: _recibidoPor.text, observacion: _observacion.text);
    if (!mounted) return;
    if (mensaje == null) return _mostrarPrimerProblema();
    // La bandeja ya no tiene este pedido: se vuelve a pedir.
    ref.invalidate(bandejaProvider);
    context.go(Rutas.bandeja, extra: mensaje);
  }

  Future<bool> _confirmarEntrega(Pedido pedido) {
    final credito = ref.read(entregaProvider(widget.id)).tipoVenta == TipoVenta.credito;
    return confirmar(
      context,
      titulo: '¿Registrar la entrega de ${pedido.numero}?',
      accion: 'Registrar entrega',
      contenido: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(pedido.cliente.nombre, style: const TextStyle(fontWeight: FontWeight.w600)),
          Text('Total: ${formatearMonto(pedido.total)}'),
          Text(
            credito
                ? 'A crédito: la deuda vence el ${formatearMomento(vencimientoDelCredito(DateTime.now()))}.'
                : 'Al contado.',
          ),
          const SizedBox(height: 8),
          const Text(
            'Se crea y se confirma la venta con los productos del pedido y se descuenta el stock. No se puede deshacer.',
            style: TextStyle(color: textoSecundario),
          ),
        ],
      ),
    );
  }

  void _mostrarPrimerProblema() {
    final estado = ref.read(entregaProvider(widget.id));
    mostrarPrimerProblema([
      if (estado.sinUbicacion) _ubicacion,
      if (estado.campos.containsKey('recibidoPor')) _lugarDeQuienRecibio,
      if (estado.campos.containsKey('tipoVenta')) _lugarDelTipo,
    ], (_) => true);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(entregaProvider(widget.id));
    // Desde el primer cuadro: si nadie la mira, la búsqueda del GPS se descarta.
    ref.watch(capturaDeUbicacionProvider(QueSeRegistra.entrega));
    const titulo = Text('Registrar entrega');
    final pedido = estado.pedido;
    if (pedido == null) {
      return Scaffold(
        appBar: AppBar(title: titulo),
        body: estado.errorDeCarga != null
            ? ErrorConReintento.de(
                estado.errorDeCarga!,
                reintentando: estado.reintentando,
                alReintentar: _viewModel.cargar,
              )
            : const Cargando('Cargando el pedido…'),
      );
    }

    return Scaffold(
      appBar: AppBar(title: titulo),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          KeyedSubtree(
            key: _ubicacion.llave,
            child: const TarjetaDeUbicacion(registro: QueSeRegistra.entrega),
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 12,
            runSpacing: 4,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Semantics(
                header: true,
                container: true,
                child: Text(pedido.numero, style: Theme.of(context).textTheme.titleMedium),
              ),
              EtiquetaDePedido(pedido.estado),
            ],
          ),
          const SizedBox(height: 4),
          Text(pedido.cliente.nombre, style: const TextStyle(fontWeight: FontWeight.w600)),
          Text(pedido.direccionEntrega, style: const TextStyle(color: textoSecundario)),
          const SizedBox(height: 16),
          for (final (i, linea) in pedido.lineas.indexed) ...[
            LineaDelPedido(linea, error: estado.campos['detalles[$i].cantidad']),
            const SizedBox(height: 8),
          ],
          TotalesDelPedido(pedido),
          const SizedBox(height: 20),
          KeyedSubtree(
            key: _lugarDeQuienRecibio.llave,
            child: CampoDeTexto(
              controller: _recibidoPor,
              etiqueta: 'Quién recibió',
              ejemplo: 'Nombre de quien recibió…',
              textCapitalization: TextCapitalization.words,
              autofillHints: const [AutofillHints.name],
              focusNode: _lugarDeQuienRecibio.foco,
              error: estado.campos['recibidoPor'],
              onChanged: (_) => _viewModel.cambioQuienRecibio(),
            ),
          ),
          const SizedBox(height: 12),
          KeyedSubtree(
            key: _lugarDelTipo.llave,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Segmentado<TipoVenta?>(
                  etiqueta: 'Tipo de venta',
                  opciones: const [(TipoVenta.contado, 'Contado'), (TipoVenta.credito, 'Crédito')],
                  valor: estado.tipoVenta,
                  alCambiar: _viewModel.elegirTipo,
                ),
                if (estado.campos['tipoVenta'] != null) ...[
                  const SizedBox(height: 4),
                  ErrorDeCampo(estado.campos['tipoVenta']!),
                ],
                if (estado.tipoVenta == TipoVenta.credito) ...[
                  const SizedBox(height: 4),
                  const Text('La deuda vence a los 30 días.', style: TextStyle(color: textoSecundario)),
                ],
              ],
            ),
          ),
          const SizedBox(height: 12),
          CampoDeTexto(
            controller: _observacion,
            etiqueta: 'Observación (opcional)',
            ejemplo: 'Por ejemplo, dónde se dejó la mercadería…',
            lineas: 3,
            textCapitalization: TextCapitalization.sentences,
            keyboardType: TextInputType.multiline,
            textInputAction: TextInputAction.newline,
            error: estado.campos['observacion'],
          ),
        ],
      ),
      bottomNavigationBar: BarraDeAcciones(
        arriba: [
          if (estado.errorAlEnviar != null)
            Aviso.deError(estado.errorAlEnviar!, accion: AccionDeAviso('Reintentar', _enviar)),
        ],
        principal: Accion(
          'Registrar entrega',
          _registrar,
          enviando: estado.enviando,
          textoEnviando: estado.esperandoUbicacion ? 'Buscando la ubicación…' : 'Registrando…',
        ),
      ),
    );
  }
}
