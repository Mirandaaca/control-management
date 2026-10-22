import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/cobranza.dart';
import 'package:gestion_control/data/models/pagina.dart';
import 'package:gestion_control/data/repositories/cobranzas_repository.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/cobranzas/cobranza_screen.dart';
import 'package:gestion_control/ui/cobranzas/cobranzas_screen.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';
import 'package:go_router/go_router.dart';

import '../ayudantes.dart';

void main() {
  late CobranzasRepositoryFalso cobranzas;
  late CamaraFalsa camara;

  setUp(() {
    cobranzas = CobranzasRepositoryFalso();
    camara = CamaraFalsa();
  });

  final rutas = <RouteBase>[
    GoRoute(
      path: '/',
      builder: (context, state) => const CobranzasScreen(),
      routes: [
        GoRoute(
          path: 'cobranzas/:id',
          builder: (context, state) =>
              CobranzaScreen(id: int.parse(state.pathParameters['id']!), mensaje: state.extra as String?),
        ),
      ],
    ),
  ];

  Future<GoRouter> abrir(
    WidgetTester tester, {
    String inicio = '/',
    double letra = 1,
    double alto = 1200,
    List<String>? permisos,
  }) => abrirConRouter(
    tester,
    rutas: rutas,
    inicio: inicio,
    letra: letra,
    alto: alto,
    overrides: [
      cobranzasRepositoryProvider.overrideWithValue(cobranzas),
      camaraProvider.overrideWithValue(camara),
      sesionProvider.overrideWith(
        () => SesionFija(
          ConSesion(
            promotorCon(
              permisos ??
                  [
                    Permisos.cobranzas.consultarDeudas,
                    Permisos.cobranzas.consultarPagos,
                    Permisos.cobranzas.registrarPago,
                  ],
            ),
          ),
        ),
      ),
    ],
  );

  group('la lista (3.2)', () {
    testWidgets('arriba, cuánto hay por cobrar y cuántas están vencidas', (tester) async {
      await abrir(tester);

      expect(find.text('Bs 467,00'), findsOneWidget);
      expect(find.text('1'), findsOneWidget);
    });

    testWidgets('al abrir muestra las por cobrar, con su saldo, su vencimiento y su mora', (tester) async {
      await abrir(tester);

      expect(cobranzas.listados.first.filtro, FiltroDeCobranzas.porCobrar);
      expect(find.text('Marta Rivero Salas'), findsOneWidget);
      expect(find.text('Venció el 15/10/2026 · 14 días de mora'), findsOneWidget);
      expect(find.text('Vencida'), findsWidgets);
      expect(find.text('COB-2026-00012 · HP-2026-00018'), findsOneWidget);
      expect(find.text('Saldo Bs 140,00'), findsOneWidget);
      expect(find.text('Vence el 07/11/2026'), findsOneWidget);
    });

    testWidgets('"Vencidas" y "Todas" piden su filtro', (tester) async {
      await abrir(tester);

      // "Vencidas" también es el título del resumen: se toca el del filtro.
      await tester.tap(find.widgetWithText(InkWell, 'Vencidas'));
      await tester.pump();
      await tester.tap(find.text('Todas'));
      await tester.pump();

      expect(cobranzas.listados.map((l) => l.filtro), [
        FiltroDeCobranzas.porCobrar,
        FiltroDeCobranzas.vencidas,
        FiltroDeCobranzas.todas,
      ]);
    });

    testWidgets('sin deudas, o sin vencidas, un mensaje para cada caso', (tester) async {
      cobranzas.alListar = (_, _) async => const Pagina(items: [], total: 0, numero: 1, tamano: 25);
      await abrir(tester);
      expect(find.text('No hay deudas por cobrar'), findsOneWidget);

      await tester.tap(find.widgetWithText(InkWell, 'Vencidas'));
      await tester.pump();
      expect(find.text('No hay deudas vencidas'), findsOneWidget);
    });

    testWidgets('abierta desde un cliente, pide solo las de él y lo nombra', (tester) async {
      tamanoDeCelular(tester, alto: 1200);
      await tester.pumpWidget(
        conLaApp(
          const CobranzasScreen(clienteId: 42, clienteNombre: 'Rosa Quispe Mamani'),
          overrides: [
            cobranzasRepositoryProvider.overrideWithValue(cobranzas),
            sesionProvider.overrideWith(() => SesionFija(ConSesion(promotorCon(const [])))),
          ],
        ),
      );
      await tester.pump();

      expect(cobranzas.listados.single.clienteId, 42);
      expect(find.text('De Rosa Quispe Mamani'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, letra: 2, alto: 800);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('el detalle', () {
    testWidgets('muestra el total, lo pagado, el saldo, el vencimiento y el historial', (tester) async {
      await abrir(tester, inicio: '/cobranzas/12');

      expect(find.text('Rosa Quispe Mamani'), findsOneWidget);
      expect(find.text('Bs 240,00'), findsOneWidget);
      expect(find.text('Bs 140,00'), findsOneWidget);
      expect(find.text('Vence el 07/11/2026.'), findsOneWidget);
      // Cada pago dice quién lo registró y si está anulado (7.3.10, paso 3.2).
      expect(find.text('QR · ref. 48213097'), findsOneWidget);
      expect(find.text('Lo registró Lucía Vargas Rojas'), findsOneWidget);
      expect(find.text('Ver comprobante'), findsOneWidget);
    });

    testWidgets('un pago anulado lo dice, con su motivo, y no recibe comprobante', (tester) async {
      cobranzas.alObtener = (_) async => cobranzaDeRosa(
        pagado: 0,
        pagos: const [
          Pago(
            id: 1,
            monto: 100,
            metodo: MetodoPago.efectivo,
            fecha: '2026-10-29T14:00:00',
            registradoPor: 'Lucía Vargas Rojas',
            anulado: true,
            motivoDeAnulacion: 'Se registró dos veces.',
          ),
        ],
        estado: EstadoCobranza.pendiente,
      );
      await abrir(tester, inicio: '/cobranzas/12');

      expect(find.text('Anulado'), findsOneWidget);
      expect(find.text('Motivo de la anulación: Se registró dos veces.'), findsOneWidget);
      expect(find.text('Adjuntar comprobante'), findsNothing);
    });

    testWidgets('adjuntar el comprobante de un pago sin él toma la foto y la sube', (tester) async {
      cobranzas.alObtener = (_) async => cobranzaDeRosa(
        pagos: const [
          Pago(
            id: 1,
            monto: 100,
            metodo: MetodoPago.efectivo,
            fecha: '2026-10-29T14:00:00',
            registradoPor: 'Carlos Mendoza Flores',
            anulado: false,
          ),
        ],
      );
      await abrir(tester, inicio: '/cobranzas/12');

      await tester.tap(find.text('Adjuntar comprobante'));
      await tester.pump();
      await tester.pump();

      expect(camara.tomas, 1);
      expect(cobranzas.subidas.single.pagoId, 1);
      expect(find.text('Se adjuntó el comprobante del pago de Bs 100,00.'), findsOneWidget);
    });

    testWidgets('reemplazarlo pide confirmar antes: la foto anterior se borra', (tester) async {
      await abrir(tester, inicio: '/cobranzas/12');

      await tester.tap(find.text('Reemplazar comprobante'));
      await tester.pumpAndSettle();
      expect(find.text('¿Reemplazar el comprobante?'), findsOneWidget);
      expect(camara.tomas, 0);

      await tester.tap(find.widgetWithText(FilledButton, 'Reemplazar comprobante'));
      await tester.pumpAndSettle();
      expect(cobranzas.subidas, hasLength(1));
      expect(find.text('Se reemplazó el comprobante del pago de Bs 100,00.'), findsOneWidget);
    });

    testWidgets('si la subida falla, Reintentar sube la misma foto', (tester) async {
      var intentos = 0;
      cobranzas.alSubir = (_, _) async {
        if (intentos++ == 0) throw sinConexion;
        return cobranzaDeRosa();
      };
      await abrir(tester, inicio: '/cobranzas/12');
      await tester.tap(find.text('Reemplazar comprobante'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Reemplazar comprobante'));
      await tester.pumpAndSettle();
      expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);

      await tester.tap(find.text('Reintentar'));
      await tester.pumpAndSettle();

      expect(camara.tomas, 1);
      expect(cobranzas.subidas, hasLength(2));
      expect(identical(cobranzas.subidas.first.foto, cobranzas.subidas.last.foto), isTrue);
    });

    testWidgets('sin el permiso de registrar pagos, no adjunta comprobantes', (tester) async {
      await abrir(tester, inicio: '/cobranzas/12', permisos: [Permisos.cobranzas.consultarDeudas]);

      expect(find.text('Reemplazar comprobante'), findsNothing);
      expect(find.text('Ver comprobante'), findsOneWidget);
    });

    testWidgets('ver el comprobante lo abre a pantalla completa', (tester) async {
      await abrir(tester, inicio: '/cobranzas/12');

      await tester.tap(find.text('Ver comprobante'));
      await tester.pumpAndSettle();

      expect(find.bySemanticsLabel('Foto del comprobante'), findsOneWidget);
      await tester.tap(find.byTooltip('Cerrar'));
      await tester.pumpAndSettle();
      expect(find.text('Comprobante'), findsNothing);
    });

    testWidgets('si el archivo no es una imagen, lo dice en vez de quedar en blanco', (tester) async {
      cobranzas.bytesDelComprobante = Uint8List.fromList(utf8.encode('no es una foto'));
      await abrir(tester, inicio: '/cobranzas/12');

      await tester.tap(find.text('Ver comprobante'));
      await tester.pumpAndSettle();
      // La decodificación de la imagen corre fuera del reloj falso de la prueba.
      await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 200)));
      await tester.pumpAndSettle();

      expect(find.text('No se pudo mostrar la foto.'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, inicio: '/cobranzas/12', letra: 2, alto: 800);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });
}
