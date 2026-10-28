import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../data/models/cliente.dart';
import '../../data/models/zona.dart';
import '../clientes/etiqueta_del_cliente.dart';
import '../core/tema.dart';

// Los marcadores de los mapas de la app (docs/diseno.md, "Mapas"), los mismos del panel con los íconos de Material:
// components/clientes/marcadores.js y components/zonas/marcadores.js.

const _marca500 = Color(0xFF3A6FB5);
const _marca800 = Color(0xFF1C3A66);

/// Los gestos de un mapa dentro de una pantalla que se desplaza: con dos dedos se mueve y se acerca, y con doble toque se
/// acerca. Con un dedo se desplaza la pantalla (en el TECNO, deslizar sobre el mapa lo movía y no se llegaba a la lista).
/// Sin girar ni la rueda del mouse.
const gestosDelMapa = InteractiveFlag.pinchZoom | InteractiveFlag.pinchMove | InteractiveFlag.doubleTapZoom;

/// Lo que mide un marcador para tocarlo: 48 dp, aunque se vea más chico.
const toqueDeMarcador = 48.0;

LatLng puntoDe(double latitud, double longitud) => LatLng(latitud, longitud);

/// Un cliente en el mapa: un círculo con borde blanco y el color de su estado, con su ícono (Activo el reloj, Disponible
/// el pin; los manuales, sin ícono). El elegido es más grande y lleva un anillo `marca-900`.
class MarcadorDeCliente extends StatelessWidget {
  const MarcadorDeCliente(this.estado, {super.key, this.elegido = false});

  final EstadoCliente estado;
  final bool elegido;

  @override
  Widget build(BuildContext context) {
    final lado = elegido ? 36.0 : 28.0;
    final icono = iconoDelEstado(estado);
    return Container(
      width: lado,
      height: lado,
      decoration: BoxDecoration(
        color: colorDelEstado(estado),
        shape: BoxShape.circle,
        border: Border.all(color: Colors.white, width: 2),
        boxShadow: [
          if (elegido) const BoxShadow(color: marca900, spreadRadius: 3),
          const BoxShadow(color: Color(0x730F172A), blurRadius: 3, offset: Offset(0, 1)),
        ],
      ),
      child: icono == null ? null : Icon(icono, size: elegido ? 18 : 14, color: Colors.white),
    );
  }
}

/// Un punto de referencia: un cuadrado redondeado `marca-900` con un edificio, de otra forma que los clientes.
class MarcadorDePunto extends StatelessWidget {
  const MarcadorDePunto({super.key, this.lado = 28});

  final double lado;

  @override
  Widget build(BuildContext context) => Container(
    width: lado,
    height: lado,
    decoration: BoxDecoration(
      color: marca900,
      borderRadius: BorderRadius.circular(6),
      border: Border.all(color: Colors.white, width: 2),
      boxShadow: const [BoxShadow(color: Color(0x730F172A), blurRadius: 3, offset: Offset(0, 1))],
    ),
    child: Icon(Icons.account_balance, size: lado / 2, color: Colors.white),
  );
}

/// Los puntos de referencia como marcadores, con su nombre para el lector de pantalla.
List<Marker> marcadoresDePuntos(Iterable<PuntoDeReferencia> puntos) => [
  for (final p in puntos)
    Marker(
      point: puntoDe(p.latitud, p.longitud),
      width: 32,
      height: 32,
      child: Semantics(
        label: 'Punto de referencia: ${p.nombre}',
        child: const Center(child: MarcadorDePunto()),
      ),
    ),
];

/// El límite de una zona, con su nombre en el centro: borde `marca-600` y relleno al 12 %; la elegida, `marca-800`, más
/// gruesa y al 20 %. Nunca rojo ni verde, que son de los estados del cliente.
Polygon limiteDeZona(Zona zona, {bool elegida = false}) => Polygon(
  points: [for (final v in zona.limite!) puntoDe(v.latitud, v.longitud)],
  color: _marca500.withValues(alpha: elegida ? 0.20 : 0.12),
  borderColor: elegida ? _marca800 : marca600,
  borderStrokeWidth: elegida ? 3 : 2,
);

/// El nombre de una zona en un recuadro blanco (8.6.9), como el panel: en el centro de su límite, o donde no tape lo
/// que hay que [evitar] (las paradas de la ruta).
Marker nombreDeZona(Zona zona, {Iterable<LatLng> evitar = const []}) => Marker(
  point: lugarDelNombre(zona.limite!, evitar),
  width: 200,
  height: 32,
  child: ExcludeSemantics(child: Center(child: EtiquetaDeMapa(zona.nombre))),
);

/// Dónde va el nombre de una zona: el centro de su límite, o, si hay marcadores que [evitar], el lugar más lejos de
/// ellos entre el centro y los puntos a mitad de camino hacia cada vértice. En el TECNO, con los clientes cerca del
/// centro, el nombre tapaba una parada. El recuadro es más ancho que alto: lo que está al lado pesa más que lo que está
/// arriba o abajo.
LatLng lugarDelNombre(List<Vertice> limite, Iterable<LatLng> evitar) {
  final latitud = limite.map((v) => v.latitud).reduce((a, b) => a + b) / limite.length;
  final longitud = limite.map((v) => v.longitud).reduce((a, b) => a + b) / limite.length;
  final centro = puntoDe(latitud, longitud);
  if (evitar.isEmpty) return centro;

  // Un grado de longitud mide menos que uno de latitud lejos del ecuador; y de ancho, el recuadro es unas 3 veces su alto.
  final ancho = math.cos(latitud * math.pi / 180) / 3;
  double cercania(LatLng lugar) => evitar
      .map((m) => math.pow((m.latitude - lugar.latitude), 2) + math.pow((m.longitude - lugar.longitude) * ancho, 2))
      .reduce(math.min)
      .toDouble();

  final candidatos = [
    centro,
    for (final v in limite) puntoDe((latitud + v.latitud) / 2, (longitud + v.longitud) / 2),
  ];
  return candidatos.reduce((mejor, otro) => cercania(otro) > cercania(mejor) ? otro : mejor);
}

/// Un texto sobre el mapa en un recuadro blanco: el nombre de una zona o "Tú". No crece con la letra del sistema: es
/// parte del dibujo, y la leyenda y las listas dicen lo mismo con la letra del usuario.
class EtiquetaDeMapa extends StatelessWidget {
  const EtiquetaDeMapa(this.contenido, {super.key});

  final String contenido;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
    decoration: BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(4),
      boxShadow: const [BoxShadow(color: Color(0x66000000), blurRadius: 3, offset: Offset(0, 1))],
    ),
    child: Text(
      contenido,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      textScaler: TextScaler.noScaling,
      style: const TextStyle(fontSize: 14, height: 20 / 14, fontWeight: FontWeight.w600, color: texto),
    ),
  );
}

/// Una entrada de la leyenda: la misma muestra que en el mapa y su nombre.
class EntradaDeLeyenda {
  const EntradaDeLeyenda(this.muestra, this.texto);

  final Widget muestra;
  final String texto;
}

/// La muestra de la zona en la leyenda: un cuadrado con el borde y el relleno del límite.
const muestraDeZona = DecoratedBox(
  decoration: BoxDecoration(
    color: Color(0x263A6FB5),
    border: Border.fromBorderSide(BorderSide(color: marca600, width: 2)),
    borderRadius: BorderRadius.all(Radius.circular(3)),
  ),
  child: SizedBox.square(dimension: 20),
);

/// La leyenda de un mapa (docs/diseno.md, "Mapas"): cada muestra con su texto, en renglones que se acomodan al ancho.
class Leyenda extends StatelessWidget {
  const Leyenda(this.entradas, {super.key});

  final List<EntradaDeLeyenda> entradas;

  @override
  Widget build(BuildContext context) => Semantics(
    container: true,
    label: 'Leyenda del mapa',
    child: Wrap(
      spacing: 16,
      runSpacing: 8,
      children: [
        for (final e in entradas)
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              ExcludeSemantics(child: e.muestra),
              const SizedBox(width: 8),
              Flexible(child: Text(e.texto)),
            ],
          ),
      ],
    ),
  );
}
