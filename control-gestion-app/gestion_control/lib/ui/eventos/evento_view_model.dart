import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/evento.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';

const _igual = Object();

class DetalleDeEvento {
  const DetalleDeEvento({
    this.evento,
    this.error,
    this.reintentando = false,
    this.mensaje,
    this.subiendo = false,
    this.eliminando,
    this.errorDeFoto,
  });

  /// Null mientras carga o si no se pudo cargar.
  final Evento? evento;
  final ErrorDeApi? error;
  final bool reintentando;

  /// El aviso de éxito de lo último que pasó.
  final String? mensaje;

  /// Se está subiendo una foto.
  final bool subiendo;

  /// La foto que se está eliminando, o null.
  final int? eliminando;
  final ErrorDeApi? errorDeFoto;

  DetalleDeEvento copiar({
    Object? evento = _igual,
    Object? error = _igual,
    bool? reintentando,
    Object? mensaje = _igual,
    bool? subiendo,
    Object? eliminando = _igual,
    Object? errorDeFoto = _igual,
  }) => DetalleDeEvento(
    evento: evento == _igual ? this.evento : evento as Evento?,
    error: error == _igual ? this.error : error as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    mensaje: mensaje == _igual ? this.mensaje : mensaje as String?,
    subiendo: subiendo ?? this.subiendo,
    eliminando: eliminando == _igual ? this.eliminando : eliminando as int?,
    errorDeFoto: errorDeFoto == _igual ? this.errorDeFoto : errorDeFoto as ErrorDeApi?,
  );
}

/// Un evento (7.3.13 y 7.3.15) y sus evidencias (7.3.14): verlas, agregar una con la cámara o la galería y eliminarla.
class EventoViewModel extends Notifier<DetalleDeEvento> {
  EventoViewModel(this.id);

  final int id;

  @override
  DetalleDeEvento build() {
    Future.microtask(cargar);
    return const DetalleDeEvento();
  }

  Future<void> cargar() async {
    state = state.copiar(reintentando: state.error != null);
    try {
      final evento = await ref.read(eventosRepositoryProvider).obtener(id);
      if (ref.mounted) state = state.copiar(evento: evento, error: null, reintentando: false);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(error: e, reintentando: false);
    }
  }

  void avisar(String mensaje) => state = state.copiar(mensaje: mensaje);

  /// Toma una foto con la cámara (7.3.14, paso 2) y la sube. El lugar de la toma es la mejor lectura del GPS que haya:
  /// la foto no espera al GPS, y sin permiso no lo pide.
  Future<void> tomarFoto() async {
    final captura = ref.read(capturaDeUbicacionProvider(QueSeRegistra.foto).notifier);
    if (ref.read(capturaDeUbicacionProvider(QueSeRegistra.foto)) is UbicacionSinIniciar) {
      captura.capturar(pedirPermiso: false);
    }
    final foto = await ref.read(camaraProvider).tomarFoto();
    if (foto == null || !ref.mounted) return;
    await _subir(() {
      final lugar = ref.read(capturaDeUbicacionProvider(QueSeRegistra.foto)).posicion;
      return ref.read(eventosRepositoryProvider).agregarFoto(id, foto, lugar: lugar);
    });
  }

  /// Elige una foto de la galería (7.3.14, ext. 2a) y la sube, sin el lugar ni la hora de la toma.
  Future<void> elegirDeLaGaleria() async {
    final foto = await ref.read(camaraProvider).elegirDeLaGaleria();
    if (foto == null || !ref.mounted) return;
    await _subir(() => ref.read(eventosRepositoryProvider).agregarFoto(id, foto));
  }

  Future<void> _subir(Future<Evento> Function() subir) async {
    state = state.copiar(subiendo: true, errorDeFoto: null, mensaje: null);
    try {
      final evento = await subir();
      if (ref.mounted) state = state.copiar(evento: evento, subiendo: false);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(subiendo: false, errorDeFoto: e);
    }
  }

  /// Elimina la foto (7.3.14, paso 4), después de confirmar. La API revisa que el evento siga pendiente.
  Future<void> eliminarFoto(int fotoId) async {
    state = state.copiar(eliminando: fotoId, errorDeFoto: null, mensaje: null);
    try {
      final evento = await ref.read(eventosRepositoryProvider).eliminarFoto(id, fotoId);
      if (ref.mounted) state = state.copiar(evento: evento, eliminando: null);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(eliminando: null, errorDeFoto: e);
    }
  }
}

final eventoProvider = NotifierProvider.autoDispose.family<EventoViewModel, DetalleDeEvento, int>(EventoViewModel.new);
