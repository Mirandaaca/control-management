import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/sesion.dart';
import 'package:gestion_control/domain/permisos.dart';

Sesion _sesionCon(Set<String> permisos) => Sesion(
  id: '1',
  email: 'a@test.local',
  nombreCompleto: 'Prueba',
  rol: 'Promotor',
  permisos: permisos,
);

void main() {
  group('Sesion.tiene', () {
    test('basta con uno de los permisos', () {
      final sesion = _sesionCon({Permisos.ventas.registrarHp});

      expect(sesion.tiene([Permisos.ventas.registrarHp]), isTrue);
      expect(
        sesion.tiene([
          Permisos.pedidos.verBandeja,
          Permisos.ventas.registrarHp,
        ]),
        isTrue,
      );
      expect(sesion.tiene([Permisos.pedidos.verBandeja]), isFalse);
    });

    test('una lista vacía no abre nada', () {
      expect(
        _sesionCon({Permisos.ventas.registrarHp}).tiene(const []),
        isFalse,
      );
    });
  });

  // La app y el panel usan las mismas claves: si cambian en permisos.js, esta prueba avisa que falta cambiarlas aquí.
  test('las claves son las mismas 66 del panel', () {
    final archivo = File('../../control-gestion-ui/src/context/permisos.js');
    if (!archivo.existsSync()) {
      markTestSkipped('No está el panel junto a la app.');
      return;
    }
    final bloque = archivo
        .readAsStringSync()
        .split('export const PANTALLAS')
        .first;
    final delPanel = RegExp(
      r"'([a-z]+\.[a-z-]+)'",
    ).allMatches(bloque).map((m) => m.group(1)!).toSet();

    expect(Permisos.todas.length, 66);
    expect(Permisos.todas.toSet(), delPanel);
  });
}
