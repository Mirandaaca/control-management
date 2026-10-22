import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/services/clave_de_idempotencia.dart';

void main() {
  test('la clave es un UUID versión 4, como el Guid de la API', () {
    final clave = ClaveDeIdempotencia().valor;

    expect(clave, matches(RegExp(r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$')));
  });

  test('sin respuesta no se sabe si la API guardó: el reintento repite la clave', () {
    final clave = ClaveDeIdempotencia();
    final primera = clave.valor;

    clave.despuesDelEnvio(huboRespuesta: false);

    expect(clave.valor, primera);
  });

  test('después de una respuesta, de error o de éxito, la próxima clave es otra', () {
    final clave = ClaveDeIdempotencia();
    final primera = clave.valor;

    clave.despuesDelEnvio(huboRespuesta: true);

    expect(clave.valor, isNot(primera));
  });
}
