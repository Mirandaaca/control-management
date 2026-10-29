import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';

import '../../data/models/camino.dart';
import '../../data/models/ruta.dart';
import '../../data/services/gps_simulado.dart';
import '../../data/services/ubicacion.dart';
import '../../domain/navegacion.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/tema.dart';
import '../core/ubicacion/mapa_de_ubicacion.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/segmentado.dart';
import '../territorio/marcadores.dart';
import 'aviso_del_gps.dart';
import 'marcadores.dart';
import 'navegacion_view_model.dart';
import 'seguimiento_de_ruta.dart';

// Las alternativas, en gris: slate-400 la línea y slate-700 su etiqueta.
const _gris = Color(0xFF94A3B8);
const _slate700 = Color(0xFF334155);

/// El ícono de cada giro, para la indicación de arriba.
IconData iconoDelGiro(Giro giro) => switch (giro) {
  Giro.salida => Icons.navigation,
  Giro.recto => Icons.straight,
  Giro.izquierda => Icons.turn_left,
  Giro.derecha => Icons.turn_right,
  Giro.izquierdaCerrada => Icons.turn_sharp_left,
  Giro.derechaCerrada => Icons.turn_sharp_right,
  Giro.izquierdaLeve => Icons.turn_slight_left,
  Giro.derechaLeve => Icons.turn_slight_right,
  Giro.mantenerIzquierda => Icons.fork_left,
  Giro.mantenerDerecha => Icons.fork_right,
  Giro.rotonda || Giro.salirDeRotonda => Icons.roundabout_right,
  Giro.retorno => Icons.u_turn_left,
  Giro.llegada => Icons.flag,
};

String _minuscula(String texto) => texto.isEmpty ? texto : texto[0].toLowerCase() + texto.substring(1);

/// Cómo llegar (tarea 25): el camino por calles hasta la próxima visita, con la indicación que viene, las alternativas
/// en gris, la distancia y el tiempo que faltan, y el mapa que sigue al promotor. Si se desvía, el camino se recalcula;
/// al llegar, "Marcar visita" vuelve a la ruta del día y abre la hoja de marcarla.
class NavegacionScreen extends ConsumerStatefulWidget {
  const NavegacionScreen({super.key, required this.abrir});

  final AbrirCamino abrir;

  @override
  ConsumerState<NavegacionScreen> createState() => _NavegacionScreenState();
}

class _NavegacionScreenState extends ConsumerState<NavegacionScreen> {
  final _mapa = MapController();
  final _toque = LayerHitNotifier<int>(null);
  var _listo = false;

  /// El mapa sigue al promotor hasta que lo mueve con los dedos; el botón de centrar lo vuelve a seguir.
  var _siguiendo = true;

  NavegacionViewModel get _viewModel => ref.read(navegacionProvider(widget.abrir).notifier);

  ClienteDeRuta get _cliente => widget.abrir.visita.cliente;

  @override
  void dispose() {
    _mapa.dispose();
    _toque.dispose();
    super.dispose();
  }

  void _seguir(Posicion posicion, {double? zoom}) {
    if (_listo) _mapa.move(puntoDe(posicion.latitud, posicion.longitud), zoom ?? _mapa.camera.zoom);
  }

  void _centrar(Posicion posicion) {
    setState(() => _siguiendo = true);
    _seguir(posicion, zoom: math.max(_mapa.camera.zoom, 16));
  }

  // Los primeros caminos se ven enteros, con las alternativas y sus etiquetas; después el mapa sigue al promotor con ese
  // acercamiento. Abajo deja lugar a la atribución, que con dos renglones tapaba el punto de partida.
  void _encuadrar(Caminos caminos) {
    if (!_listo) return;
    _mapa.fitCamera(
      CameraFit.coordinates(
        coordinates: [
          for (final camino in caminos.todos)
            for (final p in camino.puntos) puntoDe(p.latitud, p.longitud),
        ],
        padding: const EdgeInsets.fromLTRB(48, 48, 48, 88),
        maxZoom: 17,
      ),
    );
  }

  // Las etiquetas de las alternativas van encima del mapa, en su lugar de la pantalla: se mueven con él.
  void _alMoverElMapa(MapCamera camara, bool conLosDedos) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      setState(() {
        if (conLosDedos) _siguiendo = false;
      });
    });
  }

  void _alTocarUnaLinea() {
    final toque = _toque.value;
    if (toque != null && toque.hitValues.isNotEmpty) _viewModel.elegir(toque.hitValues.first);
  }

  @override
  Widget build(BuildContext context) {
    final provider = navegacionProvider(widget.abrir);
    final estado = ref.watch(provider);
    final seguimiento = ref.watch(seguimientoDeRutaProvider);
    ref.listen(provider.select((e) => e.posicion), (_, posicion) {
      if (posicion != null && _siguiendo) _seguir(posicion);
    });
    ref.listen(provider.select((e) => e.caminos), (anterior, caminos) {
      if (anterior == null && caminos != null) _encuadrar(caminos);
    });

    final camino = estado.camino;
    final progreso = estado.progreso;
    final proximo = switch ((camino, progreso?.proximo)) {
      (final camino?, final indice?) when !estado.llegaste => camino.pasos[indice],
      _ => null,
    };

    return Scaffold(
      appBar: AppBar(
        title: const Text('Cómo llegar'),
        actions: [
          if (!estado.sinVoz)
            IconButton(
              tooltip: estado.vozSilenciada ? 'Activar la voz' : 'Silenciar la voz',
              icon: Icon(estado.vozSilenciada ? Icons.volume_off : Icons.volume_up),
              onPressed: () => _viewModel.silenciar(!estado.vozSilenciada),
            ),
        ],
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (estado.llegaste)
            _Indicacion(icono: Icons.flag, texto: 'Llegaste a ${_cliente.nombreCompleto}')
          else if (proximo != null)
            _Indicacion(
              icono: iconoDelGiro(proximo.giro),
              distancia: 'En ${formatearDistancia(progreso!.metrosHastaElProximo)}',
              texto: _minuscula(indicacionDe(proximo, _cliente.nombreCompleto)),
            ),
          Expanded(child: _mapaDelCamino(estado)),
        ],
      ),
      bottomNavigationBar: ConstrainedBox(
        // Con la letra grande, lo de abajo se desplaza antes de tapar el mapa entero.
        constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.5),
        child: SingleChildScrollView(
          child: BarraDeAcciones(
            arriba: [
              if (seguimiento.rutaId == widget.abrir.rutaId)
                ?avisoDelGps(ref, seguimiento.problema, widget.abrir.rutaId),
              ..._avisos(estado),
              if (!estado.llegaste) ...[
                Segmentado<MedioDeTransporte>(
                  etiqueta: 'Cómo vas',
                  opciones: [for (final medio in MedioDeTransporte.values) (medio, medio.texto)],
                  valor: estado.medio,
                  alCambiar: _viewModel.elegirMedio,
                ),
                if (progreso != null) _Resumen(progreso: progreso, cliente: _cliente.nombreCompleto),
              ],
              if (gpsSimulado && camino != null)
                OutlinedButton(
                  onPressed: () => (ref.read(servicioDeUbicacionProvider) as GpsSimulado).recorrer(camino.puntos),
                  child: const Text('Simular recorrido'),
                ),
            ],
            principal: estado.llegaste
                ? Accion('Marcar visita', () => context.pop(true))
                : Accion('Terminar', () => context.pop()),
            secundaria: estado.llegaste ? Accion('Terminar', () => context.pop()) : null,
            conEstiloSecundario: !estado.llegaste,
          ),
        ),
      ),
    );
  }

  List<Widget> _avisos(EstadoDeLaNavegacion estado) => [
    if (estado.llegaste)
      const Aviso('Marca la visita para seguir con tu ruta.', tipo: TipoDeAviso.exito)
    else if (estado.error case final error?)
      Aviso.deError(error, accion: AccionDeAviso('Reintentar', _viewModel.reintentar))
    else if (estado.posicion == null)
      const Aviso('Buscando tu ubicación…', tipo: TipoDeAviso.informacion, buscando: true)
    else if (estado.calculando)
      const Aviso('Calculando el camino…', tipo: TipoDeAviso.informacion, buscando: true)
    else if (estado.recalculando)
      const Aviso('Recalculando…', tipo: TipoDeAviso.informacion, buscando: true)
    else if (estado.errorDelRecalculo case final error?)
      Aviso.deError(error),
    if (estado.sinVoz)
      const Aviso(
        'Tu celular no tiene voz en español: sigue las indicaciones en pantalla.',
        tipo: TipoDeAviso.informacion,
      ),
  ];

  Widget _mapaDelCamino(EstadoDeLaNavegacion estado) {
    final caminos = estado.caminos?.todos ?? const <Camino>[];
    final elegido = estado.camino;
    final posicion = estado.posicion;
    final cliente = puntoDe(_cliente.latitud, _cliente.longitud);
    List<LatLng> linea(Camino camino) => [for (final p in camino.puntos) puntoDe(p.latitud, p.longitud)];

    return LayoutBuilder(
      builder: (context, limites) => Stack(
        children: [
          Semantics(
            image: true,
            label: elegido == null
                ? 'Mapa con ${_cliente.nombreCompleto}.'
                : 'Mapa del camino hasta ${_cliente.nombreCompleto}: ${formatearDistancia(elegido.distancia.toDouble())}.',
            child: ExcludeSemantics(
              child: FlutterMap(
                mapController: _mapa,
                options: MapOptions(
                  initialCenter: posicion == null ? cliente : puntoDe(posicion.latitud, posicion.longitud),
                  initialZoom: 16,
                  onMapReady: () => setState(() => _listo = true),
                  onPositionChanged: _alMoverElMapa,
                  interactionOptions: const InteractionOptions(flags: InteractiveFlag.all & ~InteractiveFlag.rotate),
                ),
                children: [
                  const MosaicosDeOpenStreetMap(),
                  GestureDetector(
                    // Las etiquetas de abajo son las que lee el lector de pantalla.
                    excludeFromSemantics: true,
                    onTap: _alTocarUnaLinea,
                    child: PolylineLayer<int>(
                      hitNotifier: _toque,
                      polylines: [
                        for (final (indice, camino) in caminos.indexed)
                          if (indice != estado.elegido)
                            Polyline<int>(
                              points: linea(camino),
                              color: _gris,
                              strokeWidth: 6,
                              borderColor: Colors.white,
                              borderStrokeWidth: 1,
                              hitValue: indice,
                            ),
                      ],
                    ),
                  ),
                  if (elegido != null)
                    PolylineLayer(
                      polylines: [
                        Polyline(
                          points: linea(elegido),
                          color: marca600,
                          strokeWidth: 7,
                          borderColor: Colors.white,
                          borderStrokeWidth: 2,
                        ),
                      ],
                    ),
                  MarkerLayer(
                    markers: [
                      Marker(
                        point: cliente,
                        width: 64,
                        height: 32,
                        child: Center(child: Pastilla(widget.abrir.visita.orden, EstadoVisita.pendiente)),
                      ),
                      if (posicion != null) marcadorTu(puntoDe(posicion.latitud, posicion.longitud)),
                    ],
                  ),
                  const AtribucionDeOpenStreetMap(
                    alineacion: Alignment.bottomLeft,
                    texto: '© OpenRouteService, © colaboradores de OpenStreetMap',
                  ),
                ],
              ),
            ),
          ),
          if (_listo && elegido != null && !estado.llegaste)
            for (final (indice, camino) in caminos.indexed)
              if (indice != estado.elegido)
                if (_lugarDeLaEtiqueta(camino, limites.biggest) case final lugar?)
                  Positioned(
                    left: lugar.dx,
                    top: lugar.dy,
                    child: FractionalTranslation(
                      translation: const Offset(-0.5, -0.5),
                      child: _EtiquetaDeCamino(
                        duracion: camino.duracion,
                        diferencia: camino.duracion - elegido.duracion,
                        alTocar: () => _viewModel.elegir(indice),
                      ),
                    ),
                  ),
          if (!_siguiendo && posicion != null)
            Positioned(
              right: 12,
              bottom: 12,
              child: Material(
                color: Colors.white,
                shape: const CircleBorder(),
                elevation: 3,
                child: IconButton(
                  tooltip: 'Centrar el mapa en tu ubicación',
                  icon: const Icon(Icons.my_location, color: marca600),
                  onPressed: () => _centrar(posicion),
                ),
              ),
            ),
        ],
      ),
    );
  }

  // En la mitad de la alternativa, donde suele separarse del camino elegido. Fuera del mapa, no va.
  Offset? _lugarDeLaEtiqueta(Camino camino, Size mapa) {
    if (camino.puntos.isEmpty) return null;
    final medio = camino.puntos[camino.puntos.length ~/ 2];
    final lugar = _mapa.camera.latLngToScreenOffset(puntoDe(medio.latitud, medio.longitud));
    final dentro = lugar.dx >= 24 && lugar.dy >= 24 && lugar.dx <= mapa.width - 24 && lugar.dy <= mapa.height - 24;
    return dentro ? lugar : null;
  }
}

/// La indicación que viene, arriba: el ícono del giro, "En 150 m" y "gira a la derecha en Calle 4". El lector de
/// pantalla anuncia la indicación cuando cambia, sin la distancia, que cambia con cada lectura del GPS. Su letra ya es
/// grande: crece hasta 1,5 veces, así con la letra al 200 % queda lugar para el mapa.
class _Indicacion extends StatelessWidget {
  const _Indicacion({required this.icono, required this.texto, this.distancia});

  final IconData icono;
  final String texto;
  final String? distancia;

  @override
  Widget build(BuildContext context) => MediaQuery.withClampedTextScaling(
    maxScaleFactor: 1.5,
    child: Container(
      color: marca900,
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ExcludeSemantics(child: Icon(icono, color: Colors.white, size: 40)),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (distancia != null)
                  Text(
                    distancia!,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 22,
                      height: 28 / 22,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                Semantics(
                  container: true,
                  liveRegion: true,
                  child: Text(
                    texto,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: distancia == null ? 20 : 18,
                      height: 24 / 18,
                      fontWeight: distancia == null ? FontWeight.w600 : FontWeight.w400,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    ),
  );
}

/// Lo que falta hasta el cliente: "2,3 km · 8 min" y "Hasta Elena Gutiérrez Vaca".
class _Resumen extends StatelessWidget {
  const _Resumen({required this.progreso, required this.cliente});

  final Progreso progreso;
  final String cliente;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(
        '${formatearDistancia(progreso.metrosQueFaltan)} · ${formatearDuracion(progreso.segundosQueFaltan)}',
        style: Theme.of(context).textTheme.titleMedium?.copyWith(fontFeatures: const [FontFeature.tabularFigures()]),
      ),
      Text('Hasta $cliente', style: const TextStyle(color: textoSecundario)),
    ],
  );
}

/// La etiqueta de una alternativa en el mapa: cuánto tarda más o menos que el camino elegido ("+3 min"). Tocarla la
/// elige.
class _EtiquetaDeCamino extends StatelessWidget {
  const _EtiquetaDeCamino({required this.duracion, required this.diferencia, required this.alTocar});

  final int duracion;

  /// En segundos: positiva si tarda más que el elegido.
  final int diferencia;
  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context) {
    final minutos = (diferencia / 60).round();
    final (texto, dicho) = switch (minutos) {
      > 0 => ('+$minutos min', '$minutos min más'),
      < 0 => ('−${-minutos} min', '${-minutos} min menos'),
      _ => ('Igual', 'el mismo tiempo'),
    };
    return Semantics(
      button: true,
      excludeSemantics: true,
      label: 'Elegir el camino de ${formatearDuracion(duracion.toDouble())}, $dicho',
      child: Material(
        color: Colors.white,
        elevation: 2,
        shape: const StadiumBorder(side: BorderSide(color: _gris, width: 2)),
        child: InkWell(
          customBorder: const StadiumBorder(),
          onTap: alTocar,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minWidth: 48, minHeight: 48),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Center(
                widthFactor: 1,
                child: Text(
                  texto,
                  textScaler: MediaQuery.textScalerOf(context).clamp(maxScaleFactor: 1.3),
                  style: const TextStyle(fontWeight: FontWeight.w600, color: _slate700),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
