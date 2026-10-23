import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'data/repositories/clientes_repository.dart';
import 'data/repositories/cobranzas_repository.dart';
import 'data/repositories/eventos_repository.dart';
import 'data/repositories/hojas_pedido_repository.dart';
import 'data/repositories/pedidos_repository.dart';
import 'data/repositories/perfil_repository.dart';
import 'data/repositories/productos_repository.dart';
import 'data/repositories/sesion_repository.dart';
import 'data/repositories/zonas_repository.dart';
import 'data/services/almacen_de_sesion.dart';
import 'data/services/camara.dart';
import 'data/services/cliente_api.dart';
import 'data/services/conexion.dart';
import 'data/services/ubicacion.dart';
import 'ui/sesion/sesion_notifier.dart';

// Dónde se arma cada pieza de la app. Las pruebas reemplazan las que necesitan con overrides de Riverpod.

/// La URL base de la API, ya validada: main.dart la fija con `overrideWithValue`.
final urlDeLaApiProvider = Provider<String>((ref) => throw UnimplementedError('Falta la URL de la API.'));

final almacenDeSesionProvider = Provider<AlmacenDeSesion>((ref) => AlmacenDeSesion());

final verificadorDeConexionProvider = Provider<VerificadorDeConexion>((ref) => VerificadorConConnectivity());

final servicioDeUbicacionProvider = Provider<ServicioDeUbicacion>((ref) => ServicioConGeolocator());

final clienteApiProvider = Provider<Dio>(
  (ref) => crearClienteApi(
    urlDeLaApi: ref.watch(urlDeLaApiProvider),
    almacen: ref.watch(almacenDeSesionProvider),
    // La sesión se busca recién cuando llega el 401: así el cliente no depende de ella al crearse.
    alTerminarLaSesion: () => ref.read(sesionProvider.notifier).terminada(),
    conexion: ref.watch(verificadorDeConexionProvider),
  ),
);

final sesionRepositoryProvider = Provider<SesionRepository>(
  (ref) => SesionRepository(ref.watch(clienteApiProvider), ref.watch(almacenDeSesionProvider)),
);

final sesionProvider = NotifierProvider<SesionNotifier, EstadoDeSesion>(SesionNotifier.new);

final perfilRepositoryProvider = Provider<PerfilRepository>((ref) => PerfilRepository(ref.watch(clienteApiProvider)));

final productosRepositoryProvider = Provider<ProductosRepository>(
  (ref) => ProductosRepository(ref.watch(clienteApiProvider)),
);

final clientesRepositoryProvider = Provider<ClientesRepository>(
  (ref) => ClientesRepository(ref.watch(clienteApiProvider)),
);

final zonasRepositoryProvider = Provider<ZonasRepository>((ref) => ZonasRepository(ref.watch(clienteApiProvider)));

final hojasPedidoRepositoryProvider = Provider<HojasPedidoRepository>(
  (ref) => HojasPedidoRepository(ref.watch(clienteApiProvider)),
);

final cobranzasRepositoryProvider = Provider<CobranzasRepository>(
  (ref) => CobranzasRepository(ref.watch(clienteApiProvider)),
);

final pedidosRepositoryProvider = Provider<PedidosRepository>(
  (ref) => PedidosRepository(ref.watch(clienteApiProvider)),
);

final eventosRepositoryProvider = Provider<EventosRepository>(
  (ref) => EventosRepository(ref.watch(clienteApiProvider)),
);

final camaraProvider = Provider<Camara>((ref) => CamaraConImagePicker());
