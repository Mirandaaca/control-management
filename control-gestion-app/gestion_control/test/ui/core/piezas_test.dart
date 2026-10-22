import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/ui/core/widgets/barra_de_acciones.dart';
import 'package:gestion_control/ui/core/widgets/campos_numericos.dart';

import '../../ayudantes.dart';

void main() {
  group('FormatoDecimal', () {
    TextEditingValue escribir(FormatoDecimal formato, String anterior, String nuevo) =>
        formato.formatEditUpdate(TextEditingValue(text: anterior), TextEditingValue(text: nuevo));

    test('el punto se escribe como coma', () {
      expect(escribir(FormatoDecimal(), '140', '140.').text, '140,');
    });

    test('no deja más decimales de los que van, ni letras, ni dos comas', () {
      final formato = FormatoDecimal();
      expect(escribir(formato, '140,00', '140,005').text, '140,00');
      expect(escribir(formato, '14', '14a').text, '14');
      expect(escribir(formato, '14,5', '14,5,').text, '14,5');
    });
  });

  group('BarraDeAcciones', () {
    Future<void> abrir(WidgetTester tester, {required double letra}) async {
      tamanoDeCelular(tester, letra: letra);
      await tester.pumpWidget(
        conLaApp(
          Scaffold(
            bottomNavigationBar: BarraDeAcciones(
              secundaria: Accion('Guardar', () {}),
              principal: Accion('Confirmar venta', () {}),
            ),
          ),
        ),
      );
    }

    testWidgets('las dos acciones van lado a lado, la principal a la derecha en dos tercios', (tester) async {
      // Con Inter, que mide como en el celular: la letra de las pruebas dibuja cada letra como un cuadrado.
      await cargarInter();
      await abrir(tester, letra: 1);

      final guardar = tester.getRect(find.widgetWithText(OutlinedButton, 'Guardar'));
      final confirmar = tester.getRect(find.widgetWithText(FilledButton, 'Confirmar venta'));
      expect(guardar.top, confirmar.top);
      expect(guardar.right, lessThan(confirmar.left));
      expect(confirmar.width, closeTo(guardar.width * 2, 1));
      expect(tester.takeException(), isNull);
    });

    testWidgets('con la letra al 200 % se apilan, con la principal abajo', (tester) async {
      await abrir(tester, letra: 2);

      final guardar = tester.getRect(find.widgetWithText(OutlinedButton, 'Guardar'));
      final confirmar = tester.getRect(find.widgetWithText(FilledButton, 'Confirmar venta'));
      expect(confirmar.top, greaterThan(guardar.bottom));
      expect(tester.takeException(), isNull);
    });
  });

  test('el teclado de un monto es el numérico con decimales', () {
    expect(tecladoDecimal, const TextInputType.numberWithOptions(decimal: true));
    expect(FilteringTextInputFormatter.digitsOnly, isNotNull);
  });
}
