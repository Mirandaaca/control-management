import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/ruta.dart';
import 'package:gestion_control/data/repositories/clientes_repository.dart';
import 'package:gestion_control/data/repositories/rutas_repository.dart';
import 'package:gestion_control/data/repositories/zonas_repository.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/data/services/ubicacion.dart';

import '../ayudantes_de_rutas.dart';

/// Responde sin red lo que cada prueba diga, y anota lo que se le pidió.
class _ApiFalsa implements HttpClientAdapter {
  _ApiFalsa(this.responder);

  final ResponseBody Function(RequestOptions pedido) responder;
  final pedidos = <RequestOptions>[];

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancel) async {
    pedidos.add(options);
    return responder(options);
  }

  @override
  void close({bool force = false}) {}
}

ResponseBody _json(Object? cuerpo, {int estado = 200}) => ResponseBody.fromString(
  jsonEncode(cuerpo),
  estado,
  headers: {
    Headers.contentTypeHeader: ['application/json; charset=utf-8'],
  },
);

(Dio, _ApiFalsa) _api(ResponseBody Function(RequestOptions) responder) {
  final dio = Dio(BaseOptions(baseUrl: 'http://api.prueba/api'));
  final api = _ApiFalsa(responder);
  dio.httpClientAdapter = api;
  return (dio, api);
}

void main() {
  group('la ruta del día', () {
    test('lee la ruta del 8.6.9: el orden, el resultado, el atraso y la siguiente', () async {
      final (dio, api) = _api((_) => _json(rutaJson()));

      final ruta = (await RutasRepository(dio).delDia())!;

      expect(api.pedidos.single.path, '/rutas/del-dia');
      expect(ruta.numero, 'RUT-2026-00008');
      expect(ruta.estado, EstadoRuta.enCurso);
      expect(ruta.zonaNombre, 'Zona Norte');
      expect((ruta.marcadas, ruta.total, ruta.pendientes), (3, 5, 2));
      expect(ruta.visitas.map((v) => v.resultado), [
        EstadoVisita.visitado,
        EstadoVisita.ausente,
        EstadoVisita.visitado,
        EstadoVisita.pendiente,
        EstadoVisita.pendiente,
      ]);
      expect(ruta.siguiente!.cliente.nombreCompleto, 'Marco Ortiz Peña');
      expect(ruta.siguiente!.diasDeAtraso, 9);
    });

    test('sin ruta hoy (404 sin_ruta_del_dia) no es un error: es null', () async {
      final (dio, _) = _api(
        (_) => _json({'detail': 'No tienes una ruta para hoy.', 'codigo': 'sin_ruta_del_dia'}, estado: 404),
      );

      expect(await RutasRepository(dio).delDia(), isNull);
    });

    test('otro error sí sale como ErrorDeApi', () async {
      final (dio, _) = _api((_) => _json({'detail': 'Falló.', 'codigo': 'error_interno'}, estado: 500));

      expect(RutasRepository(dio).delDia(), throwsA(isA<ErrorDeApi>().having((e) => e.mensaje, 'mensaje', 'Falló.')));
    });
  });

  test('marcar manda el resultado, la ubicación con 6 decimales y la observación sin espacios', () async {
    final (dio, api) = _api((_) => _json(rutaJson()));

    await RutasRepository(dio).marcar(
      8,
      2,
      resultado: EstadoVisita.ausente,
      ubicacion: const Posicion(-17.76893012, -63.17945549, 10),
      observacion: '  El local estaba cerrado.  ',
    );

    final pedido = api.pedidos.single;
    expect((pedido.method, pedido.path), ('POST', '/rutas/8/visitas/2'));
    expect(pedido.data, {
      'resultado': 2,
      'latitud': -17.76893,
      'longitud': -63.179455,
      'observacion': 'El local estaba cerrado.',
    });
  });

  test('marcar sin observación la manda null', () async {
    final (dio, api) = _api((_) => _json(rutaJson()));

    await RutasRepository(dio).marcar(8, 4, resultado: EstadoVisita.visitado, ubicacion: const Posicion(-17, -63, 5));

    expect((api.pedidos.single.data as Map)['observacion'], isNull);
    expect((api.pedidos.single.data as Map)['resultado'], 1);
  });

  test('la ubicación del promotor va a /rutas/{id}/ubicaciones', () async {
    final (dio, api) = _api((_) => ResponseBody.fromString('', 204));

    await RutasRepository(dio).enviarUbicacion(8, const Posicion(-17.7725, -63.1712, 6));

    expect((api.pedidos.single.method, api.pedidos.single.path), ('POST', '/rutas/8/ubicaciones'));
    expect(api.pedidos.single.data, {'latitud': -17.7725, 'longitud': -63.1712});
  });

  test('iniciar y terminar devuelven la ruta', () async {
    final (dio, api) = _api((p) => _json(rutaJson(estado: p.path.endsWith('iniciar') ? 1 : 2)));
    final rutas = RutasRepository(dio);

    expect((await rutas.iniciar(8)).estado, EstadoRuta.enCurso);
    expect((await rutas.terminar(8)).estado, EstadoRuta.completada);
    expect(api.pedidos.map((p) => p.path), ['/rutas/8/iniciar', '/rutas/8/terminar']);
  });

  test('el mapa de clientes trae el estado, la zona y las visitas', () async {
    final (dio, _) = _api(
      (_) => _json([
        {
          'id': 68,
          'codigo': 'CLI-00068',
          'nombreCompleto': 'Carmen Rojas Saucedo',
          'latitud': -17.7765,
          'longitud': -63.174,
          'telefono': '70010068',
          'direccionTexto': 'Calle 7 N.º 35',
          'zonaId': 1,
          'ultimaVisita': '2026-10-30T14:05:00',
          'proximaVisita': '2026-11-14T14:05:00',
          'estado': {'id': 1, 'nombre': 'Activo', 'color': '#DC2626', 'delSistema': true},
        },
      ]),
    );

    final carmen = (await ClientesRepository(dio).mapa()).single;

    expect((carmen.zonaId, carmen.estado.id, carmen.proximaVisita), (1, 1, '2026-11-14T14:05:00'));
  });

  test('los clientes por visitar mandan los filtros y leen el atraso', () async {
    final (dio, api) = _api(
      (_) => _json({
        'items': [
          {
            'id': 61,
            'codigo': 'CLI-00061',
            'nombreCompleto': 'Elena Gutiérrez Vaca',
            'direccionTexto': 'Calle 2 N.º 88',
            'latitud': -17.77,
            'longitud': -63.18,
            'zona': {'id': 1, 'nombre': 'Zona Norte'},
            'promotor': {'id': 'u1', 'nombreCompleto': 'Carlos Mendoza Flores'},
            'ultimaVisita': '2026-10-02T14:00:00',
            'proximaVisita': '2026-10-17T14:00:00',
            'diasDeAtraso': 14,
          },
        ],
        'total': 1,
        'numeroPagina': 1,
        'tamano': 25,
      }),
    );

    final pagina = await ClientesRepository(dio).porVisitar(buscar: ' Elena ', zonaId: 1);

    expect(api.pedidos.single.path, '/clientes/por-visitar');
    expect(api.pedidos.single.queryParameters, {'buscar': 'Elena', 'zonaId': 1, 'pagina': 1, 'tamano': 25});
    expect(pagina.items.single.diasDeAtraso, 14);
    expect(pagina.items.single.promotor, 'Carlos Mendoza Flores');
  });

  test('los puntos de referencia de una zona', () async {
    final (dio, api) = _api(
      (_) => _json([
        {
          'id': 1,
          'zona': {'id': 1, 'nombre': 'Zona Norte'},
          'nombre': 'Mercado Norte',
          'descripcion': null,
          'latitud': -17.772,
          'longitud': -63.177,
        },
      ]),
    );

    final punto = (await ZonasRepository(dio).puntos(zonaId: 1)).single;

    expect(api.pedidos.single.queryParameters, {'zonaId': 1});
    expect((punto.zonaId, punto.nombre), (1, 'Mercado Norte'));
  });
}
