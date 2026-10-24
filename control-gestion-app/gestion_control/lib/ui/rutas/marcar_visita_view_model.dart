import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/ruta.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';

const _igual = Object();

class EstadoDeLaMarca {
  const EstadoDeLaMarca({
    this.resultado,
    this.errorDelResultado,
    this.enviando = false,
    this.esperandoUbicacion = false,
    this.sinUbicacion = false,
    this.error,
  });

  /// Empieza sin elegir, como el tipo de venta de la entrega: así no se marca una visita por descuido.
  final EstadoVisita? resultado;
  final String? errorDelResultado;
  final bool enviando;

  /// Se tocó "Confirmar" antes de tener una ubicación: espera la primera lectura.
  final bool esperandoUbicacion;

  /// La búsqueda terminó sin ubicación: la visita no se marca (ext. 5.1a). La tarjeta dice qué hacer.
  final bool sinUbicacion;
  final ErrorDeApi? error;

  EstadoDeLaMarca copiar({
    Object? resultado = _igual,
    Object? errorDelResultado = _igual,
    bool? enviando,
    bool? esperandoUbicacion,
    bool? sinUbicacion,
    Object? error = _igual,
  }) => EstadoDeLaMarca(
    resultado: resultado == _igual ? this.resultado : resultado as EstadoVisita?,
    errorDelResultado: errorDelResultado == _igual ? this.errorDelResultado : errorDelResultado as String?,
    enviando: enviando ?? this.enviando,
    esperandoUbicacion: esperandoUbicacion ?? this.esperandoUbicacion,
    sinUbicacion: sinUbicacion ?? this.sinUbicacion,
    error: error == _igual ? this.error : error as ErrorDeApi?,
  );
}

/// La visita que se marca: la ruta, la visita y la hora que tenía al abrir la hoja (null si estaba pendiente).
typedef VisitaAMarcar = ({int rutaId, int visitaId, String? hora});

/// Marcar una visita (7.3.20, pasos 5 a 5.3): visitado o ausente, la observación y la ubicación del GPS.
class MarcarVisitaViewModel extends Notifier<EstadoDeLaMarca> {
  MarcarVisitaViewModel(this.aMarcar);

  final VisitaAMarcar aMarcar;

  /// El último envío no tuvo respuesta: la visita pudo haber quedado marcada.
  var _sinRespuesta = false;

  @override
  EstadoDeLaMarca build() => const EstadoDeLaMarca();

  void elegir(EstadoVisita? resultado) => state = state.copiar(resultado: resultado, errorDelResultado: null);

  /// Devuelve la ruta con la visita marcada, o null si algo lo impidió: los errores quedan en el estado.
  Future<Ruta?> marcar(String observacion) async {
    if (state.enviando) return null;
    final resultado = state.resultado;
    if (resultado == null) {
      state = state.copiar(errorDelResultado: 'Elige si lo visitaste o estaba ausente.');
      return null;
    }
    state = state.copiar(enviando: true, esperandoUbicacion: true, sinUbicacion: false, error: null);

    // Sin respuesta la vez anterior: si la visita ya quedó con ese resultado, no se manda otra vez (un Visitado no se
    // vuelve a marcar: la API diría visita_ya_marcada).
    if (_sinRespuesta) {
      final ya = await _yaMarcada(resultado);
      if (!ref.mounted) return null;
      if (ya != null) {
        state = state.copiar(enviando: false, esperandoUbicacion: false);
        return ya;
      }
    }

    final ubicacion = await ref.read(capturaDeUbicacionProvider(QueSeRegistra.visita).notifier).paraGuardar();
    if (!ref.mounted) return null;
    if (ubicacion == null) {
      state = state.copiar(enviando: false, esperandoUbicacion: false, sinUbicacion: true);
      return null;
    }

    state = state.copiar(esperandoUbicacion: false);
    try {
      final ruta = await ref
          .read(rutasRepositoryProvider)
          .marcar(
            aMarcar.rutaId,
            aMarcar.visitaId,
            resultado: resultado,
            ubicacion: ubicacion,
            observacion: observacion,
          );
      _sinRespuesta = false;
      if (ref.mounted) state = state.copiar(enviando: false);
      return ruta;
    } on ErrorDeApi catch (e) {
      _sinRespuesta = e.sinConexion;
      if (ref.mounted) state = state.copiar(enviando: false, error: e);
      return null;
    }
  }

  // La visita quedó con ese resultado y con otra hora que la que tenía al abrir la hoja.
  Future<Ruta?> _yaMarcada(EstadoVisita resultado) async {
    try {
      final ruta = await ref.read(rutasRepositoryProvider).delDia();
      final visita = ruta?.visitas.where((v) => v.id == aMarcar.visitaId).firstOrNull;
      return visita != null && visita.resultado == resultado && visita.hora != aMarcar.hora ? ruta : null;
    } on ErrorDeApi catch (e) {
      debugPrint('No se pudo revisar si la visita ya quedó marcada. ${e.mensaje}');
      return null;
    }
  }
}

final marcarVisitaProvider = NotifierProvider.autoDispose.family<MarcarVisitaViewModel, EstadoDeLaMarca, VisitaAMarcar>(
  MarcarVisitaViewModel.new,
);
