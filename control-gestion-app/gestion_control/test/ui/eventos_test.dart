import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/cliente.dart';
import 'package:gestion_control/data/models/evento.dart';
import 'package:gestion_control/data/models/pagina.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/ubicacion/mapa_de_ubicacion.dart';
import 'package:gestion_control/ui/elegir/elegir_cliente_screen.dart';
import 'package:gestion_control/ui/eventos/evento_screen.dart';
import 'package:gestion_control/ui/eventos/eventos_screen.dart';
import 'package:gestion_control/ui/eventos/formulario_evento_screen.dart';
import 'package:gestion_control/ui/pedidos/pedido_screen.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';
import 'package:go_router/go_router.dart';

import '../ayudantes.dart';

void main() {
  late EventosRepositoryFalso eventos;
  late PedidosRepositoryFalso pedidos;
  late CamaraFalsa camara;
  late UbicacionFalsa gps;

  setUp(() {
    eventos = EventosRepositoryFalso();
    pedidos = PedidosRepositoryFalso();
    camara = CamaraFalsa();
    gps = UbicacionFalsa();
  });

  final permisos = [
    Permisos.eventos.consultarEventos,
    Permisos.eventos.registrarEvento,
    Permisos.eventos.modificarEvento,
    Permisos.eventos.agregarFoto,
    Permisos.eventos.eliminarFoto,
    Permisos.pedidos.consultarPedidos,
  ];

  final rutas = <RouteBase>[
    GoRoute(
      path: '/',
      builder: (context, state) => const Scaffold(body: Text('Menú')),
      routes: [
        GoRoute(path: 'eventos', builder: (context, state) => const EventosScreen()),
        GoRoute(
          path: 'eventos/nuevo',
          builder: (context, state) => FormularioEventoScreen(abrir: state.extra as AbrirNuevoEvento?),
        ),
        GoRoute(
          path: 'eventos/:id',
          builder: (context, state) =>
              EventoScreen(id: int.parse(state.pathParameters['id']!), abrir: state.extra as AbrirEvento?),
          routes: [
            GoRoute(
              path: 'editar',
              builder: (context, state) => FormularioEventoScreen(eventoId: int.parse(state.pathParameters['id']!)),
            ),
          ],
        ),
        GoRoute(
          path: 'pedidos/:id',
          builder: (context, state) => PedidoScreen(id: int.parse(state.pathParameters['id']!)),
        ),
        // La búsqueda del cliente ya tiene sus pruebas: aquí devuelve a Pedro o a Rosa.
        GoRoute(
          path: 'elegir-cliente',
          builder: (context, state) => Scaffold(
            body: Column(
              children: [
                TextButton(onPressed: () => context.pop(pedroOpcion), child: const Text('Elegir a Pedro')),
                TextButton(onPressed: () => context.pop(rosaOpcion), child: const Text('Elegir a Rosa')),
              ],
            ),
          ),
        ),
      ],
    ),
  ];

  Future<GoRouter> abrir(
    WidgetTester tester, {
    required String inicio,
    double letra = 1,
    double alto = 2400,
    List<String>? conPermisos,
  }) => abrirConRouter(
    tester,
    rutas: rutas,
    inicio: inicio,
    letra: letra,
    alto: alto,
    overrides: [
      eventosRepositoryProvider.overrideWithValue(eventos),
      pedidosRepositoryProvider.overrideWithValue(pedidos),
      camaraProvider.overrideWithValue(camara),
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

  group('la lista (7.3.13, paso 1)', () {
    testWidgets('cada evento con su número, su estado, su tipo, su cliente, su fecha y sus fotos', (tester) async {
      await abrir(tester, inicio: '/eventos');
      await tester.pump();

      expect(find.text('EVT-2026-00005'), findsOneWidget);
      expect(find.text('Pendiente'), findsOneWidget);
      expect(find.text('Cliente ausente'), findsOneWidget);
      expect(find.text('Pedro Mamani Cruz'), findsOneWidget);
      expect(find.text('30/10/2026 09:22 · 2 fotos'), findsOneWidget);
      expect(find.text('Nuevo evento'), findsOneWidget);
    });

    testWidgets('filtra por estado', (tester) async {
      await abrir(tester, inicio: '/eventos');
      await tester.pump();

      await tester.tap(find.text('Todos los estados'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Resueltos'));
      await tester.pumpAndSettle();

      expect(eventos.listados.last.estado, EstadoEvento.resuelto);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, inicio: '/eventos', letra: 2, alto: 800);
      await tester.pump();
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('nuevo evento (7.3.13, paso 2; 8.6.8)', () {
    Future<void> abrirFormulario(WidgetTester tester, {double letra = 1, double alto = 2400}) async {
      final router = await abrir(tester, inicio: '/', letra: letra, alto: alto);
      router.push('/eventos/nuevo');
      await navegar(tester);
      gps.leer(-17.768930, -63.179455, 10);
      await tester.pump();
    }

    Future<void> elegirTipo(WidgetTester tester, String texto) async {
      await tester.tap(find.text('Elige el tipo…'));
      await tester.pumpAndSettle();
      await tester.tap(find.text(texto));
      await tester.pumpAndSettle();
    }

    Future<void> elegirAPedro(WidgetTester tester) async {
      await tester.tap(find.text('Busca el cliente…'));
      await navegar(tester);
      await tester.tap(find.text('Elegir a Pedro'));
      await navegar(tester);
    }

    testWidgets('la ubicación se busca sola: sin GPS, el aviso del 8.6.12 dice "…el evento"', (tester) async {
      gps.activado = false;
      final router = await abrir(tester, inicio: '/');
      router.push('/eventos/nuevo');
      await navegar(tester);

      expect(find.text('El GPS está desactivado.'), findsOneWidget);
      expect(find.text('Actívalo para registrar el evento: sin ubicación, el evento no se guarda.'), findsOneWidget);
    });

    testWidgets('un tipo que pide foto lo dice debajo, con el texto del 8.6.8', (tester) async {
      await abrirFormulario(tester);

      await elegirTipo(tester, 'Cliente ausente · pide foto');

      expect(find.text('Este tipo se resuelve con al menos una foto: agrégala ahora o después.'), findsOneWidget);
    });

    testWidgets('el pedido se elige entre los del cliente: sin cliente, no se puede elegir', (tester) async {
      await abrirFormulario(tester);
      expect(find.text('Primero elige el cliente.'), findsOneWidget);

      await elegirAPedro(tester);
      expect(pedidos.listados.last.clienteId, 64);
      await tester.tap(find.text('Sin pedido'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('PED-2026-00031 · En ruta · entrega 30/10/2026'));
      await tester.pumpAndSettle();

      expect(find.text('PED-2026-00031 · En ruta · entrega 30/10/2026'), findsOneWidget);
    });

    testWidgets('sin el tipo, el cliente ni qué pasó, cada error va en su campo (ext. 2.2a)', (tester) async {
      await abrirFormulario(tester);

      await tester.tap(find.text('Registrar evento'));
      await tester.pump();

      expect(find.text('Elige el tipo de evento.'), findsOneWidget);
      expect(find.text('Elige el cliente o el pedido del evento.'), findsOneWidget);
      expect(find.text('Describe lo que pasó.'), findsOneWidget);
      expect(eventos.registrados, isEmpty);
    });

    testWidgets('registra con la ubicación y sube las fotos: la de la cámara con su lugar, la de la galería sin él', (
      tester,
    ) async {
      await abrirFormulario(tester);
      await elegirTipo(tester, 'Cliente ausente · pide foto');
      await elegirAPedro(tester);
      await tester.enterText(campo('Qué pasó'), 'El local estaba cerrado y nadie atendió.');
      await tester.tap(find.text('Tomar foto'));
      await tester.pump();
      await tester.tap(find.text('Elegir de la galería'));
      await tester.pump();

      expect(find.text('· 2 de 5'), findsOneWidget);
      expect(find.text('30/10/2026 09:05'), findsOneWidget);
      expect(find.text('De la galería'), findsOneWidget);

      await tester.tap(find.text('Registrar evento'));
      await navegar(tester);

      final envio = eventos.registrados.single;
      expect(envio.datos.aJson(), {
        'tipoEventoId': 1,
        'clienteId': 64,
        'pedidoId': null,
        'descripcion': 'El local estaba cerrado y nadie atendió.',
      });
      expect(envio.ubicacion.latitud, -17.768930);
      expect(envio.clave, isNotEmpty);
      expect(eventos.fotosSubidas, hasLength(2));
      expect(eventos.fotosSubidas[0].lugar?.latitud, -17.768930);
      expect(eventos.fotosSubidas[1].foto.deLaGaleria, isTrue);
      expect(eventos.fotosSubidas[1].lugar, isNull);
      expect(exito('Se registró EVT-2026-00005: queda pendiente hasta que se resuelva.'), findsOneWidget);
    });

    testWidgets('al volver, la lista de Eventos ya tiene el evento nuevo, sin deslizar para actualizar', (
      tester,
    ) async {
      var registrado = false;
      eventos.alListar = (_, _) async => Pagina(
        items: [if (registrado) resumenDeEvento(eventoDePrueba())],
        total: registrado ? 1 : 0,
        numero: 1,
        tamano: 25,
      );
      eventos.alRegistrar = (_) async {
        registrado = true;
        return eventoDePrueba();
      };
      final router = await abrir(tester, inicio: '/');
      router.push('/eventos');
      await navegar(tester);
      router.push('/eventos/nuevo');
      await navegar(tester);
      gps.leer(-17.768930, -63.179455, 10);
      await tester.pump();
      await elegirTipo(tester, 'Cliente ausente · pide foto');
      await elegirAPedro(tester);
      await tester.enterText(campo('Qué pasó'), 'El local estaba cerrado y nadie atendió.');
      await tester.tap(find.text('Tomar foto'));
      await tester.pump();

      await tester.tap(find.text('Registrar evento'));
      await navegar(tester);
      expect(eventos.listados, hasLength(2));

      router.pop();
      await navegar(tester);
      expect(router.state.uri.toString(), '/eventos');
      expect(find.text('EVT-2026-00005'), findsOneWidget);
    });

    testWidgets('quitar una foto antes de registrar no pide confirmación', (tester) async {
      await abrirFormulario(tester);
      await tester.tap(find.text('Tomar foto'));
      await tester.pump();

      await tester.tap(find.byTooltip('Quitar la foto 1'));
      await tester.pump();

      expect(find.byType(AlertDialog), findsNothing);
      expect(find.text('· 0 de 5'), findsOneWidget);
    });

    testWidgets('con 5 fotos ya no se agregan más (7.3.14, ext. 2c)', (tester) async {
      await abrirFormulario(tester);
      for (var i = 0; i < 5; i++) {
        await tester.tap(find.text('Tomar foto'));
        await tester.pump();
      }

      expect(find.text('· 5 de 5'), findsOneWidget);
      expect(tester.widget<OutlinedButton>(find.widgetWithText(OutlinedButton, 'Tomar foto')).onPressed, isNull);
      expect(find.text('Ya tiene 5 fotos: quita una para agregar otra.'), findsOneWidget);
    });

    testWidgets('si una foto no se sube, el evento no se repite y "Reintentar" sube solo la que falta', (tester) async {
      var fallar = true;
      eventos.alAgregarFoto = (_) async {
        if (fallar) throw sinConexion;
        return eventoDePrueba(fotos: [fotoDelEventoDePrueba(1)]);
      };
      await abrirFormulario(tester);
      await elegirTipo(tester, 'Otro');
      await elegirAPedro(tester);
      await tester.enterText(campo('Qué pasó'), 'Se cayó la mercadería.');
      await tester.tap(find.text('Tomar foto'));
      await tester.pump();

      await tester.tap(find.text('Registrar evento'));
      await navegar(tester);
      expect(find.text('El evento quedó registrado, pero una foto no se subió.'), findsOneWidget);

      fallar = false;
      await tester.tap(find.text('Reintentar'));
      await navegar(tester);

      expect(eventos.registrados, hasLength(1));
      expect(eventos.fotosSubidas, hasLength(1));
      expect(exito('Se registró EVT-2026-00005: queda pendiente hasta que se resuelva.'), findsOneWidget);
    });

    testWidgets('desde un pedido, vienen el pedido y su cliente (7.3.2, ext. 3a)', (tester) async {
      await abrir(tester, inicio: '/pedidos/31', conPermisos: [...permisos, Permisos.pedidos.consultarPedidos]);
      await tester.pump();

      await tester.tap(find.text('Registrar evento'));
      await navegar(tester);
      gps.leer(-17.768930, -63.179455, 10);
      await tester.pump();
      await tester.pump();

      expect(find.text('Rosa Quispe Mamani'), findsOneWidget);
      expect(find.text('PED-2026-00031 · En ruta · entrega 30/10/2026'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrirFormulario(tester, letra: 2, alto: 800);
      await tester.scrollUntilVisible(find.text('Tomar foto'), 300, scrollable: find.byType(Scrollable).first);
      await tester.tap(find.text('Tomar foto'));
      await tester.pump();
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('el detalle y sus fotos (7.3.14)', () {
    final conFotos = [fotoDelEventoDePrueba(1), fotoDelEventoDePrueba(2)];

    testWidgets('los datos, el mapa y las fotos con la hora de la toma', (tester) async {
      eventos.alObtener = (_) async => eventoDePrueba(fotos: conFotos);
      await abrir(tester, inicio: '/eventos/5');
      await tester.pump();
      await tester.pump();

      expect(find.text('Cliente ausente · pide foto'), findsOneWidget);
      expect(find.text('El local estaba cerrado y nadie atendió. Vuelvo a pasar en la tarde.'), findsOneWidget);
      expect(find.byType(MapaDeUbicacion), findsOneWidget);
      expect(find.text('· 2 de 5'), findsOneWidget);
      expect(find.text('30/10/2026 09:21'), findsNWidgets(2));
    });

    testWidgets('tocar una foto la abre con quién la subió y cuándo se tomó (paso 3)', (tester) async {
      eventos.alObtener = (_) async => eventoDePrueba(fotos: conFotos);
      await abrir(tester, inicio: '/eventos/5');
      await tester.pump();

      await tester.tap(find.bySemanticsLabel(RegExp('^Foto 1, ')));
      await tester.pumpAndSettle();

      expect(
        find.text('Subida por Carlos Mendoza Flores el 30/10/2026 09:22. Tomada el 30/10/2026 09:21.'),
        findsOneWidget,
      );
    });

    testWidgets('eliminar una foto pide confirmar con el texto del 8.6.12 (paso 4)', (tester) async {
      eventos.alObtener = (_) async => eventoDePrueba(fotos: conFotos);
      await abrir(tester, inicio: '/eventos/5');
      await tester.pump();

      await tester.tap(find.byTooltip('Eliminar la foto 2'));
      await tester.pumpAndSettle();
      expect(find.text('¿Eliminar la foto?'), findsOneWidget);
      expect(find.text('Se borra del evento y del servidor: no se puede deshacer.'), findsOneWidget);
      await tester.tap(find.text('Eliminar foto'));
      await tester.pumpAndSettle();

      expect(eventos.fotosEliminadas, [2]);
    });

    testWidgets('agregar una foto con la cámara la sube enseguida', (tester) async {
      await abrir(tester, inicio: '/eventos/5');
      await tester.pump();

      await tester.tap(find.text('Tomar foto'));
      await tester.pump();
      await tester.pump();

      expect(eventos.fotosSubidas.single.eventoId, 5);
      expect(find.text('· 1 de 5'), findsOneWidget);
    });

    testWidgets('en un evento que registró otro promotor, no hay acciones (ext. *a; 7.3.13, ext. 3b)', (tester) async {
      eventos.alObtener = (_) async => eventoDePrueba(promotorId: 'otro', fotos: conFotos);
      await abrir(tester, inicio: '/eventos/5');
      await tester.pump();

      expect(find.text('Editar'), findsNothing);
      expect(find.text('Tomar foto'), findsNothing);
      expect(find.byTooltip('Eliminar la foto 1'), findsNothing);
    });

    testWidgets('un evento resuelto muestra la solución y ya no cambia (ext. 2b, 3a y 4.1a)', (tester) async {
      eventos.alObtener = (_) async => eventoDePrueba(estado: EstadoEvento.resuelto, fotos: conFotos);
      await abrir(tester, inicio: '/eventos/5');
      await tester.pump();

      expect(find.text('Resuelto'), findsOneWidget);
      expect(find.text('Se le entregó en la tarde.'), findsOneWidget);
      expect(find.text('Editar'), findsNothing);
      expect(find.text('Tomar foto'), findsNothing);
      expect(find.byTooltip('Eliminar la foto 1'), findsNothing);
    });

    testWidgets('editar (paso 3): guarda los cambios y vuelve al detalle con el aviso', (tester) async {
      await abrir(tester, inicio: '/eventos/5');
      await tester.pump();

      await tester.tap(find.text('Editar'));
      await navegar(tester);
      expect(tester.widget<TextField>(campo('Qué pasó')).controller!.text, startsWith('El local estaba cerrado'));
      expect(find.text('Elegir de la galería'), findsNothing);
      await tester.enterText(campo('Qué pasó'), 'Volví en la tarde y tampoco estaba.');
      await tester.tap(find.text('Guardar cambios'));
      await navegar(tester);

      expect(eventos.modificados.single.datos.descripcion, 'Volví en la tarde y tampoco estaba.');
      expect(exito('EVT-2026-00005 quedó modificado.'), findsOneWidget);
      // No empuja el contenido: el aviso no está en la lista del detalle.
      expect(find.descendant(of: find.byType(ListView), matching: find.text('EVT-2026-00005 quedó modificado.')), findsNothing);

      // A los 5 segundos se va solo; una segunda edición, con el mismo texto, avisa otra vez.
      await tester.pump(const Duration(seconds: 6));
      await tester.pumpAndSettle();
      expect(exito('EVT-2026-00005 quedó modificado.'), findsNothing);
      await tester.tap(find.text('Editar'));
      await navegar(tester);
      await tester.tap(find.text('Guardar cambios'));
      await navegar(tester);
      expect(eventos.modificados, hasLength(2));
      expect(exito('EVT-2026-00005 quedó modificado.'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      eventos.alObtener = (_) async => eventoDePrueba(fotos: conFotos);
      await abrir(tester, inicio: '/eventos/5', letra: 2, alto: 800);
      await tester.pump();
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  testWidgets('para un evento, un cliente inactivo se puede elegir', (tester) async {
    const inactivo = ClienteOpcion(
      id: 80,
      codigo: 'CLI-00080',
      nombreCompleto: 'Marta Rivero Salas',
      direccion: 'Calle 12 N.º 60',
      estado: estadoInactivo,
      inactivo: true,
    );
    final clientes = ClientesRepositoryFalso()..alBuscarOpciones = (_) async => const [inactivo];
    tamanoDeCelular(tester);
    await tester.pumpWidget(
      conLaApp(
        const ElegirClienteScreen(aceptaInactivos: true),
        overrides: [
          clientesRepositoryProvider.overrideWithValue(clientes),
          sesionProvider.overrideWith(() => SesionFija(ConSesion(promotorCon(const [])))),
        ],
      ),
    );
    await tester.enterText(find.byType(TextField), 'mar');
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pump();

    final opcion = tester.widget<ListTile>(
      find.ancestor(of: find.text('Marta Rivero Salas'), matching: find.byType(ListTile)),
    );
    expect(opcion.enabled, isTrue);
  });
}
