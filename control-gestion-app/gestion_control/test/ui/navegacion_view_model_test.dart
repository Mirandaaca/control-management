import 'package:fake_async/fake_async.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/camino.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/domain/navegacion.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/rutas/navegacion_view_model.dart';

import '../ayudantes.dart';
import '../ayudantes_de_rutas.dart';

void main() {
  late RutasRepositoryFalso rutas;
  late UbicacionFalsa gps;
  late PreferenciasFalsas preferencias;
  late VozFalsa voz;
  late PantallaFalsa pantalla;

  setUp(() {
    rutas = RutasRepositoryFalso();
    gps = UbicacionFalsa();
    preferencias = PreferenciasFalsas();
    voz = VozFalsa();
    pantalla = PantallaFalsa();
  });

  // La siguiente de la ruta del 8.6.9: Marco Ortiz Peña, la 4.
  final hastaMarco = (rutaId: 8, visita: rutaDePrueba().siguiente!);
  final navegacion = navegacionProvider(hastaMarco);
  final mitadDeLaCalle7 = (latitud: desdeCarmen.latitud, longitud: (desdeCarmen.longitud + esquina.longitud) / 2);
  final fueraDelCamino = desplazar(mitadDeLaCalle7, norte: 60);
  const sinCamino = ErrorDeApi(
    mensaje: 'No se pudo calcular el camino. Sigue el mapa.',
    codigo: 'navegacion_no_disponible',
    estado: 503,
  );

  /// La navegación abierta, como cuando la pantalla la mira. [antes] corre con el contenedor listo, antes de abrirla.
  (ProviderContainer, ProviderSubscription<EstadoDeLaNavegacion>) abrir(
    FakeAsync reloj, {
    void Function(ProviderContainer c)? antes,
  }) {
    final contenedor = ProviderContainer(
      overrides: [
        rutasRepositoryProvider.overrideWithValue(rutas),
        servicioDeUbicacionProvider.overrideWithValue(gps),
        preferenciasProvider.overrideWithValue(preferencias),
        vozProvider.overrideWithValue(voz),
        pantallaEncendidaProvider.overrideWithValue(pantalla),
      ],
    );
    addTearDown(contenedor.dispose);
    antes?.call(contenedor);
    final suscripcion = contenedor.listen(navegacion, (_, _) {});
    reloj.flushMicrotasks();
    return (contenedor, suscripcion);
  }

  void leer(FakeAsync reloj, PuntoDelCamino punto, {double precision = 5}) {
    gps.leer(punto.latitud, punto.longitud, precision);
    reloj.flushMicrotasks();
  }

  test('con la primera lectura pide el camino en el medio guardado, y la voz dice lo que viene', () {
    fakeAsync((reloj) {
      preferencias.medioGuardado = MedioDeTransporte.pie;
      final (c, _) = abrir(reloj);
      expect(pantalla.encendida, isTrue);
      expect(c.read(navegacion).medio, MedioDeTransporte.pie);
      expect(rutas.caminos, isEmpty);

      leer(reloj, desdeCarmen);

      final pedido = rutas.caminos.single;
      expect((pedido.rutaId, pedido.visitaId, pedido.medio), (8, 4, MedioDeTransporte.pie));
      expect((pedido.desde.latitud, pedido.desde.longitud), (desdeCarmen.latitud, desdeCarmen.longitud));
      final estado = c.read(navegacion);
      expect(estado.calculando, isFalse);
      expect(estado.progreso!.proximo, 1);
      expect(voz.dichos, ['En 530 metros, gira a la izquierda en Calle 8']);
    });
  });

  test('al cerrarse, la pantalla vuelve a su ajuste y la voz se calla', () {
    fakeAsync((reloj) {
      final (_, suscripcion) = abrir(reloj);
      leer(reloj, desdeCarmen);

      suscripcion.close();
      reloj.elapse(const Duration(seconds: 1));

      expect(pantalla.encendida, isFalse);
      expect(voz.calladas, 1);
    });
  });

  test('un desvío de dos lecturas pide un camino nuevo, pero no antes de 20 s del anterior', () {
    fakeAsync((reloj) {
      final (c, _) = abrir(reloj);
      leer(reloj, desdeCarmen);

      leer(reloj, fueraDelCamino);
      leer(reloj, fueraDelCamino);
      expect(rutas.caminos, hasLength(1));

      reloj.elapse(esperaEntreRecalculos);
      leer(reloj, fueraDelCamino);

      expect(rutas.caminos, hasLength(2));
      expect(rutas.caminos.last.desde.latitud, fueraDelCamino.latitud);
      expect(voz.dichos, contains('Recalculando'));
      expect(c.read(navegacion).recalculando, isFalse);
    });
  });

  // En el TECNO, con la ubicación saltando, recalculaba cada 20 s y la voz repetía la primera indicación (29/10).
  test(
    'quieto lejos del camino, recalcula una vez: sin moverse 50 m no lo vuelve a pedir, ni repite la indicación',
    () {
      fakeAsync((reloj) {
        abrir(reloj);
        leer(reloj, desdeCarmen);
        reloj.elapse(esperaEntreRecalculos);
        leer(reloj, fueraDelCamino);
        leer(reloj, fueraDelCamino);
        expect(rutas.caminos, hasLength(2));

        // El camino nuevo (el mismo del falso) sigue a 60 m: quieto, no se pide otro.
        for (var i = 0; i < 3; i++) {
          reloj.elapse(esperaEntreRecalculos);
          leer(reloj, fueraDelCamino);
          leer(reloj, fueraDelCamino);
        }
        expect(rutas.caminos, hasLength(2));
        expect(voz.dichos, ['En 530 metros, gira a la izquierda en Calle 8', 'Recalculando']);

        // Se movió más de 50 m desde donde lo pidió: ahora sí.
        final masAlla = desplazar(fueraDelCamino, este: 60);
        leer(reloj, masAlla);
        leer(reloj, masAlla);
        expect(rutas.caminos, hasLength(3));
      });
    },
  );

  test('las lecturas con más de 30 m de error no cuentan para el desvío', () {
    fakeAsync((reloj) {
      abrir(reloj);
      leer(reloj, desdeCarmen);
      reloj.elapse(esperaEntreRecalculos);

      leer(reloj, fueraDelCamino, precision: 45);
      leer(reloj, fueraDelCamino, precision: 45);
      leer(reloj, fueraDelCamino);
      expect(rutas.caminos, hasLength(1));

      leer(reloj, fueraDelCamino);
      expect(rutas.caminos, hasLength(2));
    });
  });

  test('si el camino nuevo no llega, se queda el anterior y avisa', () {
    fakeAsync((reloj) {
      final (c, _) = abrir(reloj);
      leer(reloj, desdeCarmen);
      final anterior = c.read(navegacion).caminos;
      reloj.elapse(esperaEntreRecalculos);
      rutas.alPedirCamino = (_) async => throw sinCamino;

      leer(reloj, fueraDelCamino);
      leer(reloj, fueraDelCamino);

      final estado = c.read(navegacion);
      expect(rutas.caminos, hasLength(2));
      expect(estado.caminos, same(anterior));
      expect(estado.errorDelRecalculo?.mensaje, 'No se pudo calcular el camino. Sigue el mapa.');
      expect(estado.error, isNull);
    });
  });

  test('sin el primer camino, muestra el error; "Reintentar" espera los 10 s del límite de la API', () {
    fakeAsync((reloj) {
      rutas.alPedirCamino = (_) async => throw sinCamino;
      final (c, _) = abrir(reloj);
      leer(reloj, desdeCarmen);
      expect(c.read(navegacion).error, sinCamino);

      // Otra lectura no lo vuelve a pedir: después de un error, solo con "Reintentar".
      leer(reloj, desdeCarmen);
      rutas.alPedirCamino = null;
      c.read(navegacion.notifier).reintentar();
      reloj.flushMicrotasks();
      expect(rutas.caminos, hasLength(1));
      expect(c.read(navegacion).calculando, isTrue);

      reloj.elapse(esperaEntrePedidos);

      expect(rutas.caminos, hasLength(2));
      expect(c.read(navegacion).caminos, isNotNull);
      expect(c.read(navegacion).error, isNull);
    });
  });

  test('cambiar el medio lo guarda y pide su camino cuando pasan los 10 s', () {
    fakeAsync((reloj) {
      final (c, _) = abrir(reloj);
      leer(reloj, desdeCarmen);

      c.read(navegacion.notifier).elegirMedio(MedioDeTransporte.moto);
      reloj.flushMicrotasks();
      expect(preferencias.medioGuardado, MedioDeTransporte.moto);
      expect(rutas.caminos, hasLength(1));
      expect(c.read(navegacion).calculando, isTrue);

      reloj.elapse(esperaEntrePedidos);

      expect(rutas.caminos.map((p) => p.medio), [MedioDeTransporte.auto, MedioDeTransporte.moto]);
      expect(c.read(navegacion).calculando, isFalse);
    });
  });

  test('al volver a abrirla antes de 10 s del último pedido, espera lo que falta', () {
    fakeAsync((reloj) {
      abrir(reloj, antes: (c) => c.read(ultimoPedidoDeCaminoProvider.notifier).anotar());

      leer(reloj, desdeCarmen);
      expect(rutas.caminos, isEmpty);

      reloj.elapse(esperaEntrePedidos);
      expect(rutas.caminos, hasLength(1));
    });
  });

  test('a menos de 30 m del cliente, llegó: lo dice y ya no recalcula', () {
    fakeAsync((reloj) {
      final (c, _) = abrir(reloj);
      leer(reloj, desdeCarmen);

      leer(reloj, desplazar(casaDeMarco, norte: 10), precision: 8);

      expect(c.read(navegacion).llegaste, isTrue);
      expect(voz.dichos.last, 'Llegaste a Marco Ortiz Peña');
      reloj.elapse(esperaEntreRecalculos);
      leer(reloj, fueraDelCamino);
      leer(reloj, fueraDelCamino);
      expect(rutas.caminos, hasLength(1));
    });
  });

  test('elegir la alternativa sigue su camino: primero al norte', () {
    fakeAsync((reloj) {
      final (c, _) = abrir(reloj);
      leer(reloj, desdeCarmen);

      c.read(navegacion.notifier).elegir(1);

      final estado = c.read(navegacion);
      expect(estado.elegido, 1);
      expect(estado.camino!.duracion, 285);
      expect(estado.progreso!.metrosHastaElProximo, closeTo(389.2, 1));
      expect(voz.dichos.last, 'En 390 metros, gira a la derecha');
    });
  });

  test('silenciada, la voz se calla, se recuerda y no dice nada más', () {
    fakeAsync((reloj) {
      final (c, _) = abrir(reloj);
      leer(reloj, desdeCarmen);

      c.read(navegacion.notifier).silenciar(true);
      reloj.elapse(esperaEntreRecalculos);
      leer(reloj, fueraDelCamino);
      leer(reloj, fueraDelCamino);

      expect(preferencias.silenciada, isTrue);
      expect(voz.calladas, 1);
      expect(voz.dichos, hasLength(1));
    });
  });

  test('sin voz en español, avisa y sigue con el texto', () {
    fakeAsync((reloj) {
      voz.enEspanol = false;
      final (c, _) = abrir(reloj);
      leer(reloj, desdeCarmen);

      expect(c.read(navegacion).sinVoz, isTrue);
      expect(c.read(navegacion).caminos, isNotNull);
      expect(voz.dichos, isEmpty);
    });
  });
}
