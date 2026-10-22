import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/ui/core/widgets/barra_de_acciones.dart';
import 'package:gestion_control/ui/core/widgets/campos_numericos.dart';
import 'package:gestion_control/ui/core/widgets/confirmar.dart';
import 'package:gestion_control/ui/core/widgets/segmentado.dart';

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

  group('confirmar', () {
    Future<void> abrir(WidgetTester tester, String accion) async {
      await cargarInter();
      tamanoDeCelular(tester, letra: 1);
      await tester.pumpWidget(
        conLaApp(
          Builder(
            builder: (context) => TextButton(
              onPressed: () => confirmar(context, titulo: '¿Seguro?', contenido: const Text('Algo.'), accion: accion),
              child: const Text('Abrir'),
            ),
          ),
        ),
      );
      await tester.tap(find.text('Abrir'));
      await tester.pumpAndSettle();
    }

    testWidgets('mide 312 dp, como en el 8.6.12, y dos acciones cortas van lado a lado', (tester) async {
      await abrir(tester, 'Eliminar');

      final tarjeta = find.descendant(of: find.byType(Dialog), matching: find.byType(Material)).first;
      expect(tester.getSize(tarjeta).width, 312);
      final cancelar = tester.getRect(find.widgetWithText(OutlinedButton, 'Cancelar'));
      final eliminar = tester.getRect(find.widgetWithText(FilledButton, 'Eliminar'));
      expect(cancelar.top, eliminar.top);
    });

    testWidgets('si no entran lado a lado, se apilan separadas, con la acción abajo', (tester) async {
      await abrir(tester, 'Confirmar venta');

      final cancelar = tester.getRect(find.widgetWithText(OutlinedButton, 'Cancelar'));
      final confirmarVenta = tester.getRect(find.widgetWithText(FilledButton, 'Confirmar venta'));
      expect(confirmarVenta.top - cancelar.bottom, 8);
    });
  });

  group('Segmentado', () {
    Future<void> abrir(WidgetTester tester, {required double letra, int valor = 1}) async {
      await cargarInter();
      tamanoDeCelular(tester, letra: letra);
      await tester.pumpWidget(
        conLaApp(
          Scaffold(
            body: Padding(
              padding: const EdgeInsets.all(16),
              child: Segmentado(
                opciones: const [(1, 'Por cobrar'), (2, 'Vencidas'), (3, 'Todas')],
                valor: valor,
                alCambiar: (_) {},
              ),
            ),
          ),
        ),
      );
    }

    testWidgets('van lado a lado, y la elegida no parte su palabra por el check ("Vencida-s")', (tester) async {
      await abrir(tester, letra: 1, valor: 2);

      final vencidas = tester.getRect(find.text('Vencidas'));
      expect(vencidas.top, tester.getTopLeft(find.text('Todas')).dy);
      expect(vencidas.height, lessThan(2 * 16));
    });

    testWidgets('con la letra al 200 %, si una palabra no entra, se apilan en vez de partirla', (tester) async {
      await abrir(tester, letra: 2);

      final porCobrar = tester.getRect(find.text('Por cobrar'));
      expect(tester.getTopLeft(find.text('Vencidas')).dy, greaterThan(porCobrar.bottom));
      // En una línea: "cobrar" no se parte.
      expect(porCobrar.height, lessThan(2 * 16 * 2));
      expect(tester.takeException(), isNull);
    });
  });

  test('el teclado de un monto es el numérico con decimales', () {
    expect(tecladoDecimal, const TextInputType.numberWithOptions(decimal: true));
    expect(FilteringTextInputFormatter.digitsOnly, isNotNull);
  });
}
