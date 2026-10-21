import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// El token de la sesión, guardado cifrado en el celular (Keystore de Android). Queda también en memoria, así el cliente
/// de la API no lee el Keystore en cada petición. Solo se guarda el token: los permisos se piden a la API al abrir la app.
class AlmacenDeSesion {
  AlmacenDeSesion([FlutterSecureStorage? almacen]) : _almacen = almacen ?? const FlutterSecureStorage();

  static const _clave = 'token';
  final FlutterSecureStorage _almacen;
  String? _token;

  String? get token => _token;

  Future<String?> cargar() async {
    try {
      return _token = await _almacen.read(key: _clave);
    } catch (e) {
      // Pasa si Android perdió la llave del Keystore (por ejemplo, al restaurar un respaldo): se entra de nuevo.
      debugPrint('No se pudo leer el token guardado; se borra. $e');
      await borrar();
      return null;
    }
  }

  Future<void> guardar(String token) async {
    _token = token;
    await _almacen.write(key: _clave, value: token);
  }

  Future<void> borrar() async {
    _token = null;
    await _almacen.delete(key: _clave);
  }
}
