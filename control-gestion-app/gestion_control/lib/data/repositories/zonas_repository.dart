import 'package:dio/dio.dart';

import '../models/zona.dart';
import '../services/error_de_api.dart';

/// Las zonas de venta, con su promotor y su límite. Los errores salen como [ErrorDeApi].
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
}
