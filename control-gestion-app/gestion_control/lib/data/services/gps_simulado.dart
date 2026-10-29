import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/foundation.dart';

import '../../domain/navegacion.dart';
import '../models/camino.dart';
import 'ubicacion.dart';

/// El GPS simulado solo existe en debug y con `--dart-define=GPS_SIMULADO=true`: es una constante, así el APK de pruebas
/// (release) no lo trae, y un `flutter run` normal (el del TECNO con la ubicación de prueba por adb) usa el GPS real.
const gpsSimulado = kDebugMode && bool.fromEnvironment('GPS_SIMULADO');

/// Un GPS de mentira para mostrar la navegación sin salir a la calle (tarea 25). Empieza en la plaza 24 de Septiembre y,
/// con [recorrer], avanza por un camino a unos 10 m por segundo, con un desvío de 80 m a un tercio del recorrido.
///
/// También alimenta el seguimiento de 30 s y la ubicación de las visitas: úsalo solo contra la API local.
class GpsSimulado implements ServicioDeUbicacion {
  GpsSimulado({this.intervalo = const Duration(seconds: 1)});

  final Duration intervalo;
  var _actual = const Posicion(-17.78369, -63.18214, 5);
  final _lecturas = StreamController<Posicion>.broadcast();
  Timer? _reloj;

  @override
  Future<bool> gpsActivado() async => true;

  @override
  Future<PermisoDeUbicacion> permiso() async => PermisoDeUbicacion.concedido;

  @override
  Future<PermisoDeUbicacion> pedirPermiso() async => PermisoDeUbicacion.concedido;

  /// Quien se suma recibe enseguida dónde está, como en el GPS de verdad.
  @override
  Stream<Posicion> lecturas() => Stream.multi((oyente) {
    oyente.add(_actual);
    final suscripcion = _lecturas.stream.listen(oyente.add);
    oyente.onCancel = suscripcion.cancel;
  });

  /// Recorre [puntos] desde el primero, una lectura por [intervalo].
  void recorrer(List<PuntoDelCamino> puntos) {
    _reloj?.cancel();
    final trayecto = recorridoSimulado(puntos);
    var i = 0;
    _reloj = Timer.periodic(intervalo, (reloj) {
      if (i >= trayecto.length) return reloj.cancel();
      final punto = trayecto[i++];
      _actual = Posicion(punto.latitud, punto.longitud, 5);
      _lecturas.add(_actual);
    });
  }

  @override
  Future<void> abrirAjustesDeUbicacion() async {}

  @override
  Future<void> abrirAjustesDeLaApp() async {}
}

/// Los puntos de un recorrido por [puntos] cada [paso] metros, con un desvío de [desvio] metros hacia un costado durante
/// [lecturasDesviadas] lecturas, a un tercio del recorrido: así se ve el recálculo.
List<PuntoDelCamino> recorridoSimulado(
  List<PuntoDelCamino> puntos, {
  double paso = 10,
  double desvio = 80,
  int lecturasDesviadas = 6,
}) {
  if (puntos.length < 2) return [...puntos];
  final recorrido = <PuntoDelCamino>[puntos.first];
  for (var i = 0; i < puntos.length - 1; i++) {
    final (desde, hasta) = (puntos[i], puntos[i + 1]);
    final largo = distanciaEnMetros(desde, hasta);
    final partes = math.max(1, (largo / paso).ceil());
    for (var j = 1; j <= partes; j++) {
      final t = j / partes;
      recorrido.add((
        latitud: desde.latitud + (hasta.latitud - desde.latitud) * t,
        longitud: desde.longitud + (hasta.longitud - desde.longitud) * t,
      ));
    }
  }

  // El desvío va perpendicular al tramo donde empieza: hacia un costado, no hacia adelante.
  final inicio = recorrido.length ~/ 3;
  if (inicio + 1 >= recorrido.length) return recorrido;
  final (a, b) = (recorrido[inicio], recorrido[inicio + 1]);
  final este = (b.longitud - a.longitud) * math.cos(a.latitud * math.pi / 180);
  final norte = b.latitud - a.latitud;
  final largo = math.sqrt(este * este + norte * norte);
  if (largo == 0) return recorrido;
  final (haciaEste, haciaNorte) = (-norte / largo * desvio, este / largo * desvio);
  for (var i = inicio; i < math.min(recorrido.length, inicio + lecturasDesviadas); i++) {
    recorrido[i] = desplazar(recorrido[i], este: haciaEste, norte: haciaNorte);
  }
  return recorrido;
}
