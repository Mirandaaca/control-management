import 'package:dio/dio.dart';

import '../models/producto.dart';
import '../services/error_de_api.dart';

/// El catálogo de productos (2.3). Los errores salen como [ErrorDeApi].
class ProductosRepository {
  ProductosRepository(this._api);

  final Dio _api;

  /// Los productos activos, todos juntos y ordenados por nombre: son pocos, y se buscan en la lista que ya llegó.
  Future<List<Producto>> listar() async {
    try {
      final respuesta = await _api.get<List<dynamic>>('/productos/opciones');
      return respuesta.data!.map((p) => Producto.fromJson(p as Map<String, dynamic>)).toList();
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar los productos.');
    }
  }
}
