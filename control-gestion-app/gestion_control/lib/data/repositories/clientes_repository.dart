import 'package:dio/dio.dart';

import '../models/cliente.dart';
import '../models/pagina.dart';
import '../services/error_de_api.dart';
import '../services/ubicacion.dart';

/// Los clientes de la cartera (2.2 y 2.4). Los errores salen como [ErrorDeApi].
class ClientesRepository {
  ClientesRepository(this._api);

  final Dio _api;

  /// Cuántos clientes trae la búsqueda de un selector, como el `Combobox` del panel.
  static const limiteDeOpciones = 50;

  Future<Pagina<ClienteResumen>> listar({String buscar = '', int pagina = 1}) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>(
        '/clientes',
        queryParameters: {
          if (buscar.trim().isNotEmpty) 'buscar': buscar.trim(),
          'pagina': pagina,
          'tamano': tamanoDePagina,
        },
      );
      return Pagina.fromJson(respuesta.data!, ClienteResumen.fromJson);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar los clientes.');
    }
  }

  /// Para elegir un cliente: los primeros que coinciden, también los inactivos, que se muestran deshabilitados.
  Future<List<ClienteOpcion>> opciones(String buscar) async {
    try {
      final respuesta = await _api.get<List<dynamic>>(
        '/clientes/opciones',
        queryParameters: {
          if (buscar.trim().isNotEmpty) 'buscar': buscar.trim(),
          'limite': limiteDeOpciones,
          'incluirInactivos': true,
        },
      );
      return [for (final c in respuesta.data!) ClienteOpcion.fromJson(c as Map<String, dynamic>)];
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron buscar los clientes.');
    }
  }

  /// El cliente de la cartera con ese celular, o null. La API compara el celular exacto al registrar: aquí también.
  Future<ClienteResumen?> conCelular(String telefono) async {
    final buscado = telefono.trim();
    final encontrados = await listar(buscar: buscado);
    for (final cliente in encontrados.items) {
      if (cliente.telefono.trim() == buscado) return cliente;
    }
    return null;
  }

  /// Los clientes con ubicación de su cartera, con su estado y sus visitas (5.1 y 5.2). Los de un estado asignado a
  /// mano también: se ven con su color (7.3.18, ext. \*c).
  Future<List<ClienteEnElMapa>> mapa() async {
    try {
      final respuesta = await _api.get<List<dynamic>>('/clientes/mapa');
      return [for (final c in respuesta.data!) ClienteEnElMapa.fromJson(c as Map<String, dynamic>)];
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar el mapa de clientes.');
    }
  }

  /// Los clientes por visitar (5.3): primero los que nunca se visitaron y después del más atrasado al más reciente.
  Future<Pagina<ClientePorVisitar>> porVisitar({
    String buscar = '',
    int? zonaId,
    String? promotorId,
    int pagina = 1,
  }) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>(
        '/clientes/por-visitar',
        queryParameters: {
          if (buscar.trim().isNotEmpty) 'buscar': buscar.trim(),
          'zonaId': ?zonaId,
          'promotorId': ?promotorId,
          'pagina': pagina,
          'tamano': tamanoDePagina,
        },
      );
      return Pagina.fromJson(respuesta.data!, ClientePorVisitar.fromJson);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron cargar los clientes por visitar.');
    }
  }

  Future<Cliente> obtener(int id) async {
    try {
      final respuesta = await _api.get<Map<String, dynamic>>('/clientes/$id');
      return Cliente.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo cargar el cliente.');
    }
  }

  Future<Cliente> registrar(
    DatosDeCliente datos,
    Posicion ubicacion, {
    required String clave,
    bool confirmarTelefonoRepetido = false,
  }) async {
    try {
      final respuesta = await _api.post<Map<String, dynamic>>(
        '/clientes',
        data: {
          ...datos.aJson(latitud: ubicacion.latitud, longitud: ubicacion.longitud),
          'confirmarTelefonoRepetido': confirmarTelefonoRepetido,
          'claveIdempotencia': clave,
        },
      );
      return Cliente.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudo registrar el cliente.');
    }
  }

  Future<Cliente> editar(
    int id,
    DatosDeCliente datos,
    Posicion ubicacion, {
    bool confirmarTelefonoRepetido = false,
  }) async {
    try {
      final respuesta = await _api.put<Map<String, dynamic>>(
        '/clientes/$id',
        data: {
          ...datos.aJson(latitud: ubicacion.latitud, longitud: ubicacion.longitud),
          'confirmarTelefonoRepetido': confirmarTelefonoRepetido,
        },
      );
      return Cliente.fromJson(respuesta.data!);
    } catch (e) {
      throw ErrorDeApi.desde(e, porDefecto: 'No se pudieron guardar los cambios del cliente.');
    }
  }
}
