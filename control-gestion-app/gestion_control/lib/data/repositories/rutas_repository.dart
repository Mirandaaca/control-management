import 'package:dio/dio.dart';

import '../models/camino.dart';
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

  /// El camino por calles desde donde está el promotor hasta el cliente de la visita (tarea 25). Lo calcula la API con
  /// su proveedor de rutas: el celular nunca habla con el proveedor. Si no se puede, la API responde 503
  /// `navegacion_no_disponible` y el promotor sigue el mapa.
  Future<Caminos> camino(int id, int visitaId, {required Posicion desde, required MedioDeTransporte medio}) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>(
        '/rutas/$id/visitas/$visitaId/camino',
        queryParameters: {
          'latitud': desde.latitud.toStringAsFixed(6),
          'longitud': desde.longitud.toStringAsFixed(6),
          'medio': medio.valor,
        },
      );
      return Caminos.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo calcular el camino. Sigue el mapa.');
    }
  }

  /// El orden más corto para lo que le falta al promotor, desde donde está (parte C de la tarea 25): las pendientes y las
  /// [ausentes] por las que vuelve a pasar. No guarda nada.
  Future<OrdenSugerido> ordenSugerido(
    int id, {
    required Posicion desde,
    required MedioDeTransporte medio,
    List<int> ausentes = const [],
  }) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>(
        '/rutas/$id/orden-sugerido',
        queryParameters: {
          'latitud': desde.latitud.toStringAsFixed(6),
          'longitud': desde.longitud.toStringAsFixed(6),
          'medio': medio.valor,
          'ausentes': [for (final a in ausentes) '$a'],
        },
      );
      return OrdenSugerido.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo calcular el orden. Sigue con el que tienes.');
    }
  }

  /// Guarda el orden que el promotor aceptó. Las [ausentesQueVuelven] quedan pendientes otra vez. Si mientras tanto se
  /// marcó una visita, la API responde 409 `ruta_cambio`.
  Future<Ruta> reordenar(int id, {required List<int> visitas, List<int> ausentesQueVuelven = const []}) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>(
        '/rutas/$id/reordenar',
        data: {'visitas': visitas, 'ausentesQueVuelven': ausentesQueVuelven},
      );
      return Ruta.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo reordenar la ruta.');
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
