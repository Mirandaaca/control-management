import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../data/models/ruta.dart';
import '../core/tema.dart';
import '../territorio/marcadores.dart';

// Los marcadores de la ruta (5.4; docs/diseno.md, "Mapas"; 8.6.9), los de components/rutas/marcadores.js del panel con
// los íconos de Material. Muestran el estado de la visita, no el del cliente: nunca rojo ni verde.

const _slate600 = Color(0xFF475569);

/// La pastilla de una parada: su número de orden y el estado de la visita. Pendiente es blanca con el borde y el número
/// `marca-600`; visitado, `marca-600` con un check; ausente, `slate-600` con una persona tachada. [ancho] fija el ancho
/// (48 en la lista de visitas, para que todas midan lo mismo).
class Pastilla extends StatelessWidget {
  const Pastilla(this.orden, this.resultado, {super.key, this.ancho, this.sombra = true});

  final int orden;
  final EstadoVisita resultado;
  final double? ancho;
  final bool sombra;

  @override
  Widget build(BuildContext context) {
    final (fondoDe, bordeDe, textoDe) = switch (resultado) {
      EstadoVisita.pendiente => (Colors.white, marca600, marca600),
      EstadoVisita.visitado => (marca600, Colors.white, Colors.white),
      EstadoVisita.ausente => (_slate600, Colors.white, Colors.white),
    };
    final icono = switch (resultado) {
      EstadoVisita.pendiente => null,
      EstadoVisita.visitado => Icons.check,
      EstadoVisita.ausente => Icons.person_off_outlined,
    };
    return Container(
      height: 26,
      width: ancho,
      constraints: const BoxConstraints(minWidth: 30),
      padding: const EdgeInsets.symmetric(horizontal: 6),
      decoration: BoxDecoration(
        color: fondoDe,
        border: Border.all(color: bordeDe, width: 2),
        borderRadius: BorderRadius.circular(999),
        boxShadow: sombra ? const [BoxShadow(color: Color(0x730F172A), blurRadius: 3, offset: Offset(0, 1))] : null,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            '$orden',
            textScaler: TextScaler.noScaling,
            style: TextStyle(
              fontSize: 14,
              height: 1,
              fontWeight: FontWeight.w700,
              color: textoDe,
              fontFeatures: const [FontFeature.tabularFigures()],
            ),
          ),
          if (icono != null) ...[const SizedBox(width: 2), Icon(icono, size: 14, color: textoDe)],
        ],
      ),
    );
  }
}

/// Las paradas en el mapa, con su pastilla.
List<Marker> marcadoresDeParadas(List<VisitaDeRuta> visitas) => [
  for (final v in visitas)
    Marker(
      point: puntoDe(v.cliente.latitud, v.cliente.longitud),
      width: 64,
      height: 32,
      child: Center(child: Pastilla(v.orden, v.resultado)),
    ),
];

/// La línea de la ruta: une las paradas en su orden, `marca-700`, de 3 dp y punteada (8 y 6).
Polyline lineaDeRuta(List<VisitaDeRuta> visitas) => Polyline(
  points: [for (final v in visitas) puntoDe(v.cliente.latitud, v.cliente.longitud)],
  color: marca700,
  strokeWidth: 3,
  pattern: StrokePattern.dashed(segments: const [8, 6]),
);

/// El círculo del promotor: `marca-900` con una persona y un halo, de otra forma que las pastillas.
class CirculoDelPromotor extends StatelessWidget {
  const CirculoDelPromotor({super.key, this.lado = 32});

  final double lado;

  @override
  Widget build(BuildContext context) => Container(
    width: lado,
    height: lado,
    decoration: BoxDecoration(
      color: marca900,
      shape: BoxShape.circle,
      border: Border.all(color: Colors.white, width: 2),
      boxShadow: [
        if (lado >= 32) const BoxShadow(color: Color(0x2E152B4C), spreadRadius: 6),
        const BoxShadow(color: Color(0x730F172A), blurRadius: 3, offset: Offset(0, 1)),
      ],
    ),
    child: Icon(Icons.person, size: lado * 0.56, color: Colors.white),
  );
}

/// "Tú": dónde está el promotor según el GPS del celular, con su círculo y "Tú" al lado (8.6.9).
Marker marcadorTu(LatLng punto) => Marker(
  point: punto,
  width: 96,
  height: 40,
  // El punto es el centro del círculo: el marcador se corre a la derecha para que el texto quede al lado.
  alignment: const Alignment(0.6, 0),
  child: const ExcludeSemantics(
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [CirculoDelPromotor(), SizedBox(width: 6), EtiquetaDeMapa('Tú')],
    ),
  ),
);
