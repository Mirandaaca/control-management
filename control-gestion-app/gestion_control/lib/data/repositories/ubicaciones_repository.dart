import 'package:dio/dio.dart';

import '../models/seguimiento.dart';
import '../services/error_de_api.dart';
import '../services/ubicacion.dart';

/// La ubicación que la app manda sola mientras el promotor tiene entregas en ruta o su ruta en curso (tarea 31; 7.3.20,
/// paso 4.3). La oficina la ve en "Entregas de hoy" y en Rutas. Los errores salen como [ErrorDeApi].
class UbicacionesRepository {
  UbicacionesRepository(this._api);

  final Dio _api;

  /// Lo que la API sigue del promotor: su ruta de hoy en curso y cuántas entregas tiene en ruta.
  Future<Seguimiento> seguimiento() async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>('/ubicaciones/seguimiento');
      return Seguimiento.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo revisar si la oficina sigue tu ubicación.');
    }
  }

  /// Manda la posición del promotor, y devuelve lo que la API sigue. Si ya no sigue nada (se registró la última entrega,
  /// por ejemplo), responde 409 `sin_seguimiento`.
  Future<Seguimiento> enviar(Posicion ubicacion) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>(
        '/ubicaciones',
        data: {
          'latitud': double.parse(ubicacion.latitud.toStringAsFixed(6)),
          'longitud': double.parse(ubicacion.longitud.toStringAsFixed(6)),
        },
      );
      return Seguimiento.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo enviar la ubicación.');
    }
  }
}
