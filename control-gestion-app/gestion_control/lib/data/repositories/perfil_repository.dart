import 'package:dio/dio.dart';

import '../models/perfil.dart';
import '../services/error_de_api.dart';

/// El perfil propio y la contraseña (`/perfil`). Los errores salen como [ErrorDeApi].
class PerfilRepository {
  PerfilRepository(this._api);

  final Dio _api;

  Future<Perfil> obtener() async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>('/perfil');
      return Perfil.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar tu perfil.');
    }
  }

  Future<Perfil> editar({required String nombreCompleto, required String email}) async {
    try {
      final respuesta = await _api.put<Map<String, dynamic>>(
        '/perfil',
        data: {'nombreCompleto': nombreCompleto, 'email': email},
      );
      return Perfil.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron guardar tus datos.');
    }
  }

  /// Al cambiarla, la API cierra las demás sesiones abiertas del usuario; esta sigue.
  Future<void> cambiarContrasena({required String actual, required String nueva}) async {
    try {
      await _api.put<void>('/perfil/contrasena', data: {'actual': actual, 'nueva': nueva});
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cambiar la contraseña.');
    }
  }
}
