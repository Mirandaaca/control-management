import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';

/// Una lectura del GPS: dónde y con qué precisión, en metros.
class Posicion {
  const Posicion(this.latitud, this.longitud, this.precision);

  final double latitud;
  final double longitud;

  /// El radio en metros dentro del cual está el punto real ("±8 m"). Null si no se sabe, como en una ubicación guardada.
  final double? precision;
}

enum PermisoDeUbicacion {
  concedido,

  /// Desde Android 12, el usuario puede dar solo la ubicación aproximada, que se equivoca por cuadras o más.
  soloAproximada,
  denegado,

  /// El usuario marcó "No volver a preguntar": solo se cambia desde los ajustes de la app.
  denegadoParaSiempre,
}

/// La ubicación que se lee del celular: las pruebas usan una falsa.
abstract interface class ServicioDeUbicacion {
  Future<bool> gpsActivado();

  Future<PermisoDeUbicacion> permiso();

  /// Muestra el pedido de Android. Si ya tenía la aproximada, pide la precisa.
  Future<PermisoDeUbicacion> pedirPermiso();

  /// Las lecturas del GPS mientras alguien escucha. Falla con [GpsApagado] o [SinPermisoDeUbicacion].
  Stream<Posicion> lecturas();

  Future<void> abrirAjustesDeUbicacion();

  Future<void> abrirAjustesDeLaApp();
}

class GpsApagado implements Exception {
  const GpsApagado();
}

class SinPermisoDeUbicacion implements Exception {
  const SinPermisoDeUbicacion();
}

/// Por qué no hay lecturas del GPS: el seguimiento de la ruta y la navegación hasta un pedido lo avisan con su acción.
enum ProblemaDelGps { desactivado, sinPermiso, sinPermisoParaSiempre }

/// Si se puede leer el GPS: activado y con permiso. Con [pedirPermiso], lo pide si falta (abre el pedido de Android); al
/// volver a la app solo se revisa, porque pedirlo pausa y retoma la app. Con la aproximada alcanza.
Future<ProblemaDelGps?> revisarElGps(ServicioDeUbicacion gps, {required bool pedirPermiso}) async {
  if (!await gps.gpsActivado()) return ProblemaDelGps.desactivado;
  var permiso = await gps.permiso();
  if (pedirPermiso && permiso == PermisoDeUbicacion.denegado) permiso = await gps.pedirPermiso();
  return switch (permiso) {
    PermisoDeUbicacion.denegado => ProblemaDelGps.sinPermiso,
    PermisoDeUbicacion.denegadoParaSiempre => ProblemaDelGps.sinPermisoParaSiempre,
    PermisoDeUbicacion.concedido || PermisoDeUbicacion.soloAproximada => null,
  };
}

/// El problema que dice un error de las lecturas, o null si es uno cualquiera: en la web, el GPS manda uno de vez en
/// cuando y sigue leyendo.
ProblemaDelGps? problemaDelError(Object error) => switch (error) {
  GpsApagado() => ProblemaDelGps.desactivado,
  SinPermisoDeUbicacion() => ProblemaDelGps.sinPermiso,
  _ => null,
};

class ServicioConGeolocator implements ServicioDeUbicacion {
  @override
  Future<bool> gpsActivado() => Geolocator.isLocationServiceEnabled();

  @override
  Future<PermisoDeUbicacion> permiso() async => switch (await Geolocator.checkPermission()) {
    LocationPermission.deniedForever => PermisoDeUbicacion.denegadoParaSiempre,
    LocationPermission.denied || LocationPermission.unableToDetermine => PermisoDeUbicacion.denegado,
    LocationPermission.whileInUse || LocationPermission.always =>
      await _soloAproximada() ? PermisoDeUbicacion.soloAproximada : PermisoDeUbicacion.concedido,
  };

  @override
  Future<PermisoDeUbicacion> pedirPermiso() async => switch (await Geolocator.requestPermission()) {
    // Solo la respuesta del pedido sabe si es para siempre: checkPermission, en Android, lo devuelve como "denied".
    LocationPermission.deniedForever => PermisoDeUbicacion.denegadoParaSiempre,
    LocationPermission.denied || LocationPermission.unableToDetermine => PermisoDeUbicacion.denegado,
    LocationPermission.whileInUse || LocationPermission.always => await permiso(),
  };

  Future<bool> _soloAproximada() async {
    // En la web no existe: el navegador da la ubicación que tiene.
    if (kIsWeb) return false;
    try {
      return await Geolocator.getLocationAccuracy() == LocationAccuracyStatus.reduced;
    } catch (e) {
      debugPrint('No se pudo saber si la ubicación es aproximada. $e');
      return false;
    }
  }

  /// Un solo flujo del GPS para toda la app: el seguimiento de la ruta y la tarjeta de un formulario escuchan las mismas
  /// lecturas. Quien se suma recibe enseguida la última, si tiene menos de [frescura]: en la web, un segundo
  /// `watchPosition` no recibe nada hasta que el punto cambia, y en Android esperaría la lectura siguiente.
  ServicioConGeolocator({Stream<Posicion> Function()? fuente, DateTime Function()? ahora})
    : _fuente = fuente ?? _delGps,
      _ahora = ahora ?? DateTime.now;

  final Stream<Posicion> Function() _fuente;
  final DateTime Function() _ahora;

  /// Hasta cuándo sirve la última lectura para quien recién se suma.
  static const frescura = Duration(seconds: 15);

  late final _compartido = StreamController<Posicion>.broadcast(onListen: _encender, onCancel: _apagar);
  StreamSubscription<Posicion>? _origen;
  (Posicion, DateTime)? _ultima;

  @override
  Stream<Posicion> lecturas() => Stream.multi((oyente) {
    if (_ultima case (final lectura, final cuando) when _ahora().difference(cuando) < frescura) oyente.add(lectura);
    final suscripcion = _compartido.stream.listen(oyente.add, onError: oyente.addError);
    // Si el flujo del GPS se cortó (por ejemplo, se apagó el GPS) y todavía hay quien escucha, se vuelve a abrir.
    if (_origen == null) _encender();
    oyente.onCancel = suscripcion.cancel;
  });

  void _encender() {
    if (_origen != null) return;
    _origen = _fuente().listen(
      (lectura) {
        _ultima = (lectura, _ahora());
        _compartido.add(lectura);
      },
      onError: _compartido.addError,
      onDone: () => _origen = null,
    );
  }

  void _apagar() {
    _origen?.cancel();
    _origen = null;
  }

  static Stream<Posicion> _delGps() {
    final ajustes = !kIsWeb && defaultTargetPlatform == TargetPlatform.android
        ? AndroidSettings(accuracy: LocationAccuracy.high, intervalDuration: const Duration(seconds: 1))
        : const LocationSettings(accuracy: LocationAccuracy.high);
    return Geolocator.getPositionStream(locationSettings: ajustes)
        .map((p) => Posicion(p.latitude, p.longitude, p.accuracy))
        .handleError(
          (Object e) => throw switch (e) {
            LocationServiceDisabledException() => const GpsApagado(),
            PermissionDeniedException() => const SinPermisoDeUbicacion(),
            _ => e,
          },
        );
  }

  @override
  Future<void> abrirAjustesDeUbicacion() => Geolocator.openLocationSettings();

  @override
  Future<void> abrirAjustesDeLaApp() => Geolocator.openAppSettings();
}
