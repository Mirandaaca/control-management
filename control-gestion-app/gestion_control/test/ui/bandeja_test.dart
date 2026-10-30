import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/hoja_pedido.dart';
import 'package:gestion_control/data/models/pedido.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/formato.dart';
import 'package:gestion_control/ui/core/ubicacion/mapa_de_ubicacion.dart';
import 'package:gestion_control/ui/pedidos/bandeja_screen.dart';
import 'package:gestion_control/ui/pedidos/entrega_screen.dart';
import 'package:gestion_control/ui/pedidos/pedido_screen.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';
import 'package:go_router/go_router.dart';

import '../ayudantes.dart';

void main() {
  late PedidosRepositoryFalso pedidos;
  late UbicacionFalsa gps;

  setUp(() {
    pedidos = PedidosRepositoryFalso();
    gps = UbicacionFalsa();
  });

  final permisos = [Permisos.pedidos.verBandeja, Permisos.pedidos.registrarEntrega, Permisos.pedidos.consultarPedidos];

  final rutas = <RouteBase>[
    GoRoute(
      path: '/',
      builder: (context, state) => const Scaffold(body: Text('Menú')),
      routes: [
        GoRoute(
          path: 'bandeja',
          builder: (context, state) => const BandejaScreen(),
          routes: [
            GoRoute(
              path: ':id',
              builder: (context, state) =>
                  PedidoScreen(id: int.parse(state.pathParameters['id']!), desdeLaBandeja: true),
              routes: [
                GoRoute(
                  path: 'entrega',
                  builder: (context, state) => EntregaScreen(id: int.parse(state.pathParameters['id']!)),
                ),
              ],
            ),
          ],
        ),
      ],
    ),
  ];

  Future<GoRouter> abrir(
    WidgetTester tester, {
    String inicio = '/bandeja',
    double letra = 1,
    double alto = 2000,
    List<String>? conPermisos,
  }) => abrirConRouter(
    tester,
    rutas: rutas,
    inicio: inicio,
    letra: letra,
    alto: alto,
    overrides: [
      pedidosRepositoryProvider.overrideWithValue(pedidos),
      servicioDeUbicacionProvider.overrideWithValue(gps),
      mosaicosDelMapaProvider.overrideWithValue(false),
      sesionProvider.overrideWith(() => SesionFija(ConSesion(promotorCon(conPermisos ?? permisos)))),
    ],
  );

  // Esperar una navegación: pumpAndSettle no sirve con el indicador del GPS girando (dejaría pasar sus 30 s).
  Future<void> navegar(WidgetTester tester) async {
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
  }

  group('la bandeja del día (7.3.3, pasos 3 a 4; 8.6.10)', () {
    testWidgets('el día de hoy, cuántas entregas y cada pedido por hora', (tester) async {
      await abrir(tester);
      await tester.pump();

      expect(find.text('Hoy, ${formatearMomento(DateTime.now().toUtc())}'), findsOneWidget);
      expect(find.text('3 entregas'), findsOneWidget);
      expect(find.text('09:00'), findsOneWidget);
      expect(find.text('10:30'), findsOneWidget);
      expect(find.text('15:00'), findsOneWidget);
      expect(find.text('Rosa Quispe Mamani'), findsOneWidget);
      expect(find.text('Calle 4 N.º 120, frente al Mercado Norte'), findsOneWidget);
      expect(find.text('PED-2026-00031'), findsOneWidget);
      expect(find.text('Bs 76,00'), findsOneWidget);
    });

    testWidgets('solo los que están en ruta tienen "Registrar entrega"; el pendiente dice por qué', (tester) async {
      await abrir(tester);
      await tester.pump();

      expect(find.text('Registrar entrega'), findsNWidgets(2));
      expect(find.text('Se entrega cuando salga a ruta.'), findsOneWidget);
    });

    testWidgets('sin el permiso de entregar, no ofrece "Registrar entrega"', (tester) async {
      await abrir(tester, conPermisos: [Permisos.pedidos.verBandeja]);
      await tester.pump();

      expect(find.text('Registrar entrega'), findsNothing);
    });

    testWidgets('vacía, con el texto del 8.6.12 (ext. 3.1a)', (tester) async {
      pedidos.alPedirLaBandeja = () async => const [];
      await abrir(tester);
      await tester.pump();

      expect(find.text('No tienes entregas para hoy'), findsOneWidget);
      expect(
        find.text(
          'Aquí aparecen los pedidos que la secretaria te asigna para hoy, menos los entregados y los anulados.',
        ),
        findsOneWidget,
      );
    });

    testWidgets('sin conexión, "Reintentar" la vuelve a pedir', (tester) async {
      pedidos.alPedirLaBandeja = () => throw ErrorDeApi.sinInternet;
      await abrir(tester);
      await tester.pump();
      expect(find.text('No tienes conexión a internet.'), findsOneWidget);

      pedidos.alPedirLaBandeja = () async => bandejaDePrueba;
      await tester.tap(find.text('Reintentar'));
      await tester.pump();
      await tester.pump();

      expect(find.text('3 entregas'), findsOneWidget);
    });

    testWidgets('tocar un pedido abre su detalle, con "Registrar entrega" si está en ruta', (tester) async {
      await abrir(tester);
      await tester.pump();

      await tester.tap(find.text('Rosa Quispe Mamani'));
      await navegar(tester);

      expect(find.text('Historial'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Registrar entrega'), findsOneWidget);
    });

    testWidgets('el detalle de uno pendiente dice que se entrega cuando salga a ruta, sin el botón', (tester) async {
      pedidos.alObtener = (_) async => pedidoDePrueba(id: 34, estado: EstadosDePedido.pendiente);
      await abrir(tester, inicio: '/bandeja/34');
      await tester.pump();

      expect(find.text('Se entrega cuando salga a ruta.'), findsOneWidget);
      expect(find.text('Registrar entrega'), findsNothing);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, letra: 2, alto: 800);
      await tester.pump();
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('registrar la entrega (7.3.2, paso 3)', () {
    Future<void> abrirEntrega(WidgetTester tester, {double letra = 1, double alto = 2000}) async {
      await abrir(tester, inicio: '/bandeja/31/entrega', letra: letra, alto: alto);
      await tester.pump();
      gps.leer(-17.7801, -63.1795, 8);
      await tester.pump();
    }

    Future<void> llenar(WidgetTester tester, {TipoVenta tipo = TipoVenta.credito}) async {
      await tester.enterText(campo('Quién recibió'), 'Doña Rosa');
      await tester.tap(find.text(tipo == TipoVenta.credito ? 'Crédito' : 'Contado'));
      await tester.pump();
    }

    Future<void> registrarYConfirmar(WidgetTester tester) async {
      await tester.tap(find.widgetWithText(FilledButton, 'Registrar entrega'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));
      await tester.tap(find.descendant(of: find.byType(AlertDialog), matching: find.text('Registrar entrega')));
      await navegar(tester);
    }

    testWidgets('muestra los productos y el monto, y busca la ubicación sola ("…la entrega")', (tester) async {
      gps.activado = false;
      await abrir(tester, inicio: '/bandeja/31/entrega');
      await tester.pump();
      await tester.pump();

      expect(find.text('2 × Bs 38,00'), findsOneWidget);
      expect(find.text('El GPS está desactivado.'), findsOneWidget);
      expect(find.text('Actívalo para registrar la entrega: sin ubicación, la entrega no se guarda.'), findsOneWidget);
    });

    testWidgets('sin quién recibió ni el tipo de venta, cada error va en su campo (ext. 3.1a)', (tester) async {
      await abrirEntrega(tester);

      await tester.tap(find.widgetWithText(FilledButton, 'Registrar entrega'));
      await tester.pump();

      expect(find.text('Escribe quién recibió el pedido.'), findsOneWidget);
      expect(find.text('Elige si la venta es al contado o a crédito.'), findsOneWidget);
      expect(find.byType(AlertDialog), findsNothing);
      expect(pedidos.entregas, isEmpty);
    });

    testWidgets('pide confirmar y registra con la ubicación; vuelve a la bandeja con el aviso', (tester) async {
      await abrirEntrega(tester);
      await llenar(tester);

      await tester.tap(find.widgetWithText(FilledButton, 'Registrar entrega'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));
      expect(find.text('¿Registrar la entrega de PED-2026-00031?'), findsOneWidget);
      expect(
        find.text(
          'Se crea y se confirma la venta con los productos del pedido y se descuenta el stock. No se puede deshacer.',
        ),
        findsOneWidget,
      );
      await tester.tap(find.descendant(of: find.byType(AlertDialog), matching: find.text('Registrar entrega')));
      await navegar(tester);

      final envio = pedidos.entregas.single;
      expect(envio.id, 31);
      expect(envio.datos.recibidoPor, 'Doña Rosa');
      expect(envio.datos.tipoVenta, TipoVenta.credito);
      expect(envio.ubicacion.latitud, -17.7801);
      // En la bandeja, a donde volvió: el SnackBar pasa de una pantalla a otra.
      expect(find.text('Bandeja del día'), findsOneWidget);
      expect(
        exito(
          'PED-2026-00031 quedó entregado: nació la venta HP-2026-00040, se descontó el stock y nació la deuda '
          'COB-2026-00030.',
        ),
        findsOneWidget,
      );
      expect(pedidos.bandejas, greaterThanOrEqualTo(1));
    });

    testWidgets('sin stock, el error va en la línea del producto que no alcanza (ext. 3.2a)', (tester) async {
      pedidos.alObtener = (_) async => pedidoDePrueba(conDosLineas: true);
      pedidos.alEntregar = (_) => throw const ErrorDeApi(
        mensaje: 'No hay stock suficiente de Detergente en polvo 1 kg: hay 2 y la venta pide 4.',
        codigo: 'stock_insuficiente',
        estado: 400,
        campos: {
          'detalles[1].cantidad': 'No hay stock suficiente de Detergente en polvo 1 kg: hay 2 y la venta pide 4.',
        },
      );
      await abrirEntrega(tester);
      await llenar(tester);

      await registrarYConfirmar(tester);

      final linea = find.ancestor(of: find.text('Detergente en polvo 1 kg'), matching: find.byType(LineaDelPedido));
      expect(
        find.descendant(
          of: linea,
          matching: find.text('No hay stock suficiente de Detergente en polvo 1 kg: hay 2 y la venta pide 4.'),
        ),
        findsOneWidget,
      );
    });

    testWidgets('sin respuesta, "Reintentar" relee el pedido: si ya quedó entregado, no lo envía otra vez', (
      tester,
    ) async {
      pedidos.alEntregar = (_) => throw sinConexion;
      await abrirEntrega(tester);
      await llenar(tester);
      await registrarYConfirmar(tester);
      expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);

      pedidos.alObtener = (_) async => pedidoDePrueba(estado: EstadosDePedido.entregado);
      await tester.tap(find.text('Reintentar'));
      await navegar(tester);

      expect(pedidos.entregas, hasLength(1));
      expect(find.textContaining('PED-2026-00031 quedó entregado'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrirEntrega(tester, letra: 2, alto: 800);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });
}
