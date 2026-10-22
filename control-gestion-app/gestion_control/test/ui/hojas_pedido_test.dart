import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/cliente.dart';
import 'package:gestion_control/data/models/hoja_pedido.dart';
import 'package:gestion_control/data/models/pagina.dart';
import 'package:gestion_control/data/models/producto.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/domain/credito.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/formato.dart';
import 'package:gestion_control/ui/core/ubicacion/mapa_de_ubicacion.dart';
import 'package:gestion_control/ui/elegir/elegir_cliente_screen.dart';
import 'package:gestion_control/ui/elegir/elegir_producto_screen.dart';
import 'package:gestion_control/ui/hojas_pedido/hoja_pedido_screen.dart';
import 'package:gestion_control/ui/hojas_pedido/hojas_pedido_screen.dart';
import 'package:gestion_control/ui/hojas_pedido/nueva_hoja_screen.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';
import 'package:go_router/go_router.dart';

import '../ayudantes.dart';

const silviaOpcion = ClienteOpcion(
  id: 92,
  codigo: 'CLI-00092',
  nombreCompleto: 'Silvia Torrico Méndez',
  direccion: 'Calle 6 N.º 310',
  estado: estadoDisponible,
  inactivo: false,
);

void main() {
  late HojasPedidoRepositoryFalso hojas;
  late ProductosRepositoryFalso productos;
  late ClientesRepositoryFalso clientes;
  late UbicacionFalsa gps;

  setUp(() {
    hojas = HojasPedidoRepositoryFalso();
    productos = ProductosRepositoryFalso();
    clientes = ClientesRepositoryFalso()..alBuscarOpciones = (_) async => const [rosaOpcion];
    gps = UbicacionFalsa();
  });

  final permisos = [
    Permisos.ventas.registrarHp,
    Permisos.ventas.confirmarHp,
    Permisos.ventas.consultarHp,
    Permisos.ventas.registrarCliente,
  ];

  final rutas = <RouteBase>[
    GoRoute(
      path: '/',
      builder: (context, state) => const HojasPedidoScreen(),
      routes: [
        GoRoute(
          path: 'hojas-pedido/nueva',
          builder: (context, state) => NuevaHojaScreen(cliente: state.extra as ClienteOpcion?),
        ),
        GoRoute(
          path: 'hojas-pedido/:id',
          builder: (context, state) =>
              HojaPedidoScreen(id: int.parse(state.pathParameters['id']!), abrir: state.extra as AbrirHoja?),
        ),
        GoRoute(path: 'elegir-cliente', builder: (context, state) => const ElegirClienteScreen()),
        GoRoute(
          path: 'elegir-producto',
          builder: (context, state) => ElegirProductoScreen(productos: state.extra! as List<Producto>),
        ),
        // El formulario del cliente ya tiene sus pruebas: aquí solo devuelve al cliente registrado.
        GoRoute(
          path: 'clientes/nuevo',
          builder: (context, state) => Scaffold(
            body: Center(
              child: TextButton(onPressed: () => context.pop(silviaOpcion), child: const Text('Registrar de mentira')),
            ),
          ),
        ),
      ],
    ),
  ];

  Future<GoRouter> abrir(WidgetTester tester, {String inicio = '/', double letra = 1, double alto = 2000}) =>
      abrirConRouter(
        tester,
        rutas: rutas,
        inicio: inicio,
        letra: letra,
        alto: alto,
        overrides: [
          hojasPedidoRepositoryProvider.overrideWithValue(hojas),
          productosRepositoryProvider.overrideWithValue(productos),
          clientesRepositoryProvider.overrideWithValue(clientes),
          servicioDeUbicacionProvider.overrideWithValue(gps),
          mosaicosDelMapaProvider.overrideWithValue(false),
          sesionProvider.overrideWith(() => SesionFija(ConSesion(promotorCon(permisos)))),
        ],
      );

  // Esperar una navegación: pumpAndSettle no sirve con el indicador del GPS girando (dejaría pasar sus 30 s).
  Future<void> navegar(WidgetTester tester) async {
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
  }

  group('registrar una venta (8.6.5)', () {
    Future<void> abrirFormulario(WidgetTester tester, {double letra = 1, double alto = 2000}) async {
      final router = await abrir(tester, letra: letra, alto: alto);
      router.push('/hojas-pedido/nueva', extra: rosaOpcion);
      await navegar(tester);
      gps.leer(-17.783412, -63.182106, 8);
      await tester.pump();
    }

    Future<void> elegirProducto(WidgetTester tester, String nombre, {int linea = 0}) async {
      await tester.tap(find.text('Busca el producto…').at(linea));
      await navegar(tester);
      await tester.tap(find.text(nombre));
      await navegar(tester);
    }

    // La HP-2026-00018: 4 arroz con 1,00 de descuento c/u, 6 detergente y 7,00 sobre el total.
    Future<void> llenarComoLa18(WidgetTester tester) async {
      await elegirProducto(tester, 'Arroz grano largo 5 kg');
      await tester.enterText(campo('Cantidad').at(0), '4');
      await tester.enterText(campo('Descuento c/u (Bs)').at(0), '1');
      await tester.tap(find.text('Agregar producto'));
      await navegar(tester);
      await tester.tap(find.text('Detergente en polvo 1 kg'));
      await navegar(tester);
      await tester.enterText(campo('Cantidad').at(1), '6');
      await tester.tap(find.text('Crédito'));
      await tester.enterText(campo('Descuento sobre el total (Bs, opcional)'), '7');
      await tester.pump();
    }

    testWidgets('abierta desde un cliente, lo trae elegido, y busca la ubicación sola', (tester) async {
      await abrirFormulario(tester);

      expect(find.text('Rosa Quispe Mamani · CLI-00042'), findsOneWidget);
      expect(find.text('Ubicación capturada · ±8 m'), findsOneWidget);
    });

    testWidgets('cada línea muestra su precio y su stock, y el total cambia al editar', (tester) async {
      await abrirFormulario(tester);

      await llenarComoLa18(tester);

      expect(find.text('Bs 38,00 c/u · Stock: 120'), findsOneWidget);
      expect(find.text('Bs 16,50 c/u · Stock: 85'), findsOneWidget);
      expect(find.text('Bs 148,00'), findsOneWidget);
      expect(find.text('La deuda vence a los 30 días.'), findsOneWidget);
      // En el resumen y en la barra de abajo.
      expect(find.text('Bs 240,00'), findsNWidgets(2));
    });

    testWidgets('el producto es un botón del tamaño de su campo, no de toda la línea', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrirFormulario(tester);

      // Voice Access toca el centro del botón: tiene que caer en el campo, no en "Cantidad".
      final boton = tester.getSemantics(find.bySemanticsLabel(RegExp('^Producto 1')));
      final campoDelProducto = tester.getRect(find.text('Busca el producto…'));
      final cantidad = tester.getRect(campo('Cantidad'));
      expect(boton.rect.height, lessThan(cantidad.bottom - campoDelProducto.top));
      semantica.dispose();
    });

    testWidgets('si la cantidad supera el stock, lo avisa en ámbar en su línea', (tester) async {
      await abrirFormulario(tester);
      await elegirProducto(tester, 'Arroz grano largo 5 kg');

      await tester.enterText(campo('Cantidad'), '130');
      await tester.pump();

      expect(find.text('Solo hay 120 en stock.'), findsOneWidget);
    });

    testWidgets('uno sin stock se puede elegir: lo dice y la venta se guarda', (tester) async {
      await abrirFormulario(tester);
      await elegirProducto(tester, 'Jabón en barra');
      await tester.enterText(campo('Cantidad'), '2');
      await tester.pump();

      expect(find.text('Bs 6,00 c/u · Sin stock'), findsOneWidget);
      expect(find.text('Solo hay 0 en stock.'), findsOneWidget);
      await tester.tap(find.text('Guardar'));
      await navegar(tester);
      expect(hojas.guardadas, hasLength(1));
    });

    testWidgets('el descuento por unidad mayor que el precio se marca en su línea', (tester) async {
      await abrirFormulario(tester);
      await elegirProducto(tester, 'Arroz grano largo 5 kg');
      await tester.enterText(campo('Cantidad'), '4');
      await tester.enterText(campo('Descuento c/u (Bs)'), '39');

      await tester.tap(find.text('Guardar'));
      await tester.pump();

      expect(errorDelCampo(tester, 'Descuento c/u (Bs)'), 'El descuento no puede superar el precio (Bs 38,00).');
      expect(hojas.guardadas, isEmpty);
    });

    testWidgets('sin cliente ni cantidad, cada error va en su campo', (tester) async {
      final router = await abrir(tester);
      router.push('/hojas-pedido/nueva');
      await navegar(tester);
      gps.leer(-17.78, -63.18, 8);
      await tester.pump();

      await tester.tap(find.text('Guardar'));
      await tester.pump();

      expect(find.text('Elige el cliente.'), findsOneWidget);
      expect(find.text('Elige el producto.'), findsOneWidget);
      expect(errorDelCampo(tester, 'Cantidad'), 'La cantidad debe ser mayor que cero.');
    });

    testWidgets('"Guardar" la deja en borrador y abre su detalle con el aviso', (tester) async {
      final router = await (() async {
        await abrirFormulario(tester);
        return GoRouter.of(tester.element(find.byType(NuevaHojaScreen)));
      })();
      await llenarComoLa18(tester);

      await tester.tap(find.text('Guardar'));
      await navegar(tester);

      final envio = hojas.guardadas.single;
      expect(envio.datos.clienteId, 42);
      expect(envio.datos.tipoVenta, TipoVenta.credito);
      expect(envio.datos.descuento, 7);
      expect(envio.datos.lineas.map((l) => (l.productoId, l.cantidad, l.descuento)), [(1, 4.0, 1.0), (2, 6.0, 0.0)]);
      expect(envio.ubicacion.precision, 8);
      expect(router.state.uri.toString(), '/hojas-pedido/19');
      expect(find.text('HP-2026-00019 quedó en borrador: confírmala cuando cierres la venta.'), findsOneWidget);
    });

    testWidgets('"Confirmar venta" pide confirmar con el resumen y la cierra en un paso', (tester) async {
      await abrirFormulario(tester);
      await llenarComoLa18(tester);

      await tester.tap(find.widgetWithText(FilledButton, 'Confirmar venta'));
      await tester.pump();

      final vence = formatearMomento(vencimientoDelCredito(DateTime.now()));
      expect(find.text('¿Confirmar la venta?'), findsOneWidget);
      expect(find.text('2 productos, 10 unidades'), findsOneWidget);
      expect(find.text('A crédito: la deuda vence el $vence.'), findsOneWidget);
      expect(find.text('Total: Bs 240,00'), findsOneWidget);
      expect(hojas.confirmadasNuevas, isEmpty);

      await tester.tap(find.descendant(of: find.byType(AlertDialog), matching: find.text('Confirmar venta')));
      await navegar(tester);

      expect(hojas.confirmadasNuevas, hasLength(1));
      expect(
        find.text('HP-2026-00018 quedó confirmada por Bs 240,00 a crédito: la deuda vence el $vence.'),
        findsOneWidget,
      );
    });

    testWidgets('sin stock al confirmar, el error dice qué producto y cuánto hay, en su línea', (tester) async {
      hojas.alConfirmarNueva = (_) async => throw const ErrorDeApi(
        mensaje: 'No hay stock suficiente de Detergente en polvo 1 kg: hay 5 y la venta pide 6.',
        codigo: 'stock_insuficiente',
        estado: 400,
        campos: {
          'detalles[1].cantidad': 'No hay stock suficiente de Detergente en polvo 1 kg: hay 5 y la venta pide 6.',
        },
      );
      await abrirFormulario(tester);
      await llenarComoLa18(tester);

      await tester.tap(find.widgetWithText(FilledButton, 'Confirmar venta'));
      await tester.pump();
      await tester.tap(find.descendant(of: find.byType(AlertDialog), matching: find.text('Confirmar venta')));
      await navegar(tester);

      expect(find.byType(NuevaHojaScreen), findsOneWidget);
      expect(
        tester.widgetList<TextField>(campo('Cantidad')).length,
        2,
        reason: 'El formulario sigue abierto con sus dos líneas, para corregirlas.',
      );
      expect(
        find.text('No hay stock suficiente de Detergente en polvo 1 kg: hay 5 y la venta pide 6.'),
        findsOneWidget,
      );
      // El stock se trae de nuevo, para los avisos de cada línea.
      expect(productos.listados, 2);
    });

    testWidgets('sin conexión no se pierde nada, y Reintentar repite la misma clave', (tester) async {
      var intentos = 0;
      hojas.alGuardar = (_) async {
        if (intentos++ == 0) throw sinConexion;
        return hojaDePrueba(id: 19, numero: 'HP-2026-00019', estado: EstadoHP.borrador);
      };
      await abrirFormulario(tester);
      await llenarComoLa18(tester);

      await tester.tap(find.text('Guardar'));
      await tester.pump();
      await tester.pump();
      expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);
      expect(find.text('Bs 240,00'), findsNWidgets(2));

      await tester.tap(find.text('Reintentar'));
      await navegar(tester);
      expect(hojas.guardadas, hasLength(2));
      expect(hojas.guardadas.first.clave, hojas.guardadas.last.clave);
    });

    testWidgets('quitar una línea la saca de la venta y del total', (tester) async {
      await abrirFormulario(tester);
      await llenarComoLa18(tester);

      await tester.tap(find.byTooltip('Quitar el producto 2'));
      await tester.pump();

      expect(find.text('Detergente en polvo 1 kg'), findsNothing);
      // 148 − 7 = 141.
      expect(find.text('Bs 141,00'), findsNWidgets(2));
    });

    testWidgets('con la letra al 200 % cumple las guías, y Guardar y Confirmar se apilan', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrirFormulario(tester, letra: 2, alto: 800);

      final guardar = tester.getRect(find.widgetWithText(OutlinedButton, 'Guardar'));
      final confirmar = tester.getRect(find.widgetWithText(FilledButton, 'Confirmar venta'));
      expect(confirmar.top, greaterThan(guardar.bottom));
      await cumpleLasGuias(tester);
      semantica.dispose();
    });

    testWidgets('con la letra al 200 %, el título entra en la barra, más chico, en vez de cortarse', (tester) async {
      await abrirFormulario(tester, letra: 2, alto: 800);

      final titulo = tester.renderObject<RenderParagraph>(find.text('Nueva hoja de pedido'));
      expect(titulo.didExceedMaxLines, isFalse);
      expect(tester.getRect(find.text('Nueva hoja de pedido')).right, lessThanOrEqualTo(360));
    });
  });

  group('elegir el cliente', () {
    Future<void> abrirBusqueda(WidgetTester tester) async {
      final router = await abrir(tester);
      router.push('/hojas-pedido/nueva');
      await navegar(tester);
      await tester.tap(find.text('Busca el cliente…'));
      await navegar(tester);
    }

    testWidgets('busca en la API mientras escribe, 300 ms después de la última tecla', (tester) async {
      await abrirBusqueda(tester);
      expect(find.text('CLI-00042 · Calle 4 N.º 120, frente al Mercado Norte'), findsOneWidget);

      await tester.enterText(campo('Buscar cliente'), 'ro');
      await tester.enterText(campo('Buscar cliente'), 'rosa');
      await tester.pump(const Duration(milliseconds: 300));

      expect(clientes.busquedas, ['', 'rosa']);
    });

    testWidgets('un cliente inactivo sale deshabilitado, con su estado', (tester) async {
      clientes.alBuscarOpciones = (_) async => const [
        ClienteOpcion(
          id: 50,
          codigo: 'CLI-00050',
          nombreCompleto: 'Tienda Cerrada',
          direccion: 'Calle 9',
          estado: estadoInactivo,
          inactivo: true,
        ),
      ];
      await abrirBusqueda(tester);

      expect(find.text('Inactivo'), findsOneWidget);
      expect(tester.widget<ListTile>(find.byType(ListTile)).enabled, isFalse);
    });

    testWidgets('con 50 resultados avisa que hay más', (tester) async {
      clientes.alBuscarOpciones = (_) async => List.filled(50, rosaOpcion);
      await abrirBusqueda(tester);

      expect(find.text('50 resultados. Hay más: escribe para afinar la búsqueda.'), findsOneWidget);
    });

    testWidgets('elegir uno vuelve a la venta con él', (tester) async {
      await abrirBusqueda(tester);

      await tester.tap(find.text('Rosa Quispe Mamani'));
      await navegar(tester);

      expect(find.text('Rosa Quispe Mamani · CLI-00042'), findsOneWidget);
    });

    testWidgets('si no aparece, registrarlo vuelve a la venta con el cliente nuevo', (tester) async {
      await abrirBusqueda(tester);

      await tester.tap(find.text('Registrar cliente nuevo'));
      await navegar(tester);
      await tester.tap(find.text('Registrar de mentira'));
      await navegar(tester);

      expect(find.text('Silvia Torrico Méndez · CLI-00092'), findsOneWidget);
    });
  });

  group('el detalle', () {
    testWidgets('muestra el cliente, el tipo, la ubicación, los productos y el total', (tester) async {
      await abrir(tester, inicio: '/hojas-pedido/18');

      expect(find.text('HP-2026-00018'), findsWidgets);
      expect(find.text('Confirmada'), findsOneWidget);
      expect(find.text('Crédito'), findsOneWidget);
      expect(find.text('COB-2026-00012'), findsOneWidget);
      expect(find.byType(MapaDeUbicacion), findsOneWidget);
      expect(find.text('4 × Bs 38,00 − Bs 1,00 c/u'), findsOneWidget);
      expect(find.text('Bs 240,00'), findsOneWidget);
      expect(find.text('Confirmar venta'), findsNothing);
    });

    testWidgets('un borrador suyo se confirma con su resumen', (tester) async {
      hojas.alObtener = (_) async => hojaDePrueba(estado: EstadoHP.borrador, tipo: TipoVenta.contado);
      hojas.alConfirmar = (_) async => hojaDePrueba(tipo: TipoVenta.contado);
      await abrir(tester, inicio: '/hojas-pedido/18');

      await tester.tap(find.widgetWithText(FilledButton, 'Confirmar venta'));
      await tester.pump();
      expect(find.text('Al contado.'), findsOneWidget);
      await tester.tap(find.descendant(of: find.byType(AlertDialog), matching: find.text('Confirmar venta')));
      await tester.pumpAndSettle();

      expect(hojas.confirmadas, [18]);
      expect(find.text('HP-2026-00018 quedó confirmada por Bs 240,00 al contado.'), findsOneWidget);
    });

    testWidgets('sin stock, cada línea dice cuánto hay, y avisa cómo corregirla', (tester) async {
      hojas.alObtener = (_) async => hojaDePrueba(estado: EstadoHP.borrador);
      hojas.alConfirmar = (_) async => throw const ErrorDeApi(
        mensaje: 'No hay stock suficiente de Arroz grano largo 5 kg: hay 3 y la venta pide 4.',
        codigo: 'stock_insuficiente',
        estado: 400,
        campos: {'detalles[0].cantidad': 'No hay stock suficiente de Arroz grano largo 5 kg: hay 3 y la venta pide 4.'},
      );
      await abrir(tester, inicio: '/hojas-pedido/18');

      await tester.tap(find.widgetWithText(FilledButton, 'Confirmar venta'));
      await tester.pump();
      await tester.tap(find.descendant(of: find.byType(AlertDialog), matching: find.text('Confirmar venta')));
      await tester.pumpAndSettle();

      expect(find.text('No hay stock suficiente de Arroz grano largo 5 kg: hay 3 y la venta pide 4.'), findsOneWidget);
      expect(find.text('No alcanza el stock para confirmarla.'), findsOneWidget);
    });

    testWidgets('el borrador de otro promotor no se confirma aquí', (tester) async {
      hojas.alObtener = (_) async => hojaDePrueba(estado: EstadoHP.borrador, promotorId: 'otro');
      await abrir(tester, inicio: '/hojas-pedido/18');

      expect(find.text('Confirmar venta'), findsNothing);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      hojas.alObtener = (_) async => hojaDePrueba(estado: EstadoHP.borrador);
      final semantica = tester.ensureSemantics();
      await abrir(tester, inicio: '/hojas-pedido/18', letra: 2, alto: 800);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('la lista', () {
    testWidgets('muestra cada una con su estado, su cliente, su fecha, su tipo y su total', (tester) async {
      await abrir(tester, alto: 800);

      expect(find.text('HP-2026-00018'), findsOneWidget);
      expect(find.text('Confirmada'), findsOneWidget);
      expect(find.text('Rosa Quispe Mamani'), findsOneWidget);
      expect(find.text('08/10/2026 09:30 · Crédito'), findsOneWidget);
      expect(find.text('Bs 240,00'), findsOneWidget);
    });

    testWidgets('filtra por estado', (tester) async {
      await abrir(tester, alto: 800);

      await tester.tap(find.text('Todos los estados'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Borrador').last);
      await tester.pumpAndSettle();

      expect(hojas.listados.last.estado, EstadoHP.borrador);
    });

    testWidgets('un borrador suyo tiene "Confirmar venta", que abre el detalle y pide la confirmación', (tester) async {
      hojas.alListar = (_, _, _, _) async =>
          Pagina(items: [resumenDe(hojaDePrueba(estado: EstadoHP.borrador))], total: 1, numero: 1, tamano: 25);
      hojas.alObtener = (_) async => hojaDePrueba(estado: EstadoHP.borrador);
      await abrir(tester, alto: 800);

      await tester.tap(find.widgetWithText(OutlinedButton, 'Confirmar venta'));
      await tester.pumpAndSettle();

      expect(find.text('¿Confirmar la venta?'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, letra: 2, alto: 800);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });
}
