import 'dart:math';

/// La clave de idempotencia de un formulario (un cliente, una venta, un pago), como `claveIdempotencia` en la API: si
/// el mismo envío llega dos veces, la API lo registra una sola vez.
///
/// Se repite solo cuando no se sabe qué pasó con el envío anterior: sin respuesta o con el tiempo agotado, la API pudo
/// haberlo guardado. Después de una respuesta, de error o de éxito, la próxima es otra: así, si la venta falló por stock
/// y el promotor corrige la línea, el reintento sí se registra.
class ClaveDeIdempotencia {
  ClaveDeIdempotencia({Random? azar}) : _azar = azar ?? Random.secure() {
    _valor = _nueva();
  }

  final Random _azar;
  late String _valor;

  String get valor => _valor;

  /// Después de enviar: con respuesta, la próxima clave es otra; sin respuesta, se repite.
  void despuesDelEnvio({required bool huboRespuesta}) {
    if (huboRespuesta) _valor = _nueva();
  }

  // Un UUID versión 4, que es lo que la API recibe como Guid.
  String _nueva() {
    final bytes = List<int>.generate(16, (_) => _azar.nextInt(256));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-'
        '${hex.substring(16, 20)}-${hex.substring(20)}';
  }
}
