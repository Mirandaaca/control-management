import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:latlong2/latlong.dart';

import '../../data/models/cliente.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../clientes/etiqueta_del_cliente.dart';
import '../core/formato.dart';
import '../core/tema.dart';
import '../core/ubicacion/mapa_de_ubicacion.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/selectores.dart';
import '../core/widgets/vacio.dart';
import '../sesion/sesion_notifier.dart';
import 'mapa_de_clientes_view_model.dart';
import 'marcadores.dart';
import 'visitas.dart';

/// Sin nada que encuadrar, el mapa abre en el centro de Santa Cruz (docs/diseno.md, "Mapas").
const _santaCruz = LatLng(-17.7833, -63.1821);

String _cuantos(int n) => n == 1 ? '1 cliente' : '$n clientes';

/// El mapa de clientes (5.1 y 5.2; `frmMapaClientesMovil`; sin diseño en el 8.6, sigue el panel): las zonas con sus
/// límites y sus puntos de referencia, y cada cliente con el color de su estado. Al tocar uno, su estado y sus visitas.
class MapaDeClientesScreen extends ConsumerWidget {
  const MapaDeClientesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estado = ref.watch(mapaDeClientesProvider);
    final viewModel = ref.read(mapaDeClientesProvider.notifier);
    final sesion = ref.watch(sesionProvider);
    final veTodas = sesion is ConSesion && sesion.sesion.tiene([Permisos.seguridad.verCarteraCompleta]);

    return Scaffold(
      appBar: AppBar(title: const Text('Mapa de clientes')),
      body: switch (estado) {
        EstadoDelMapaDeClientes(cargado: false, error: final error?) => ErrorConReintento.de(
          error,
          reintentando: estado.reintentando,
          alReintentar: viewModel.cargar,
        ),
        EstadoDelMapaDeClientes(cargado: false) => const Cargando('Cargando el mapa…'),
        EstadoDelMapaDeClientes(zonas: []) => Vacio(
          icono: Icons.map_outlined,
          titulo: veTodas ? 'Todavía no hay zonas' : 'Todavía no tienes zonas',
          texto: veTodas
              ? 'Cuando se registre una zona con sus clientes, aquí se ven en el mapa.'
              : 'Cuando el gerente general te asigne una zona, aquí ves a sus clientes en el mapa.',
        ),
        _ => RefreshIndicator(
          onRefresh: viewModel.cargar,
          child: ListView(
            padding: const EdgeInsets.only(bottom: 24),
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    _ElegirZona(estado: estado, veTodas: veTodas, alElegir: viewModel.elegirZona),
                    const SizedBox(height: 8),
                    Text(
                      '${_cuantos(estado.visibles.length)} en el mapa. Toca un cliente para ver su estado y su próxima '
                      'visita.',
                      style: const TextStyle(color: textoSecundario),
                    ),
                    for (final aviso in _avisos(estado)) ...[
                      const SizedBox(height: 12),
                      Aviso(aviso, tipo: TipoDeAviso.informacion),
                    ],
                  ],
                ),
              ),
              _MapaDeClientes(estado: estado, alTocar: viewModel.elegirCliente),
              Container(
                color: Colors.white,
                padding: const EdgeInsets.all(16),
                child: Leyenda([
                  for (final e in _estadosQueAparecen(estado.visibles))
                    EntradaDeLeyenda(MarcadorDeCliente(e), e.nombre),
                  if (estado.puntosVisibles.isNotEmpty)
                    const EntradaDeLeyenda(MarcadorDePunto(lado: 24), 'Punto de referencia'),
                  const EntradaDeLeyenda(muestraDeZona, 'Zona'),
                ]),
              ),
              if (estado.elegido case final cliente?)
                Padding(padding: const EdgeInsets.fromLTRB(16, 16, 16, 0), child: _ClienteElegido(cliente)),
            ],
          ),
        ),
      },
    );
  }

  // 7.3.17, ext. 2.2a y 2.4a.
  List<String> _avisos(EstadoDelMapaDeClientes estado) {
    final zona = estado.zona;
    if (zona == null) return const [];
    return [
      if (zona.limite == null) '${zona.nombre} todavía no tiene límite.',
      if (estado.visibles.isEmpty) '${zona.nombre} todavía no tiene clientes en el mapa.',
    ];
  }

  // Primero Activo y Disponible, y después los manuales por nombre.
  List<EstadoCliente> _estadosQueAparecen(List<ClienteEnElMapa> clientes) {
    final estados = {for (final c in clientes) c.estado.id: c.estado}.values.toList();
    int orden(EstadoCliente e) => switch (e.id) {
      EstadoCliente.idActivo => 0,
      EstadoCliente.idDisponible => 1,
      _ => 2,
    };
    return estados..sort((a, b) => orden(a) != orden(b) ? orden(a) - orden(b) : a.nombre.compareTo(b.nombre));
  }
}

/// "Zona" ▼: todas sus zonas o una (7.3.17, pasos 1 y 2).
class _ElegirZona extends StatelessWidget {
  const _ElegirZona({required this.estado, required this.veTodas, required this.alElegir});

  final EstadoDelMapaDeClientes estado;
  final bool veTodas;
  final ValueChanged<int?> alElegir;

  @override
  Widget build(BuildContext context) {
    final todas = veTodas ? 'Todas las zonas' : 'Todas mis zonas';
    return CampoDeEleccion(
      etiqueta: 'Zona',
      valor: estado.zona?.nombre ?? todas,
      alTocar: () async {
        // 0 es "todas": null es que se cerró la lista sin elegir.
        final elegida = await elegirDeLaLista<int>(
          context,
          titulo: 'Zona',
          opciones: [
            (0, todas),
            for (final z in estado.zonas) (z.id, '${z.nombre} · ${_cuantos(estado.clientesDe(z.id))}'),
          ],
          actual: estado.zonaId ?? 0,
        );
        if (elegida != null) alElegir(elegida == 0 ? null : elegida);
      },
    );
  }
}

/// El mapa: los límites (el de la zona elegida, resaltado), los puntos de referencia y los clientes. Elegir otra zona lo
/// vuelve a encuadrar.
class _MapaDeClientes extends StatelessWidget {
  const _MapaDeClientes({required this.estado, required this.alTocar});

  final EstadoDelMapaDeClientes estado;
  final ValueChanged<int?> alTocar;

  @override
  Widget build(BuildContext context) {
    final zonasConLimite = [
      for (final z in estado.zonas)
        if (z.limite != null && (estado.zonaId == null || z.id == estado.zonaId)) z,
    ];
    final clientes = [...estado.visibles]
      // El elegido, encima de los demás.
      ..sort((a, b) => (a.id == estado.clienteId ? 1 : 0) - (b.id == estado.clienteId ? 1 : 0));
    final paraEncuadrar = [
      for (final z in zonasConLimite)
        for (final v in z.limite!) puntoDe(v.latitud, v.longitud),
      for (final c in clientes) puntoDe(c.latitud, c.longitud),
      for (final p in estado.puntosVisibles) puntoDe(p.latitud, p.longitud),
    ];
    return SizedBox(
      height: 360,
      child: Semantics(
        container: true,
        label: 'Mapa de clientes: cada cliente con el color de su estado',
        child: FlutterMap(
          key: ValueKey(estado.zonaId),
          options: MapOptions(
            initialCenter: _santaCruz,
            initialZoom: 13,
            initialCameraFit: paraEncuadrar.isEmpty
                ? null
                : CameraFit.coordinates(coordinates: paraEncuadrar, padding: const EdgeInsets.all(32), maxZoom: 17),
            interactionOptions: const InteractionOptions(flags: gestosDelMapa),
          ),
          children: [
            const MosaicosDeOpenStreetMap(),
            PolygonLayer(polygons: [for (final z in zonasConLimite) limiteDeZona(z, elegida: z.id == estado.zonaId)]),
            MarkerLayer(
              markers: [
                for (final z in zonasConLimite) nombreDeZona(z),
                ...marcadoresDePuntos(estado.puntosVisibles),
                for (final c in clientes)
                  Marker(
                    point: puntoDe(c.latitud, c.longitud),
                    width: toqueDeMarcador,
                    height: toqueDeMarcador,
                    child: Semantics(
                      button: true,
                      selected: c.id == estado.clienteId,
                      label: '${c.nombreCompleto}, ${c.estado.nombre}',
                      excludeSemantics: true,
                      child: GestureDetector(
                        behavior: HitTestBehavior.opaque,
                        onTap: () => alTocar(c.id),
                        child: Center(child: MarcadorDeCliente(c.estado, elegido: c.id == estado.clienteId)),
                      ),
                    ),
                  ),
              ],
            ),
            const AtribucionDeOpenStreetMap(),
          ],
        ),
      ),
    );
  }
}

/// El cliente elegido (7.3.17, paso 3, y 7.3.18, paso 2): su estado, su dirección, su celular y sus visitas.
class _ClienteElegido extends StatelessWidget {
  const _ClienteElegido(this.cliente);

  final ClienteEnElMapa cliente;

  String get _proxima {
    if (!cliente.estado.delSistema) return 'Fuera del ciclo de visitas';
    if (cliente.proximaVisita == null) return 'Sin visitas: está disponible para re-visita.';
    return '${formatearFecha(cliente.proximaVisita)} · ${cuantoFalta(cliente.proximaVisita)}';
  }

  @override
  Widget build(BuildContext context) => Semantics(
    container: true,
    liveRegion: true,
    child: Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Semantics(
              header: true,
              container: true,
              child: Text(cliente.nombreCompleto, style: Theme.of(context).textTheme.titleMedium),
            ),
            Text(cliente.codigo, style: const TextStyle(color: textoSecundario)),
            const SizedBox(height: 8),
            EtiquetaDelCliente(cliente.estado),
            const SizedBox(height: 12),
            _Dato('Dirección', cliente.direccion),
            _Dato('Celular', cliente.telefono),
            _Dato(
              'Última visita',
              cliente.ultimaVisita == null ? 'Nunca' : formatearFecha(cliente.ultimaVisita, hora: true),
            ),
            _Dato('Próxima visita', _proxima),
          ],
        ),
      ),
    ),
  );
}

class _Dato extends StatelessWidget {
  const _Dato(this.titulo, this.valor);

  final String titulo;
  final String valor;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          titulo,
          style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: textoSecundario),
        ),
        Text(valor),
      ],
    ),
  );
}
