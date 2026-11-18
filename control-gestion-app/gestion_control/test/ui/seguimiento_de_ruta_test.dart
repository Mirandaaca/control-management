import 'package:fake_async/fake_async.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/ruta.dart';
import 'package:gestion_control/data/models/seguimiento.dart';
import 'package:gestion_control/data/services/ubicacion.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/rutas/seguimiento_de_ruta.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';

import '../ayudantes.dart';
import '../ayudantes_de_rutas.dart';

void main() {
  late RutasRepositoryFalso rutas;
  late UbicacionesRepositoryFalso ubicaciones;
  late UbicacionFalsa gps;

  setUp(() {
    rutas = RutasRepositoryFalso();
    ubicaciones = UbicacionesRepositoryFalso(rutas: rutas);
    gps = UbicacionFalsa();
  });

  /// El seguimiento con una sesión de promotor, vivo como en la app (App lo escucha).
  ProviderContainer arrancar(FakeAsync reloj, {List<String>? permisos}) {
    final contenedor = ProviderContainer(
      overrides: [
        sesionProvider.overrideWith(
          () => SesionFija(
            ConSesion(promotorCon(permisos ?? [Permisos.territorial.recorrerRuta, Permisos.pedidos.registrarEntrega])),
          ),
        ),
        ubicacionesRepositoryProvider.overrideWithValue(ubicaciones),
        servicioDeUbicacionProvider.overrideWithValue(gps),
      ],
    );
    addTearDown(contenedor.dispose);
    contenedor.listen(seguimientoDeRutaProvider, (_, _) {});
    reloj.flushMicrotasks();
    return contenedor;
  }

  SeguimientoDeRuta seguimiento(ProviderContainer c) => c.read(seguimientoDeRutaProvider.notifier);
  List<double> enviadas() => ubicaciones.enviadas.map((u) => u.latitud).toList();

  group('la ruta en curso (7.3.20, paso 4.3)', () {
    test('con la ruta en curso al abrir la app, manda la primera lectura y después una cada 30 s', () {
      fakeAsync((reloj) {
        final c = arrancar(reloj);
        expect(c.read(seguimientoDeRutaProvider).rutaId, 8);

        gps.leer(-17.7725, -63.1712, 6);
        reloj.flushMicrotasks();
        expect(enviadas(), [-17.7725]);
        expect(c.read(seguimientoDeRutaProvider).ultima!.latitud, -17.7725);

        // Más lecturas antes de los 30 s: se manda solo la última, a los 30 s.
        gps.leer(-17.7726, -63.1713, 5);
        gps.leer(-17.7727, -63.1714, 4);
        reloj.elapse(intervaloDelSeguimiento);
        expect(enviadas(), [-17.7725, -17.7727]);

        // Sin lectura nueva, no repite la vieja.
        reloj.elapse(intervaloDelSeguimiento);
        expect(enviadas(), hasLength(2));
      });
    });

    test('con la ruta planificada y sin entregas no manda nada', () {
      fakeAsync((reloj) {
        rutas.alPedirDelDia = () async => rutaDePrueba(estado: EstadoRuta.planificada);
        final c = arrancar(reloj);

        gps.leer(-17.77, -63.17, 5);
        reloj.elapse(intervaloDelSeguimiento * 2);

        expect(c.read(seguimientoDeRutaProvider).activo, isFalse);
        expect(ubicaciones.enviadas, isEmpty);
      });
    });

    test('sin el permiso de recorrer rutas ni el de entregar no pregunta qué seguir', () {
      fakeAsync((reloj) {
        arrancar(reloj, permisos: [Permisos.pedidos.verBandeja]);

        expect(ubicaciones.revisiones, 0);
      });
    });

    test('al iniciar la ruta pide el permiso del GPS si falta, y empieza a mandar', () {
      fakeAsync((reloj) {
        rutas.alPedirDelDia = () async => rutaDePrueba(estado: EstadoRuta.planificada);
        gps.permisoActual = PermisoDeUbicacion.denegado;
        gps.alPedir = PermisoDeUbicacion.concedido;
        final c = arrancar(reloj);

        // La API ya la tiene en curso cuando la pantalla de la ruta llama a empezar.
        rutas.alPedirDelDia = () async => rutaDePrueba();
        seguimiento(c).empezar(8, pedirPermiso: true);
        reloj.flushMicrotasks();
        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();

        expect(gps.pedidosDePermiso, 1);
        expect(ubicaciones.enviadas, hasLength(1));
        expect(c.read(seguimientoDeRutaProvider).rutaId, 8);
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
        expect(ubicaciones.enviadas, hasLength(1));
      });
    });

    test('en segundo plano deja de mandar, y al volver pregunta qué seguir y sigue', () {
      fakeAsync((reloj) {
        final c = arrancar(reloj);
        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();
        expect(ubicaciones.enviadas, hasLength(1));

        seguimiento(c).alCambiarElCiclo(AppLifecycleState.inactive);
        seguimiento(c).alCambiarElCiclo(AppLifecycleState.hidden);
        seguimiento(c).alCambiarElCiclo(AppLifecycleState.paused);
        gps.leer(-17.78, -63.18, 5);
        reloj.elapse(intervaloDelSeguimiento * 3);
        expect(ubicaciones.enviadas, hasLength(1));

        final preguntas = ubicaciones.revisiones;
        seguimiento(c).alCambiarElCiclo(AppLifecycleState.resumed);
        reloj.flushMicrotasks();
        gps.leer(-17.79, -63.19, 5);
        reloj.flushMicrotasks();

        expect(ubicaciones.revisiones, preguntas + 1);
        expect(enviadas(), [-17.77, -17.79]);
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
        expect(enviadas(), [-17.77, -17.78]);
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

    test('si la API dice que ya no hay nada que seguir, se detiene', () {
      fakeAsync((reloj) {
        final c = arrancar(reloj);
        ubicaciones.alEnviar = (_) async => throw sinSeguimiento;

        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();
        gps.leer(-17.78, -63.18, 5);
        reloj.elapse(intervaloDelSeguimiento * 2);

        expect(c.read(seguimientoDeRutaProvider).activo, isFalse);
        expect(ubicaciones.enviadas, hasLength(1));
      });
    });

    test('un envío que falla por la red no lo detiene: manda en la vuelta siguiente', () {
      fakeAsync((reloj) {
        var fallar = true;
        final c = arrancar(reloj);
        ubicaciones.alEnviar = (_) async {
          if (fallar) throw sinConexion;
          return const Seguimiento(rutaEnCurso: 8);
        };
        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();

        fallar = false;
        gps.leer(-17.78, -63.18, 5);
        reloj.elapse(intervaloDelSeguimiento);

        expect(c.read(seguimientoDeRutaProvider).rutaId, 8);
        expect(ubicaciones.enviadas, hasLength(2));
      });
    });

    test('al terminar la ruta sin entregas en ruta, deja de mandar (paso 7.1)', () {
      fakeAsync((reloj) {
        final c = arrancar(reloj);
        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();

        rutas.alPedirDelDia = () async => rutaDePrueba(estado: EstadoRuta.completada);
        seguimiento(c).rutaTerminada();
        reloj.flushMicrotasks();
        gps.leer(-17.78, -63.18, 5);
        reloj.elapse(intervaloDelSeguimiento * 2);

        expect(ubicaciones.enviadas, hasLength(1));
        expect(c.read(seguimientoDeRutaProvider).activo, isFalse);
        expect(c.read(seguimientoDeRutaProvider).ultima, isNull);
      });
    });

    test('sin sesión deja de mandar', () {
      fakeAsync((reloj) {
        final c = arrancar(reloj);
        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();

        seguimiento(c).detener();
        gps.leer(-17.78, -63.18, 5);
        reloj.elapse(intervaloDelSeguimiento * 2);

        expect(ubicaciones.enviadas, hasLength(1));
        expect(c.read(seguimientoDeRutaProvider).ultima, isNull);
      });
    });
  });

  group('las entregas en ruta (tarea 31)', () {
    setUp(() => rutas.alPedirDelDia = () async => null);

    test('con entregas en ruta y sin ruta, manda la primera lectura y después una cada 10 s', () {
      fakeAsync((reloj) {
        ubicaciones.entregasEnRuta = 2;
        final c = arrancar(reloj);
        expect(c.read(seguimientoDeRutaProvider).entregas, 2);
        expect(c.read(seguimientoDeRutaProvider).rutaId, isNull);

        gps.leer(-17.7701, -63.1702, 5);
        reloj.flushMicrotasks();
        gps.leer(-17.7702, -63.1703, 5);
        reloj.elapse(intervaloConEntregas);
        gps.leer(-17.7703, -63.1704, 5);
        reloj.elapse(intervaloConEntregas);

        expect(enviadas(), [-17.7701, -17.7702, -17.7703]);
      });
    });

    test('con la ruta y entregas va cada 10 s; sin entregas en ruta vuelve a 30 s', () {
      fakeAsync((reloj) {
        rutas.alPedirDelDia = () async => rutaDePrueba();
        ubicaciones.entregasEnRuta = 1;
        final c = arrancar(reloj);
        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();
        expect(c.read(seguimientoDeRutaProvider).intervalo, intervaloConEntregas);

        // Se registró la entrega: la respuesta del envío siguiente lo dice.
        ubicaciones.entregasEnRuta = 0;
        gps.leer(-17.78, -63.18, 5);
        reloj.elapse(intervaloConEntregas);
        expect(enviadas(), [-17.77, -17.78]);
        expect(c.read(seguimientoDeRutaProvider).intervalo, intervaloDelSeguimiento);

        gps.leer(-17.79, -63.19, 5);
        reloj.elapse(intervaloConEntregas * 2);
        expect(enviadas(), hasLength(2));
        reloj.elapse(intervaloConEntregas);
        expect(enviadas(), [-17.77, -17.78, -17.79]);
      });
    });

    test('al registrar la última entrega, sin ruta, deja de mandar', () {
      fakeAsync((reloj) {
        ubicaciones.entregasEnRuta = 1;
        final c = arrancar(reloj);
        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();

        ubicaciones.entregasEnRuta = 0;
        gps.leer(-17.78, -63.18, 5);
        reloj.elapse(intervaloConEntregas);
        gps.leer(-17.79, -63.19, 5);
        reloj.elapse(intervaloConEntregas * 3);

        // El segundo envío respondió sin_seguimiento: no hubo un tercero.
        expect(enviadas(), [-17.77, -17.78]);
        expect(c.read(seguimientoDeRutaProvider).activo, isFalse);
      });
    });

    test('al terminar la ruta con entregas en ruta, sigue mandando cada 10 s', () {
      fakeAsync((reloj) {
        rutas.alPedirDelDia = () async => rutaDePrueba();
        ubicaciones.entregasEnRuta = 1;
        final c = arrancar(reloj);
        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();

        rutas.alPedirDelDia = () async => rutaDePrueba(estado: EstadoRuta.completada);
        seguimiento(c).rutaTerminada();
        reloj.flushMicrotasks();
        gps.leer(-17.78, -63.18, 5);
        reloj.elapse(intervaloConEntregas);

        expect(c.read(seguimientoDeRutaProvider).rutaId, isNull);
        expect(c.read(seguimientoDeRutaProvider).entregas, 1);
        expect(enviadas(), [-17.77, -17.78]);
      });
    });

    test('al abrir la bandeja (revisar), empieza a seguir los pedidos que salieron a ruta', () {
      fakeAsync((reloj) {
        final c = arrancar(reloj);
        gps.leer(-17.77, -63.17, 5);
        reloj.elapse(intervaloConEntregas * 2);
        expect(c.read(seguimientoDeRutaProvider).activo, isFalse);
        expect(ubicaciones.enviadas, isEmpty);

        ubicaciones.entregasEnRuta = 1;
        seguimiento(c).revisar();
        reloj.flushMicrotasks();
        gps.leer(-17.78, -63.18, 5);
        reloj.flushMicrotasks();

        expect(c.read(seguimientoDeRutaProvider).entregas, 1);
        expect(enviadas(), [-17.78]);
      });
    });

    test('al volver a la app, también sin nada que seguir, pregunta: si salieron pedidos a ruta, empieza', () {
      fakeAsync((reloj) {
        final c = arrancar(reloj);
        seguimiento(c).alCambiarElCiclo(AppLifecycleState.paused);
        ubicaciones.entregasEnRuta = 1;

        seguimiento(c).alCambiarElCiclo(AppLifecycleState.resumed);
        reloj.flushMicrotasks();
        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();

        expect(c.read(seguimientoDeRutaProvider).entregas, 1);
        expect(ubicaciones.enviadas, hasLength(1));
      });
    });

    test('con solo el permiso de entregar también sigue', () {
      fakeAsync((reloj) {
        ubicaciones.entregasEnRuta = 1;
        final c = arrancar(reloj, permisos: [Permisos.pedidos.registrarEntrega]);

        expect(ubicaciones.revisiones, 1);
        expect(c.read(seguimientoDeRutaProvider).entregas, 1);
      });
    });

    test('"Dar permiso" (pedirPermiso) lo vuelve a pedir y, si lo dan, empieza a mandar', () {
      fakeAsync((reloj) {
        ubicaciones.entregasEnRuta = 1;
        gps.permisoActual = PermisoDeUbicacion.denegado;
        final c = arrancar(reloj);
        expect(c.read(seguimientoDeRutaProvider).problema, ProblemaDelGps.sinPermiso);
        expect(gps.pedidosDePermiso, 0);

        gps.alPedir = PermisoDeUbicacion.concedido;
        seguimiento(c).pedirPermiso();
        reloj.flushMicrotasks();
        gps.leer(-17.77, -63.17, 5);
        reloj.flushMicrotasks();

        expect(gps.pedidosDePermiso, 1);
        expect(c.read(seguimientoDeRutaProvider).problema, isNull);
        expect(ubicaciones.enviadas, hasLength(1));
      });
    });
  });
}
