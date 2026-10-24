import 'package:dio/dio.dart';

import '../models/zona.dart';
import '../services/error_de_api.dart';

/// Las zonas de venta, con su promotor y su límite, y sus puntos de referencia (5.5). Los errores salen como
/// [ErrorDeApi].
class ZonasRepository {
  ZonasRepository(this._api);

  final Dio _api;

  Future<List<Zona>> listar() async {
    try {
      final respuesta = await _api.get<List<dynamic>>('/zonas');
      return [for (final z in respuesta.data!) Zona.fromJson(z as Map<String, dynamic>)];
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar las zonas.');
    }
  }

  Future<Zona> obtener(int id) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>('/zonas/$id');
      return Zona.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar la zona.');
    }
  }

  /// Los puntos de referencia de una zona, o de todas.
  Future<List<PuntoDeReferencia>> puntos({int? zonaId}) async {
    try {
      final respuesta = await _api.get<List<dynamic>>('/puntos-referencia', queryParameters: {'zonaId': ?zonaId});
      return [for (final p in respuesta.data!) PuntoDeReferencia.fromJson(p as Map<String, dynamic>)];
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar los puntos de referencia.');
    }
  }
}
