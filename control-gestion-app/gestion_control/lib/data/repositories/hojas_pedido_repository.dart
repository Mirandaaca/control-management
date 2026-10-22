import 'package:dio/dio.dart';

import '../models/hoja_pedido.dart';
import '../models/pagina.dart';
import '../services/error_de_api.dart';
import '../services/ubicacion.dart';

/// Las hojas de pedido (2.1). Los errores salen como [ErrorDeApi].
class HojasPedidoRepository {
  HojasPedidoRepository(this._api);

  final Dio _api;

  /// Las de su cartera, de la última a la primera. [dia] es un día de Bolivia (aaaa-mm-dd).
  Future<Pagina<HojaPedidoResumen>> listar({String buscar = '', EstadoHP? estado, String? dia, int pagina = 1}) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>(
        '/hojas-pedido',
        queryParameters: {
          if (buscar.trim().isNotEmpty) 'buscar': buscar.trim(),
          'estado': ?estado?.index,
          if (dia != null) ...{'desde': dia, 'hasta': dia},
          'pagina': pagina,
          'tamano': tamanoDePagina,
        },
      );
      return Pagina.fromJson(respuesta.data!, HojaPedidoResumen.fromJson);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar las hojas de pedido.');
    }
  }

  Future<HojaPedido> obtener(int id) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>('/hojas-pedido/$id');
      return HojaPedido.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar la hoja de pedido.');
    }
  }

  /// "Guardar": la HP queda en borrador (7.3.5, ext. 3a).
  Future<HojaPedido> guardar(DatosDeHoja datos, Posicion ubicacion, {required String clave}) =>
      _registrar('/hojas-pedido', datos, ubicacion, clave, 'No se pudo guardar la hoja de pedido.');

  /// "Confirmar venta" en un formulario nuevo: la registra confirmada en un solo paso. Sin stock, no queda nada.
  Future<HojaPedido> confirmarNueva(DatosDeHoja datos, Posicion ubicacion, {required String clave}) =>
      _registrar('/hojas-pedido/confirmadas', datos, ubicacion, clave, 'No se pudo confirmar la venta.');

  Future<HojaPedido> _registrar(
    String ruta,
    DatosDeHoja datos,
    Posicion ubicacion,
    String clave,
    String porDefecto,
  ) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>(
        ruta,
        data: {
          ...datos.aJson(latitud: ubicacion.latitud, longitud: ubicacion.longitud, precision: ubicacion.precision),
          'claveIdempotencia': clave,
        },
      );
      return HojaPedido.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: porDefecto);
    }
  }

  /// Confirma un borrador. Solo su dueño (la regla la revisa la API).
  Future<HojaPedido> confirmar(int id) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>('/hojas-pedido/$id/confirmar');
      return HojaPedido.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo confirmar la venta.');
    }
  }
}
