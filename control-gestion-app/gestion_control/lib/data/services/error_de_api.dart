import 'package:dio/dio.dart';

import 'cliente_api.dart';

/// Un error de la API traducido para el usuario, como `mensajeDeError` y `erroresPorCampo` del panel. La API responde
/// todo error con ProblemDetails: `{ title, detail, codigo, traceId, errors }`.
class ErrorDeApi implements Exception {
  const ErrorDeApi({
    required this.mensaje,
    this.codigo,
    this.estado,
    this.campos = const {},
    this.sinConexion = false,
    this.detalle,
  });

  factory ErrorDeApi.desde(Object error, {String porDefecto = 'Ocurrió un error.'}) {
    if (error is ErrorDeApi) return error;
    if (error is! DioException) return ErrorDeApi(mensaje: porDefecto);

    final respuesta = error.response;
    if (respuesta == null) {
      return error.requestOptions.extra[InterceptorDeConexion.sinRed] == true ? sinInternet : sinServidor;
    }

    final datos = respuesta.data;
    final cuerpo = datos is Map<String, dynamic> ? datos : const <String, dynamic>{};
    final campos = <String, String>{};
    if (cuerpo['errors'] case final Map<String, dynamic> errores) {
      errores.forEach((campo, mensajes) => campos[campo] = mensajes is List ? mensajes.join(' ') : '$mensajes');
    }
    // Con errores por campo se muestran ellos: el detail de la validación solo dice "Revisa los datos marcados."
    final detail = cuerpo['detail'];
    return ErrorDeApi(
      mensaje: campos.isNotEmpty ? campos.values.join(' ') : (detail is String ? detail : porDefecto),
      codigo: cuerpo['codigo'] as String?,
      estado: respuesta.statusCode,
      campos: campos,
    );
  }

  /// El celular no tiene red (8.6.12). Como todo error sin respuesta, la pantalla ofrece "Reintentar".
  static const sinInternet = ErrorDeApi(
    mensaje: 'No tienes conexión a internet.',
    detalle: 'Revisa los datos móviles o el wifi y vuelve a intentarlo.',
    sinConexion: true,
  );

  /// El celular tiene red y el servidor no respondió: una red débil o el servidor despertando. Dice qué hacer, como el
  /// panel desde la tarea 22; la pantalla ofrece "Reintentar".
  static const sinServidor = ErrorDeApi(
    mensaje: 'No se pudo conectar con el servidor.',
    detalle: 'Espera un momento y vuelve a intentarlo.',
    sinConexion: true,
  );

  /// El texto para el usuario.
  final String mensaje;

  /// Qué hacer, cuando el mensaje solo no alcanza: con él, el mensaje va como título del aviso (8.6.12).
  final String? detalle;

  /// El código estable de la API, en snake_case (por ejemplo, `correo_duplicado`).
  final String? codigo;

  final int? estado;

  /// Los errores de validación, por campo y con las claves en camelCase: van junto a su campo.
  final Map<String, String> campos;

  /// La API no respondió: la pantalla ofrece "Reintentar". Tampoco se sabe si llegó a guardar lo enviado, así que un
  /// reintento repite la misma clave de idempotencia.
  final bool sinConexion;

  /// Los errores por campo, más los de reglas de negocio que pertenecen a un campo (por ejemplo, `correo_duplicado` va
  /// en `email`): así cada uno se muestra junto a su campo, como `CAMPO_DEL_ERROR` en el panel.
  Map<String, String> camposCon(Map<String, String> campoDelCodigo) => {...campos, ?campoDelCodigo[codigo]: mensaje};

  @override
  String toString() => mensaje;
}
