import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/cliente.dart';
import 'package:gestion_control/data/models/pagina.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/domain/geometria.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/clientes/cliente_screen.dart';
import 'package:gestion_control/ui/clientes/clientes_screen.dart';
import 'package:gestion_control/ui/clientes/formulario_cliente_screen.dart';
import 'package:gestion_control/ui/core/ubicacion/mapa_de_ubicacion.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';
import 'package:go_router/go_router.dart';

import '../ayudantes.dart';

void main() {
  late ClientesRepositoryFalso clientes;
  late ZonasRepositoryFalso zonas;
  late UbicacionFalsa gps;

  setUp(() {
    clientes = ClientesRepositoryFalso();
    zonas = ZonasRepositoryFalso();
    gps = UbicacionFalsa();
  });

  final permisos = [
    Permisos.ventas.registrarCliente,
    Permisos.ventas.editarCliente,
    Permisos.ventas.verUbicacionCliente,
  ];

  List<Object> overrides() => [
    clientesRepositoryProvider.overrideWithValue(clientes),
    zonasRepositoryProvider.overrideWithValue(zonas),
    servicioDeUbicacionProvider.overrideWithValue(gps),
    mosaicosDelMapaProvider.overrideWithValue(false),
    sesionProvider.overrideWith(() => SesionFija(ConSesion(promotorCon(permisos)))),
  ];

  final rutas = <RouteBase>[
    GoRoute(
      path: '/',
      builder: (context, state) => const ClientesScreen(),
      routes: [
        GoRoute(
          path: 'clientes/nuevo',
          builder: (context, state) => FormularioClienteScreen(devolver: state.extra == true),
        ),
        GoRoute(
          path: 'clientes/:id',
          builder: (context, state) =>
              ClienteScreen(id: int.parse(state.pathParameters['id']!), mensaje: state.extra as String?),
        ),
        GoRoute(
          path: 'clientes/:id/editar',
          builder: (context, state) =>
              FormularioClienteScreen(id: int.parse(state.pathParameters['id']!), capturarAlAbrir: state.extra == true),
        ),
      ],
    ),
  ];

  Future<GoRouter> abrir(WidgetTester tester, {String inicio = '/', double letra = 1, double alto = 1600}) =>
      abrirConRouter(tester, rutas: rutas, inicio: inicio, overrides: overrides().cast(), letra: letra, alto: alto);

  group('el punto dentro del límite, como en el panel', () {
    test('un punto adentro y uno afuera del cuadrado de la Zona Norte', () {
      expect(puntoEnPoligono(-17.771845, -63.17629, zonaNorte.limite!), isTrue);
      expect(puntoEnPoligono(-17.81, -63.18, zonaNorte.limite!), isFalse);
    });

    test('en una zona con forma de L, el hueco queda afuera', () {
      const ele = [
        (latitud: 0.0, longitud: 0.0),
        (latitud: 0.0, longitud: 2.0),
        (latitud: 1.0, longitud: 2.0),
        (latitud: 1.0, longitud: 1.0),
        (latitud: 2.0, longitud: 1.0),
        (latitud: 2.0, longitud: 0.0),
      ];
      expect(puntoEnPoligono(0.5, 1.5, ele), isTrue);
      expect(puntoEnPoligono(1.5, 1.5, ele), isFalse);
    });

    test('la zona que contiene el punto; una sin límite no cuenta', () {
      expect(zonaQueContiene(-17.81, -63.18, [zonaDeOtro, zonaNorte, zonaSur])?.nombre, 'Zona Sur');
      expect(zonaQueContiene(-17.70, -63.18, [zonaNorte, zonaSur]), isNull);
    });
  });

  group('la lista', () {
    testWidgets('muestra cada cliente con su código, su zona, su dirección, su estado y lo que debe', (tester) async {
      await abrir(tester, alto: 800);

      expect(find.text('Rosa Quispe Mamani'), findsOneWidget);
      expect(find.text('CLI-00042 · Zona Norte'), findsOneWidget);
      expect(find.text('Calle 4 N.º 120, frente al Mercado Norte'), findsOneWidget);
      expect(find.text('Activo'), findsOneWidget);
      expect(find.text('Debe Bs 140,00'), findsOneWidget);
      expect(find.text('1 cliente'), findsOneWidget);
    });

    testWidgets('busca en la API 300 ms después de la última tecla', (tester) async {
      await abrir(tester, alto: 800);

      await tester.enterText(campo('Buscar'), 'ro');
      await tester.pump(const Duration(milliseconds: 100));
      await tester.enterText(campo('Buscar'), 'rosa');
      await tester.pump(const Duration(milliseconds: 300));

      expect(clientes.listados.map((l) => l.$1), ['', 'rosa']);
    });

    testWidgets('con más de una página, "Cargar más" trae la siguiente', (tester) async {
      clientes.alListar = (_, pagina) async => Pagina(
        items: [
          for (var i = 0; i < 2; i++)
            ClienteResumen(
              id: pagina * 10 + i,
              codigo: 'CLI-$pagina$i',
              nombreCompleto: 'Cliente $pagina-$i',
              telefono: '7000000$i',
              direccion: 'Calle $i',
              zona: zonaNorteResumen,
              estado: estadoDisponible,
              saldoPendiente: 0,
            ),
        ],
        total: 4,
        numero: pagina,
        tamano: 2,
      );
      await abrir(tester);

      await tester.tap(find.text('Cargar más'));
      await tester.pump();

      expect(find.text('Cliente 2-1'), findsOneWidget);
      expect(find.text('Cargar más'), findsNothing);
    });

    testWidgets('sin clientes y sin coincidencias, dos mensajes distintos', (tester) async {
      clientes.alListar = (_, _) async => const Pagina(items: [], total: 0, numero: 1, tamano: 25);
      await abrir(tester, alto: 800);
      expect(find.text('Todavía no tienes clientes'), findsOneWidget);

      await tester.enterText(campo('Buscar'), 'xyz');
      await tester.pump(const Duration(milliseconds: 300));
      expect(find.text('Ningún cliente coincide con la búsqueda.'), findsOneWidget);
    });

    testWidgets('si la carga falla, avisa y Reintentar vuelve a pedir', (tester) async {
      clientes.alListar = (_, _) async => throw sinConexion;
      await abrir(tester, alto: 800);
      expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);

      clientes.alListar = (_, _) async => const Pagina(items: [rosaResumen], total: 1, numero: 1, tamano: 25);
      await tester.tap(find.text('Reintentar'));
      await tester.pump();
      expect(find.text('Rosa Quispe Mamani'), findsOneWidget);
    });

    testWidgets('tocar un cliente lo abre', (tester) async {
      final router = await abrir(tester, alto: 800);

      await tester.tap(find.text('Rosa Quispe Mamani'));
      await tester.pumpAndSettle();

      expect(router.state.uri.toString(), '/clientes/42');
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, letra: 2, alto: 800);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('el detalle', () {
    testWidgets('muestra sus datos, su estado, lo que debe y su ubicación en el mapa', (tester) async {
      await abrir(tester, inicio: '/clientes/42');

      expect(find.text('Rosa Quispe Mamani'), findsOneWidget);
      expect(find.text('Debe Bs 140,00'), findsOneWidget);
      expect(find.text('70010042'), findsOneWidget);
      expect(find.text('Zona Norte'), findsOneWidget);
      expect(find.byType(MapaDeUbicacion), findsOneWidget);
      expect(find.text('08/10/2026'), findsOneWidget);
    });

    testWidgets('un cliente sin ubicación lo dice y ofrece capturarla', (tester) async {
      clientes.alObtener = (_) async => Cliente(
        id: 42,
        codigo: rosa.codigo,
        nombreCompleto: rosa.nombreCompleto,
        telefono: rosa.telefono,
        direccion: rosa.direccion,
        latitud: 0,
        longitud: 0,
        zona: zonaNorteResumen,
        estado: estadoActivo,
        saldoPendiente: 0,
      );
      final router = await abrir(tester, inicio: '/clientes/42');

      expect(find.text('Este cliente no tiene ubicación.'), findsOneWidget);
      expect(find.byType(MapaDeUbicacion), findsNothing);
      await tester.tap(find.text('Capturar ubicación'));
      // pump y no pumpAndSettle: con el indicador girando, pumpAndSettle dejaría pasar los 30 s del GPS.
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 500));
      expect(router.state.uri.toString(), '/clientes/42/editar');
      expect(find.text('Buscando la señal del GPS…'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, inicio: '/clientes/42', letra: 2, alto: 800);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('registrar', () {
    Future<void> escribir(WidgetTester tester, {String celular = '70010092'}) async {
      await tester.enterText(campo('Nombre completo'), 'Silvia Torrico Méndez');
      await tester.enterText(campo('Celular'), celular);
      await tester.enterText(campo('Dirección'), 'Calle 6 N.º 310, a media cuadra del Mercado Norte');
      await tester.enterText(campo('Razón social (opcional)'), 'Tienda Silvia');
    }

    Future<void> registrar(WidgetTester tester) async {
      await tester.tap(find.widgetWithText(FilledButton, 'Registrar cliente'));
      await tester.pump();
      await tester.pump();
    }

    testWidgets('con la ubicación y los obligatorios, lo registra y abre al cliente', (tester) async {
      final router = await abrir(tester, inicio: '/clientes/nuevo');
      gps.leer(-17.771845, -63.17629, 6);
      await tester.pump();
      expect(find.text('Ubicación capturada · ±6 m'), findsOneWidget);
      expect(find.byType(MapaDeUbicacion), findsOneWidget);
      // Con una sola zona, viene elegida.
      expect(find.text('Zona Norte'), findsOneWidget);

      await escribir(tester);
      await registrar(tester);
      await tester.pumpAndSettle();

      final envio = clientes.registros.single;
      expect(envio.datos.nombreCompleto, 'Silvia Torrico Méndez');
      expect(envio.datos.zonaId, 1);
      expect(envio.datos.razonSocial, 'Tienda Silvia');
      expect(envio.ubicacion.latitud, -17.771845);
      expect(envio.confirmar, isFalse);
      expect(router.state.uri.toString(), '/clientes/92');
      expect(find.text('Se registró a Silvia Torrico Méndez.'), findsOneWidget);
    });

    testWidgets('sin los obligatorios, cada error va en su campo y no envía nada', (tester) async {
      zonas.alListar = () async => const [zonaNorte, zonaSur];
      await abrir(tester, inicio: '/clientes/nuevo');
      gps.leer(-17.70, -63.18, 6);
      await tester.pump();

      await registrar(tester);

      expect(errorDelCampo(tester, 'Nombre completo'), 'El nombre es obligatorio.');
      expect(errorDelCampo(tester, 'Celular'), 'El celular es obligatorio.');
      expect(errorDelCampo(tester, 'Dirección'), 'La dirección es obligatoria.');
      expect(find.text('Elige la zona.'), findsOneWidget);
      expect(clientes.registros, isEmpty);
    });

    testWidgets('con varias zonas, sugiere la que contiene el punto, con el texto del panel', (tester) async {
      zonas.alListar = () async => const [zonaSur, zonaNorte];
      await abrir(tester, inicio: '/clientes/nuevo');
      expect(find.text('Elige la zona…'), findsOneWidget);

      gps.leer(-17.771845, -63.17629, 6);
      await tester.pump();

      expect(find.text('Zona Norte'), findsOneWidget);
      expect(find.text('El punto cae dentro del límite de esta zona.'), findsOneWidget);
    });

    testWidgets('un celular de su cartera se avisa al salir del campo, y registrar lo confirma', (tester) async {
      clientes.alBuscarCelular = (_) async => rosaResumen;
      await abrir(tester, inicio: '/clientes/nuevo');
      gps.leer(-17.771845, -63.17629, 6);
      await tester.pump();

      await escribir(tester, celular: '70010042');
      await tester.pump();

      expect(clientes.celulares, ['70010042']);
      expect(find.text('Ya existe el cliente Rosa Quispe Mamani (CLI-00042) con ese celular.'), findsOneWidget);
      expect(find.text('Abrir cliente'), findsOneWidget);
      await registrar(tester);
      expect(clientes.registros.single.confirmar, isTrue);
    });

    testWidgets('un celular de otra cartera se avisa al registrar, y tocar otra vez lo registra igual', (tester) async {
      var primero = true;
      clientes.alRegistrar = (envio) async {
        if (primero) {
          primero = false;
          throw const ErrorDeApi(
            mensaje: 'Ya existe otro cliente con ese celular.',
            codigo: 'telefono_repetido',
            estado: 409,
          );
        }
        return rosa;
      };
      await abrir(tester, inicio: '/clientes/nuevo');
      gps.leer(-17.771845, -63.17629, 6);
      await tester.pump();
      await escribir(tester, celular: '70010080');

      await registrar(tester);

      expect(find.text('Ya hay un cliente con este celular en otra cartera.'), findsOneWidget);
      expect(find.text('Toca “Registrar cliente” otra vez para registrarlo igual.'), findsOneWidget);
      await registrar(tester);
      expect(clientes.registros.last.confirmar, isTrue);
      // Hubo respuesta: el segundo envío lleva otra clave.
      expect(clientes.registros.first.clave, isNot(clientes.registros.last.clave));
    });

    testWidgets('si toca registrar antes de la ubicación, espera la primera lectura y envía', (tester) async {
      await abrir(tester, inicio: '/clientes/nuevo');
      await escribir(tester);

      await registrar(tester);
      expect(find.text('Buscando la ubicación…'), findsOneWidget);
      expect(clientes.registros, isEmpty);

      gps.leer(-17.771845, -63.17629, 45);
      await tester.pump();
      await tester.pump();
      expect(clientes.registros.single.ubicacion.precision, 45);
    });

    testWidgets('sin GPS no se registra: la tarjeta dice qué falta', (tester) async {
      gps.activado = false;
      await abrir(tester, inicio: '/clientes/nuevo');
      await escribir(tester);

      await registrar(tester);

      expect(clientes.registros, isEmpty);
      expect(find.text('El GPS está desactivado.'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Registrar cliente'), findsOneWidget);
    });

    testWidgets('sin conexión no se pierde nada, y Reintentar repite la misma clave', (tester) async {
      var intentos = 0;
      clientes.alRegistrar = (envio) async {
        if (intentos++ == 0) throw sinConexion;
        return rosa;
      };
      await abrir(tester, inicio: '/clientes/nuevo');
      gps.leer(-17.771845, -63.17629, 6);
      await tester.pump();
      await escribir(tester);

      await registrar(tester);
      expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);
      expect(tester.widget<TextField>(campo('Nombre completo')).controller!.text, 'Silvia Torrico Méndez');

      await tester.tap(find.text('Reintentar'));
      await tester.pump();
      await tester.pump();
      expect(clientes.registros, hasLength(2));
      expect(clientes.registros.first.clave, clientes.registros.last.clave);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, inicio: '/clientes/nuevo', letra: 2, alto: 800);
      gps.leer(-17.771845, -63.17629, 6);
      await tester.pump();
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('editar', () {
    testWidgets('trae sus datos y su ubicación guardada, que no cambia sola', (tester) async {
      final router = await abrir(tester);
      final resultado = router.push<String>('/clientes/42/editar');
      await tester.pumpAndSettle();

      expect(tester.widget<TextField>(campo('Celular')).controller!.text, '70010042');
      expect(find.text('Ubicación guardada'), findsOneWidget);
      expect(find.text('Captúrala de nuevo solo si estás en el negocio del cliente.'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Guardar cambios'));
      await tester.pumpAndSettle();

      expect(clientes.ediciones.single.ubicacion.latitud, -17.783412);
      expect(await resultado, 'Se guardaron los cambios de Rosa Quispe Mamani.');
    });

    testWidgets('"Volver a capturar" toma la ubicación del GPS', (tester) async {
      final router = await abrir(tester);
      router.push('/clientes/42/editar');
      await tester.pumpAndSettle();

      await tester.tap(find.text('Volver a capturar'));
      await tester.pump();
      gps.leer(-17.7835, -63.1822, 5);
      await tester.pump();

      expect(find.text('Ubicación capturada · ±5 m'), findsOneWidget);
    });
  });
}
