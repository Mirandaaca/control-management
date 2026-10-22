import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/texto.dart';
import 'package:gestion_control/ui/productos/productos_screen.dart';

import '../ayudantes.dart';

void main() {
  late ProductosRepositoryFalso productos;

  setUp(() => productos = ProductosRepositoryFalso());

  Future<void> abrir(WidgetTester tester, {double letra = 1}) async {
    tamanoDeCelular(tester, letra: letra);
    await tester.pumpWidget(
      conLaApp(const ProductosScreen(), overrides: [productosRepositoryProvider.overrideWithValue(productos)]),
    );
    await tester.pump();
  }

  test('buscar no distingue mayúsculas ni tildes, como en el panel', () {
    expect(normalizar('  Jabón en BARRA '), 'jabon en barra');
    expect(coincide('jabon', ['Jabón en barra']), isTrue);
    expect(coincide('', ['lo que sea']), isTrue);
    expect(coincide('arroz', ['Detergente', null]), isFalse);
  });

  testWidgets('muestra cada producto con su código, su categoría, su precio y su stock', (tester) async {
    await abrir(tester);

    expect(find.text('Arroz grano largo 5 kg'), findsOneWidget);
    expect(find.text('ARZ-5KG · Arroz'), findsOneWidget);
    expect(find.text('Bs 38,00'), findsOneWidget);
    expect(find.text('Stock: 120'), findsOneWidget);
    expect(find.text('Bs 16,50'), findsOneWidget);
    expect(find.text('3 productos'), findsOneWidget);
  });

  testWidgets('uno sin stock lo dice con su etiqueta', (tester) async {
    await abrir(tester);

    expect(find.text('Sin stock'), findsOneWidget);
    expect(find.text('Stock: 0'), findsNothing);
  });

  testWidgets('filtra por el buscador y por la categoría', (tester) async {
    await abrir(tester);

    await tester.enterText(campo('Buscar'), 'det');
    await tester.pump();
    expect(find.text('Detergente en polvo 1 kg'), findsOneWidget);
    expect(find.text('Arroz grano largo 5 kg'), findsNothing);
    expect(find.text('1 de 3 productos'), findsOneWidget);

    await tester.enterText(campo('Buscar'), '');
    await tester.tap(find.text('Todas las categorías'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Limpieza').last);
    await tester.pumpAndSettle();
    expect(find.text('2 de 3 productos'), findsOneWidget);
    expect(find.text('Jabón en barra'), findsOneWidget);
    expect(find.text('Arroz grano largo 5 kg'), findsNothing);
  });

  testWidgets('sin coincidencias lo dice, distinto de una lista vacía', (tester) async {
    await abrir(tester);

    await tester.enterText(campo('Buscar'), 'cemento');
    await tester.pump();

    expect(find.text('Ningún producto coincide con la búsqueda.'), findsOneWidget);
  });

  testWidgets('sin productos lo dice', (tester) async {
    productos.alListar = () async => const [];
    await abrir(tester);

    expect(find.text('Todavía no hay productos'), findsOneWidget);
  });

  testWidgets('si la carga falla, ofrece reintentar', (tester) async {
    productos.alListar = () async => throw sinConexion;
    await abrir(tester);
    expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);

    productos.alListar = () async => const [arroz];
    await tester.tap(find.text('Reintentar'));
    await tester.pump();
    await tester.pump();
    expect(find.text('Arroz grano largo 5 kg'), findsOneWidget);
  });

  testWidgets('sin internet lo dice con qué revisar', (tester) async {
    productos.alListar = () async => throw ErrorDeApi.sinInternet;
    await abrir(tester);

    expect(find.text('No tienes conexión a internet.'), findsOneWidget);
    expect(find.text('Revisa los datos móviles o el wifi y vuelve a intentarlo.'), findsOneWidget);
  });

  testWidgets('con la letra al 200 % cumple las guías de accesibilidad', (tester) async {
    final semantica = tester.ensureSemantics();
    await abrir(tester, letra: 2);
    await cumpleLasGuias(tester);
    semantica.dispose();
  });
}
