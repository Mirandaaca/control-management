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
}
