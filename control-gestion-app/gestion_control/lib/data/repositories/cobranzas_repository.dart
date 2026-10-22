import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../models/cobranza.dart';
import '../models/pagina.dart';
import '../services/camara.dart';
import '../services/error_de_api.dart';

/// Qué cobranzas muestra la lista (7.3.11).
enum FiltroDeCobranzas { porCobrar, vencidas, todas }

/// Las cobranzas de los clientes de la cartera (3.1 y 3.2). Los errores salen como [ErrorDeApi].
class CobranzasRepository {
  CobranzasRepository(this._api);

  final Dio _api;

  /// Por cobrar y vencidas van de la más atrasada a la más reciente; todas, de la última emitida a la primera.
  Future<Pagina<CobranzaResumen>> listar({
    FiltroDeCobranzas filtro = FiltroDeCobranzas.porCobrar,
    int? clienteId,
    String buscar = '',
    int pagina = 1,
  }) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>(
        '/cobranzas',
        queryParameters: {
          if (filtro == FiltroDeCobranzas.porCobrar) 'porCobrar': true,
          if (filtro == FiltroDeCobranzas.vencidas) 'estado': EstadoCobranza.vencida.index,
          'clienteId': ?clienteId,
          if (buscar.trim().isNotEmpty) 'buscar': buscar.trim(),
          'pagina': pagina,
          'tamano': tamanoDePagina,
        },
      );
      return Pagina.fromJson(respuesta.data!, CobranzaResumen.fromJson);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar las cobranzas.');
    }
  }

  Future<ResumenDeCobranzas> resumen({int? clienteId}) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>(
        '/cobranzas/resumen',
        queryParameters: {'clienteId': ?clienteId},
      );
      return ResumenDeCobranzas.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar lo que hay por cobrar.');
    }
  }

  Future<Cobranza> obtener(int id) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>('/cobranzas/$id');
      return Cobranza.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar la cobranza.');
    }
  }

  /// La clave hace que el mismo pago, enviado dos veces, se registre una sola vez.
  Future<PagoRegistrado> registrarPago(int cobranzaId, DatosDePago datos, {required String clave}) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>(
        '/cobranzas/$cobranzaId/pagos',
        data: {...datos.aJson(), 'claveIdempotencia': clave},
      );
      return PagoRegistrado.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo registrar el pago.');
    }
  }

  /// Adjunta la foto del comprobante a un pago; una nueva reemplaza a la anterior.
  Future<Cobranza> subirComprobante(int cobranzaId, int pagoId, FotoTomada foto) async {
    try {
      final respuesta = await _api.put<Map<String, dynamic>>(
        '/cobranzas/$cobranzaId/pagos/$pagoId/comprobante',
        data: FormData.fromMap({'archivo': MultipartFile.fromBytes(foto.bytes, filename: foto.nombre)}),
      );
      return Cobranza.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo subir el comprobante.');
    }
  }

  /// La foto de un comprobante, con la sesión: las fotos no se sirven sin token.
  Future<Uint8List> comprobante(int evidenciaId) async {
    try {
      final respuesta = await _api.get<List<int>>(
        '/evidencias/$evidenciaId/archivo',
        options: Options(responseType: ResponseType.bytes),
      );
      return Uint8List.fromList(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar el comprobante.');
    }
  }
}
