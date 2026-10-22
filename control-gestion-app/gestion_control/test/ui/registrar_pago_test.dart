import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/cobranza.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/cobranzas/cobranza_screen.dart';
import 'package:gestion_control/ui/cobranzas/registrar_pago_screen.dart';
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
      path: '/cobranzas/:id',
      builder: (context, state) => CobranzaScreen(id: int.parse(state.pathParameters['id']!)),
      routes: [
        GoRoute(
          path: 'pago',
          builder: (context, state) => RegistrarPagoScreen(cobranzaId: int.parse(state.pathParameters['id']!)),
        ),
      ],
    ),
  ];

  Future<void> abrir(WidgetTester tester, {double letra = 1, double alto = 1600, bool alPago = true}) async {
    await abrirConRouter(
      tester,
      rutas: rutas,
      inicio: '/cobranzas/12',
      letra: letra,
      alto: alto,
      overrides: [
        cobranzasRepositoryProvider.overrideWithValue(cobranzas),
        camaraProvider.overrideWithValue(camara),
        sesionProvider.overrideWith(
          () => SesionFija(
            ConSesion(promotorCon([Permisos.cobranzas.consultarDeudas, Permisos.cobranzas.registrarPago])),
          ),
        ),
      ],
    );
    if (!alPago) return;
    await tester.tap(find.widgetWithText(FilledButton, 'Registrar pago'));
    await tester.pumpAndSettle();
  }

  Future<void> registrar(WidgetTester tester) async {
    await tester.tap(find.widgetWithText(FilledButton, 'Registrar pago'));
    await tester.pumpAndSettle();
  }

  testWidgets('solo las cobranzas que reciben pagos muestran "Registrar pago"', (tester) async {
    cobranzas.alObtener = (_) async => cobranzaDeRosa(pagado: 240);
    await abrir(tester, alPago: false);

    expect(find.text('Pagada'), findsOneWidget);
    expect(find.text('Registrar pago'), findsNothing);
  });

  testWidgets('muestra la cobranza con su saldo, como en el 8.6.7', (tester) async {
    await abrir(tester);

    expect(find.text('COB-2026-00012 · HP-2026-00018'), findsOneWidget);
    expect(find.text('Bs 140,00'), findsOneWidget);
    expect(find.text('Vence el 07/11/2026.'), findsOneWidget);
    expect(find.text('Tomar foto'), findsOneWidget);
    // Con efectivo, la referencia no se muestra.
    expect(find.text('Número de referencia del QR'), findsNothing);
  });

  testWidgets('el monto va hasta el saldo, con el error del 8.6.12 en el campo', (tester) async {
    await abrir(tester);

    await tester.enterText(campo('Monto (Bs)'), '150,00');
    await registrar(tester);
    expect(errorDelCampo(tester, 'Monto (Bs)'), 'El monto no puede superar el saldo (Bs 140,00).');

    await tester.enterText(campo('Monto (Bs)'), '0');
    await registrar(tester);
    expect(errorDelCampo(tester, 'Monto (Bs)'), 'El monto debe ser mayor que cero.');
    expect(cobranzas.pagos, isEmpty);

    // Al corregirlo, el error viejo se va: no queda junto al monto nuevo.
    await tester.enterText(campo('Monto (Bs)'), '140,00');
    await tester.pump();
    expect(errorDelCampo(tester, 'Monto (Bs)'), isNull);
  });

  testWidgets('con QR, el número de referencia es obligatorio', (tester) async {
    await abrir(tester);

    await tester.enterText(campo('Monto (Bs)'), '140');
    await tester.tap(find.text('QR'));
    await tester.pump();
    await registrar(tester);

    expect(errorDelCampo(tester, 'Número de referencia del QR'), 'Escribe el número de referencia del pago con QR.');
    expect(cobranzas.pagos, isEmpty);
  });

  testWidgets('el pago del 8.6.7: con QR y su comprobante, deja la cobranza pagada', (tester) async {
    await abrir(tester);

    await tester.enterText(campo('Monto (Bs)'), '140,00');
    await tester.tap(find.text('QR'));
    await tester.pump();
    await tester.enterText(campo('Número de referencia del QR'), '51762204');
    await tester.enterText(campo('Observaciones (opcional)'), 'Pago del saldo de la HP-2026-00018');
    await tester.tap(find.text('Tomar foto'));
    await tester.pump();
    expect(find.text('Foto tomada'), findsOneWidget);
    expect(find.text('30/10/2026 09:05'), findsOneWidget);

    cobranzas.alObtener = (_) async => cobranzaDeRosa(pagado: 240);
    await registrar(tester);

    final envio = cobranzas.pagos.single;
    expect(envio.datos.monto, 140);
    expect(envio.datos.metodo, MetodoPago.qr);
    expect(envio.datos.referencia, '51762204');
    expect(cobranzas.subidas.single.pagoId, 2);
    expect(find.text('Se registró el pago, con su comprobante: COB-2026-00012 quedó pagada.'), findsOneWidget);
  });

  testWidgets('sin foto y sin saldar, el aviso dice lo que queda por cobrar', (tester) async {
    await abrir(tester);

    await tester.enterText(campo('Monto (Bs)'), '40');
    await registrar(tester);

    expect(cobranzas.subidas, isEmpty);
    expect(find.text('Se registró el pago en COB-2026-00012: quedan Bs 100,00 por cobrar.'), findsOneWidget);
  });

  testWidgets('quitar la foto no pide confirmación: todavía no se subió', (tester) async {
    await abrir(tester);
    await tester.tap(find.text('Tomar foto'));
    await tester.pump();

    await tester.tap(find.text('Quitar'));
    await tester.pump();

    expect(find.byType(AlertDialog), findsNothing);
    expect(find.text('Tomar foto'), findsOneWidget);
  });

  testWidgets('si el comprobante no se sube, el pago no se repite: Reintentar sube solo la foto', (tester) async {
    var intentos = 0;
    cobranzas.alSubir = (_, _) async {
      if (intentos++ == 0) throw sinConexion;
      return cobranzaDeRosa(pagado: 240);
    };
    await abrir(tester);
    await tester.enterText(campo('Monto (Bs)'), '140');
    await tester.tap(find.text('Tomar foto'));
    await tester.pump();

    await registrar(tester);
    expect(find.text('El pago quedó registrado, pero el comprobante no se subió.'), findsOneWidget);
    expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);

    await tester.tap(find.text('Reintentar'));
    await tester.pumpAndSettle();

    expect(cobranzas.pagos, hasLength(1));
    expect(cobranzas.subidas, hasLength(2));
    expect(find.text('Se registró el pago, con su comprobante: COB-2026-00012 quedó pagada.'), findsOneWidget);
  });

  testWidgets('si otro usuario cambió la cobranza, muestra el saldo nuevo y deja el formulario igual', (tester) async {
    cobranzas.alRegistrarPago = (_) async => throw const ErrorDeApi(
      mensaje: 'La cobranza o el saldo del cliente cambió mientras registrabas el pago. Vuelve a intentarlo.',
      codigo: 'conflicto_concurrencia',
      estado: 409,
    );
    await abrir(tester);
    await tester.enterText(campo('Monto (Bs)'), '100');
    cobranzas.alObtener = (_) async => cobranzaDeRosa(pagado: 140);

    await registrar(tester);

    expect(find.text('Bs 100,00'), findsWidgets);
    expect(tester.widget<TextField>(campo('Monto (Bs)')).controller!.text, '100');
    expect(
      find.text('La cobranza o el saldo del cliente cambió mientras registrabas el pago. Vuelve a intentarlo.'),
      findsOneWidget,
    );
  });

  testWidgets('sin conexión no se pierde nada, y Reintentar repite la misma clave', (tester) async {
    var intentos = 0;
    cobranzas.alRegistrarPago = (envio) async {
      if (intentos++ == 0) throw sinConexion;
      return const PagoRegistrado(id: 2, estado: EstadoCobranza.parcial, saldo: 100);
    };
    await abrir(tester);
    await tester.enterText(campo('Monto (Bs)'), '40');

    await registrar(tester);
    expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);
    await tester.tap(find.text('Reintentar'));
    await tester.pumpAndSettle();

    expect(cobranzas.pagos, hasLength(2));
    expect(cobranzas.pagos.first.clave, cobranzas.pagos.last.clave);
  });

  testWidgets('con la letra al 200 % cumple las guías, con la foto tomada', (tester) async {
    final semantica = tester.ensureSemantics();
    await abrir(tester, letra: 2, alto: 800);
    await tester.scrollUntilVisible(find.text('Tomar foto'), 200, scrollable: find.byType(Scrollable).first);
    await tester.tap(find.text('Tomar foto'));
    await tester.pump();

    await cumpleLasGuias(tester);
    semantica.dispose();
  });
}
