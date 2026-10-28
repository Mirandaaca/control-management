import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/ui/territorio/marcadores.dart';
import 'package:latlong2/latlong.dart';

import '../ayudantes.dart';

void main() {
  group('el nombre de la zona en el mapa', () {
    final centro = puntoDe(-17.775, -63.18);

    test('sin nada que evitar, va en el centro del límite, como en el mapa de clientes', () {
      expect(lugarDelNombre(zonaNorte.limite!, const []), centro);
    });

    test('con las paradas cerca del centro, se corre a donde no las tapa', () {
      // Las de la ruta de Carlos en datos-ejemplo.md: Rosa, Juan y Elena, a unas cuadras del centro de la Zona Norte.
      final paradas = [
        puntoDe(-17.783412, -63.182106),
        puntoDe(-17.779620, -63.178850),
        puntoDe(-17.776050, -63.180900),
      ];

      final lugar = lugarDelNombre(zonaNorte.limite!, paradas);

      expect(lugar, isNot(centro));
      // Más lejos de la parada más cercana de lo que estaba en el centro.
      double distancia(LatLng a, LatLng b) => (a.latitude - b.latitude).abs() + (a.longitude - b.longitude).abs();
      double masCercana(LatLng p) => paradas.map((m) => distancia(p, m)).reduce((a, b) => a < b ? a : b);
      expect(masCercana(lugar), greaterThan(masCercana(centro) * 5));
      // Y sigue dentro de la zona.
      expect(lugar.latitude, inInclusiveRange(-17.790, -17.760));
      expect(lugar.longitude, inInclusiveRange(-63.195, -63.165));
    });

    test('las paradas lejos del centro no lo mueven', () {
      final paradas = [puntoDe(-17.761, -63.194), puntoDe(-17.789, -63.166)];

      expect(lugarDelNombre(zonaNorte.limite!, paradas), centro);
    });
  });
}
