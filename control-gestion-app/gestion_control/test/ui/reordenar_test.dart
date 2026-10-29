import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/camino.dart';
import 'package:gestion_control/data/models/ruta.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/ubicacion/mapa_de_ubicacion.dart';
import 'package:gestion_control/ui/rutas/reordenar_hoja.dart';
import 'package:gestion_control/ui/rutas/ruta_del_dia_screen.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';
import 'package:go_router/go_router.dart';

import '../ayudantes.dart';
import '../ayudantes_de_rutas.dart';

// Parte C de la tarea 25: "Reordenar desde aquí" en la ruta del 8.6.9 en curso (3 de 5 marcadas: Elena y Carmen
// visitadas, Pedro ausente, Marco y Luis pendientes).
void main() {
  late RutasRepositoryFalso rutas;
  late UbicacionFalsa gps;
  late PreferenciasFalsas preferencias;

  setUp(() {
    rutas = RutasRepositoryFalso();
    gps = UbicacionFalsa();
    preferencias = PreferenciasFalsas();
  });

  final pantallas = <RouteBase>[
    GoRoute(
      path: '/',
      builder: (context, state) => const Scaffold(body: Text('Menú')),
      routes: [GoRoute(path: 'ruta', builder: (context, state) => const RutaDelDiaScreen())],
    ),
  ];

  Future<void> esperar(WidgetTester tester) async {
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
  }

  Future<void> abrir(WidgetTester tester, {double letra = 1, bool conGps = true}) async {
    await abrirConRouter(
      tester,
      rutas: pantallas,
      inicio: '/ruta',
      letra: letra,
      alto: 2400,
      overrides: [
        rutasRepositoryProvider.overrideWithValue(rutas),
        zonasRepositoryProvider.overrideWithValue(ZonasRepositoryFalso()),
        clientesRepositoryProvider.overrideWithValue(ClientesRepositoryFalso()),
        servicioDeUbicacionProvider.overrideWithValue(gps),
        preferenciasProvider.overrideWithValue(preferencias),
        mosaicosDelMapaProvider.overrideWithValue(false),
        sesionProvider.overrideWith(() => SesionFija(ConSesion(promotorCon([Permisos.territorial.recorrerRuta])))),
      ],
    );
    await esperar(tester);
    if (conGps) {
      gps.leer(-17.7765, -63.1740, 5);
      await esperar(tester);
    }
  }

  Finder enLaHoja(Finder buscado) => find.descendant(of: find.byType(HojaDeReordenar), matching: buscado);
  Finder boton<T extends Widget>(String texto) => find.widgetWithText(T, texto);

  Future<void> tocar(WidgetTester tester, Finder buscado) async {
    await tester.ensureVisible(buscado);
    await tester.pump();
    await tester.tap(buscado);
    await esperar(tester);
  }

  testWidgets('sin ubicación del GPS no se ofrece; con ella, sí', (tester) async {
    await abrir(tester, conGps: false);
    expect(boton<OutlinedButton>('Reordenar desde aquí'), findsNothing);

    gps.leer(-17.7765, -63.1740, 5);
    await esperar(tester);
    expect(boton<OutlinedButton>('Reordenar desde aquí'), findsOneWidget);
  });

  testWidgets('con una sola visita por hacer no se ofrece', (tester) async {
    // Pedro (la 2) y Marco (la 4) también visitados: solo falta Luis.
    rutas.alPedirDelDia = () async {
      final json = rutaJson();
      final visitas = json['visitas'] as List;
      for (final i in [1, 3]) {
        visitas[i] = {...visitas[i] as Map<String, dynamic>, 'resultado': 1, 'horaVisita': '2026-10-30T15:00:00'};
      }
      return Ruta.fromJson(json);
    };
    await abrir(tester);

    expect(boton<OutlinedButton>('Reordenar desde aquí'), findsNothing);
  });

  testWidgets('con la ausente: se elige volver a pasar, se ve el orden y al aplicarlo la ruta queda reordenada', (
    tester,
  ) async {
    preferencias.medioGuardado = MedioDeTransporte.pie;
    rutas.alReordenar = (_, _, _) async => rutaDePrueba();
    await abrir(tester);

    await tocar(tester, boton<OutlinedButton>('Reordenar desde aquí'));
    expect(enLaHoja(find.text('¿Vuelves a pasar por estas visitas ausentes?')), findsOneWidget);
    expect(enLaHoja(find.text('Volver a pasar por Pedro Mamani Cruz')), findsOneWidget);
    expect(enLaHoja(find.text('Ausente a las 09:20')), findsOneWidget);
    expect(tester.widget<CheckboxListTile>(find.byType(CheckboxListTile)).value, isTrue);

    await tocar(tester, boton<FilledButton>('Calcular el orden'));
    final pedido = rutas.ordenes.single;
    expect((pedido.rutaId, pedido.medio), (8, MedioDeTransporte.pie));
    expect(pedido.ausentes, [2]);
    expect((pedido.desde.latitud, pedido.desde.longitud), (-17.7765, -63.1740));
    expect(enLaHoja(find.text('Unos 25 min y 7,4 km para las 3 visitas')), findsOneWidget);
    expect(enLaHoja(find.text('Luis Fernández Áñez')), findsOneWidget);
    expect(enLaHoja(find.text('Pedro Mamani Cruz (vuelves a pasar)')), findsOneWidget);

    await tocar(tester, boton<FilledButton>('Aplicar el orden'));

    expect(rutas.reordenadas.single.$1, [5, 4, 2]);
    expect(rutas.reordenadas.single.$2, [2]);
    expect(find.byType(HojaDeReordenar), findsNothing);
    expect(exito('La ruta quedó reordenada.'), findsOneWidget);
  });

  testWidgets('si no vuelve a pasar por la ausente, el orden es solo de las pendientes', (tester) async {
    await abrir(tester);
    await tocar(tester, boton<OutlinedButton>('Reordenar desde aquí'));

    await tocar(tester, find.byType(CheckboxListTile));
    await tocar(tester, boton<FilledButton>('Calcular el orden'));

    expect(rutas.ordenes.single.ausentes, isEmpty);
    expect(enLaHoja(find.text('Unos 25 min y 7,4 km para las 2 visitas')), findsOneWidget);
    expect(enLaHoja(find.textContaining('Pedro')), findsNothing);
  });

  testWidgets('si el orden no se puede calcular, lo dice con "Reintentar"; sin la ausente, se calcula al abrir', (
    tester,
  ) async {
    rutas.alPedirDelDia = () async => conVisitaMarcada(rutaDePrueba(), 2, EstadoVisita.visitado);
    rutas.alPedirOrden = (_) async => throw const ErrorDeApi(
      mensaje: 'No se pudo calcular el orden. Sigue con el que tienes.',
      codigo: 'orden_no_disponible',
      estado: 503,
    );
    await abrir(tester);

    await tocar(tester, boton<OutlinedButton>('Reordenar desde aquí'));

    expect(enLaHoja(find.text('¿Vuelves a pasar por estas visitas ausentes?')), findsNothing);
    expect(enLaHoja(find.text('No se pudo calcular el orden. Sigue con el que tienes.')), findsOneWidget);
    expect(enLaHoja(find.text('Reintentar')), findsOneWidget);
  });

  testWidgets('si la ruta cambió al aplicarlo, lo avisa y no cierra la hoja', (tester) async {
    rutas.alReordenar = (_, _, _) async => throw const ErrorDeApi(
      mensaje: 'La ruta cambió mientras calculabas el orden: vuelve a calcularlo.',
      codigo: 'ruta_cambio',
      estado: 409,
    );
    await abrir(tester);
    await tocar(tester, boton<OutlinedButton>('Reordenar desde aquí'));
    await tocar(tester, boton<FilledButton>('Calcular el orden'));

    await tocar(tester, boton<FilledButton>('Aplicar el orden'));

    expect(enLaHoja(find.text('La ruta cambió mientras calculabas el orden: vuelve a calcularlo.')), findsOneWidget);
    expect(find.byType(HojaDeReordenar), findsOneWidget);
  });

  testWidgets('la hoja con la letra al 200 % cumple las guías', (tester) async {
    await abrir(tester, letra: 2);
    await tocar(tester, boton<OutlinedButton>('Reordenar desde aquí'));
    await cumpleLasGuias(tester);

    await tocar(tester, boton<FilledButton>('Calcular el orden'));
    await cumpleLasGuias(tester);
  });
}
