import 'package:dio/dio.dart';

import '../models/hoja_pedido.dart';
import '../services/error_de_api.dart';

/// Los usuarios, solo para elegir un promotor en un filtro. Los errores salen como [ErrorDeApi].
class UsuariosRepository {
  UsuariosRepository(this._api);

  final Dio _api;

  /// Los promotores activos, con su id y su nombre. Lo pide quien tiene uno de los permisos de `eligenPromotor`.
  Future<List<Referencia>> promotores() async {
    try {
      final respuesta = await _api.get<List<dynamic>>('/usuarios/promotores');
      return [for (final p in respuesta.data!) Referencia.fromJson(p as Map<String, dynamic>)];
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar los promotores.');
    }
  }
}
