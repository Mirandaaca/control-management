import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'data/repositories/sesion_repository.dart';
import 'data/services/almacen_de_sesion.dart';
import 'data/services/cliente_api.dart';
import 'ui/sesion/sesion_notifier.dart';

// Dónde se arma cada pieza de la app. Las pruebas reemplazan las que necesitan con overrides de Riverpod.

/// La URL base de la API, ya validada: main.dart la fija con `overrideWithValue`.
final urlDeLaApiProvider = Provider<String>((ref) => throw UnimplementedError('Falta la URL de la API.'));

final almacenDeSesionProvider = Provider<AlmacenDeSesion>((ref) => AlmacenDeSesion());

final clienteApiProvider = Provider<Dio>(
  (ref) => crearClienteApi(
    urlDeLaApi: ref.watch(urlDeLaApiProvider),
    almacen: ref.watch(almacenDeSesionProvider),
    // La sesión se busca recién cuando llega el 401: así el cliente no depende de ella al crearse.
    alTerminarLaSesion: () => ref.read(sesionProvider.notifier).terminada(),
  ),
);

final sesionRepositoryProvider = Provider<SesionRepository>(
  (ref) => SesionRepository(ref.watch(clienteApiProvider), ref.watch(almacenDeSesionProvider)),
);

final sesionProvider = NotifierProvider<SesionNotifier, EstadoDeSesion>(SesionNotifier.new);
