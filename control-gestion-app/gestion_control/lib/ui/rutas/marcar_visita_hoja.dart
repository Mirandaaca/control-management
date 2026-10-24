import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/ruta.dart';
import '../core/tema.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';
import '../core/ubicacion/tarjeta_de_ubicacion.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/segmentado.dart';
import 'marcar_visita_view_model.dart';

/// Abre la hoja de marcar la visita (aviso del alcance: una hoja con Visitado o Ausente, la observación y "Confirmar").
/// Devuelve la ruta con la visita marcada, o null si se cerró sin marcar.
Future<Ruta?> marcarVisita(BuildContext context, {required int rutaId, required VisitaDeRuta visita}) =>
    showModalBottomSheet<Ruta>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      showDragHandle: true,
      backgroundColor: Colors.white,
      builder: (_) => HojaDeMarcarVisita(rutaId: rutaId, visita: visita),
    );

/// Marcar una visita (7.3.20, pasos 5 y 5.1): el resultado, la observación y la ubicación del GPS, que se captura sola
/// al abrir. Sin ubicación no se marca (ext. 5.1a).
class HojaDeMarcarVisita extends ConsumerStatefulWidget {
  const HojaDeMarcarVisita({super.key, required this.rutaId, required this.visita});

  final int rutaId;
  final VisitaDeRuta visita;

  @override
  ConsumerState<HojaDeMarcarVisita> createState() => _HojaDeMarcarVisitaState();
}

class _HojaDeMarcarVisitaState extends ConsumerState<HojaDeMarcarVisita> {
  final _observacion = TextEditingController();

  VisitaAMarcar get _clave => (rutaId: widget.rutaId, visitaId: widget.visita.id, hora: widget.visita.hora);

  @override
  void initState() {
    super.initState();
    Future.microtask(() => ref.read(capturaDeUbicacionProvider(QueSeRegistra.visita).notifier).capturar());
  }

  @override
  void dispose() {
    _observacion.dispose();
    super.dispose();
  }

  Future<void> _confirmar() async {
    FocusScope.of(context).unfocus();
    final ruta = await ref.read(marcarVisitaProvider(_clave).notifier).marcar(_observacion.text);
    if (ruta != null && mounted) Navigator.of(context).pop(ruta);
  }

  @override
  Widget build(BuildContext context) {
    // Mirarla la mantiene viva desde el primer cuadro: si nadie la mira, la búsqueda del GPS se pierde.
    ref.watch(capturaDeUbicacionProvider(QueSeRegistra.visita));
    final estado = ref.watch(marcarVisitaProvider(_clave));
    final viewModel = ref.read(marcarVisitaProvider(_clave).notifier);
    final visita = widget.visita;

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Semantics(
              header: true,
              container: true,
              child: Text('Marcar visita', style: Theme.of(context).textTheme.titleLarge),
            ),
            const SizedBox(height: 4),
            Text(
              '${visita.orden} · ${visita.cliente.nombreCompleto}',
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            Text(visita.cliente.direccion, style: const TextStyle(color: textoSecundario)),
            const SizedBox(height: 16),
            Segmentado<EstadoVisita?>(
              etiqueta: 'Resultado',
              opciones: const [(EstadoVisita.visitado, 'Visitado'), (EstadoVisita.ausente, 'Ausente')],
              valor: estado.resultado,
              alCambiar: viewModel.elegir,
            ),
            if (estado.errorDelResultado case final mensaje?) ...[
              const SizedBox(height: 4),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Padding(
                    padding: EdgeInsets.only(top: 2),
                    child: ExcludeSemantics(child: Icon(Icons.error_outline, size: 16, color: error)),
                  ),
                  const SizedBox(width: 4),
                  Expanded(
                    child: Semantics(
                      liveRegion: true,
                      child: Text(mensaje, style: const TextStyle(color: error, fontSize: 14)),
                    ),
                  ),
                ],
              ),
            ],
            const SizedBox(height: 16),
            CampoDeTexto(
              controller: _observacion,
              etiqueta: 'Observación (opcional)',
              ejemplo: 'Por ejemplo: el local estaba cerrado.',
              lineas: 3,
              textCapitalization: TextCapitalization.sentences,
              textInputAction: TextInputAction.newline,
              keyboardType: TextInputType.multiline,
              inputFormatters: [LengthLimitingTextInputFormatter(500)],
            ),
            const SizedBox(height: 16),
            const TarjetaDeUbicacion(registro: QueSeRegistra.visita),
            if (estado.error case final error?) ...[const SizedBox(height: 16), Aviso.deError(error)],
            const SizedBox(height: 16),
            FilledButton(
              onPressed: estado.enviando ? null : _confirmar,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  if (estado.enviando) ...[
                    const SizedBox.square(
                      dimension: 18,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                    ),
                    const SizedBox(width: 12),
                  ],
                  Flexible(
                    child: Text(
                      estado.esperandoUbicacion
                          ? 'Esperando la ubicación…'
                          : (estado.enviando ? 'Confirmando…' : 'Confirmar'),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
