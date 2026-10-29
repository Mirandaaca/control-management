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

/// "© OpenStreetMap", que la licencia de los mosaicos pide visible. [alineacion]: la esquina donde va. Con la letra al
/// 200 % tapaba una franja del mapa: crece hasta 1,3 veces, y sigue legible. [texto] suma otra fuente, como el
/// proveedor del camino de la navegación.
class AtribucionDeOpenStreetMap extends StatelessWidget {
  const AtribucionDeOpenStreetMap({super.key, this.alineacion = Alignment.bottomRight, this.texto = '© OpenStreetMap'});

  final Alignment alineacion;
  final String texto;

  @override
  Widget build(BuildContext context) => Align(
    alignment: alineacion,
    child: ColoredBox(
      color: const Color(0xE6FFFFFF),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
        child: Text(
          texto,
          textScaler: MediaQuery.textScalerOf(context).clamp(maxScaleFactor: 1.3),
          style: const TextStyle(fontSize: 14, color: Color(0xFF0369A1)),
        ),
      ),
    ),
  );
}

/// Debajo de un mapa dentro de una pantalla que se desplaza, donde un dedo baja la pantalla (`gestosDelMapa`): cómo se
/// mueve el mapa. Va abajo y no encima: encima tapaba las paradas del borde.
class AvisoDeDosDedos extends StatelessWidget {
  const AvisoDeDosDedos({super.key});

  @override
  Widget build(BuildContext context) => const ColoredBox(
    color: Colors.white,
    child: Padding(
      padding: EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: Row(
        children: [
          ExcludeSemantics(child: Icon(Icons.pan_tool_alt_outlined, size: 18, color: textoSecundario)),
          SizedBox(width: 8),
          Expanded(
            child: Text(
              'Mueve el mapa con dos dedos o ábrelo completo.',
              style: TextStyle(fontSize: 14, color: textoSecundario),
            ),
          ),
        ],
      ),
    ),
  );
}

/// El botón que abre el mapa a pantalla completa ([verMapaCompleto]), arriba a la derecha del mapa chico.
class BotonDeMapaCompleto extends StatelessWidget {
  const BotonDeMapaCompleto({super.key, required this.alTocar});

  final VoidCallback alTocar;

  @override
  Widget build(BuildContext context) => Align(
    alignment: Alignment.topRight,
    child: Padding(
      padding: const EdgeInsets.all(8),
      child: Material(
        color: Colors.white,
        shape: const CircleBorder(),
        elevation: 3,
        child: IconButton(
          tooltip: 'Ver el mapa completo',
          icon: const Icon(Icons.open_in_full, color: marca600),
          onPressed: alTocar,
        ),
      ),
    ),
  );
}

/// Abre un mapa a pantalla completa, donde se mueve con un dedo. [mapa] lo arma con lo que se muestre en ese momento:
/// si mira sus providers, se actualiza solo (por ejemplo, "Tú" en la ruta del día).
Future<void> verMapaCompleto(BuildContext context, {required String titulo, required WidgetBuilder mapa}) =>
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        fullscreenDialog: true,
        builder: (context) => Scaffold(
          appBar: AppBar(title: Text(titulo)),
          body: mapa(context),
        ),
      ),
    );
