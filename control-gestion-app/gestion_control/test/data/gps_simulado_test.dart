import 'package:fake_async/fake_async.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/services/gps_simulado.dart';
import 'package:gestion_control/data/services/ubicacion.dart';
import 'package:gestion_control/domain/navegacion.dart';

import '../ayudantes_de_rutas.dart';

void main() {
  test('recorre el camino cada 10 m, con un desvío de 80 m a un tercio', () {
    final recorrido = recorridoSimulado([desdeCarmen, esquina]);
    final calle7 = Trazado([desdeCarmen, esquina]);
    final inicio = recorrido.length ~/ 3;

    expect(recorrido.length, 1 + 53);
    expect(recorrido.first, desdeCarmen);
    expect(recorrido.last, esquina);
    for (var i = 0; i < recorrido.length; i++) {
      final desviado = i >= inicio && i < inicio + 6;
      expect(calle7.proyectar(recorrido[i]).distancia, closeTo(desviado ? 80 : 0, 0.5), reason: 'lectura $i');
    }
    expect(distanciaEnMetros(recorrido[1], recorrido[2]), closeTo(529.4 / 53, 0.1));
  });

  test('empieza donde está y avanza una lectura por segundo', () {
    fakeAsync((reloj) {
      final gps = GpsSimulado();
      final lecturas = <Posicion>[];
      gps.lecturas().listen(lecturas.add);
      reloj.flushMicrotasks();
      expect(lecturas, hasLength(1));

      gps.recorrer([desdeCarmen, esquina]);
      reloj.elapse(const Duration(seconds: 3));

      expect(lecturas, hasLength(4));
      expect((lecturas[1].latitud, lecturas[1].longitud), (desdeCarmen.latitud, desdeCarmen.longitud));
      expect(lecturas.last.precision, 5);
    });
  });
}
