import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:latlong2/latlong.dart';

import '../formato.dart';
import '../tema.dart';

/// Si el mapa pide los mosaicos de OpenStreetMap. Las pruebas lo apagan: no tienen red.
final mosaicosDelMapaProvider = Provider<bool>((ref) => true);

/// El punto de una ubicación en un mapa de OpenStreetMap (2.4, 8.6.6): un círculo `marca-600` con borde blanco, como
/// en el panel (docs/diseno.md, "Mapas"). Es para mirar: no se arrastra, así el formulario se desplaza sin pelear con
/// el mapa.
class MapaDeUbicacion extends ConsumerWidget {
  const MapaDeUbicacion({super.key, required this.latitud, required this.longitud});

  final double latitud;
  final double longitud;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final punto = LatLng(latitud, longitud);
    return Semantics(
      image: true,
      label:
          'Mapa con la ubicación: latitud ${formatearCoordenada(latitud)}, longitud ${formatearCoordenada(longitud)}',
      child: ExcludeSemantics(
        child: Container(
          height: 160,
          decoration: BoxDecoration(
            border: Border.all(color: const Color(0xFFE2E8F0)),
            borderRadius: BorderRadius.circular(8),
          ),
          clipBehavior: Clip.antiAlias,
          child: FlutterMap(
            // Una ubicación nueva ("Volver a capturar") centra el mapa otra vez.
            key: ValueKey((latitud, longitud)),
            options: MapOptions(
              initialCenter: punto,
              initialZoom: 17,
              interactionOptions: const InteractionOptions(flags: InteractiveFlag.none),
            ),
            children: [
              const MosaicosDeOpenStreetMap(),
              CircleLayer(
                circles: [
                  CircleMarker(
                    point: punto,
                    radius: 9,
                    color: marca600,
                    borderColor: Colors.white,
                    borderStrokeWidth: 2,
                  ),
                ],
              ),
              const AtribucionDeOpenStreetMap(),
            ],
          ),
        ),
      ),
    );
  }
}

/// Los mosaicos de OpenStreetMap, salvo en las pruebas ([mosaicosDelMapaProvider]).
class MosaicosDeOpenStreetMap extends ConsumerWidget {
  const MosaicosDeOpenStreetMap({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (!ref.watch(mosaicosDelMapaProvider)) return const SizedBox.shrink();
    return TileLayer(
      urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
      // La política de uso de los mosaicos pide que la app se identifique.
      userAgentPackageName: 'com.delcampo.controlgestion',
    );
  }
}

/// "© OpenStreetMap", que la licencia de los mosaicos pide visible. [alineacion]: la esquina donde va.
class AtribucionDeOpenStreetMap extends StatelessWidget {
  const AtribucionDeOpenStreetMap({super.key, this.alineacion = Alignment.bottomRight});

  final Alignment alineacion;

  @override
  Widget build(BuildContext context) => Align(
    alignment: alineacion,
    child: const ColoredBox(
      color: Color(0xE6FFFFFF),
      child: Padding(
        padding: EdgeInsets.symmetric(horizontal: 6, vertical: 2),
        child: Text('© OpenStreetMap', style: TextStyle(fontSize: 14, color: Color(0xFF0369A1))),
      ),
    ),
  );
}
