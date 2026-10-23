import 'package:dio/dio.dart';

import '../models/pagina.dart';
import '../models/pedido.dart';
import '../services/error_de_api.dart';
import '../services/ubicacion.dart';

/// Los pedidos (1.1 a 1.3): los de su cartera y los que tiene asignados, la bandeja del día y la entrega. Los errores
/// salen como [ErrorDeApi].
class PedidosRepository {
  PedidosRepository(this._api);

  final Dio _api;

  /// Los de su cartera y los asignados, del último registrado al primero.
  Future<Pagina<PedidoResumen>> listar({
    String buscar = '',
    int? estadoId,
    int? clienteId,
    int pagina = 1,
    int tamano = tamanoDePagina,
  }) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>(
        '/pedidos',
        queryParameters: {
          if (buscar.trim().isNotEmpty) 'buscar': buscar.trim(),
          'estadoId': ?estadoId,
          'clienteId': ?clienteId,
          'pagina': pagina,
          'tamano': tamano,
        },
      );
      return Pagina.fromJson(respuesta.data!, PedidoResumen.fromJson);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar los pedidos.');
    }
  }

  Future<List<EstadoPedido>> estados() async {
    try {
      final respuesta = await _api.get<List<dynamic>>('/pedidos/estados');
      return [for (final e in respuesta.data!) EstadoPedido.fromJson(e as Map<String, dynamic>)];
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar los estados del pedido.');
    }
  }

  /// La bandeja del día (1.3): los que tiene asignados para hoy, sin los entregados ni los anulados, por hora.
  Future<List<PedidoResumen>> bandeja() async {
    try {
      final respuesta = await _api.get<List<dynamic>>('/pedidos/bandeja');
      return [for (final p in respuesta.data!) PedidoResumen.fromJson(p as Map<String, dynamic>)];
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar la bandeja.');
    }
  }

  Future<Pedido> obtener(int id) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>('/pedidos/$id');
      return Pedido.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar el pedido.');
    }
  }

  /// Registra el pedido pendiente. La misma clave devuelve el mismo pedido.
  Future<Pedido> registrar(DatosDePedido datos, {required String clave}) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>('/pedidos', data: datos.aJson(clave: clave));
      return Pedido.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo registrar el pedido.');
    }
  }

  /// La entrega (1.2): crea y confirma la venta con los productos del pedido. Solo el promotor asignado y en ruta.
  Future<Pedido> entregar(int id, DatosDeEntrega datos, Posicion ubicacion) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>(
        '/pedidos/$id/entregar',
        data: datos.aJson(latitud: ubicacion.latitud, longitud: ubicacion.longitud, precision: ubicacion.precision),
      );
      return Pedido.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo registrar la entrega.');
    }
  }
}
