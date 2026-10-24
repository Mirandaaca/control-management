import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../data/models/ruta.dart';
import '../../data/models/zona.dart';
import '../../data/services/ubicacion.dart';
import '../core/tema.dart';
import '../core/ubicacion/mapa_de_ubicacion.dart';
import '../territorio/marcadores.dart';
import 'marcadores.dart';

/// El mapa de la ruta del día (8.6.9; 7.3.20, paso 3.2): el límite de la zona, sus puntos de referencia, las paradas
/// numeradas con el estado de cada visita, la línea en el orden de visita y "Tú". Se encuadra al abrir y no se mueve al
/// marcar. Se mueve con el dedo y se acerca con dos dedos, sin girar.
class MapaDeLaRuta extends StatefulWidget {
  const MapaDeLaRuta({super.key, required this.ruta, this.zona, this.puntos = const [], this.tu});

  final Ruta ruta;
  final Zona? zona;
  final List<PuntoDeReferencia> puntos;

  /// Dónde está el promotor según el GPS del celular; null si no se sabe o la ruta no está en curso.
  final Posicion? tu;

  @override
  State<MapaDeLaRuta> createState() => _MapaDeLaRutaState();
}

class _MapaDeLaRutaState extends State<MapaDeLaRuta> {
  final _mapa = MapController();

  @override
  void dispose() {
    _mapa.dispose();
    super.dispose();
  }

  List<LatLng> get _paraEncuadrar => [
    for (final v in widget.ruta.visitas) puntoDe(v.cliente.latitud, v.cliente.longitud),
    for (final v in widget.zona?.limite ?? const <Vertice>[]) puntoDe(v.latitud, v.longitud),
  ];

  CameraFit get _encuadre =>
      CameraFit.coordinates(coordinates: _paraEncuadrar, padding: const EdgeInsets.all(32), maxZoom: 17);

  // La zona llega después de la ruta: cuando llega su límite, el mapa se encuadra otra vez, con él. Marcar una visita no
  // lo mueve.
  @override
  void didUpdateWidget(MapaDeLaRuta anterior) {
    super.didUpdateWidget(anterior);
    if (anterior.zona?.limite == null && widget.zona?.limite != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _mapa.fitCamera(_encuadre);
      });
    }
  }

  void _centrarEnTi() {
    final tu = widget.tu;
    if (tu == null) return;
    _mapa.move(puntoDe(tu.latitud, tu.longitud), math.max(_mapa.camera.zoom, 16));
  }

  @override
  Widget build(BuildContext context) {
    final ruta = widget.ruta;
    final tu = widget.tu;
    final zona = widget.zona;
    final conLimite = zona != null && zona.limite != null;
    return SizedBox(
      height: 280,
      child: Stack(
        children: [
          Semantics(
            image: true,
            label: 'Mapa de la ruta: ${ruta.total} visitas, ${ruta.marcadas} marcadas.',
            child: ExcludeSemantics(
              child: FlutterMap(
                mapController: _mapa,
                options: MapOptions(
                  initialCameraFit: _encuadre,
                  interactionOptions: const InteractionOptions(
                    flags: InteractiveFlag.all & ~InteractiveFlag.rotate & ~InteractiveFlag.scrollWheelZoom,
                  ),
                ),
                children: [
                  const MosaicosDeOpenStreetMap(),
                  if (conLimite) PolygonLayer(polygons: [limiteDeZona(zona)]),
                  PolylineLayer(polylines: [lineaDeRuta(ruta.visitas)]),
                  MarkerLayer(
                    markers: [
                      if (conLimite) nombreDeZona(zona),
                      ...marcadoresDePuntos(widget.puntos),
                      ...marcadoresDeParadas(ruta.visitas),
                      if (tu != null) marcadorTu(puntoDe(tu.latitud, tu.longitud)),
                    ],
                  ),
                  const AtribucionDeOpenStreetMap(alineacion: Alignment.bottomLeft),
                ],
              ),
            ),
          ),
          if (tu != null)
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
                  onPressed: _centrarEnTi,
                ),
              ),
            ),
        ],
      ),
    );
  }
}
