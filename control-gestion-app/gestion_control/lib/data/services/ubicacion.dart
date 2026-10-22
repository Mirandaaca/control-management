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

  @override
  Stream<Posicion> lecturas() {
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
