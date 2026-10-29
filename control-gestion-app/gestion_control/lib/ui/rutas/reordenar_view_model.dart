import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/camino.dart';
import '../../data/models/ruta.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import 'seguimiento_de_ruta.dart';

/// La ruta que se reordena y sus visitas ausentes, por las que el promotor puede volver a pasar.
typedef AbrirReorden = ({int rutaId, List<VisitaDeRuta> ausentes});

const _igual = Object();

class EstadoDelReorden {
  const EstadoDelReorden({
    this.eligiendo = false,
    this.vuelven = const {},
    this.cargando = false,
    this.propuesta,
    this.error,
    this.aplicando = false,
  });

  /// Primero se eligen las ausentes por las que vuelve a pasar; sin ausentes, se calcula enseguida.
  final bool eligiendo;
  final Set<int> vuelven;
  final bool cargando;
  final OrdenSugerido? propuesta;
  final ErrorDeApi? error;
  final bool aplicando;

  EstadoDelReorden copiar({
    bool? eligiendo,
    Set<int>? vuelven,
    bool? cargando,
    Object? propuesta = _igual,
    Object? error = _igual,
    bool? aplicando,
  }) => EstadoDelReorden(
    eligiendo: eligiendo ?? this.eligiendo,
    vuelven: vuelven ?? this.vuelven,
    cargando: cargando ?? this.cargando,
    propuesta: propuesta == _igual ? this.propuesta : propuesta as OrdenSugerido?,
    error: error == _igual ? this.error : error as ErrorDeApi?,
    aplicando: aplicando ?? this.aplicando,
  );
}

/// Reordenar lo que le falta al promotor desde donde está (parte C de la tarea 25): el orden más corto de las pendientes
/// y de las ausentes por las que vuelve a pasar, que se ve antes de aplicarlo.
class ReordenarViewModel extends Notifier<EstadoDelReorden> {
  ReordenarViewModel(this.abrir);

  final AbrirReorden abrir;

  @override
  EstadoDelReorden build() {
    if (abrir.ausentes.isEmpty) {
      Future.microtask(calcular);
      return const EstadoDelReorden(cargando: true);
    }
    // Todas marcadas de entrada: el promotor desmarca las que no.
    return EstadoDelReorden(eligiendo: true, vuelven: {for (final v in abrir.ausentes) v.id});
  }

  void alternar(int visitaId, bool vuelve) =>
      state = state.copiar(vuelven: vuelve ? {...state.vuelven, visitaId} : ({...state.vuelven}..remove(visitaId)));

  /// Pide el orden desde la última lectura del GPS, con el medio que el promotor eligió en "Cómo vas".
  Future<void> calcular() async {
    final desde = ref.read(seguimientoDeRutaProvider).ultima;
    if (desde == null) {
      state = state.copiar(
        cargando: false,
        error: const ErrorDeApi(
          mensaje: 'Todavía no hay una ubicación del GPS: espera un momento y vuelve a intentarlo.',
        ),
      );
      return;
    }
    state = state.copiar(cargando: true, error: null);
    final medio = await ref.read(preferenciasProvider).medio() ?? MedioDeTransporte.auto;
    try {
      final propuesta = await ref
          .read(rutasRepositoryProvider)
          .ordenSugerido(abrir.rutaId, desde: desde, medio: medio, ausentes: state.vuelven.toList());
      if (!ref.mounted) return;
      state = state.copiar(eligiendo: false, cargando: false, propuesta: propuesta);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(cargando: false, error: e);
    }
  }

  /// Guarda el orden propuesto. Devuelve la ruta reordenada, o null si no se pudo: el error queda en el estado.
  Future<Ruta?> aplicar() async {
    final propuesta = state.propuesta;
    if (propuesta == null || state.aplicando) return null;
    state = state.copiar(aplicando: true, error: null);
    try {
      final ruta = await ref
          .read(rutasRepositoryProvider)
          .reordenar(
            abrir.rutaId,
            visitas: [for (final v in propuesta.visitas) v.id],
            ausentesQueVuelven: [for (final v in propuesta.visitas.where((v) => v.ausente)) v.id],
          );
      if (ref.mounted) state = state.copiar(aplicando: false);
      return ruta;
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(aplicando: false, error: e);
      return null;
    }
  }
}

final reordenarProvider = NotifierProvider.autoDispose.family<ReordenarViewModel, EstadoDelReorden, AbrirReorden>(
  ReordenarViewModel.new,
);
