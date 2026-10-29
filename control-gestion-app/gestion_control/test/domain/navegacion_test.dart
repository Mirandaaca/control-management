import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/camino.dart';
import 'package:gestion_control/domain/navegacion.dart';

import '../ayudantes_de_rutas.dart';

void main() {
  // El camino de ayudantes_de_rutas.dart: 529 m al este por la Calle 7 y 389 m al norte por la Calle 8.
  final camino = caminosDePrueba().principal;
  final trazado = Trazado(camino.puntos);
  final mitadDeLaCalle7 = (latitud: desdeCarmen.latitud, longitud: (desdeCarmen.longitud + esquina.longitud) / 2);

  group('la distancia y la línea', () {
    test('mide lo que mide cada calle', () {
      expect(distanciaEnMetros(desdeCarmen, esquina), closeTo(529.4, 0.5));
      expect(distanciaEnMetros(esquina, casaDeMarco), closeTo(389.2, 0.5));
      expect(trazado.largo, closeTo(918.6, 1));
    });

    test('sobre la línea, está a 0 m y lleva lo recorrido', () {
      expect(trazado.proyectar(desdeCarmen).distancia, closeTo(0, 0.01));
      expect(trazado.proyectar(mitadDeLaCalle7).avance, closeTo(264.7, 1));
    });

    test('a un costado, la distancia es la de la perpendicular y el avance el del punto más cercano', () {
      final proyeccion = trazado.proyectar(desplazar(mitadDeLaCalle7, norte: 60));

      expect(proyeccion.distancia, closeTo(60, 0.5));
      expect(proyeccion.avance, closeTo(264.7, 1));
    });

    test('pasado el final, el punto más cercano es el final', () {
      final proyeccion = trazado.proyectar(desplazar(casaDeMarco, norte: 20));

      expect(proyeccion.distancia, closeTo(20, 0.5));
      expect(proyeccion.avance, closeTo(trazado.largo, 0.01));
    });
  });

  group('el progreso', () {
    test('al salir, la indicación es el giro de la esquina, y falta todo el camino', () {
      final progreso = progresoEn(camino, trazado, desdeCarmen);

      expect(progreso.proximo, 1);
      expect(progreso.metrosHastaElProximo, closeTo(529.4, 1));
      expect(progreso.metrosQueFaltan, closeTo(918.6, 1));
      expect(progreso.segundosQueFaltan, closeTo(105, 0.01));
    });

    test('en la mitad de la Calle 7, falta la mitad de ese paso más el siguiente', () {
      final progreso = progresoEn(camino, trazado, mitadDeLaCalle7);

      expect(progreso.proximo, 1);
      expect(progreso.metrosHastaElProximo, closeTo(264.7, 1));
      expect(progreso.segundosQueFaltan, closeTo(30 + 45, 0.5));
    });

    test('a 8 m de la esquina ya muestra la indicación siguiente: la llegada', () {
      final progreso = progresoEn(camino, trazado, desplazar(esquina, este: -8));

      expect(progreso.proximo, 2);
      expect(progreso.metrosHastaElProximo, closeTo(389.2 + 8, 1));
    });

    test('a 12 m de la esquina, todavía no', () {
      expect(progresoEn(camino, trazado, desplazar(esquina, este: -12)).proximo, 1);
    });

    test('un camino sin pasos reparte el tiempo por la distancia', () {
      final sinPasos = Camino(distancia: 919, duracion: 100, puntos: camino.puntos, pasos: const []);

      final progreso = progresoEn(sinPasos, trazado, esquina);

      expect(progreso.proximo, isNull);
      expect(progreso.segundosQueFaltan, closeTo(100 * 389.2 / 918.6, 0.5));
    });
  });

  group('el desvío', () {
    test('son dos lecturas seguidas a más de 50 m', () {
      final desvio = DetectorDeDesvio();

      expect(desvio.anotar(60, 5), isFalse);
      expect(desvio.anotar(60, 5), isTrue);
    });

    test('una lectura cerca del camino corta la cuenta', () {
      final desvio = DetectorDeDesvio();

      desvio.anotar(60, 5);
      expect(desvio.anotar(20, 5), isFalse);
      expect(desvio.anotar(60, 5), isFalse);
    });

    test('una lectura con más de 30 m de error no cuenta ni corta la cuenta', () {
      final desvio = DetectorDeDesvio();

      desvio.anotar(60, 5);
      expect(desvio.anotar(60, 45), isFalse);
      expect(desvio.anotar(10, null), isFalse);
      expect(desvio.anotar(60, 5), isTrue);
    });
  });

  group('la llegada', () {
    test('a menos de 30 m del cliente, con una lectura precisa', () {
      expect(llego(desplazar(casaDeMarco, norte: 20), 10, casaDeMarco), isTrue);
      expect(llego(desplazar(casaDeMarco, norte: 20), 40, casaDeMarco), isFalse);
      expect(llego(desplazar(casaDeMarco, norte: 40), 5, casaDeMarco), isFalse);
    });
  });

  group('la voz', () {
    const giro = 'Gira a la izquierda en Calle 8';

    test('en auto, avisa a 200 m con la distancia y a 40 m solo la indicación, una vez cada una', () {
      final anunciador = Anunciador.para(MedioDeTransporte.auto);

      expect(anunciador.anunciar(1, 500, giro), isNull);
      expect(anunciador.anunciar(1, 190, giro), 'En 190 metros, gira a la izquierda en Calle 8');
      expect(anunciador.anunciar(1, 150, giro), isNull);
      expect(anunciador.anunciar(1, 35, giro), giro);
      expect(anunciador.anunciar(1, 10, giro), isNull);
      expect(anunciador.anunciar(2, 380, 'Llegas a Marco Ortiz Peña'), isNull);
    });

    test('a pie, avisa una sola vez, a 30 m', () {
      final anunciador = Anunciador.para(MedioDeTransporte.pie);

      expect(anunciador.anunciar(1, 100, giro), isNull);
      expect(anunciador.anunciar(1, 25, giro), giro);
    });

    test('un paso corto, que empieza ya cerca, dice solo la indicación', () {
      final anunciador = Anunciador.para(MedioDeTransporte.moto);

      expect(anunciador.anunciar(1, 30, giro), giro);
      expect(anunciador.anunciar(1, 20, giro), isNull);
    });

    test('al empezar dice la indicación con su distancia, y no la repite en ese tramo', () {
      final anunciador = Anunciador.para(MedioDeTransporte.auto);

      expect(anunciador.inicial(1, 150, giro), 'En 150 metros, gira a la izquierda en Calle 8');
      expect(anunciador.anunciar(1, 140, giro), isNull);
      expect(anunciador.anunciar(1, 35, giro), giro);
    });

    test('dice la distancia de 10 en 10 metros, y en kilómetros desde el kilómetro', () {
      expect(metrosHablados(3), '10 metros');
      expect(metrosHablados(529), '530 metros');
      expect(metrosHablados(996), '1 kilómetro');
      expect(metrosHablados(1240), '1,2 kilómetros');
      expect(metrosHablados(2000), '2 kilómetros');
    });
  });
}
