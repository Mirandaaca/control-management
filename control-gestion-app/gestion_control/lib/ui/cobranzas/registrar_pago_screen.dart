import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/cobranza.dart';
import '../../data/services/camara.dart';
import '../core/formato.dart';
import '../core/primer_problema.dart';
import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/campos_numericos.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/segmentado.dart';
import 'cobranza_screen.dart';
import 'registrar_pago_view_model.dart';

/// Registrar un pago (3.1; 8.6.7): la cobranza con su saldo, el monto, el método, la referencia del QR, las
/// observaciones y la foto del comprobante. Al registrarlo vuelve al detalle con el aviso del 8.6.12.
class RegistrarPagoScreen extends ConsumerStatefulWidget {
  const RegistrarPagoScreen({super.key, required this.cobranzaId});

  final int cobranzaId;

  @override
  ConsumerState<RegistrarPagoScreen> createState() => _RegistrarPagoScreenState();
}

class _RegistrarPagoScreenState extends ConsumerState<RegistrarPagoScreen> {
  final _monto = TextEditingController();
  final _referencia = TextEditingController();
  final _observaciones = TextEditingController();
  final _lugarDelMonto = (llave: GlobalKey(), foco: FocusNode());
  final _lugarDeLaReferencia = (llave: GlobalKey(), foco: FocusNode());

  RegistrarPagoViewModel get _viewModel => ref.read(registrarPagoProvider(widget.cobranzaId).notifier);

  @override
  void dispose() {
    _monto.dispose();
    _referencia.dispose();
    _observaciones.dispose();
    _lugarDelMonto.foco.dispose();
    _lugarDeLaReferencia.foco.dispose();
    super.dispose();
  }

  Future<void> _registrar() async {
    FocusScope.of(context).unfocus();
    final mensaje = await _viewModel.registrar(
      monto: _monto.text,
      referencia: _referencia.text,
      observaciones: _observaciones.text,
    );
    if (!mounted) return;
    if (mensaje != null) return context.pop(mensaje);
    final campos = ref.read(registrarPagoProvider(widget.cobranzaId)).campos;
    mostrarPrimerProblema([
      if (campos.containsKey('monto')) _lugarDelMonto,
      if (campos.containsKey('referencia')) _lugarDeLaReferencia,
    ], (_) => true);
  }

  Future<void> _reintentarComprobante() async {
    final mensaje = await _viewModel.reintentarComprobante();
    if (mensaje != null && mounted) context.pop(mensaje);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(registrarPagoProvider(widget.cobranzaId));
    const titulo = Text('Registrar pago');
    final cobranza = estado.cobranza;
    if (cobranza == null) {
      return Scaffold(
        appBar: AppBar(title: titulo),
        body: estado.errorDeCarga != null
            ? ErrorConReintento.de(
                estado.errorDeCarga!,
                reintentando: estado.reintentando,
                alReintentar: _viewModel.cargar,
              )
            : const Cargando('Cargando la cobranza…'),
      );
    }
    // Con el pago ya registrado, lo escrito no se cambia: solo falta el comprobante.
    final registrado = estado.registrado != null;

    return Scaffold(
      appBar: AppBar(title: titulo),
      body: IgnorePointer(
        ignoring: registrado,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: [
            TarjetaDeSaldo(cobranza),
            const SizedBox(height: 16),
            KeyedSubtree(
              key: _lugarDelMonto.llave,
              child: CampoDeTexto(
                controller: _monto,
                etiqueta: 'Monto (Bs)',
                keyboardType: tecladoDecimal,
                inputFormatters: [FormatoDecimal()],
                textAlign: TextAlign.end,
                focusNode: _lugarDelMonto.foco,
                error: estado.campos['monto'],
              ),
            ),
            const SizedBox(height: 12),
            Segmentado(
              etiqueta: 'Método',
              opciones: const [(MetodoPago.efectivo, 'Efectivo'), (MetodoPago.qr, 'QR')],
              valor: estado.metodo,
              alCambiar: _viewModel.elegirMetodo,
            ),
            // Con QR, la referencia es obligatoria; con efectivo, el campo no se muestra.
            if (estado.metodo == MetodoPago.qr) ...[
              const SizedBox(height: 12),
              KeyedSubtree(
                key: _lugarDeLaReferencia.llave,
                child: CampoDeTexto(
                  controller: _referencia,
                  etiqueta: 'Número de referencia del QR',
                  ayuda: 'Está en el comprobante del banco: sirve para encontrar el pago.',
                  focusNode: _lugarDeLaReferencia.foco,
                  error: estado.campos['referencia'],
                ),
              ),
            ],
            const SizedBox(height: 12),
            CampoDeTexto(
              controller: _observaciones,
              etiqueta: 'Observaciones (opcional)',
              lineas: 3,
              textCapitalization: TextCapitalization.sentences,
              error: estado.campos['observaciones'],
            ),
            const SizedBox(height: 16),
            _Comprobante(
              foto: estado.foto,
              alTomar: estado.enviando ? null : _viewModel.tomarFoto,
              alQuitar: estado.enviando ? null : _viewModel.quitarFoto,
            ),
          ],
        ),
      ),
      bottomNavigationBar: registrado
          ? BarraDeAcciones(
              arriba: [
                Aviso(
                  estado.errorDelComprobante?.mensaje ?? '',
                  tipo: TipoDeAviso.aviso,
                  titulo: 'El pago quedó registrado, pero el comprobante no se subió.',
                ),
              ],
              secundaria: Accion('Terminar sin foto', () {
                final mensaje = _viewModel.terminarSinComprobante();
                if (mensaje != null) context.pop(mensaje);
              }),
              principal: Accion(
                'Reintentar',
                _reintentarComprobante,
                enviando: estado.enviando,
                textoEnviando: 'Subiendo…',
              ),
            )
          : BarraDeAcciones(
              arriba: [
                if (estado.errorAlEnviar != null)
                  Aviso.deError(estado.errorAlEnviar!, accion: AccionDeAviso('Reintentar', _registrar)),
              ],
              principal: Accion('Registrar pago', _registrar, enviando: estado.enviando, textoEnviando: 'Registrando…'),
            ),
    );
  }
}

/// La foto del comprobante (8.6.7): opcional y tomada con la cámara. Sin foto, el recuadro dice "Tomar foto".
class _Comprobante extends StatelessWidget {
  const _Comprobante({required this.foto, required this.alTomar, required this.alQuitar});

  final FotoTomada? foto;
  final VoidCallback? alTomar;
  final VoidCallback? alQuitar;

  @override
  Widget build(BuildContext context) {
    final foto = this.foto;
    // Con la letra grande, "Quitar" va debajo de los datos de la foto: al lado no entra.
    final apilado = MediaQuery.textScalerOf(context).scale(16) > 22;
    final quitar = OutlinedButton(onPressed: alQuitar, child: const Text('Quitar'));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'Comprobante (opcional)',
          style: Theme.of(context).textTheme.labelMedium?.copyWith(color: const Color(0xFF334155)),
        ),
        const SizedBox(height: 4),
        Card(
          margin: EdgeInsets.zero,
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: foto == null
                ? OutlinedButton.icon(
                    onPressed: alTomar,
                    icon: const Icon(Icons.photo_camera_outlined),
                    label: const Text('Tomar foto'),
                  )
                : Row(
                    crossAxisAlignment: CrossAxisAlignment.center,
                    children: [
                      ClipRRect(
                        borderRadius: BorderRadius.circular(8),
                        child: Semantics(
                          image: true,
                          label: 'Foto del comprobante',
                          child: Image.memory(foto.bytes, width: 72, height: 96, fit: BoxFit.cover),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text('Foto tomada', style: TextStyle(fontWeight: FontWeight.w600)),
                            Text(
                              formatearMomento(foto.tomadaUtc, hora: true),
                              style: const TextStyle(color: textoSecundario),
                            ),
                            if (apilado) ...[const SizedBox(height: 8), quitar],
                          ],
                        ),
                      ),
                      if (!apilado) ...[const SizedBox(width: 12), quitar],
                    ],
                  ),
          ),
        ),
        const SizedBox(height: 4),
        const Text(
          'La foto del comprobante del QR o del recibo.',
          style: TextStyle(color: textoSecundario, fontSize: 14, height: 20 / 14),
        ),
      ],
    );
  }
}
