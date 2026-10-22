import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/services/ubicacion.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/ubicacion/captura_de_ubicacion.dart';
import 'package:gestion_control/ui/core/ubicacion/mapa_de_ubicacion.dart';
import 'package:gestion_control/ui/core/ubicacion/tarjeta_de_ubicacion.dart';

import '../../ayudantes.dart';

/// Un formulario de mentira: abre la tarjeta y empieza a capturar, como la venta o el cliente.
class _Formulario extends ConsumerStatefulWidget {
  const _Formulario(this.registro);

  final QueSeRegistra registro;

  @override
  ConsumerState<_Formulario> createState() => _FormularioState();
}

class _FormularioState extends ConsumerState<_Formulario> {
  @override
  void initState() {
    super.initState();
    Future.microtask(() => ref.read(capturaDeUbicacionProvider(widget.registro).notifier).capturar());
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: ListView(
      padding: const EdgeInsets.all(16),
      children: [TarjetaDeUbicacion(registro: widget.registro)],
    ),
  );
}

void main() {
  late UbicacionFalsa gps;

  setUp(() => gps = UbicacionFalsa());

  Future<void> abrir(WidgetTester tester, {QueSeRegistra registro = QueSeRegistra.venta, double letra = 1}) async {
    tamanoDeCelular(tester, letra: letra);
    await tester.pumpWidget(
      conLaApp(_Formulario(registro), overrides: [servicioDeUbicacionProvider.overrideWithValue(gps)]),
    );
    await tester.pump();
  }

  CapturaDeUbicacion captura(WidgetTester tester, [QueSeRegistra registro = QueSeRegistra.venta]) =>
      ProviderScope.containerOf(
        tester.element(find.byType(TarjetaDeUbicacion)),
      ).read(capturaDeUbicacionProvider(registro).notifier);

  testWidgets('mientras busca lo dice, y una lectura de ±8 m queda capturada', (tester) async {
    await abrir(tester);
    expect(find.text('Buscando la señal del GPS…'), findsOneWidget);

    gps.leer(-17.783412, -63.182106, 8);
    await tester.pump();

    expect(find.text('Ubicación capturada · ±8 m'), findsOneWidget);
    expect(find.text('Lat. -17,783412 · Long. -63,182106'), findsOneWidget);
    expect(find.text('Volver a capturar'), findsOneWidget);
  });

  testWidgets('si no llega a 30 m, a los 30 s se queda con la mejor lectura, marcada poco precisa', (tester) async {
    await abrir(tester);

    gps.leer(-17.78, -63.18, 80);
    gps.leer(-17.783, -63.182, 45);
    gps.leer(-17.79, -63.19, 60);
    await tester.pump(esperaDelGps);

    expect(find.text('Ubicación poco precisa · ±45 m'), findsOneWidget);
    expect(find.text('Lat. -17,783000 · Long. -63,182000'), findsOneWidget);
  });

  testWidgets('sin ninguna lectura en 30 s, no hay señal, y Reintentar vuelve a buscar', (tester) async {
    await abrir(tester);

    await tester.pump(esperaDelGps);

    expect(find.text('No hay señal del GPS.'), findsOneWidget);
    expect(
      find.text('Busca un lugar abierto y vuelve a intentarlo: sin ubicación, la venta no se guarda.'),
      findsOneWidget,
    );
    await tester.tap(find.text('Reintentar'));
    await tester.pump();
    expect(find.text('Buscando la señal del GPS…'), findsOneWidget);
  });

  testWidgets('con el GPS desactivado pide activarlo, y nombra lo que se registra', (tester) async {
    gps.activado = false;
    await abrir(tester, registro: QueSeRegistra.cliente);

    expect(find.text('El GPS está desactivado.'), findsOneWidget);
    expect(find.text('Actívalo para registrar el cliente: sin ubicación, el cliente no se guarda.'), findsOneWidget);
    await tester.tap(find.text('Activar GPS'));
    expect(gps.ajustesDeUbicacion, 1);
  });

  testWidgets('sin permiso lo pide; si el usuario lo niega, la tarjeta ofrece darlo', (tester) async {
    gps
      ..permisoActual = PermisoDeUbicacion.denegado
      ..alPedir = PermisoDeUbicacion.denegado;
    await abrir(tester);

    expect(gps.pedidosDePermiso, 1);
    expect(find.text('La app no tiene permiso para usar tu ubicación.'), findsOneWidget);
    expect(find.text('Dale permiso para registrar la venta: sin ubicación, la venta no se guarda.'), findsOneWidget);

    gps.alPedir = PermisoDeUbicacion.concedido;
    await tester.tap(find.text('Dar permiso'));
    await tester.pump();
    expect(find.text('Buscando la señal del GPS…'), findsOneWidget);
  });

  testWidgets('negado para siempre, "Dar permiso" abre los ajustes de la app', (tester) async {
    gps.permisoActual = PermisoDeUbicacion.denegadoParaSiempre;
    await abrir(tester);

    await tester.tap(find.text('Dar permiso'));
    expect(gps.ajustesDeLaApp, 1);
  });

  testWidgets('con solo la ubicación aproximada, la lectura es poco precisa y ofrece pedir la precisa', (tester) async {
    gps.permisoActual = PermisoDeUbicacion.soloAproximada;
    await abrir(tester);

    gps.leer(-17.78, -63.18, 8);
    await tester.pump();
    expect(find.text('Buscando la señal del GPS…'), findsOneWidget);
    await tester.pump(esperaDelGps);

    expect(find.text('La app solo tiene tu ubicación aproximada.'), findsOneWidget);
    expect(find.text('Ubicación poco precisa · ±8 m'), findsOneWidget);
    gps.alPedir = PermisoDeUbicacion.concedido;
    await tester.tap(find.text('Dar permiso'));
    expect(gps.pedidosDePermiso, 1);
  });

  testWidgets('el GPS que se apaga mientras busca lo dice', (tester) async {
    await abrir(tester);

    gps.fallar(const GpsApagado());
    await tester.pump();

    expect(find.text('El GPS está desactivado.'), findsOneWidget);
  });

  testWidgets('guardar mientras busca usa la mejor lectura; sin ninguna, espera la primera', (tester) async {
    await abrir(tester);
    Posicion? paraGuardar;
    var listo = false;
    captura(tester).paraGuardar().then((p) {
      paraGuardar = p;
      listo = true;
    });
    await tester.pump();
    expect(listo, isFalse);

    gps.leer(-17.78, -63.18, 50);
    await tester.pump();

    expect(listo, isTrue);
    expect(paraGuardar?.precision, 50);
    expect(find.text('Buscando la señal del GPS…'), findsOneWidget);
  });

  testWidgets('guardar sin ninguna lectura cuando se acaba el tiempo no da ubicación', (tester) async {
    await abrir(tester);
    Posicion? paraGuardar = const Posicion(0, 0, 0);
    captura(tester).paraGuardar().then((p) => paraGuardar = p);

    await tester.pump(esperaDelGps);

    expect(paraGuardar, isNull);
  });

  testWidgets('con la letra al 200 % cumple las guías, capturada y con el GPS desactivado', (tester) async {
    final semantica = tester.ensureSemantics();
    await abrir(tester, letra: 2);
    gps.leer(-17.783412, -63.182106, 8);
    await tester.pump();
    await cumpleLasGuias(tester);

    gps.activado = false;
    await captura(tester).capturar();
    await tester.pump();
    expect(find.text('Activar GPS'), findsOneWidget);
    await cumpleLasGuias(tester);
    semantica.dispose();
  });

  testWidgets('el mapa muestra el punto con su atribución, y el lector lee dónde está', (tester) async {
    final semantica = tester.ensureSemantics();
    tamanoDeCelular(tester);
    await tester.pumpWidget(
      conLaApp(
        const Scaffold(body: MapaDeUbicacion(latitud: -17.771845, longitud: -63.17629)),
        overrides: [mosaicosDelMapaProvider.overrideWithValue(false)],
      ),
    );

    expect(find.text('© OpenStreetMap'), findsOneWidget);
    expect(find.bySemanticsLabel('Mapa con la ubicación: latitud -17,771845, longitud -63,176290'), findsOneWidget);
    semantica.dispose();
  });
}
