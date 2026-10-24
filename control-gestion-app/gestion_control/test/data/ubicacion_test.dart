import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/services/ubicacion.dart';

void main() {
  late StreamController<Posicion> gps;
  late int aperturas;
  late DateTime ahora;
  late ServicioConGeolocator servicio;

  setUp(() {
    aperturas = 0;
    ahora = DateTime.utc(2026, 10, 30, 14);
    gps = StreamController<Posicion>(onListen: () => aperturas++);
    servicio = ServicioConGeolocator(fuente: () => gps.stream, ahora: () => ahora);
  });

  test('el seguimiento y la tarjeta escuchan un solo flujo del GPS', () async {
    final primero = <double>[];
    final segundo = <double>[];
    final a = servicio.lecturas().listen((p) => primero.add(p.latitud));
    final b = servicio.lecturas().listen((p) => segundo.add(p.latitud));

    gps.add(const Posicion(-17.1, -63.1, 5));
    await Future<void>.delayed(Duration.zero);

    expect(aperturas, 1);
    expect(primero, [-17.1]);
    expect(segundo, [-17.1]);
    await a.cancel();
    await b.cancel();
  });

  test('quien se suma recibe enseguida la última lectura, si es reciente', () async {
    final a = servicio.lecturas().listen((_) {});
    gps.add(const Posicion(-17.1, -63.1, 5));
    await Future<void>.delayed(Duration.zero);

    ahora = ahora.add(const Duration(seconds: 10));
    final recibidas = <double>[];
    final b = servicio.lecturas().listen((p) => recibidas.add(p.latitud));
    await Future<void>.delayed(Duration.zero);

    expect(recibidas, [-17.1]);
    await a.cancel();
    await b.cancel();
  });

  test('una lectura vieja no se repite: puede ser de otro lugar', () async {
    final a = servicio.lecturas().listen((_) {});
    gps.add(const Posicion(-17.1, -63.1, 5));
    await Future<void>.delayed(Duration.zero);

    ahora = ahora.add(ServicioConGeolocator.frescura);
    final recibidas = <double>[];
    final b = servicio.lecturas().listen((p) => recibidas.add(p.latitud));
    await Future<void>.delayed(Duration.zero);

    expect(recibidas, isEmpty);
    await a.cancel();
    await b.cancel();
  });

  test('cuando nadie escucha, el GPS se apaga; al volver alguien, se enciende otra vez', () async {
    final a = servicio.lecturas().listen((_) {});
    await a.cancel();
    expect(gps.hasListener, isFalse);

    gps = StreamController<Posicion>(onListen: () => aperturas++);
    final b = servicio.lecturas().listen((_) {});
    await Future<void>.delayed(Duration.zero);

    expect(aperturas, 2);
    expect(gps.hasListener, isTrue);
    await b.cancel();
  });

  test('los errores del GPS llegan a quien escucha', () async {
    Object? error;
    final a = servicio.lecturas().listen((_) {}, onError: (Object e) => error = e);

    gps.addError(const GpsApagado());
    await Future<void>.delayed(Duration.zero);

    expect(error, isA<GpsApagado>());
    await a.cancel();
  });
}
