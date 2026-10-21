import 'package:flutter/material.dart';

import '../core/tema.dart';

/// La franja de arriba del inicio de sesión: el mismo mapa abstracto del territorio que el panel (docs/diseno.md,
/// "Inicio de sesión"), con la marca encima. Es decorativa: el lector de pantalla solo lee el nombre del sistema.
class FranjaDelTerritorio extends StatelessWidget {
  const FranjaDelTerritorio({super.key});

  @override
  Widget build(BuildContext context) {
    final arriba = MediaQuery.paddingOf(context).top;
    return Container(
      height: 208 + arriba,
      color: marca900,
      child: Stack(
        fit: StackFit.expand,
        children: [
          const ExcludeSemantics(child: CustomPaint(painter: _Territorio())),
          Positioned(
            left: 16,
            top: arriba + 16,
            right: 16,
            child: Align(
              alignment: Alignment.topLeft,
              child: Container(
                padding: const EdgeInsets.fromLTRB(12, 12, 20, 12),
                decoration: BoxDecoration(
                  // El fondo detrás del texto lo separa de la ruta, que pasa por debajo.
                  color: marca900.withValues(alpha: 0.8),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    ExcludeSemantics(
                      child: Container(
                        width: 40,
                        height: 40,
                        alignment: Alignment.center,
                        decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(12)),
                        // Es un logotipo, como un ícono: no crece con la letra del sistema.
                        child: const Text(
                          'DC',
                          textScaler: TextScaler.noScaling,
                          style: TextStyle(color: marca700, fontWeight: FontWeight.w600, fontSize: 14),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    const Flexible(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            'Control de Gestión',
                            style: TextStyle(color: Colors.white, fontWeight: FontWeight.w600),
                          ),
                          Text('Del Campo SRL', style: TextStyle(color: Color(0xFFB9D1EB), fontSize: 14)),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Polígonos de zonas de venta, una ruta y el promotor, con las coordenadas de `Login.jsx` del panel (lienzo de
/// 800 × 900). Se escala para cubrir la franja, como `preserveAspectRatio="xMidYMid slice"`.
class _Territorio extends CustomPainter {
  const _Territorio();

  static const _zonas = [
    ([0, 0, 330, 0, 290, 240, 0, 300], Color(0xFF1C3A66)),
    ([330, 0, 800, 0, 800, 180, 560, 260, 290, 240], Color(0xFF22477D)),
    ([0, 300, 290, 240, 360, 520, 120, 610, 0, 560], Color(0xFF152B4C)),
    ([290, 240, 560, 260, 620, 500, 360, 520], Color(0xFF2A5899)),
    ([560, 260, 800, 180, 800, 560, 620, 500], Color(0xFF1C3A66)),
    ([0, 560, 120, 610, 360, 520, 420, 900, 0, 900], Color(0xFF22477D)),
    ([360, 520, 620, 500, 800, 560, 800, 900, 420, 900], Color(0xFF152B4C)),
  ];
  static const _clientes = [
    Offset(150, 150),
    Offset(245, 330),
    Offset(430, 370),
    Offset(520, 300),
    Offset(690, 380),
    Offset(610, 470),
  ];
  static const _promotor = Offset(470, 470);
  static const _trigo = Color(0xFFE0A84A);

  @override
  void paint(Canvas canvas, Size size) {
    final escala = size.width / 800 > size.height / 900 ? size.width / 800 : size.height / 900;
    canvas
      ..save()
      // Escalado para cubrir, el dibujo es más alto que la franja: lo que sobra no debe tapar el formulario.
      ..clipRect(Offset.zero & size)
      ..translate((size.width - 800 * escala) / 2, (size.height - 900 * escala) / 2)
      ..scale(escala);

    final borde = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.5
      ..color = const Color(0xFF3A6FB5).withValues(alpha: 0.55);
    for (final (puntos, color) in _zonas) {
      final zona = Path()..moveTo(puntos[0].toDouble(), puntos[1].toDouble());
      for (var i = 2; i < puntos.length; i += 2) {
        zona.lineTo(puntos[i].toDouble(), puntos[i + 1].toDouble());
      }
      zona.close();
      canvas
        ..drawPath(zona, Paint()..color = color)
        ..drawPath(zona, borde);
    }

    final ruta = Path()..moveTo(_clientes.first.dx, _clientes.first.dy);
    for (final punto in [..._clientes.skip(1), _promotor]) {
      ruta.lineTo(punto.dx, punto.dy);
    }
    canvas.drawPath(
      ruta,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 3
        ..strokeCap = StrokeCap.round
        ..strokeJoin = StrokeJoin.round
        ..color = const Color(0xFF8BB2DD),
    );

    final bordeCliente = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3
      ..color = marca600;
    for (final cliente in _clientes) {
      canvas
        ..drawCircle(cliente, 7, Paint()..color = Colors.white)
        ..drawCircle(cliente, 7, bordeCliente);
    }

    canvas
      ..drawCircle(
        _promotor,
        20,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2
          ..color = _trigo.withValues(alpha: 0.6),
      )
      ..drawCircle(_promotor, 10, Paint()..color = _trigo)
      ..drawCircle(
        _promotor,
        10,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = 3
          ..color = Colors.white,
      )
      ..restore();
  }

  @override
  bool shouldRepaint(_Territorio oldDelegate) => false;
}
