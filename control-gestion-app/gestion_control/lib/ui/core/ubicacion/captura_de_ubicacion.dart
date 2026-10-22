import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/services/ubicacion.dart';
import '../../../proveedores.dart';

/// Cuánto espera la app una lectura buena. No le cuesta tiempo al promotor: la captura corre mientras llena el
/// formulario.
const esperaDelGps = Duration(seconds: 30);

/// Hasta aquí la lectura es buena; por encima, "poco precisa". En la calle el GPS da de 3 a 10 m: más de 30 m suele ser
/// la ubicación por wifi o por antenas, que puede poner la venta en otra cuadra.
const precisionBuena = 30.0;

/// Qué se registra con la ubicación: el final de cada aviso lo nombra ("sin ubicación, la venta no se guarda").
enum QueSeRegistra {
  venta('la venta'),
  cliente('el cliente');

  const QueSeRegistra(this.nombre);

  final String nombre;
}

sealed class EstadoDeUbicacion {
  const EstadoDeUbicacion();

  /// La lectura que se guardaría ahora, si hay alguna.
  Posicion? get posicion => null;
}

class UbicacionSinIniciar extends EstadoDeUbicacion {
  const UbicacionSinIniciar();
}

/// Buscando la señal. [mejor] es la mejor lectura hasta ahora, que todavía no llega a [precisionBuena].
class UbicacionBuscando extends EstadoDeUbicacion {
  const UbicacionBuscando([this.mejor]);

  final Posicion? mejor;

  @override
  Posicion? get posicion => mejor;
}

class UbicacionCapturada extends EstadoDeUbicacion {
  const UbicacionCapturada(this.lectura, {this.soloAproximada = false});

  final Posicion lectura;

  /// El usuario dio solo la ubicación aproximada: la lectura cuenta como poco precisa aunque diga otra cosa.
  final bool soloAproximada;

  bool get precisa => !soloAproximada && (lectura.precision ?? 0) <= precisionBuena;

  @override
  Posicion get posicion => lectura;
}

/// La ubicación guardada de un cliente, al editarlo: no cambia sola, solo con "Volver a capturar".
class UbicacionGuardada extends EstadoDeUbicacion {
  const UbicacionGuardada(this.lectura);

  final Posicion lectura;

  @override
  Posicion get posicion => lectura;
}

/// Un cliente viejo, guardado sin ubicación (0, 0).
class SinUbicacionGuardada extends EstadoDeUbicacion {
  const SinUbicacionGuardada();
}

class GpsDesactivado extends EstadoDeUbicacion {
  const GpsDesactivado();
}

class UbicacionSinPermiso extends EstadoDeUbicacion {
  const UbicacionSinPermiso({this.paraSiempre = false});

  /// Ya no se puede pedir: se da desde los ajustes de la app.
  final bool paraSiempre;
}

/// Se acabó el tiempo de espera sin ninguna lectura.
class SinSenalDelGps extends EstadoDeUbicacion {
  const SinSenalDelGps();
}

/// La ubicación de un formulario (la venta o el cliente). Cada formulario tiene la suya: si desde una venta se registra
/// un cliente nuevo, la ubicación de la venta no se pierde.
class CapturaDeUbicacion extends Notifier<EstadoDeUbicacion> {
  CapturaDeUbicacion(this.registro);

  final QueSeRegistra registro;

  StreamSubscription<Posicion>? _lecturas;
  Timer? _tope;
  Completer<Posicion?>? _esperando;
  var _soloAproximada = false;

  @override
  EstadoDeUbicacion build() {
    ref.onDispose(_detener);
    return const UbicacionSinIniciar();
  }

  ServicioDeUbicacion get _servicio => ref.read(servicioDeUbicacionProvider);

  void usarGuardada(double latitud, double longitud) {
    _detener();
    state = latitud == 0 && longitud == 0
        ? const SinUbicacionGuardada()
        : UbicacionGuardada(Posicion(latitud, longitud, null));
  }

  /// Busca la ubicación: espera hasta [esperaDelGps] una lectura de [precisionBuena] o mejor y, si no llega, se queda
  /// con la mejor que tuvo. Nunca usa la última ubicación conocida: puede ser de otro lugar.
  Future<void> capturar() async {
    _detener();
    state = const UbicacionBuscando();
    final servicio = _servicio;
    if (!await servicio.gpsActivado()) return _terminar(const GpsDesactivado());
    var permiso = await servicio.permiso();
    if (permiso == PermisoDeUbicacion.denegado) permiso = await servicio.pedirPermiso();
    if (!ref.mounted) return;
    if (permiso == PermisoDeUbicacion.denegado || permiso == PermisoDeUbicacion.denegadoParaSiempre) {
      return _terminar(UbicacionSinPermiso(paraSiempre: permiso == PermisoDeUbicacion.denegadoParaSiempre));
    }

    _soloAproximada = permiso == PermisoDeUbicacion.soloAproximada;
    _tope = Timer(esperaDelGps, () {
      final mejor = state.posicion;
      _terminar(mejor == null ? const SinSenalDelGps() : UbicacionCapturada(mejor, soloAproximada: _soloAproximada));
    });
    _lecturas = servicio.lecturas().listen(_leer, onError: _fallar);
  }

  void _leer(Posicion lectura) {
    final mejor = state.posicion;
    final nueva = mejor == null || (lectura.precision ?? double.infinity) < (mejor.precision ?? double.infinity)
        ? lectura
        : mejor;
    if (!_soloAproximada && (nueva.precision ?? double.infinity) <= precisionBuena) {
      return _terminar(UbicacionCapturada(nueva));
    }
    state = UbicacionBuscando(nueva);
    _avisarAQuienEspera();
  }

  void _fallar(Object error) {
    final mejor = state.posicion;
    _terminar(switch (error) {
      GpsApagado() => const GpsDesactivado(),
      SinPermisoDeUbicacion() => const UbicacionSinPermiso(),
      _ => mejor == null ? const SinSenalDelGps() : UbicacionCapturada(mejor, soloAproximada: _soloAproximada),
    });
  }

  void _terminar(EstadoDeUbicacion estado) {
    _detener();
    if (!ref.mounted) return;
    state = estado;
    _avisarAQuienEspera();
  }

  void _detener() {
    _lecturas?.cancel();
    _lecturas = null;
    _tope?.cancel();
    _tope = null;
  }

  // Quien tocó "Guardar" sin ninguna lectura espera la primera, o el fin de la búsqueda.
  void _avisarAQuienEspera() {
    final esperando = _esperando;
    if (esperando == null || (state.posicion == null && state is UbicacionBuscando)) return;
    _esperando = null;
    esperando.complete(state.posicion);
  }

  /// La ubicación para guardar: la capturada, la guardada o la mejor lectura hasta ahora. Sin ninguna todavía, espera la
  /// primera. Null si la búsqueda termina sin lecturas, o si no hay GPS o permiso.
  Future<Posicion?> paraGuardar() {
    final actual = state.posicion;
    if (actual != null) return Future.value(actual);
    if (state is! UbicacionBuscando) return Future.value(null);
    return (_esperando ??= Completer<Posicion?>()).future;
  }

  /// "Dar permiso": lo pide, o abre los ajustes de la app si el usuario ya dijo que no para siempre. Con la ubicación
  /// aproximada, pide la precisa.
  Future<void> darPermiso() async {
    if (state case UbicacionSinPermiso(paraSiempre: true)) return _servicio.abrirAjustesDeLaApp();
    final permiso = await _servicio.pedirPermiso();
    if (!ref.mounted) return;
    if (permiso == PermisoDeUbicacion.concedido || permiso == PermisoDeUbicacion.soloAproximada) return capturar();
    state = UbicacionSinPermiso(paraSiempre: permiso == PermisoDeUbicacion.denegadoParaSiempre);
  }

  Future<void> activarGps() => _servicio.abrirAjustesDeUbicacion();

  /// Al volver de los ajustes del celular: si faltaba el GPS, el permiso o la ubicación precisa, vuelve a buscar.
  void alVolverALaApp() {
    final faltabaAlgo = switch (state) {
      GpsDesactivado() || UbicacionSinPermiso() => true,
      UbicacionCapturada(soloAproximada: true) => true,
      _ => false,
    };
    if (faltabaAlgo) capturar();
  }
}

final capturaDeUbicacionProvider = NotifierProvider.autoDispose
    .family<CapturaDeUbicacion, EstadoDeUbicacion, QueSeRegistra>(CapturaDeUbicacion.new);
