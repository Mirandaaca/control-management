import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/ui/core/formato.dart';

void main() {
  group('formatearFecha', () {
    test('una fecha de la API sin "Z" se toma como UTC y sale en la hora de Bolivia', () {
      expect(formatearFecha('2026-10-21T18:30:00', hora: true), '21/10/2026 14:30');
      expect(formatearFecha('2026-10-21T18:30:00'), '21/10/2026');
    });

    test('una fecha con "Z" o con desfase también sale en la hora de Bolivia', () {
      expect(formatearFecha('2026-10-21T18:30:00Z', hora: true), '21/10/2026 14:30');
      expect(formatearFecha('2026-10-21T14:30:00-04:00', hora: true), '21/10/2026 14:30');
    });

    test('las 03:30 UTC son las 23:30 del día anterior en Bolivia', () {
      expect(formatearFecha('2026-10-22T03:30:00', hora: true), '21/10/2026 23:30');
      expect(formatearFecha('2026-10-22T03:30:00'), '21/10/2026');
    });

    test('acepta los 7 decimales de los segundos que manda .NET', () {
      expect(formatearFecha('2026-10-21T18:30:00.1234567', hora: true), '21/10/2026 14:30');
    });

    test('sin fecha, o con una que no se entiende, devuelve una raya', () {
      expect(formatearFecha(null), '—');
      expect(formatearFecha(''), '—');
      expect(formatearFecha('no es una fecha'), '—');
    });
  });

  group('montos y cantidades, como en Bolivia', () {
    test('un monto lleva Bs, punto de miles, coma decimal y dos decimales', () {
      expect(formatearMonto(240), 'Bs 240,00');
      expect(formatearMonto(16.5), 'Bs 16,50');
      expect(formatearMonto(1234.5), 'Bs 1.234,50');
      expect(formatearMonto(1069), 'Bs 1.069,00');
      expect(formatearMonto(1234567.891), 'Bs 1.234.567,89');
    });

    test('la suma de montos con decimales no deja restos de redondeo', () {
      expect(formatearMonto(0.1 + 0.2), 'Bs 0,30');
      expect(formatearMonto(4 * (38 - 1) + 6 * 16.5 - 7), 'Bs 240,00');
    });

    test('una cantidad va sin ceros de más', () {
      expect(formatearCantidad(120), '120');
      expect(formatearCantidad(12.5), '12,5');
      expect(formatearCantidad(1200), '1.200');
      expect(formatearCantidad(0.25), '0,25');
    });

    test('una coordenada lleva seis decimales con coma', () {
      expect(formatearCoordenada(-17.783412), '-17,783412');
      expect(formatearCoordenada(-63.18), '-63,180000');
    });

    test('lo escrito se lee con coma o con punto decimal', () {
      expect(leerNumero('140,00'), 140);
      expect(leerNumero('12.5'), 12.5);
      expect(leerNumero(' 7 '), 7);
      expect(leerNumero(''), isNull);
      expect(leerNumero('abc'), isNull);
    });

    test('en centavos, 140,00 y 139,999… son lo mismo', () {
      expect(centavos(140), centavos(139.99999999));
      expect(centavos(150) > centavos(140), isTrue);
    });
  });
}
