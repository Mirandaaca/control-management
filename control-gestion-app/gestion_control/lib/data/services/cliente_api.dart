import 'package:dio/dio.dart';

import 'almacen_de_sesion.dart';

/// La única instancia de dio de la app, como `api/axios.js` en el panel. Los repositorios la reciben; las pantallas
/// nunca la usan directo.
Dio crearClienteApi({
  required String urlDeLaApi,
  required AlmacenDeSesion almacen,
  required void Function() alTerminarLaSesion,
}) {
  final dio = Dio(
    BaseOptions(
      baseUrl: urlDeLaApi,
      // En la calle la señal es variable: sin estos topes, una petición podría quedar esperando sin fin.
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 20),
    ),
  );
  dio.interceptors.add(InterceptorDeSesion(almacen, alTerminarLaSesion));
  return dio;
}

/// Pone el token en cada petición y, si la API responde 401 fuera del inicio y del cierre de sesión, avisa que la
/// sesión terminó: la app vuelve al inicio de sesión con "Tu sesión terminó: vuelve a entrar.".
class InterceptorDeSesion extends Interceptor {
  InterceptorDeSesion(this._almacen, this._alTerminarLaSesion);

  final AlmacenDeSesion _almacen;
  final void Function() _alTerminarLaSesion;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final token = _almacen.token;
    // El cierre de sesión manda su token a mano: para entonces, el token guardado ya se borró.
    if (token != null && !options.headers.containsKey('Authorization')) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    // En el inicio de sesión, un 401 significa "credenciales incorrectas", y en el cierre, que ya estaba cerrada.
    final ruta = err.requestOptions.path;
    final esDeLaSesion = ruta.endsWith('/auth/login') || ruta.endsWith('/auth/logout');
    // Solo cuenta un 401 que trajo la sesión guardada ahora: el de una petición de antes de cerrar sesión, o de una
    // sesión anterior, no debe sacar a la actual.
    final token = _almacen.token;
    final conLaSesionActual = token != null && err.requestOptions.headers['Authorization'] == 'Bearer $token';
    if (err.response?.statusCode == 401 && !esDeLaSesion && conLaSesionActual) {
      _alTerminarLaSesion();
    }
    handler.next(err);
  }
}
