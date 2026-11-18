import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/seguimiento.dart';
import '../../data/services/error_de_api.dart';
import '../../data/services/ubicacion.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../sesion/sesion_notifier.dart';

/// Cada cuánto se manda la ubicación del promotor con la ruta en curso (7.3.20, paso 4.3).
const intervaloDelSeguimiento = Duration(seconds: 30);

/// Cada cuánto, con entregas en ruta (tarea 31): la oficina sigue la entrega en "Entregas de hoy".
const intervaloConEntregas = Duration(seconds: 10);

const _igual = Object();

class EstadoDelSeguimiento {
  const EstadoDelSeguimiento({this.rutaId, this.entregas = 0, this.ultima, this.problema});

  /// La ruta en curso que se sigue, o null si no hay.
  final int? rutaId;

  /// Cuántas entregas en ruta tiene hoy (tarea 31).
  final int entregas;

  /// La última lectura del GPS: "Tú" en el mapa de la ruta.
  final Posicion? ultima;
  final ProblemaDelGps? problema;

  /// Si hay algo que seguir: la ruta en curso o alguna entrega en ruta.
  bool get activo => rutaId != null || entregas > 0;

  /// Con entregas en ruta, cada 10 s; con la ruta sola, cada 30 s.
  Duration get intervalo => entregas > 0 ? intervaloConEntregas : intervaloDelSeguimiento;

  EstadoDelSeguimiento copiar({
    Object? rutaId = _igual,
    int? entregas,
    Object? ultima = _igual,
    Object? problema = _igual,
  }) => EstadoDelSeguimiento(
    rutaId: rutaId == _igual ? this.rutaId : rutaId as int?,
    entregas: entregas ?? this.entregas,
    ultima: ultima == _igual ? this.ultima : ultima as Posicion?,
    problema: problema == _igual ? this.problema : problema as ProblemaDelGps?,
  );
}

/// Manda la ubicación del promotor mientras su ruta está en curso (7.3.20, paso 4.3) o, desde la tarea 31, mientras tiene
/// entregas en ruta para hoy, con la app abierta y desde cualquier pantalla: el promotor registra ventas y eventos en
/// medio de la ruta, y entrega desde la bandeja. Cada 30 s con la ruta sola; cada 10 s con entregas. Sin servicio en
/// segundo plano (el alcance lo deja fuera): con la app en segundo plano o la pantalla apagada se detiene, y al volver
/// sigue.
///
/// Qué seguir lo dice la API (`GET /ubicaciones/seguimiento`), al entrar, al volver a la app y al abrir la bandeja; y
/// cada envío responde lo mismo, así cambia el ritmo cuando llega o se registra una entrega.
///
/// Solo pide el permiso del GPS al iniciar la ruta o con "Dar permiso". Al abrir la app o al volver a ella solo lo revisa:
/// pedirlo pausa y retoma la app, y con el permiso negado para siempre sería un ciclo sin fin (tarea 13).
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
        if (_puedeSeguir) {
          revisar();
        } else {
          detener();
        }
      });
    }, fireImmediately: true);
    return const EstadoDelSeguimiento();
  }

  /// Quien recorre una ruta o entrega pedidos: los demás no mandan su ubicación.
  bool get _puedeSeguir {
    final sesion = ref.read(sesionProvider);
    return sesion is ConSesion &&
        sesion.sesion.tiene([Permisos.territorial.recorrerRuta, Permisos.pedidos.registrarEntrega]);
  }

  /// Pregunta a la API qué hay que seguir, y empieza, cambia el ritmo o se detiene según eso. Un error no cambia nada: se
  /// vuelve a preguntar al volver a la app o al abrir la bandeja.
  Future<void> revisar() async {
    if (!_puedeSeguir) return;
    try {
      final seguimiento = await ref.read(ubicacionesRepositoryProvider).seguimiento();
      if (!ref.mounted) return;
      await _seguir(seguimiento);
    } on ErrorDeApi catch (e) {
      debugPrint('El seguimiento no pudo revisar qué seguir. ${e.mensaje}');
      if (state.activo && _enPrimerPlano && _lecturas == null) await _encender(pedirPermiso: false);
    }
  }

  /// Empieza a seguir la ruta [rutaId]. Con [pedirPermiso] (al iniciar la ruta desde su pantalla), pide el del GPS.
  Future<void> empezar(int rutaId, {bool pedirPermiso = false}) async {
    final otra = state.rutaId != rutaId;
    // Una lectura de otra ruta no es "Tú" en esta, salvo que el GPS siga leyendo (por las entregas).
    state = state.copiar(rutaId: rutaId, ultima: otra && _lecturas == null ? null : state.ultima);
    if (!_enPrimerPlano) return;
    if (otra || _lecturas == null || pedirPermiso) await _encender(pedirPermiso: pedirPermiso);
  }

  /// La ruta terminó (paso 7.1): deja de seguirla. Si quedan entregas en ruta, sigue por ellas, cada 10 s como antes.
  Future<void> rutaTerminada() async {
    if (state.entregas == 0) {
      detener();
    } else {
      state = state.copiar(rutaId: null);
    }
    // Lo confirma con la API: puede haber entregas que salieron a ruta mientras tanto.
    await revisar();
  }

  /// "Dar permiso" en el aviso del GPS de la bandeja: lo vuelve a pedir y, si lo dan, empieza a mandar.
  Future<void> pedirPermiso() async {
    if (state.activo && _enPrimerPlano) await _encender(pedirPermiso: true);
  }

  /// Ya no hay sesión (o nada que seguir): deja de mandar.
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
        // También sin nada que seguir: mientras la app estaba atrás, pudieron salir a ruta sus pedidos.
        revisar();
      // Un diálogo encima (inactive) no la pausa: la app sigue a la vista.
      case AppLifecycleState.inactive:
        break;
      case AppLifecycleState.hidden || AppLifecycleState.paused || AppLifecycleState.detached:
        _enPrimerPlano = false;
        _apagar();
    }
  }

  ServicioDeUbicacion get _gps => ref.read(servicioDeUbicacionProvider);

  /// Lo que respondió la API: empieza, cambia el ritmo o se detiene.
  Future<void> _seguir(Seguimiento seguimiento) async {
    if (!seguimiento.hayAlgo) return detener();
    final antes = state;
    final otraRuta = seguimiento.rutaEnCurso != null && seguimiento.rutaEnCurso != antes.rutaId;
    state = antes.copiar(
      rutaId: seguimiento.rutaEnCurso,
      entregas: seguimiento.entregasEnRuta,
      ultima: otraRuta && _lecturas == null ? null : antes.ultima,
    );
    if (!_enPrimerPlano) return;
    if (_reloj == null || otraRuta) {
      await _encender(pedirPermiso: false);
    } else if (antes.intervalo != state.intervalo) {
      _reprogramar();
    }
  }

  Future<void> _encender({required bool pedirPermiso}) async {
    _apagar();
    final vuelta = _vuelta;
    final ProblemaDelGps? problema;
    try {
      problema = await revisarElGps(_gps, pedirPermiso: pedirPermiso);
    } catch (e) {
      debugPrint('El seguimiento no pudo revisar el GPS. $e');
      return;
    }
    if (!ref.mounted || vuelta != _vuelta || !state.activo || !_enPrimerPlano) return;
    state = state.copiar(problema: problema);
    _yaEnvio = false;
    // Sin GPS, el reloj sigue: en cada vuelta lo vuelve a revisar, por si lo activaron sin salir de la app.
    _reloj = Timer.periodic(state.intervalo, (_) => _cadaVuelta());
    if (problema == null) _escuchar();
  }

  /// Cambia el ritmo sin soltar el GPS: llegó la primera entrega en ruta, o se registró la última.
  void _reprogramar() {
    if (_reloj == null) return;
    _reloj!.cancel();
    _reloj = Timer.periodic(state.intervalo, (_) => _cadaVuelta());
  }

  void _escuchar() {
    _lecturas = _gps.lecturas().listen(
      (lectura) {
        _nueva = lectura;
        state = state.copiar(ultima: lectura, problema: null);
        // La primera se manda apenas llega: la oficina ve al promotor sin esperar la primera vuelta.
        if (!_yaEnvio) _enviar();
      },
      onError: (Object error) {
        final problema = problemaDelError(error);
        // Un error cualquiera no corta el flujo: en la web, el GPS manda uno de vez en cuando y sigue leyendo.
        if (problema == null) return debugPrint('El GPS del seguimiento falló. $error');
        _lecturas?.cancel();
        _lecturas = null;
        state = state.copiar(problema: problema);
      },
    );
  }

  Future<void> _cadaVuelta() async {
    await _enviar();
    if (_lecturas != null) return;
    // Sin lecturas (el GPS estaba apagado o sin permiso): lo vuelve a intentar, sin pedir el permiso.
    final vuelta = _vuelta;
    final ProblemaDelGps? problema;
    try {
      problema = await revisarElGps(_gps, pedirPermiso: false);
    } catch (e) {
      debugPrint('El seguimiento no pudo revisar el GPS. $e');
      return;
    }
    if (!ref.mounted || vuelta != _vuelta || !state.activo) return;
    state = state.copiar(problema: problema);
    if (problema == null && _lecturas == null) _escuchar();
  }

  Future<void> _enviar() async {
    final lectura = _nueva;
    if (!state.activo || lectura == null) return;
    _nueva = null;
    _yaEnvio = true;
    final vuelta = _vuelta;
    try {
      final seguimiento = await ref.read(ubicacionesRepositoryProvider).enviar(lectura);
      // La respuesta de un encendido viejo no cambia el de ahora.
      if (!ref.mounted || vuelta != _vuelta) return;
      await _seguir(seguimiento);
    } on ErrorDeApi catch (e) {
      // Ya no hay nada que seguir (se registró la última entrega, o la ruta terminó), o le quitaron el permiso.
      if (e.codigo == 'sin_seguimiento' || e.estado == 403) {
        if (ref.mounted && vuelta == _vuelta) detener();
        return;
      }
      // Sin señal o sin servidor: se intenta en la vuelta siguiente (ext. 4.3a; el panel muestra la última).
      debugPrint('No se pudo enviar la ubicación. ${e.mensaje}');
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
