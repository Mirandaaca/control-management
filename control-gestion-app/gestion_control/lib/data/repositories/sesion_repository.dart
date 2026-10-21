import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../models/sesion.dart';
import '../services/almacen_de_sesion.dart';
import '../services/error_de_api.dart';

/// Iniciar, consultar y cerrar la sesión en la API (`/auth`). Los errores salen como [ErrorDeApi].
class SesionRepository {
  SesionRepository(this._api, this._almacen);

  // La API guarda desde dónde se abrió cada sesión: 0 es el panel y 1, la app.
  static const _origenApp = 1;

  final Dio _api;
  final AlmacenDeSesion _almacen;

  Future<String?> tokenGuardado() => _almacen.cargar();

  Future<Sesion> iniciar(String email, String password) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>(
        '/auth/login',
        data: {'email': email, 'password': password, 'origen': _origenApp},
      );
      await _almacen.guardar(respuesta.data!['token'] as String);
      return Sesion.fromJson(respuesta.data!['usuario'] as Map<String, dynamic>);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo iniciar sesión.');
    }
  }

  /// La sesión leída de la base, con los permisos al día.
  Future<Sesion> yo() async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>('/auth/yo');
      return Sesion.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar tu sesión.');
    }
  }

  /// Borra el token y cierra la sesión en la API, así ese token deja de servir. El token se borra antes: una petición
  /// que responda 401 por este cierre ya no se toma como una sesión terminada. Si la API no responde, la sesión del
  /// servidor vence sola por inactividad.
  Future<void> cerrar() async {
    final token = _almacen.token;
    await _almacen.borrar();
    if (token == null) return;
    try {
      await _api.post<void>('/auth/logout', options: Options(headers: {'Authorization': 'Bearer $token'}));
    } catch (e) {
      debugPrint('No se pudo cerrar la sesión en el servidor; vencerá por inactividad. $e');
    }
  }

  /// Borra el token de una sesión que la API ya dio por terminada.
  Future<void> olvidar() => _almacen.borrar();
}
