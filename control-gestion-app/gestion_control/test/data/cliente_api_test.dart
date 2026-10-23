import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/config.dart';
import 'package:gestion_control/data/repositories/sesion_repository.dart';
import 'package:gestion_control/data/services/almacen_de_sesion.dart';
import 'package:gestion_control/data/services/cliente_api.dart';
import 'package:gestion_control/data/services/conexion.dart';
import 'package:gestion_control/data/services/error_de_api.dart';

/// Responde sin red: cada prueba dice qué devuelve la "API" y ve qué se le pidió.
class AdaptadorFalso implements HttpClientAdapter {
  AdaptadorFalso(this.responder);

  final ResponseBody Function(RequestOptions pedido) responder;
  final pedidos = <RequestOptions>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    pedidos.add(options);
    return responder(options);
  }

  @override
  void close({bool force = false}) {}
}

class ConexionFija implements VerificadorDeConexion {
  ConexionFija(this.red);

  final bool red;

  @override
  Future<bool> hayRed() async => red;
}

ResponseBody problema(int estado, Map<String, dynamic> cuerpo) => ResponseBody.fromString(
  jsonEncode(cuerpo),
  estado,
  headers: {
    Headers.contentTypeHeader: ['application/problem+json; charset=utf-8'],
  },
);

void main() {
  late AlmacenDeSesion almacen;
  late int sesionesTerminadas;

  Future<(Dio, AdaptadorFalso)> cliente(
    ResponseBody Function(RequestOptions) responder, {
    String? token,
    bool? hayRed,
  }) async {
    FlutterSecureStorage.setMockInitialValues({'token': ?token});
    almacen = AlmacenDeSesion();
    await almacen.cargar();
    sesionesTerminadas = 0;
    final dio = crearClienteApi(
      urlDeLaApi: 'http://api.prueba/api',
      almacen: almacen,
      alTerminarLaSesion: () => sesionesTerminadas++,
      conexion: hayRed == null ? null : ConexionFija(hayRed),
    );
    final adaptador = AdaptadorFalso(responder);
    dio.httpClientAdapter = adaptador;
    return (dio, adaptador);
  }

  Future<ErrorDeApi> errorDe(Future<Object?> Function() peticion) async {
    try {
      await peticion();
    } catch (e) {
      return ErrorDeApi.desde(e);
    }
    fail('La petición no falló.');
  }

  test('pone el token guardado en cada petición', () async {
    final (dio, adaptador) = await cliente((_) => ResponseBody.fromString('{}', 200), token: 'abc');

    await dio.get('/auth/yo');

    expect(adaptador.pedidos.single.headers['Authorization'], 'Bearer abc');
    expect(adaptador.pedidos.single.uri.toString(), 'http://api.prueba/api/auth/yo');
  });

  test('el detail del ProblemDetails es el mensaje, con su código', () async {
    final (dio, _) = await cliente(
      (_) => problema(409, {'detail': 'Ya existe un usuario con ese correo.', 'codigo': 'correo_duplicado'}),
    );

    final error = await errorDe(() => dio.put('/perfil', data: {}));

    expect(error.mensaje, 'Ya existe un usuario con ese correo.');
    expect(error.codigo, 'correo_duplicado');
    expect(error.estado, 409);
    expect(error.sinConexion, isFalse);
  });

  test('los errores de validación quedan por campo', () async {
    final (dio, _) = await cliente(
      (_) => problema(400, {
        'detail': 'Revisa los datos marcados.',
        'codigo': 'validacion',
        'errors': {
          'email': ['El correo no es válido.'],
          'nueva': ['La contraseña debe tener al menos 8 caracteres.', 'La contraseña debe tener al menos un número.'],
        },
      }),
    );

    final error = await errorDe(() => dio.put('/perfil', data: {}));

    expect(error.campos, {
      'email': 'El correo no es válido.',
      'nueva': 'La contraseña debe tener al menos 8 caracteres. La contraseña debe tener al menos un número.',
    });
    expect(error.mensaje, isNot('Revisa los datos marcados.'));
  });

  test('sin respuesta de la API dice que no se pudo conectar', () async {
    final (dio, _) = await cliente((_) => throw const SocketException('Connection refused'));

    final error = await errorDe(() => dio.get('/perfil'));

    expect(error.mensaje, 'No se pudo conectar con el servidor.');
    expect(error.detalle, isNull);
    expect(error.sinConexion, isTrue);
  });

  test('sin respuesta y sin red en el celular dice que no hay internet', () async {
    final (dio, _) = await cliente((_) => throw const SocketException('Network is unreachable'), hayRed: false);

    final error = await errorDe(() => dio.get('/perfil'));

    expect(error.mensaje, 'No tienes conexión a internet.');
    expect(error.detalle, 'Revisa los datos móviles o el wifi y vuelve a intentarlo.');
    expect(error.sinConexion, isTrue);
  });

  test('con red en el celular, el servidor que no responde no es "sin internet"', () async {
    final (dio, _) = await cliente((_) => throw const SocketException('Connection refused'), hayRed: true);

    expect((await errorDe(() => dio.get('/perfil'))).mensaje, 'No se pudo conectar con el servidor.');
  });

  group('un 401', () {
    ResponseBody sesionTerminada(RequestOptions _) =>
        problema(401, {'detail': 'Tu sesión terminó: vuelve a entrar.', 'codigo': 'sesion_terminada'});

    test('fuera del inicio de sesión avisa que la sesión terminó', () async {
      final (dio, _) = await cliente(sesionTerminada, token: 'abc');

      await errorDe(() => dio.get('/perfil'));

      expect(sesionesTerminadas, 1);
    });

    test('en el inicio de sesión son credenciales incorrectas: no avisa', () async {
      final (dio, _) = await cliente(
        (_) => problema(401, {'detail': 'Credenciales incorrectas.', 'codigo': 'credenciales_incorrectas'}),
      );

      final error = await errorDe(() => dio.post('/auth/login', data: {}));

      expect(error.mensaje, 'Credenciales incorrectas.');
      expect(sesionesTerminadas, 0);
    });

    test('en el cierre de sesión no avisa', () async {
      final (dio, _) = await cliente(sesionTerminada, token: 'abc');

      await errorDe(() => dio.post('/auth/logout'));

      expect(sesionesTerminadas, 0);
    });

    test('de una petición con otro token (una sesión anterior) no saca a la actual', () async {
      final (dio, _) = await cliente(sesionTerminada, token: 'abc');

      await errorDe(() => dio.get('/perfil', options: Options(headers: {'Authorization': 'Bearer viejo'})));

      expect(sesionesTerminadas, 0);
    });
  });

  group('despertar la API', () {
    test('pide /health en la raíz del servidor, fuera de /api', () async {
      final (dio, adaptador) = await cliente((_) => ResponseBody.fromString('Healthy', 200));

      await SesionRepository(dio, almacen).despertar();

      expect(adaptador.pedidos.single.uri.toString(), 'http://api.prueba/health');
    });

    test('si la API no responde, no lanza: el inicio de sesión lo intentará igual', () async {
      final (dio, _) = await cliente(
        (_) => throw DioException.connectionError(requestOptions: RequestOptions(), reason: 'sin servidor'),
      );

      await SesionRepository(dio, almacen).despertar();
    });
  });

  group('leerUrlDeLaApi', () {
    test('agrega /api a la raíz', () {
      expect(leerUrlDeLaApi('http://192.168.6.25:5124'), 'http://192.168.6.25:5124/api');
      expect(leerUrlDeLaApi(' http://192.168.6.25:5124/ '), 'http://192.168.6.25:5124/api');
      expect(leerUrlDeLaApi('http://localhost:5124/api'), 'http://localhost:5124/api');
    });

    test('sin URL, sin esquema o con otro esquema no hay API', () {
      expect(leerUrlDeLaApi(''), isNull);
      expect(leerUrlDeLaApi('192.168.6.25:5124'), isNull);
      expect(leerUrlDeLaApi('ftp://192.168.6.25'), isNull);
    });
  });
}
