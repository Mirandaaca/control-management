import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/ruta.dart';
import '../../data/models/zona.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import 'seguimiento_de_ruta.dart';

const _igual = Object();

class EstadoDeLaRuta {
  const EstadoDeLaRuta({
    this.cargada = false,
    this.ruta,
    this.zona,
    this.puntos = const [],
    this.errorDeCarga,
    this.reintentando = false,
    this.enviando = false,
    this.errorDeAccion,
    this.mensaje,
    this.recienMarcada,
  });

  /// Ya respondió la API: con [ruta] null, el promotor no tiene ruta hoy (ext. 3.1a).
  final bool cargada;
  final Ruta? ruta;

  /// La zona de la ruta, con su límite, y sus puntos de referencia. Si no se pudieron cargar, el mapa va sin ellos.
  final Zona? zona;
  final List<PuntoDeReferencia> puntos;
  final ErrorDeApi? errorDeCarga;
  final bool reintentando;

  /// Iniciando o terminando la ruta.
  final bool enviando;
  final ErrorDeApi? errorDeAccion;
  final String? mensaje;

  /// La visita que se acaba de marcar: su tarjeta ofrece lo que sigue ("Nueva hoja de pedido" o "Registrar evento").
  final int? recienMarcada;

  EstadoDeLaRuta copiar({
    bool? cargada,
    Object? ruta = _igual,
    Object? zona = _igual,
    List<PuntoDeReferencia>? puntos,
    Object? errorDeCarga = _igual,
    bool? reintentando,
    bool? enviando,
    Object? errorDeAccion = _igual,
    Object? mensaje = _igual,
    Object? recienMarcada = _igual,
  }) => EstadoDeLaRuta(
    cargada: cargada ?? this.cargada,
    ruta: ruta == _igual ? this.ruta : ruta as Ruta?,
    zona: zona == _igual ? this.zona : zona as Zona?,
    puntos: puntos ?? this.puntos,
    errorDeCarga: errorDeCarga == _igual ? this.errorDeCarga : errorDeCarga as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    enviando: enviando ?? this.enviando,
    errorDeAccion: errorDeAccion == _igual ? this.errorDeAccion : errorDeAccion as ErrorDeApi?,
    mensaje: mensaje == _igual ? this.mensaje : mensaje as String?,
    recienMarcada: recienMarcada == _igual ? this.recienMarcada : recienMarcada as int?,
  );
}

/// La ruta del día (5.4; 7.3.20, pasos 3 a 7): verla, iniciarla, marcar las visitas y terminarla.
class RutaDelDiaViewModel extends Notifier<EstadoDeLaRuta> {
  @override
  EstadoDeLaRuta build() {
    Future.microtask(cargar);
    return const EstadoDeLaRuta();
  }

  /// La pide otra vez al abrir, al deslizar hacia abajo y al volver a la app: los días de atraso los calcula la API el día
  /// en que se pide (aviso del alcance). Mientras, lo que ya se ve se queda.
  Future<void> cargar() async {
    state = state.copiar(reintentando: state.errorDeCarga != null);
    try {
      final ruta = await ref.read(rutasRepositoryProvider).delDia();
      if (!ref.mounted) return;
      final otraZona = ruta != null && ruta.zonaId != state.zona?.id;
      state = state.copiar(cargada: true, ruta: ruta, errorDeCarga: null, reintentando: false);
      if (otraZona) await _cargarZona(ruta.zonaId);
      // Una ruta que ya estaba en curso (por ejemplo, se abrió la app de nuevo): el seguimiento la sigue sin pedir permisos.
      if (ruta != null && ruta.estado == EstadoRuta.enCurso && ref.read(seguimientoDeRutaProvider).rutaId != ruta.id) {
        await ref.read(seguimientoDeRutaProvider.notifier).empezar(ruta.id);
      }
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(errorDeCarga: e, reintentando: false);
    }
  }

  Future<void> _cargarZona(int zonaId) async {
    final zonas = ref.read(zonasRepositoryProvider);
    try {
      final (zona, puntos) = await (zonas.obtener(zonaId), zonas.puntos(zonaId: zonaId)).wait;
      if (ref.mounted) state = state.copiar(zona: zona, puntos: puntos);
    } on ParallelWaitError<(Zona?, List<PuntoDeReferencia>?), (AsyncError?, AsyncError?)> catch (e) {
      // Sin la zona, el mapa muestra igual las paradas: no vale la pena un error en la pantalla.
      debugPrint('No se pudo cargar la zona de la ruta. ${e.errors.$1?.error ?? e.errors.$2?.error}');
      if (ref.mounted) state = state.copiar(zona: e.values.$1, puntos: e.values.$2 ?? const []);
    }
  }

  /// Paso 4: la ruta pasa a en curso y empieza el seguimiento, que pide el permiso del GPS si falta.
  Future<void> iniciar() async {
    final ruta = state.ruta;
    if (ruta == null || state.enviando) return;
    state = state.copiar(enviando: true, errorDeAccion: null, mensaje: null);
    try {
      final iniciada = await ref.read(rutasRepositoryProvider).iniciar(ruta.id);
      if (!ref.mounted) return;
      state = state.copiar(
        ruta: iniciada,
        enviando: false,
        mensaje: 'La ruta está en curso desde las ${formatearHora(iniciada.inicio)}.',
      );
      await ref.read(seguimientoDeRutaProvider.notifier).empezar(iniciada.id, pedirPermiso: true);
    } on ErrorDeApi catch (e) {
      await _fallo(e);
    }
  }

  /// Paso 7: la ruta queda completada y el seguimiento se detiene (paso 7.1). Las visitas sin marcar quedan pendientes.
  Future<void> terminar() async {
    final ruta = state.ruta;
    if (ruta == null || state.enviando) return;
    state = state.copiar(enviando: true, errorDeAccion: null, mensaje: null);
    try {
      final terminada = await ref.read(rutasRepositoryProvider).terminar(ruta.id);
      // Si la pantalla ya se cerró, el seguimiento se detiene solo: la API le responde ruta_no_en_curso.
      if (!ref.mounted) return;
      ref.read(seguimientoDeRutaProvider.notifier).detener();
      state = state.copiar(
        ruta: terminada,
        enviando: false,
        recienMarcada: null,
        mensaje: 'La ruta quedó completada a las ${formatearHora(terminada.fin)}.',
      );
    } on ErrorDeApi catch (e) {
      await _fallo(e);
    }
  }

  /// La hoja de marcar visita devolvió la ruta con la visita marcada.
  void marcada(Ruta ruta, int visitaId) =>
      state = state.copiar(ruta: ruta, recienMarcada: visitaId, mensaje: null, errorDeAccion: null);

  /// Una ruta que cambió por otro lado (ya iniciada o ya terminada) se vuelve a pedir, con el aviso de la API.
  Future<void> _fallo(ErrorDeApi e) async {
    if (!ref.mounted) return;
    state = state.copiar(enviando: false, errorDeAccion: e);
    if (e.codigo == 'ruta_ya_iniciada' || e.codigo == 'ruta_no_en_curso') await cargar();
  }

  void cerrarMensaje() => state = state.copiar(mensaje: null, errorDeAccion: null);
}

final rutaDelDiaProvider = NotifierProvider.autoDispose<RutaDelDiaViewModel, EstadoDeLaRuta>(RutaDelDiaViewModel.new);
