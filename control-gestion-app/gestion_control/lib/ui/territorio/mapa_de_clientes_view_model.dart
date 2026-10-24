import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cliente.dart';
import '../../data/models/zona.dart';
import '../../data/services/error_de_api.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../sesion/sesion_notifier.dart';

const _igual = Object();

class EstadoDelMapaDeClientes {
  const EstadoDelMapaDeClientes({
    this.cargado = false,
    this.zonas = const [],
    this.puntos = const [],
    this.clientes = const [],
    this.zonaId,
    this.clienteId,
    this.error,
    this.reintentando = false,
  });

  final bool cargado;

  /// Las zonas que el usuario ve: sin cartera completa, solo las suyas (7.3.17, ext. 1a).
  final List<Zona> zonas;
  final List<PuntoDeReferencia> puntos;

  /// Los clientes de su cartera con ubicación (la API ya filtra).
  final List<ClienteEnElMapa> clientes;

  /// La zona elegida; null es "todas".
  final int? zonaId;
  final int? clienteId;
  final ErrorDeApi? error;
  final bool reintentando;

  Zona? get zona => zonas.where((z) => z.id == zonaId).firstOrNull;

  /// Los clientes que se ven: los de la zona elegida, o todos.
  List<ClienteEnElMapa> get visibles => zonaId == null ? clientes : clientes.where((c) => c.zonaId == zonaId).toList();

  List<PuntoDeReferencia> get puntosVisibles {
    final ids = {for (final z in zonas) z.id};
    return puntos.where((p) => ids.contains(p.zonaId) && (zonaId == null || p.zonaId == zonaId)).toList();
  }

  ClienteEnElMapa? get elegido => visibles.where((c) => c.id == clienteId).firstOrNull;

  int clientesDe(int zonaId) => clientes.where((c) => c.zonaId == zonaId).length;

  EstadoDelMapaDeClientes copiar({
    bool? cargado,
    List<Zona>? zonas,
    List<PuntoDeReferencia>? puntos,
    List<ClienteEnElMapa>? clientes,
    Object? zonaId = _igual,
    Object? clienteId = _igual,
    Object? error = _igual,
    bool? reintentando,
  }) => EstadoDelMapaDeClientes(
    cargado: cargado ?? this.cargado,
    zonas: zonas ?? this.zonas,
    puntos: puntos ?? this.puntos,
    clientes: clientes ?? this.clientes,
    zonaId: zonaId == _igual ? this.zonaId : zonaId as int?,
    clienteId: clienteId == _igual ? this.clienteId : clienteId as int?,
    error: error == _igual ? this.error : error as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
  );
}

/// El mapa de clientes (5.1 y 5.2; 7.3.17 y 7.3.18): las zonas con sus límites y sus puntos de referencia, y los
/// clientes con el color de su estado. Al entrar se ven todos; al elegir una zona, solo los suyos.
class MapaDeClientesViewModel extends Notifier<EstadoDelMapaDeClientes> {
  @override
  EstadoDelMapaDeClientes build() {
    Future.microtask(cargar);
    return const EstadoDelMapaDeClientes();
  }

  Future<void> cargar() async {
    state = state.copiar(reintentando: state.error != null);
    final zonas = ref.read(zonasRepositoryProvider);
    try {
      final (todas, puntos, clientes) = await (
        zonas.listar(),
        zonas.puntos(),
        ref.read(clientesRepositoryProvider).mapa(),
      ).wait;
      if (!ref.mounted) return;
      final sesion = ref.read(sesionProvider);
      final veTodas = sesion is ConSesion && sesion.sesion.tiene([Permisos.seguridad.verCarteraCompleta]);
      final suyas = sesion is ConSesion ? sesion.sesion.id : null;
      final visibles = veTodas ? todas : todas.where((z) => z.promotorId == suyas).toList();
      state = state.copiar(
        cargado: true,
        zonas: visibles,
        puntos: puntos,
        clientes: clientes,
        // Con una sola zona, entra con ella elegida: así se ven sus avisos.
        zonaId: visibles.length == 1 ? visibles.single.id : state.zonaId,
        error: null,
        reintentando: false,
      );
    } on ParallelWaitError<dynamic, dynamic> catch (e) {
      if (!ref.mounted) return;
      final errores = e.errors as (AsyncError?, AsyncError?, AsyncError?);
      final primero = errores.$1?.error ?? errores.$2?.error ?? errores.$3?.error;
      state = state.copiar(
        error: ErrorDeApi.desde(primero ?? e, porDefecto: 'No se pudo cargar el mapa de clientes.'),
        reintentando: false,
      );
    }
  }

  void elegirZona(int? zonaId) => state = state.copiar(zonaId: zonaId, clienteId: null);

  void elegirCliente(int? clienteId) => state = state.copiar(clienteId: clienteId);
}

final mapaDeClientesProvider = NotifierProvider.autoDispose<MapaDeClientesViewModel, EstadoDelMapaDeClientes>(
  MapaDeClientesViewModel.new,
);
