import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/pagina.dart';
import 'package:gestion_control/data/models/ruta.dart';
import 'package:gestion_control/data/models/zona.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/ubicacion/mapa_de_ubicacion.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';
import 'package:gestion_control/ui/territorio/mapa_de_clientes_screen.dart';
import 'package:gestion_control/ui/territorio/marcadores.dart';
import 'package:gestion_control/ui/territorio/por_visitar_screen.dart';
import 'package:gestion_control/ui/territorio/visitas.dart';

import '../ayudantes.dart';
import '../ayudantes_de_rutas.dart';

void main() {
  late ZonasRepositoryFalso zonas;
  late ClientesRepositoryFalso clientes;

  setUp(() {
    zonas = ZonasRepositoryFalso()..alListar = () async => const [zonaNorte, zonaSur, zonaDeOtro];
    zonas.alPedirPuntos = (_) async => const [
      PuntoDeReferencia(id: 1, zonaId: 1, nombre: 'Mercado Norte', latitud: -17.772, longitud: -63.177),
    ];
    clientes = ClientesRepositoryFalso()..alPedirMapa = () async => clientesDelMapa;
  });

  Future<void> abrir(WidgetTester tester, Widget pantalla, {double letra = 1, List<String>? permisos}) async {
    tamanoDeCelular(tester, letra: letra, alto: 2400);
    await tester.pumpWidget(
      conLaApp(
        pantalla,
        overrides: [
          zonasRepositoryProvider.overrideWithValue(zonas),
          clientesRepositoryProvider.overrideWithValue(clientes),
          mosaicosDelMapaProvider.overrideWithValue(false),
          sesionProvider.overrideWith(
            () => SesionFija(
              ConSesion(
                promotorCon(
                  permisos ?? [Permisos.territorial.verMapaClientes, Permisos.territorial.verClientesPorVisitar],
                ),
              ),
            ),
          ),
        ],
      ),
    );
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));
  }

  Future<void> elegir(WidgetTester tester, String campo, String opcion) async {
    await tester.tap(find.bySemanticsLabel(campo).first);
    await tester.pumpAndSettle();
    await tester.tap(find.text(opcion).last);
    await tester.pumpAndSettle();
  }

  // El círculo del cliente en el mapa, que es donde toca el promotor.
  // En la Zona Norte los marcadores no se pisan.
  Future<void> tocarCliente(WidgetTester tester, String etiqueta) async {
    if (find.text('Zona Norte').evaluate().isEmpty) await elegir(tester, 'Zona', 'Zona Norte · 3 clientes');
    await tester.tap(find.descendant(of: find.bySemanticsLabel(etiqueta), matching: find.byType(MarcadorDeCliente)));
    await tester.pump();
  }

  group('los textos de las visitas', () {
    final ahora = DateTime.utc(2026, 10, 30, 14, 42); // 10:42 en Bolivia.

    test('cuánto falta para la próxima visita, en días de Bolivia', () {
      expect(cuantoFalta('2026-11-14T14:05:00', ahora: ahora), 'en 15 días');
      expect(cuantoFalta('2026-10-31T03:00:00', ahora: ahora), 'hoy'); // 23:00 del 30 en Bolivia.
      expect(cuantoFalta('2026-10-31T14:00:00', ahora: ahora), 'en 1 día');
      expect(cuantoFalta('2026-10-27T14:00:00', ahora: ahora), 'vencida hace 3 días');
      expect(cuantoFalta(null, ahora: ahora), isNull);
    });

    test('el atraso de la lista por visitar', () {
      expect(textoDelAtraso(14), '14 días');
      expect(textoDelAtraso(1), '1 día');
      expect(textoDelAtraso(0), 'hoy');
      expect(textoDelAtraso(null), isNull);
    });

    test('el resultado de cada visita de la ruta', () {
      final ruta = rutaDePrueba();
      expect(ruta.visitas.map(resultadoDeLaVisita), [
        'Visitado a las 08:40',
        'Ausente a las 09:20',
        'Visitado a las 10:05',
        'Pendiente · 9 días de atraso',
        'Pendiente · 4 días de atraso',
      ]);
      final planificada = rutaDePrueba(estado: EstadoRuta.planificada).visitas;
      expect(resultadoDeLaVisita(planificada.first), 'Pendiente · 14 días de atraso');
    });
  });

  group('el mapa de clientes (7.3.17 y 7.3.18)', () {
    testWidgets('el promotor ve solo sus zonas, con todos sus clientes al entrar', (tester) async {
      await abrir(tester, const MapaDeClientesScreen());

      expect(find.text('Todas mis zonas'), findsOneWidget);
      expect(
        find.text('4 clientes en el mapa. Toca un cliente para ver su estado y su próxima visita.'),
        findsOneWidget,
      );
      await tester.tap(find.bySemanticsLabel('Zona').first);
      await tester.pumpAndSettle();
      expect(find.text('Zona Norte · 3 clientes'), findsOneWidget);
      expect(find.text('Zona Sur · 1 cliente'), findsOneWidget);
      expect(find.textContaining('Zona Este'), findsNothing);
    });

    testWidgets('la leyenda lleva los estados que aparecen, el punto de referencia y la zona', (tester) async {
      await abrir(tester, const MapaDeClientesScreen());

      final leyenda = find.byType(Leyenda);
      for (final texto in ['Activo', 'Disponible para re-visita', 'Inactivo', 'Punto de referencia', 'Zona']) {
        expect(
          find.descendant(of: leyenda, matching: find.text(texto)),
          findsOneWidget,
          reason: texto,
        );
      }
    });

    testWidgets('al elegir una zona, solo sus clientes', (tester) async {
      await abrir(tester, const MapaDeClientesScreen());

      await elegir(tester, 'Zona', 'Zona Sur · 1 cliente');

      expect(
        find.text('1 cliente en el mapa. Toca un cliente para ver su estado y su próxima visita.'),
        findsOneWidget,
      );
      expect(find.byType(MarcadorDeCliente), findsNWidgets(1 + 1)); // Más el de la leyenda.
    });

    testWidgets('una zona sin límite y sin clientes lo dice (ext. 2.2a y 2.4a)', (tester) async {
      zonas.alListar = () async => const [Zona(id: 5, codigo: 'ZO', nombre: 'Zona Oeste', promotorId: 'u1')];
      await abrir(tester, const MapaDeClientesScreen());

      // Con una sola zona, entra con ella elegida.
      expect(find.text('Zona Oeste todavía no tiene límite.'), findsOneWidget);
      expect(find.text('Zona Oeste todavía no tiene clientes en el mapa.'), findsOneWidget);
    });

    testWidgets('tocar un cliente muestra su estado y su próxima visita', (tester) async {
      await abrir(tester, const MapaDeClientesScreen());

      await tocarCliente(tester, 'Carmen Rojas Saucedo, Activo');

      expect(find.text('CLI-00068'), findsOneWidget);
      expect(find.text('Calle 7 N.º 35, a media cuadra de la Plaza Norte'), findsOneWidget);
      expect(find.text('70010068'), findsOneWidget);
      expect(find.text('30/10/2026 10:05'), findsOneWidget);
      expect(find.textContaining('14/11/2026 · '), findsOneWidget);
    });

    testWidgets('uno sin visitas está disponible, y uno inactivo está fuera del ciclo', (tester) async {
      await abrir(tester, const MapaDeClientesScreen());

      await tocarCliente(tester, 'Elena Gutiérrez Vaca, Disponible para re-visita');
      expect(find.text('Nunca'), findsOneWidget);
      expect(find.text('Sin visitas: está disponible para re-visita.'), findsOneWidget);

      await tocarCliente(tester, 'Silvia Torrico Méndez, Inactivo');
      expect(find.text('Fuera del ciclo de visitas'), findsOneWidget);
    });

    testWidgets('sin zonas lo dice', (tester) async {
      zonas.alListar = () async => const [zonaDeOtro];
      await abrir(tester, const MapaDeClientesScreen());

      expect(find.text('Todavía no tienes zonas'), findsOneWidget);
    });

    testWidgets('el mapa avisa que se mueve con dos dedos; completo, se mueve con uno y muestra el cliente tocado', (
      tester,
    ) async {
      await abrir(tester, const MapaDeClientesScreen());
      int gestos() => tester.widget<FlutterMap>(find.byType(FlutterMap)).options.interactionOptions.flags;
      expect(find.text('Mueve el mapa con dos dedos o ábrelo completo.'), findsOneWidget);
      expect(gestos(), gestosDelMapa);

      await tester.tap(find.byTooltip('Ver el mapa completo'));
      await tester.pumpAndSettle();
      expect(gestos(), gestosDelMapaCompleto);
      expect(find.text('Mueve el mapa con dos dedos o ábrelo completo.'), findsNothing);

      await tester.tap(
        find.descendant(
          of: find.bySemanticsLabel('Carmen Rojas Saucedo, Activo'),
          matching: find.byType(MarcadorDeCliente),
        ),
      );
      await tester.pump();
      expect(find.text('CLI-00068'), findsOneWidget);
      await cumpleLasGuias(tester);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      await abrir(tester, const MapaDeClientesScreen(), letra: 2);
      await tocarCliente(tester, 'Carmen Rojas Saucedo, Activo');

      await cumpleLasGuias(tester);
    });
  });

  group('los clientes por visitar (7.3.19)', () {
    setUp(() {
      clientes.alPorVisitar = (buscar, zonaId, promotorId, pagina) async => Pagina(
        items: buscar.isEmpty ? porVisitarDePrueba : const [],
        total: buscar.isEmpty ? porVisitarDePrueba.length : 0,
        numero: 1,
        tamano: 25,
      );
    });

    testWidgets('la fecha de hoy y cada cliente en el orden de la API, con su atraso', (tester) async {
      await abrir(tester, const PorVisitarScreen());

      expect(find.textContaining('los clientes cuya próxima visita ya llegó'), findsOneWidget);
      expect(find.text('3 clientes'), findsOneWidget);
      final silvia = tester.getTopLeft(find.text('Silvia Torrico Méndez')).dy;
      final elena = tester.getTopLeft(find.text('Elena Gutiérrez Vaca')).dy;
      expect(silvia, lessThan(elena));
      expect(find.text('Sin visitas'), findsOneWidget);
      expect(find.text('Atraso: 14 días'), findsOneWidget);
      expect(find.text('Atraso: hoy'), findsOneWidget);
      expect(find.text('Última visita 02/10/2026'), findsOneWidget);
    });

    testWidgets('el promotor no tiene el filtro de promotor; la zona filtra en la API', (tester) async {
      await abrir(tester, const PorVisitarScreen());
      expect(find.bySemanticsLabel('Promotor'), findsNothing);

      await elegir(tester, 'Zona', 'Zona Norte');

      expect(clientes.pedidosPorVisitar.last.zonaId, 1);
    });

    testWidgets('vacía, con el texto del panel (ext. 1.4a)', (tester) async {
      clientes.alPorVisitar = (_, _, _, _) async => const Pagina(items: [], total: 0, numero: 1, tamano: 25);
      await abrir(tester, const PorVisitarScreen());

      expect(find.text('Ningún cliente debe ser visitado todavía.'), findsOneWidget);
    });

    testWidgets('sin coincidencias con la búsqueda', (tester) async {
      await abrir(tester, const PorVisitarScreen());

      await tester.enterText(campo('Buscar'), 'Zzz');
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pump();

      expect(find.text('Ningún cliente por visitar coincide con la búsqueda.'), findsOneWidget);
    });

    testWidgets('tocar un cliente muestra su ubicación y sus visitas (paso 3)', (tester) async {
      await abrir(tester, const PorVisitarScreen());

      await tester.tap(find.text('Elena Gutiérrez Vaca'));
      await tester.pumpAndSettle();

      expect(find.byType(MapaDeUbicacion), findsOneWidget);
      expect(find.text('02/10/2026 10:00'), findsOneWidget);
      expect(find.textContaining('17/10/2026 · vencida hace'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      await abrir(tester, const PorVisitarScreen(), letra: 2);

      await cumpleLasGuias(tester);
    });
  });
}
