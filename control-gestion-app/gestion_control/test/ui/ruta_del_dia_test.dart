import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/ruta.dart';
import 'package:gestion_control/data/services/ubicacion.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/ubicacion/mapa_de_ubicacion.dart';
import 'package:gestion_control/ui/rutas/marcar_visita_hoja.dart';
import 'package:gestion_control/ui/rutas/ruta_del_dia_screen.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';
import 'package:go_router/go_router.dart';

import '../ayudantes.dart';
import '../ayudantes_de_rutas.dart';

void main() {
  late RutasRepositoryFalso rutas;
  late UbicacionFalsa gps;
  late ZonasRepositoryFalso zonas;
  late ClientesRepositoryFalso clientes;

  setUp(() {
    rutas = RutasRepositoryFalso();
    gps = UbicacionFalsa();
    zonas = ZonasRepositoryFalso();
    clientes = ClientesRepositoryFalso();
  });

  final permisos = [Permisos.territorial.recorrerRuta, Permisos.ventas.registrarHp, Permisos.eventos.registrarEvento];

  final pantallas = <RouteBase>[
    GoRoute(
      path: '/',
      builder: (context, state) => const Scaffold(body: Text('Menú')),
      routes: [
        GoRoute(path: 'ruta', builder: (context, state) => const RutaDelDiaScreen()),
        destino('hojas-pedido/nueva'),
        destino('eventos/nuevo'),
      ],
    ),
  ];

  Future<GoRouter> abrir(WidgetTester tester, {double letra = 1, double alto = 2400, List<String>? conPermisos}) async {
    final router = await abrirConRouter(
      tester,
      rutas: pantallas,
      inicio: '/ruta',
      letra: letra,
      alto: alto,
      overrides: [
        rutasRepositoryProvider.overrideWithValue(rutas),
        zonasRepositoryProvider.overrideWithValue(zonas),
        clientesRepositoryProvider.overrideWithValue(clientes),
        servicioDeUbicacionProvider.overrideWithValue(gps),
        mosaicosDelMapaProvider.overrideWithValue(false),
        sesionProvider.overrideWith(() => SesionFija(ConSesion(promotorCon(conPermisos ?? permisos)))),
      ],
    );
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));
    return router;
  }

  // pumpAndSettle no sirve con el indicador del GPS girando (dejaría pasar sus 30 s).
  Future<void> esperar(WidgetTester tester) async {
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
  }

  Finder boton<T extends Widget>(String texto) => find.widgetWithText(T, texto);

  // "Visitado" y "Ausente" también están en la leyenda, detrás de la hoja.
  Finder enLaHoja(String texto) => find.descendant(of: find.byType(HojaDeMarcarVisita), matching: find.text(texto));

  Future<void> tocar(WidgetTester tester, Finder buscado) async {
    await tester.ensureVisible(buscado);
    await tester.tap(buscado);
    await esperar(tester);
  }

  group('la ruta en curso (8.6.9)', () {
    testWidgets('la fecha, la zona, desde cuándo está en curso y cada visita con su resultado', (tester) async {
      await abrir(tester);

      expect(find.text('30/10/2026 · Zona Norte'), findsOneWidget);
      expect(find.text('En curso desde las 08:15'), findsOneWidget);
      expect(find.text('Visitas · 3 de 5 marcadas'), findsOneWidget);
      expect(find.text('Visitado a las 08:40'), findsOneWidget);
      expect(find.text('Ausente a las 09:20'), findsOneWidget);
      expect(find.text('Visitado a las 10:05'), findsOneWidget);
      expect(find.text('Pendiente · 9 días de atraso'), findsOneWidget);
      expect(find.text('Pendiente · 4 días de atraso'), findsOneWidget);
    });

    testWidgets('"Marcar visita": primario en la siguiente, secundario en las otras pendientes y en la ausente', (
      tester,
    ) async {
      await abrir(tester);

      expect(boton<FilledButton>('Marcar visita'), findsOneWidget);
      expect(boton<OutlinedButton>('Marcar visita'), findsNWidgets(2));
      final primario = tester.getTopLeft(boton<FilledButton>('Marcar visita')).dy;
      expect(primario, greaterThan(tester.getTopLeft(find.text('Marco Ortiz Peña')).dy));
      expect(primario, lessThan(tester.getTopLeft(find.text('Luis Fernández Áñez')).dy));
    });

    testWidgets('con visitas sin marcar, "Terminar ruta" es secundario y avisa cuántas quedan (ext. 7.1a)', (
      tester,
    ) async {
      await abrir(tester);
      expect(boton<OutlinedButton>('Terminar ruta'), findsOneWidget);

      await tocar(tester, boton<OutlinedButton>('Terminar ruta'));
      expect(find.text('¿Terminar la ruta?'), findsOneWidget);
      expect(
        find.text('Quedan 2 visitas sin marcar: quedan pendientes. La app deja de enviar tu ubicación.'),
        findsOneWidget,
      );
      await tester.tap(boton<FilledButton>('Terminar ruta'));
      await esperar(tester);

      expect(rutas.finales, [8]);
      expect(find.text('La ruta quedó completada a las 16:10.'), findsOneWidget);
      expect(find.text('Completada a las 16:10'), findsOneWidget);
      expect(find.text('Marcar visita'), findsNothing);
      expect(find.text('Terminar ruta'), findsNothing);
    });

    testWidgets('con todas marcadas, "Terminar ruta" pasa a primario', (tester) async {
      rutas.alPedirDelDia = () async => rutaDePrueba(todasMarcadas: true);
      await abrir(tester);

      expect(boton<FilledButton>('Terminar ruta'), findsOneWidget);
      await tocar(tester, boton<FilledButton>('Terminar ruta'));
      expect(find.text('Marcaste las 5 visitas. La app deja de enviar tu ubicación.'), findsOneWidget);
    });

    testWidgets('sin GPS, avisa que la oficina no sigue la ruta, con "Activar GPS"', (tester) async {
      gps.activado = false;
      await abrir(tester);

      expect(find.text('El GPS está desactivado.'), findsOneWidget);
      expect(find.text('Actívalo para que la oficina siga tu ruta y para marcar tus visitas.'), findsOneWidget);
      await tocar(tester, find.text('Activar GPS'));
      expect(gps.ajustesDeUbicacion, 1);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      gps.leer(-17.7725, -63.1712, 6);
      await abrir(tester, letra: 2, alto: 4000);

      await cumpleLasGuias(tester);
    });
  });

  group('la ruta planificada (7.3.20, paso 4)', () {
    setUp(() => rutas.alPedirDelDia = () async => rutaDePrueba(estado: EstadoRuta.planificada));

    testWidgets('sus visitas no se marcan hasta iniciarla', (tester) async {
      await abrir(tester);

      expect(find.text('Planificada'), findsOneWidget);
      expect(find.text('Marcar visita'), findsNothing);
      expect(find.text('Pendiente · 14 días de atraso'), findsOneWidget);
    });

    testWidgets('"Iniciar ruta" pide confirmar, dice que se envía la ubicación y la inicia', (tester) async {
      await abrir(tester);

      await tocar(tester, boton<FilledButton>('Iniciar ruta'));
      expect(find.text('¿Iniciar la ruta?'), findsOneWidget);
      expect(
        find.text(
          'Zona Norte · 5 visitas. Mientras la ruta esté en curso y la app esté abierta, la app envía tu ubicación '
          'cada 30 segundos.',
        ),
        findsOneWidget,
      );
      await tester.tap(boton<FilledButton>('Iniciar ruta').last);
      await esperar(tester);

      expect(rutas.inicios, [8]);
      expect(find.text('La ruta está en curso desde las 08:15.'), findsOneWidget);
      expect(boton<FilledButton>('Marcar visita'), findsOneWidget);
    });

    testWidgets('al iniciarla pide el permiso del GPS si falta', (tester) async {
      gps.permisoActual = PermisoDeUbicacion.denegado;
      await abrir(tester);

      await tocar(tester, boton<FilledButton>('Iniciar ruta'));
      await tester.tap(boton<FilledButton>('Iniciar ruta').last);
      await esperar(tester);

      expect(gps.pedidosDePermiso, 1);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      await abrir(tester, letra: 2, alto: 4000);

      await cumpleLasGuias(tester);
    });
  });

  testWidgets('sin ruta hoy lo dice (ext. 3.1a)', (tester) async {
    rutas.alPedirDelDia = () async => null;
    await abrir(tester);

    expect(find.text('No tienes una ruta para hoy'), findsOneWidget);
    expect(
      find.text(
        'Aquí aparece la ruta que el gerente general te planifica para cada día, con los clientes en el orden de '
        'visita.',
      ),
      findsOneWidget,
    );
  });

  testWidgets('sin conexión, "Reintentar" la vuelve a pedir', (tester) async {
    rutas.alPedirDelDia = () async => throw sinConexion;
    await abrir(tester);
    expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);

    rutas.alPedirDelDia = () async => rutaDePrueba();
    await tocar(tester, find.text('Reintentar'));

    expect(find.text('Visitas · 3 de 5 marcadas'), findsOneWidget);
  });

  group('marcar una visita (pasos 5 a 5.3)', () {
    Future<GoRouter> abrirLaHoja(WidgetTester tester, {double letra = 1}) async {
      final router = await abrir(tester, letra: letra);
      await tocar(tester, boton<FilledButton>('Marcar visita'));
      return router;
    }

    testWidgets('la hoja: el cliente, el resultado sin elegir, la observación y la ubicación "…la visita"', (
      tester,
    ) async {
      gps.activado = false;
      await abrirLaHoja(tester);

      expect(find.text('4 · Marco Ortiz Peña'), findsOneWidget);
      expect(find.text('Resultado'), findsOneWidget);
      expect(campo('Observación (opcional)'), findsOneWidget);
      expect(find.text('Actívalo para registrar la visita: sin ubicación, la visita no se guarda.'), findsOneWidget);
    });

    testWidgets('sin elegir el resultado no envía', (tester) async {
      await abrirLaHoja(tester);

      await tocar(tester, boton<FilledButton>('Confirmar'));

      expect(find.text('Elige si lo visitaste o estaba ausente.'), findsOneWidget);
      expect(rutas.marcas, isEmpty);
    });

    testWidgets('sin ubicación no marca la visita (ext. 5.1a)', (tester) async {
      gps.activado = false;
      await abrirLaHoja(tester);

      await tocar(tester, enLaHoja('Visitado'));
      await tocar(tester, boton<FilledButton>('Confirmar'));

      expect(rutas.marcas, isEmpty);
      expect(find.text('Marcar visita'), findsWidgets);
    });

    testWidgets('visitado: marca con la ubicación y la observación, y ofrece "Nueva hoja de pedido" (ext. 5a)', (
      tester,
    ) async {
      final router = await abrirLaHoja(tester);
      gps.leer(-17.7731, -63.1689, 6);
      await esperar(tester);

      await tocar(tester, enLaHoja('Visitado'));
      await tester.enterText(campo('Observación (opcional)'), 'Compró arroz.');
      await tocar(tester, boton<FilledButton>('Confirmar'));

      final marca = rutas.marcas.single;
      expect((marca.visitaId, marca.resultado, marca.observacion), (4, EstadoVisita.visitado, 'Compró arroz.'));
      expect(marca.ubicacion.latitud, -17.7731);
      expect(find.text('Resultado'), findsNothing);
      expect(find.text('Visitado a las 11:00'), findsOneWidget);
      expect(find.text('Visitas · 4 de 5 marcadas'), findsOneWidget);

      await tocar(tester, boton<OutlinedButton>('Nueva hoja de pedido'));
      expect(router.state.uri.toString(), '/hojas-pedido/nueva');
    });

    testWidgets('ausente: ofrece "Registrar evento" con el cliente elegido (ext. 5.3a)', (tester) async {
      final router = await abrirLaHoja(tester);
      gps.leer(-17.7731, -63.1689, 6);
      await esperar(tester);

      await tocar(tester, enLaHoja('Ausente'));
      await tocar(tester, boton<FilledButton>('Confirmar'));
      expect(find.text('Ausente a las 11:00'), findsOneWidget);
      expect(find.text('Nueva hoja de pedido'), findsNothing);

      await tocar(tester, boton<OutlinedButton>('Registrar evento'));
      expect(router.state.uri.toString(), '/eventos/nuevo');
    });

    testWidgets('sin respuesta, "Confirmar" relee la ruta: si la visita ya quedó, no la manda otra vez', (
      tester,
    ) async {
      rutas.alMarcar = (_) async => throw sinConexion;
      await abrirLaHoja(tester);
      gps.leer(-17.7731, -63.1689, 6);
      await esperar(tester);
      await tocar(tester, enLaHoja('Visitado'));
      await tocar(tester, boton<FilledButton>('Confirmar'));
      expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);

      rutas.alPedirDelDia = () async => conVisitaMarcada(rutaDePrueba(), 4, EstadoVisita.visitado);
      await tocar(tester, boton<FilledButton>('Confirmar'));

      expect(rutas.marcas, hasLength(1));
      expect(find.text('Visitado a las 11:00'), findsOneWidget);
    });

    testWidgets('la hoja con la letra al 200 % cumple las guías', (tester) async {
      await abrirLaHoja(tester, letra: 2);

      await cumpleLasGuias(tester);
    });
  });
}
