import 'package:fake_async/fake_async.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/ruta.dart';
import 'package:gestion_control/data/services/ubicacion.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/rutas/seguimiento_de_ruta.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';

import '../ayudantes.dart';
import '../ayudantes_de_rutas.dart';

void main() {
  late RutasRepositoryFalso rutas;
  late UbicacionFalsa gps;

  setUp(() {
    rutas = RutasRepositoryFalso();
    gps = UbicacionFalsa();
  });

  /// El seguimiento con una sesión de promotor, vivo como en la app (App lo escucha).
  ProviderContainer arrancar(FakeAsync reloj, {List<String>? permisos}) {
    final contenedor = ProviderContainer(
      overrides: [
        sesionProvider.overrideWith(
          () => SesionFija(ConSesion(promotorCon(permisos ?? [Permisos.territorial.recorrerRuta]))),
        ),
        rutasRepositoryProvider.overrideWithValue(rutas),
        servicioDeUbicacionProvider.overrideWithValue(gps),
      ],
    );
    addTearDown(contenedor.dispose);
    contenedor.listen(seguimientoDeRutaProvider, (_, _) {});
    reloj.flushMicrotasks();
    return contenedor;
  }

  SeguimientoDeRuta seguimiento(ProviderContainer c) => c.read(seguimientoDeRutaProvider.notifier);

  test('con la ruta en curso al abrir la app, manda la primera lectura y después una cada 30 s', () {
    fakeAsync((reloj) {
      final c = arrancar(reloj);
      expect(c.read(seguimientoDeRutaProvider).rutaId, 8);

      gps.leer(-17.7725, -63.1712, 6);
      reloj.flushMicrotasks();
      expect(rutas.ubicaciones.map((u) => u.$1), [8]);
      expect(c.read(seguimientoDeRutaProvider).ultima!.latitud, -17.7725);

      // Más lecturas antes de los 30 s: se manda solo la última, a los 30 s.
      gps.leer(-17.7726, -63.1713, 5);
      gps.leer(-17.7727, -63.1714, 4);
      reloj.elapse(intervaloDelSeguimiento);
      expect(rutas.ubicaciones.map((u) => u.$2.latitud), [-17.7725, -17.7727]);

      // Sin lectura nueva, no repite la vieja.
      reloj.elapse(intervaloDelSeguimiento);
      expect(rutas.ubicaciones, hasLength(2));
    });
  });

  test('con la ruta planificada no manda nada', () {
    fakeAsync((reloj) {
      rutas.alPedirDelDia = () async => rutaDePrueba(estado: EstadoRuta.planificada);
      final c = arrancar(reloj);

      gps.leer(-17.77, -63.17, 5);
      reloj.elapse(intervaloDelSeguimiento * 2);

      expect(c.read(seguimientoDeRutaProvider).rutaId, isNull);
      expect(rutas.ubicaciones, isEmpty);
    });
  });

  test('sin el permiso de recorrer rutas no pregunta por la ruta del día', () {
    fakeAsync((reloj) {
      arrancar(reloj, permisos: [Permisos.pedidos.verBandeja]);

      expect(rutas.pedidosDelDia, 0);
    });
  });

  test('al iniciar la ruta pide el permiso del GPS si falta, y empieza a mandar', () {
    fakeAsync((reloj) {
      rutas.alPedirDelDia = () async => rutaDePrueba(estado: EstadoRuta.planificada);
      gps.permisoActual = PermisoDeUbicacion.denegado;
      gps.alPedir = PermisoDeUbicacion.concedido;
      final c = arrancar(reloj);

      seguimiento(c).empezar(8, pedirPermiso: true);
      reloj.flushMicrotasks();
      gps.leer(-17.77, -63.17, 5);
      reloj.flushMicrotasks();

      expect(gps.pedidosDePermiso, 1);
      expect(rutas.ubicaciones, hasLength(1));
    });
  });

  test('al abrir la app sin permiso no lo pide: avisa que falta', () {
    fakeAsync((reloj) {
      gps.permisoActual = PermisoDeUbicacion.denegado;
      gps.alPedir = PermisoDeUbicacion.concedido;
      final c = arrancar(reloj);

      expect(gps.pedidosDePermiso, 0);
      expect(c.read(seguimientoDeRutaProvider).problema, ProblemaDelGps.sinPermiso);
    });
  });

  test('con el GPS apagado lo avisa, y vuelve a mirar en cada vuelta hasta que lo activan', () {
    fakeAsync((reloj) {
      gps.activado = false;
      final c = arrancar(reloj);
      expect(c.read(seguimientoDeRutaProvider).problema, ProblemaDelGps.desactivado);

      gps.activado = true;
      reloj.elapse(intervaloDelSeguimiento);
      gps.leer(-17.77, -63.17, 5);
      reloj.flushMicrotasks();

      expect(c.read(seguimientoDeRutaProvider).problema, isNull);
      expect(rutas.ubicaciones, hasLength(1));
    });
  });

  test('en segundo plano deja de mandar, y al volver pregunta por la ruta y sigue', () {
    fakeAsync((reloj) {
      final c = arrancar(reloj);
      gps.leer(-17.77, -63.17, 5);
      reloj.flushMicrotasks();
      expect(rutas.ubicaciones, hasLength(1));

      seguimiento(c).alCambiarElCiclo(AppLifecycleState.inactive);
      seguimiento(c).alCambiarElCiclo(AppLifecycleState.hidden);
      seguimiento(c).alCambiarElCiclo(AppLifecycleState.paused);
      gps.leer(-17.78, -63.18, 5);
      reloj.elapse(intervaloDelSeguimiento * 3);
      expect(rutas.ubicaciones, hasLength(1));

      final preguntas = rutas.pedidosDelDia;
      seguimiento(c).alCambiarElCiclo(AppLifecycleState.resumed);
      reloj.flushMicrotasks();
      gps.leer(-17.79, -63.19, 5);
      reloj.flushMicrotasks();

      expect(rutas.pedidosDelDia, preguntas + 1);
      expect(rutas.ubicaciones.map((u) => u.$2.latitud), [-17.77, -17.79]);
    });
  });

  test('un error cualquiera del GPS no lo corta: sigue leyendo y mandando (en la web pasa)', () {
    fakeAsync((reloj) {
      final c = arrancar(reloj);
      gps.leer(-17.77, -63.17, 5);
      reloj.flushMicrotasks();

      gps.fallar(Exception('Something went wrong while listening for position updates'));
      gps.leer(-17.78, -63.18, 5);
      reloj.elapse(intervaloDelSeguimiento);

      expect(c.read(seguimientoDeRutaProvider).problema, isNull);
      expect(rutas.ubicaciones.map((u) => u.$2.latitud), [-17.77, -17.78]);
    });
  });

  test('con el GPS apagado en medio de la ruta, lo avisa', () {
    fakeAsync((reloj) {
      final c = arrancar(reloj);
      gps.leer(-17.77, -63.17, 5);
      reloj.flushMicrotasks();

      gps.fallar(const GpsApagado());
      reloj.flushMicrotasks();

      expect(c.read(seguimientoDeRutaProvider).problema, ProblemaDelGps.desactivado);
    });
  });

  test('si la API dice que la ruta ya no está en curso, se detiene', () {
    fakeAsync((reloj) {
      rutas.alEnviar = (_, _) async => throw rutaNoEnCurso;
      final c = arrancar(reloj);

      gps.leer(-17.77, -63.17, 5);
      reloj.flushMicrotasks();
      gps.leer(-17.78, -63.18, 5);
      reloj.elapse(intervaloDelSeguimiento * 2);

      expect(c.read(seguimientoDeRutaProvider).rutaId, isNull);
      expect(rutas.ubicaciones, hasLength(1));
    });
  });

  test('un envío que falla por la red no lo detiene: manda en la vuelta siguiente', () {
    fakeAsync((reloj) {
      var fallar = true;
      rutas.alEnviar = (_, _) async {
        if (fallar) throw sinConexion;
      };
      final c = arrancar(reloj);
      gps.leer(-17.77, -63.17, 5);
      reloj.flushMicrotasks();

      fallar = false;
      gps.leer(-17.78, -63.18, 5);
      reloj.elapse(intervaloDelSeguimiento);

      expect(c.read(seguimientoDeRutaProvider).rutaId, 8);
      expect(rutas.ubicaciones, hasLength(2));
    });
  });

  test('al terminar la ruta deja de mandar (paso 7.1)', () {
    fakeAsync((reloj) {
      final c = arrancar(reloj);
      gps.leer(-17.77, -63.17, 5);
      reloj.flushMicrotasks();

      seguimiento(c).detener();
      gps.leer(-17.78, -63.18, 5);
      reloj.elapse(intervaloDelSeguimiento * 2);

      expect(rutas.ubicaciones, hasLength(1));
      expect(c.read(seguimientoDeRutaProvider).ultima, isNull);
    });
  });
}
