import 'package:dio/dio.dart';

import '../models/ruta.dart';
import '../services/error_de_api.dart';
import '../services/ubicacion.dart';

/// La ruta del día del promotor (5.4, 7.3.20): verla, iniciarla, marcar las visitas, mandar su ubicación y terminarla.
/// Solo su promotor la recorre: la API lo revisa. Los errores salen como [ErrorDeApi].
class RutasRepository {
  RutasRepository(this._api);

  final Dio _api;

  /// La ruta de hoy, o null si no tiene (ext. 3.1a: la API responde 404 `sin_ruta_del_dia`).
  Future<Ruta?> delDia() async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>('/rutas/del-dia');
      return Ruta.fromJson(respuesta.data!);
    } catch (e) {
      final error = ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar la ruta del día.');
      if (error.codigo == 'sin_ruta_del_dia') return null;
      throw error;
    }
  }

  Future<Ruta> iniciar(int id) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>('/rutas/$id/iniciar');
      return Ruta.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo iniciar la ruta.');
    }
  }

  /// Marca la visita como visitada o ausente, con la ubicación del celular (sin ella no se marca: ext. 5.1a). La hora la
  /// pone el servidor. Devuelve la ruta con la visita marcada.
  Future<Ruta> marcar(
    int id,
    int visitaId, {
    required EstadoVisita resultado,
    required Posicion ubicacion,
    String observacion = '',
  }) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>(
        '/rutas/$id/visitas/$visitaId',
        data: {
          'resultado': resultado.valor,
          'latitud': double.parse(ubicacion.latitud.toStringAsFixed(6)),
          'longitud': double.parse(ubicacion.longitud.toStringAsFixed(6)),
          'observacion': observacion.trim().isEmpty ? null : observacion.trim(),
        },
      );
      return Ruta.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo marcar la visita.');
    }
  }

  /// La posición del promotor, que la app manda sola cada 30 s con la ruta en curso (paso 4.3).
  Future<void> enviarUbicacion(int id, Posicion ubicacion) async {
    try {
      await _api.post<void>(
        '/rutas/$id/ubicaciones',
        data: {
          'latitud': double.parse(ubicacion.latitud.toStringAsFixed(6)),
          'longitud': double.parse(ubicacion.longitud.toStringAsFixed(6)),
        },
      );
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo enviar la ubicación.');
    }
  }

  /// Las visitas sin marcar quedan pendientes (ext. 7.1a).
  Future<Ruta> terminar(int id) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>('/rutas/$id/terminar');
      return Ruta.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo terminar la ruta.');
    }
  }
}
