import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/camino.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/data/services/ubicacion.dart';
import 'package:gestion_control/domain/navegacion.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/ubicacion/mapa_de_ubicacion.dart';
import 'package:gestion_control/ui/rutas/navegacion_screen.dart';
import 'package:gestion_control/ui/rutas/navegacion_view_model.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';
import 'package:go_router/go_router.dart';

import '../ayudantes.dart';
import '../ayudantes_de_rutas.dart';

void main() {
  late RutasRepositoryFalso rutas;
  late PedidosRepositoryFalso pedidos;
  late UbicacionFalsa gps;
  late PreferenciasFalsas preferencias;
  late VozFalsa voz;
  late PantallaFalsa pantalla;

  setUp(() {
    rutas = RutasRepositoryFalso();
    pedidos = PedidosRepositoryFalso();
    gps = UbicacionFalsa();
    preferencias = PreferenciasFalsas();
    voz = VozFalsa();
    pantalla = PantallaFalsa();
  });

  // La siguiente de la ruta del 8.6.9: Marco Ortiz Peña, la 4.
  final hastaMarco = VisitaDeLaRuta(8, rutaDePrueba().siguiente!);

  // Un pedido de Marco, en el mismo lugar: el camino de prueba sirve igual.
  const hastaElPedido = PedidoEnRuta(id: 37, cliente: 'Marco Ortiz Peña', ubicacion: casaDeMarco);

  // Lo que devolvió la navegación al cerrarse: true es "Marcar visita" o "Registrar entrega".
  bool? volvio;
  var cerrada = false;
  DestinoDelCamino destino = hastaMarco;

  final pantallas = <RouteBase>[
    GoRoute(
      path: '/',
      builder: (context, state) => Scaffold(
        body: FilledButton(
          onPressed: () async {
            volvio = await context.push<bool>('/camino', extra: destino);
            cerrada = true;
          },
          child: const Text('Abrir'),
        ),
      ),
      routes: [
        GoRoute(
          path: 'camino',
          builder: (context, state) => NavegacionScreen(destino: state.extra! as DestinoDelCamino),
        ),
      ],
    ),
  ];

  // pumpAndSettle no sirve con un indicador girando ("Calculando el camino…").
  Future<void> esperar(WidgetTester tester) async {
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
  }

  Future<void> abrir(WidgetTester tester, {double letra = 1, DestinoDelCamino? hasta}) async {
    volvio = null;
    cerrada = false;
    destino = hasta ?? hastaMarco;
    await abrirConRouter(
      tester,
      rutas: pantallas,
      letra: letra,
      overrides: [
        rutasRepositoryProvider.overrideWithValue(rutas),
        pedidosRepositoryProvider.overrideWithValue(pedidos),
        servicioDeUbicacionProvider.overrideWithValue(gps),
        preferenciasProvider.overrideWithValue(preferencias),
        vozProvider.overrideWithValue(voz),
        pantallaEncendidaProvider.overrideWithValue(pantalla),
        mosaicosDelMapaProvider.overrideWithValue(false),
        sesionProvider.overrideWith(
          () => SesionFija(
            ConSesion(promotorCon([Permisos.territorial.recorrerRuta, Permisos.pedidos.registrarEntrega])),
          ),
        ),
      ],
    );
    await tester.tap(find.text('Abrir'));
    await esperar(tester);
  }

  // Como Android al volver de los ajustes: la app pasa por inactiva y vuelve a estar a la vista.
  Future<void> volverALaApp(WidgetTester tester) async {
    for (final ciclo in [AppLifecycleState.inactive, AppLifecycleState.resumed]) {
      await tester.binding.defaultBinaryMessenger.handlePlatformMessage(
        'flutter/lifecycle',
        const StringCodec().encodeMessage(ciclo.toString()),
        (_) {},
      );
    }
    await esperar(tester);
  }

  Future<void> leer(WidgetTester tester, PuntoDelCamino punto, {double precision = 5}) async {
    gps.leer(punto.latitud, punto.longitud, precision);
    await esperar(tester);
    await esperar(tester);
  }

  testWidgets('mientras no hay ubicación, la busca, con la pantalla encendida', (tester) async {
    await abrir(tester);

    expect(find.text('Buscando tu ubicación…'), findsOneWidget);
    expect(find.text('Cómo llegar'), findsOneWidget);
    expect(pantalla.encendida, isTrue);
  });

  testWidgets('la indicación que viene, lo que falta, cómo vas, la alternativa y la atribución', (tester) async {
    await abrir(tester);
    await leer(tester, desdeCarmen);

    expect(find.text('En 530 m'), findsOneWidget);
    expect(find.text('gira a la izquierda en Calle 8'), findsOneWidget);
    expect(find.byIcon(Icons.turn_left), findsOneWidget);
    expect(find.text('920 m · 2 min'), findsOneWidget);
    expect(find.text('Hasta Marco Ortiz Peña'), findsOneWidget);
    expect(find.text('Cómo vas'), findsOneWidget);
    expect(find.bySemanticsLabel('En auto'), findsOneWidget);
    expect(find.text('+3 min'), findsOneWidget);
    expect(find.text('© OpenRouteService, © colaboradores de OpenStreetMap'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Terminar'), findsOneWidget);
  });

  testWidgets('con la letra al 200 % cumple las guías', (tester) async {
    await abrir(tester, letra: 2);
    await leer(tester, desdeCarmen);

    expect(find.text('En 530 m'), findsOneWidget);
    await cumpleLasGuias(tester);
  });

  testWidgets('tocar "+3 min" elige la alternativa, y el principal queda con "−3 min"', (tester) async {
    await abrir(tester);
    await leer(tester, desdeCarmen);

    await tester.tap(find.text('+3 min'));
    await esperar(tester);

    expect(find.text('En 390 m'), findsOneWidget);
    expect(find.text('gira a la derecha'), findsOneWidget);
    expect(find.text('−3 min'), findsOneWidget);
    expect(find.text('920 m · 5 min'), findsOneWidget);
  });

  testWidgets('"Silenciar la voz" la calla y la app lo recuerda', (tester) async {
    await abrir(tester);
    await leer(tester, desdeCarmen);

    await tester.tap(find.byTooltip('Silenciar la voz'));
    await esperar(tester);

    expect(find.byTooltip('Activar la voz'), findsOneWidget);
    expect(preferencias.silenciada, isTrue);
    expect(voz.calladas, 1);
  });

  testWidgets('"En moto" se recuerda y pide su camino', (tester) async {
    await abrir(tester);
    await leer(tester, desdeCarmen);

    await tester.tap(find.text('En moto'));
    await esperar(tester);
    expect(find.text('Calculando el camino…'), findsOneWidget);
    expect(preferencias.medioGuardado, MedioDeTransporte.moto);

    await tester.pump(esperaEntrePedidos);
    await esperar(tester);
    expect(rutas.caminos.last.medio, MedioDeTransporte.moto);
    expect(find.text('Calculando el camino…'), findsNothing);
  });

  testWidgets('al mover el mapa deja de seguirte, y el botón de centrar te vuelve a seguir', (tester) async {
    await abrir(tester);
    await leer(tester, desdeCarmen);
    expect(find.byTooltip('Centrar el mapa en tu ubicación'), findsNothing);

    await tester.drag(find.byType(FlutterMap), const Offset(0, 120));
    await esperar(tester);
    expect(find.byTooltip('Centrar el mapa en tu ubicación'), findsOneWidget);

    await tester.tap(find.byTooltip('Centrar el mapa en tu ubicación'));
    await esperar(tester);
    expect(find.byTooltip('Centrar el mapa en tu ubicación'), findsNothing);
  });

  testWidgets('al llegar, "Marcar visita" vuelve a la ruta con la orden de marcarla', (tester) async {
    await abrir(tester);
    await leer(tester, desdeCarmen);

    await leer(tester, desplazar(casaDeMarco, norte: 10), precision: 8);

    expect(find.text('Llegaste a Marco Ortiz Peña'), findsOneWidget);
    expect(find.text('Marca la visita para seguir con tu ruta.'), findsOneWidget);
    expect(find.text('Cómo vas'), findsNothing);
    await tester.tap(find.widgetWithText(FilledButton, 'Marcar visita'));
    await esperar(tester);

    expect(cerrada, isTrue);
    expect(volvio, isTrue);
  });

  testWidgets('"Terminar" vuelve a la ruta sin marcar', (tester) async {
    await abrir(tester);
    await leer(tester, desdeCarmen);

    await tester.tap(find.widgetWithText(OutlinedButton, 'Terminar'));
    await esperar(tester);

    expect(cerrada, isTrue);
    expect(volvio, isNull);
    expect(pantalla.encendida, isFalse);
  });

  testWidgets('sin camino, el aviso de la API con "Reintentar": el promotor sigue el mapa', (tester) async {
    rutas.alPedirCamino = (_) async => throw const ErrorDeApi(
      mensaje: 'No se pudo calcular el camino. Sigue el mapa.',
      codigo: 'navegacion_no_disponible',
      estado: 503,
    );
    await abrir(tester);
    await leer(tester, desdeCarmen);

    expect(find.text('No se pudo calcular el camino. Sigue el mapa.'), findsOneWidget);
    expect(find.text('Reintentar'), findsOneWidget);
    expect(find.text('En 530 m'), findsNothing);
  });

  testWidgets('sin voz en español, lo avisa y no muestra el botón de la voz', (tester) async {
    voz.enEspanol = false;
    await abrir(tester);
    await leer(tester, desdeCarmen);

    expect(find.text('Tu celular no tiene voz en español: sigue las indicaciones en pantalla.'), findsOneWidget);
    expect(find.byTooltip('Silenciar la voz'), findsNothing);
  });

  group('hasta un pedido en ruta, desde la bandeja', () {
    setUp(() => pedidos.alPedirCamino = (_) async => caminosDePrueba());

    testWidgets('pide el camino del pedido, no el de la ruta, y lo sigue', (tester) async {
      await abrir(tester, hasta: hastaElPedido);
      await leer(tester, desdeCarmen);

      expect(pedidos.caminos.single.id, 37);
      expect(rutas.caminos, isEmpty);
      expect(find.text('En 530 m'), findsOneWidget);
      expect(find.text('Hasta Marco Ortiz Peña'), findsOneWidget);
    });

    testWidgets('al llegar, "Registrar entrega" vuelve al pedido con la orden de registrarla', (tester) async {
      await abrir(tester, hasta: hastaElPedido);
      await leer(tester, desdeCarmen);

      await leer(tester, desplazar(casaDeMarco, norte: 10), precision: 8);

      expect(find.text('Llegaste a Marco Ortiz Peña'), findsOneWidget);
      expect(find.text('Registra la entrega del pedido.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Registrar entrega'));
      await esperar(tester);

      expect(cerrada, isTrue);
      expect(volvio, isTrue);
    });

    testWidgets('con el GPS apagado lo avisa con "Activar GPS", y al volver a la app lo revisa otra vez', (
      tester,
    ) async {
      gps.activado = false;
      await abrir(tester, hasta: hastaElPedido);

      expect(find.text('El GPS está desactivado.'), findsOneWidget);
      expect(find.text('Actívalo para seguir el camino hasta el cliente.'), findsOneWidget);
      expect(find.text('Buscando tu ubicación…'), findsNothing);
      await tester.tap(find.text('Activar GPS'));
      expect(gps.ajustesDeUbicacion, 1);

      gps.activado = true;
      await volverALaApp(tester);
      expect(find.text('El GPS está desactivado.'), findsNothing);
      await leer(tester, desdeCarmen);
      expect(find.text('En 530 m'), findsOneWidget);
    });

    testWidgets('sin permiso lo pide al abrir; si lo niega, "Dar permiso" lo vuelve a pedir', (tester) async {
      gps.permisoActual = PermisoDeUbicacion.denegado;
      await abrir(tester, hasta: hastaElPedido);

      expect(gps.pedidosDePermiso, 1);
      expect(find.text('La app no tiene permiso para usar tu ubicación.'), findsOneWidget);

      gps.alPedir = PermisoDeUbicacion.concedido;
      await tester.tap(find.text('Dar permiso'));
      await esperar(tester);

      expect(gps.pedidosDePermiso, 2);
      expect(find.text('La app no tiene permiso para usar tu ubicación.'), findsNothing);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      await abrir(tester, hasta: hastaElPedido, letra: 2);
      await leer(tester, desdeCarmen);

      expect(find.text('En 530 m'), findsOneWidget);
      await cumpleLasGuias(tester);
    });
  });
}
