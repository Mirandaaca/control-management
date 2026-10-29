import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/camino.dart';
import '../../data/models/ruta.dart';
import '../core/formato.dart';
import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../territorio/visitas.dart';
import 'marcadores.dart';
import 'reordenar_view_model.dart';

/// Abre la hoja de reordenar (parte C de la tarea 25). Devuelve la ruta con el orden nuevo, o null si se cerró sin
/// aplicarlo.
Future<Ruta?> reordenarRuta(BuildContext context, {required Ruta ruta}) => showModalBottomSheet<Ruta>(
  context: context,
  isScrollControlled: true,
  useSafeArea: true,
  showDragHandle: true,
  backgroundColor: Colors.white,
  builder: (_) => HojaDeReordenar(
    rutaId: ruta.id,
    ausentes: [for (final v in ruta.visitas.where((v) => v.resultado == EstadoVisita.ausente)) v],
  ),
);

/// Reordenar desde aquí: si hay ausentes, primero se eligen por cuáles vuelve a pasar; después, el orden más corto, que
/// se ve antes de aplicarlo.
class HojaDeReordenar extends ConsumerStatefulWidget {
  const HojaDeReordenar({super.key, required this.rutaId, required this.ausentes});

  final int rutaId;
  final List<VisitaDeRuta> ausentes;

  @override
  ConsumerState<HojaDeReordenar> createState() => _HojaDeReordenarState();
}

class _HojaDeReordenarState extends ConsumerState<HojaDeReordenar> {
  // Se arma una sola vez: es la clave del view model.
  late final AbrirReorden _abrir = (rutaId: widget.rutaId, ausentes: widget.ausentes);

  Future<void> _aplicar() async {
    final ruta = await ref.read(reordenarProvider(_abrir).notifier).aplicar();
    if (ruta != null && mounted) Navigator.of(context).pop(ruta);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(reordenarProvider(_abrir));
    final viewModel = ref.read(reordenarProvider(_abrir).notifier);
    final propuesta = estado.propuesta;

    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Semantics(
            header: true,
            container: true,
            child: Text('Reordenar desde aquí', style: Theme.of(context).textTheme.titleLarge),
          ),
          const SizedBox(height: 4),
          const Text(
            'Ordena lo que te falta por el camino más corto desde donde estás.',
            style: TextStyle(color: textoSecundario),
          ),
          const SizedBox(height: 16),
          if (estado.eligiendo) ...[
            Text('¿Vuelves a pasar por estas visitas ausentes?', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            for (final visita in widget.ausentes)
              CheckboxListTile(
                value: estado.vuelven.contains(visita.id),
                onChanged: (vuelve) => viewModel.alternar(visita.id, vuelve ?? false),
                controlAffinity: ListTileControlAffinity.leading,
                contentPadding: EdgeInsets.zero,
                title: Text('Volver a pasar por ${visita.cliente.nombreCompleto}'),
                subtitle: Text(resultadoDeLaVisita(visita), style: const TextStyle(color: textoSecundario)),
              ),
            if (estado.error case final error?) ...[const SizedBox(height: 16), Aviso.deError(error)],
            const SizedBox(height: 16),
            _Boton(
              texto: 'Calcular el orden',
              textoEnviando: 'Calculando…',
              enviando: estado.cargando,
              alTocar: viewModel.calcular,
            ),
          ] else ...[
            if (estado.cargando)
              const Aviso('Calculando el orden…', tipo: TipoDeAviso.informacion, buscando: true)
            else if (propuesta == null && estado.error != null)
              Aviso.deError(estado.error!, accion: AccionDeAviso('Reintentar', viewModel.calcular)),
            if (propuesta != null) ...[
              Text(_resumen(propuesta), style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 12),
              for (final visita in propuesta.visitas) _VisitaPropuesta(visita),
              if (estado.error case final error?) ...[const SizedBox(height: 16), Aviso.deError(error)],
              const SizedBox(height: 16),
              _Boton(
                texto: 'Aplicar el orden',
                textoEnviando: 'Aplicando…',
                enviando: estado.aplicando,
                alTocar: _aplicar,
              ),
            ],
          ],
          const SizedBox(height: 8),
          OutlinedButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
        ],
      ),
    );
  }

  String _resumen(OrdenSugerido propuesta) {
    final cuantas = propuesta.visitas.length;
    return 'Unos ${formatearDuracion(propuesta.duracion.toDouble())} y '
        '${formatearDistancia(propuesta.distancia.toDouble())} para las $cuantas visitas';
  }
}

/// Una visita del orden propuesto: su número nuevo y el cliente.
class _VisitaPropuesta extends StatelessWidget {
  const _VisitaPropuesta(this.visita);

  final VisitaOrdenada visita;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 6),
    child: Row(
      children: [
        ExcludeSemantics(child: Pastilla(visita.orden, EstadoVisita.pendiente, ancho: 48, sombra: false)),
        const SizedBox(width: 12),
        Expanded(
          child: Semantics(
            label: '${visita.orden}. ',
            child: Text(
              visita.ausente ? '${visita.nombreCompleto} (vuelves a pasar)' : visita.nombreCompleto,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
        ),
      ],
    ),
  );
}

class _Boton extends StatelessWidget {
  const _Boton({required this.texto, required this.textoEnviando, required this.enviando, required this.alTocar});

  final String texto;
  final String textoEnviando;
  final bool enviando;
  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context) => FilledButton(
    onPressed: enviando ? null : alTocar,
    child: Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        if (enviando) ...[
          const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white)),
          const SizedBox(width: 12),
        ],
        Flexible(child: Text(enviando ? textoEnviando : texto, textAlign: TextAlign.center)),
      ],
    ),
  );
}
