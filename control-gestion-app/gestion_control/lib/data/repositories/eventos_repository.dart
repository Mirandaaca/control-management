import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../models/evento.dart';
import '../models/pagina.dart';
import '../services/camara.dart';
import '../services/error_de_api.dart';
import '../services/ubicacion.dart';

/// Los eventos y sus fotos (4.1 a 4.3). Los errores salen como [ErrorDeApi].
class EventosRepository {
  EventosRepository(this._api);

  final Dio _api;

  /// Los de su cartera, los de sus pedidos asignados y los que registró (7.3.13, ext. 1a), del último al primero.
  Future<Pagina<EventoResumen>> listar({String buscar = '', EstadoEvento? estado, int pagina = 1}) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>(
        '/eventos',
        queryParameters: {
          if (buscar.trim().isNotEmpty) 'buscar': buscar.trim(),
          'estado': ?estado?.index,
          'pagina': pagina,
          'tamano': tamanoDePagina,
        },
      );
      return Pagina.fromJson(respuesta.data!, EventoResumen.fromJson);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar los eventos.');
    }
  }

  Future<List<TipoDeEvento>> tipos() async {
    try {
      final respuesta = await _api.get<List<dynamic>>('/eventos/tipos');
      return [for (final t in respuesta.data!) TipoDeEvento.fromJson(t as Map<String, dynamic>)];
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar los tipos de evento.');
    }
  }

  Future<Evento> obtener(int id) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>('/eventos/$id');
      return Evento.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar el evento.');
    }
  }

  /// Registra el evento pendiente, con la ubicación del GPS. La misma clave devuelve el mismo evento.
  Future<Evento> registrar(DatosDeEvento datos, Posicion ubicacion, {required String clave}) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>(
        '/eventos',
        data: {
          ...datos.aJson(),
          'latitud': double.parse(ubicacion.latitud.toStringAsFixed(6)),
          'longitud': double.parse(ubicacion.longitud.toStringAsFixed(6)),
          'claveIdempotencia': clave,
        },
      );
      return Evento.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo registrar el evento.');
    }
  }

  /// El tipo, el cliente o el pedido y la descripción. La ubicación es la del registro y no cambia.
  Future<Evento> modificar(int id, DatosDeEvento datos) async {
    try {
      final respuesta = await _api.put<Map<String, dynamic>>('/eventos/$id', data: datos.aJson());
      return Evento.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron guardar los cambios del evento.');
    }
  }

  /// Sube una foto (multipart, campo `archivo`), con el lugar y la hora de la toma si salió de la cámara de la app. Una
  /// de la galería va sin ellos: la app no los sabe con certeza.
  Future<Evento> agregarFoto(int id, FotoTomada foto, {Posicion? lugar}) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>(
        '/eventos/$id/fotos',
        data: FormData.fromMap({
          'archivo': MultipartFile.fromBytes(foto.bytes, filename: foto.nombre),
          if (!foto.deLaGaleria) ...{
            'fechaCaptura': foto.tomadaUtc.toIso8601String(),
            if (lugar != null) ...{
              'latitud': lugar.latitud.toStringAsFixed(6),
              'longitud': lugar.longitud.toStringAsFixed(6),
            },
          },
        }),
      );
      return Evento.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo subir la foto.');
    }
  }

  Future<Evento> eliminarFoto(int id, int fotoId) async {
    try {
      final respuesta = await _api.delete<Map<String, dynamic>>('/eventos/$id/fotos/$fotoId');
      return Evento.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo eliminar la foto.');
    }
  }

  /// La foto, con el token.
  Future<Uint8List> archivo(int evidenciaId) async {
    try {
      final respuesta = await _api.get<List<int>>(
        '/evidencias/$evidenciaId/archivo',
        options: Options(responseType: ResponseType.bytes),
      );
      return Uint8List.fromList(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar la foto.');
    }
  }
}
