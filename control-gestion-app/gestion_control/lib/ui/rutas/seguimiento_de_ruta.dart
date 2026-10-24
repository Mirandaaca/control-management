import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/ruta.dart';
import '../../data/services/error_de_api.dart';
import '../../data/services/ubicacion.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../sesion/sesion_notifier.dart';

/// Cada cuánto se manda la ubicación del promotor con la ruta en curso (7.3.20, paso 4.3).
const intervaloDelSeguimiento = Duration(seconds: 30);

/// Por qué el seguimiento no tiene GPS: la pantalla de la ruta lo avisa con su acción.
enum ProblemaDelGps { desactivado, sinPermiso, sinPermisoParaSiempre }

class EstadoDelSeguimiento {
  const EstadoDelSeguimiento({this.rutaId, this.ultima, this.problema});

  /// La ruta en curso que se sigue, o null si no hay.
  final int? rutaId;

  /// La última lectura del GPS: "Tú" en el mapa de la ruta.
  final Posicion? ultima;
  final ProblemaDelGps? problema;
}

/// Manda la ubicación del promotor cada 30 s mientras su ruta está en curso y la app está abierta (paso 4.3), desde
/// cualquier pantalla: el promotor registra ventas y eventos en medio de la ruta. Sin servicio en segundo plano (el
/// alcance lo deja fuera): con la app en segundo plano o la pantalla apagada se detiene, y al volver sigue.
///
/// Solo pide el permiso del GPS al iniciar la ruta. Al abrir la app o al volver a ella solo lo revisa: pedirlo pausa y
/// retoma la app, y con el permiso negado para siempre sería un ciclo sin fin (tarea 13).
class SeguimientoDeRuta extends Notifier<EstadoDelSeguimiento> {
  StreamSubscription<Posicion>? _lecturas;
  Timer? _reloj;

  /// La lectura que todavía no se mandó.
  Posicion? _nueva;
  var _yaEnvio = false;
  var _enPrimerPlano = true;

  // Cada encendido tiene su número: si la app se pausó mientras esperaba el permiso, el encendido viejo no sigue.
  var _vuelta = 0;

  @override
  EstadoDelSeguimiento build() {
    ref.onDispose(_apagar);
    // En un microtask: el primer aviso llega mientras se construye, y ahí todavía no se puede cambiar el estado.
    ref.listen(sesionProvider, (_, estado) {
      Future.microtask(() {
        if (!ref.mounted) return;
        if (estado is ConSesion && estado.sesion.tiene([Permisos.territorial.recorrerRuta])) {
          revisar();
        } else {
          detener();
        }
      });
    }, fireImmediately: true);
    return const EstadoDelSeguimiento();
  }

  /// Pregunta a la API si la ruta de hoy está en curso, y empieza o se detiene según eso. Un error no cambia nada: se
  /// vuelve a preguntar al volver a la app.
  Future<void> revisar() async {
    try {
      final ruta = await ref.read(rutasRepositoryProvider).delDia();
      if (!ref.mounted) return;
      if (ruta != null && ruta.estado == EstadoRuta.enCurso) {
        await empezar(ruta.id);
      } else {
        detener();
      }
    } on ErrorDeApi catch (e) {
      debugPrint('El seguimiento no pudo revisar la ruta del día. ${e.mensaje}');
      if (state.rutaId != null && _enPrimerPlano && _lecturas == null) await _encender(pedirPermiso: false);
    }
  }

  /// Empieza a seguir la ruta [rutaId]. Con [pedirPermiso] (al iniciar la ruta desde su pantalla), pide el del GPS.
  Future<void> empezar(int rutaId, {bool pedirPermiso = false}) async {
    final otra = state.rutaId != rutaId;
    state = EstadoDelSeguimiento(rutaId: rutaId, ultima: otra ? null : state.ultima, problema: state.problema);
    if (!_enPrimerPlano) return;
    if (otra || _lecturas == null || pedirPermiso) await _encender(pedirPermiso: pedirPermiso);
  }

  /// La ruta terminó, o ya no hay sesión: deja de mandar (paso 7.1).
  void detener() {
    _apagar();
    if (ref.mounted) state = const EstadoDelSeguimiento();
  }

  /// Lo avisa `App` cuando la app pasa a segundo plano o vuelve.
  void alCambiarElCiclo(AppLifecycleState ciclo) {
    switch (ciclo) {
      case AppLifecycleState.resumed:
        if (_enPrimerPlano) return;
        _enPrimerPlano = true;
        if (state.rutaId != null) revisar();
      // Un diálogo encima (inactive) no la pausa: la app sigue a la vista.
      case AppLifecycleState.inactive:
        break;
      case AppLifecycleState.hidden || AppLifecycleState.paused || AppLifecycleState.detached:
        _enPrimerPlano = false;
        _apagar();
    }
  }

  ServicioDeUbicacion get _gps => ref.read(servicioDeUbicacionProvider);

  Future<void> _encender({required bool pedirPermiso}) async {
    _apagar();
    final vuelta = _vuelta;
    final ProblemaDelGps? problema;
    try {
      problema = await _revisarElGps(pedirPermiso: pedirPermiso);
    } catch (e) {
      debugPrint('El seguimiento no pudo revisar el GPS. $e');
      return;
    }
    if (!ref.mounted || vuelta != _vuelta || state.rutaId == null || !_enPrimerPlano) return;
    state = EstadoDelSeguimiento(rutaId: state.rutaId, ultima: state.ultima, problema: problema);
    _yaEnvio = false;
    // Sin GPS, el reloj sigue: en cada vuelta lo vuelve a revisar, por si lo activaron sin salir de la app.
    _reloj = Timer.periodic(intervaloDelSeguimiento, (_) => _cadaVuelta());
    if (problema == null) _escuchar();
  }

  Future<ProblemaDelGps?> _revisarElGps({required bool pedirPermiso}) async {
    if (!await _gps.gpsActivado()) return ProblemaDelGps.desactivado;
    var permiso = await _gps.permiso();
    if (pedirPermiso && permiso == PermisoDeUbicacion.denegado) permiso = await _gps.pedirPermiso();
    return switch (permiso) {
      PermisoDeUbicacion.denegado => ProblemaDelGps.sinPermiso,
      PermisoDeUbicacion.denegadoParaSiempre => ProblemaDelGps.sinPermisoParaSiempre,
      // Con la aproximada también se manda: es mejor que nada para la oficina.
      PermisoDeUbicacion.concedido || PermisoDeUbicacion.soloAproximada => null,
    };
  }

  void _escuchar() {
    _lecturas = _gps.lecturas().listen(
      (lectura) {
        _nueva = lectura;
        state = EstadoDelSeguimiento(rutaId: state.rutaId, ultima: lectura);
        // La primera se manda apenas llega: la oficina ve al promotor sin esperar 30 s.
        if (!_yaEnvio) _enviar();
      },
      onError: (Object error) {
        _lecturas?.cancel();
        _lecturas = null;
        final problema = switch (error) {
          GpsApagado() => ProblemaDelGps.desactivado,
          SinPermisoDeUbicacion() => ProblemaDelGps.sinPermiso,
          _ => null,
        };
        if (problema == null) debugPrint('El GPS del seguimiento falló. $error');
        state = EstadoDelSeguimiento(rutaId: state.rutaId, ultima: state.ultima, problema: problema);
      },
    );
  }

  Future<void> _cadaVuelta() async {
    if (_lecturas != null) return _enviar();
    // Sin lecturas (el GPS estaba apagado o falló): lo vuelve a intentar, sin pedir el permiso.
    final vuelta = _vuelta;
    final ProblemaDelGps? problema;
    try {
      problema = await _revisarElGps(pedirPermiso: false);
    } catch (e) {
      debugPrint('El seguimiento no pudo revisar el GPS. $e');
      return;
    }
    if (!ref.mounted || vuelta != _vuelta || state.rutaId == null) return;
    state = EstadoDelSeguimiento(rutaId: state.rutaId, ultima: state.ultima, problema: problema);
    if (problema == null && _lecturas == null) _escuchar();
  }

  Future<void> _enviar() async {
    final rutaId = state.rutaId;
    final lectura = _nueva;
    if (rutaId == null || lectura == null) return;
    _nueva = null;
    _yaEnvio = true;
    try {
      await ref.read(rutasRepositoryProvider).enviarUbicacion(rutaId, lectura);
    } on ErrorDeApi catch (e) {
      // La ruta ya no está en curso (o ya no existe): no hay nada que seguir.
      if (e.codigo == 'ruta_no_en_curso' || e.estado == 404) {
        if (ref.mounted && state.rutaId == rutaId) detener();
        return;
      }
      // Sin señal o sin servidor: se intenta en la vuelta siguiente (ext. 4.3a; el panel muestra la última).
      debugPrint('No se pudo enviar la ubicación de la ruta. ${e.mensaje}');
    }
  }

  void _apagar() {
    _vuelta++;
    _lecturas?.cancel();
    _lecturas = null;
    _reloj?.cancel();
    _reloj = null;
    _nueva = null;
  }
}

final seguimientoDeRutaProvider = NotifierProvider<SeguimientoDeRuta, EstadoDelSeguimiento>(SeguimientoDeRuta.new);
