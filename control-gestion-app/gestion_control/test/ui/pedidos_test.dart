import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/cliente.dart';
import 'package:gestion_control/data/models/pagina.dart';
import 'package:gestion_control/data/models/pedido.dart';
import 'package:gestion_control/data/models/producto.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/core/router.dart';
import 'package:gestion_control/ui/elegir/elegir_producto_screen.dart';
import 'package:gestion_control/ui/pedidos/nuevo_pedido_screen.dart';
import 'package:gestion_control/ui/pedidos/nuevo_pedido_view_model.dart';
import 'package:gestion_control/ui/pedidos/pedido_screen.dart';
import 'package:gestion_control/ui/pedidos/pedidos_screen.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';
import 'package:go_router/go_router.dart';

import '../ayudantes.dart';

const juanOpcion = ClienteOpcion(
  id: 57,
  codigo: 'CLI-00057',
  nombreCompleto: 'Juan Choque Condori',
  direccion: 'Av. Norte N.º 455, al lado de la farmacia',
  estado: estadoActivo,
  inactivo: false,
);

void main() {
  late PedidosRepositoryFalso pedidos;
  late ProductosRepositoryFalso productos;

  setUp(() {
    pedidos = PedidosRepositoryFalso();
    productos = ProductosRepositoryFalso();
  });

  final permisos = [Permisos.pedidos.consultarPedidos, Permisos.pedidos.registrarPedido, Permisos.ventas.consultarHp];

  final rutas = <RouteBase>[
    GoRoute(
      path: '/',
      builder: (context, state) => const Scaffold(body: Text('Menú')),
      routes: [
        GoRoute(path: 'pedidos', builder: (context, state) => const PedidosScreen()),
        GoRoute(path: 'pedidos/nuevo', builder: (context, state) => const NuevoPedidoScreen()),
        GoRoute(
          path: 'pedidos/:id',
          builder: (context, state) =>
              PedidoScreen(id: int.parse(state.pathParameters['id']!), abrir: state.extra as AbrirPedido?),
        ),
        // La búsqueda del cliente ya tiene sus pruebas: aquí devuelve a Rosa o a Juan.
        GoRoute(
          path: 'elegir-cliente',
          builder: (context, state) => Scaffold(
            body: Column(
              children: [
                TextButton(onPressed: () => context.pop(rosaOpcion), child: const Text('Elegir a Rosa')),
                TextButton(onPressed: () => context.pop(juanOpcion), child: const Text('Elegir a Juan')),
              ],
            ),
          ),
        ),
        GoRoute(
          path: 'elegir-producto',
          builder: (context, state) => ElegirProductoScreen(productos: state.extra! as List<Producto>),
        ),
        destino('hojas-pedido/:id'),
      ],
    ),
  ];

  Future<GoRouter> abrir(
    WidgetTester tester, {
    required String inicio,
    double letra = 1,
    double alto = 2000,
    List<String>? conPermisos,
  }) => abrirConRouter(
    tester,
    rutas: rutas,
    inicio: inicio,
    letra: letra,
    alto: alto,
    overrides: [
      pedidosRepositoryProvider.overrideWithValue(pedidos),
      productosRepositoryProvider.overrideWithValue(productos),
      sesionProvider.overrideWith(() => SesionFija(ConSesion(promotorCon(conPermisos ?? permisos)))),
    ],
  );

  Future<void> navegar(WidgetTester tester) async {
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
  }

  group('la lista (7.3.1, paso 1)', () {
    testWidgets('cada pedido con su número, su estado, su cliente, su entrega y su monto', (tester) async {
      await abrir(tester, inicio: '/pedidos');
      await tester.pump();

      expect(find.text('PED-2026-00031'), findsOneWidget);
      expect(find.text('En ruta'), findsOneWidget);
      expect(find.text('Rosa Quispe Mamani'), findsOneWidget);
      expect(find.text('Entrega 30/10/2026 09:00'), findsOneWidget);
      expect(find.text('Bs 76,00'), findsOneWidget);
      expect(find.text('Nuevo pedido'), findsOneWidget);
    });

    testWidgets('sin el permiso de registrar, no ofrece "Nuevo pedido"', (tester) async {
      await abrir(tester, inicio: '/pedidos', conPermisos: [Permisos.pedidos.consultarPedidos]);
      await tester.pump();

      expect(find.text('Nuevo pedido'), findsNothing);
    });

    testWidgets('filtra por estado con los estados de la API', (tester) async {
      await abrir(tester, inicio: '/pedidos');
      await tester.pump();

      await tester.tap(find.text('Todos los estados'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('En proceso').last);
      await tester.pumpAndSettle();

      expect(pedidos.listados.last.estadoId, EstadosDePedido.enProceso);
    });

    testWidgets('vacía, lo dice', (tester) async {
      pedidos.alListar = (_, _, _) async => const Pagina(items: [], total: 0, numero: 1, tamano: 25);
      await abrir(tester, inicio: '/pedidos');
      await tester.pump();

      expect(find.text('Todavía no hay pedidos de tus clientes'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, inicio: '/pedidos', letra: 2, alto: 800);
      await tester.pump();
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('nuevo pedido (7.3.1, paso 2)', () {
    Future<GoRouter> abrirFormulario(WidgetTester tester, {double letra = 1, double alto = 2000}) async {
      final router = await abrir(tester, inicio: '/', letra: letra, alto: alto);
      router.push('/pedidos/nuevo');
      await navegar(tester);
      return router;
    }

    Future<void> elegirFechaYHora(WidgetTester tester) async {
      // Sin fecha elegida, el calendario propone mañana, y el reloj las 09:00: los dos se aceptan tal cual.
      await tester.tap(find.text('Elige el día…'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('ACEPTAR'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Elige la hora…'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('ACEPTAR'));
      await tester.pumpAndSettle();
    }

    Future<void> llenar(WidgetTester tester) async {
      await tester.tap(find.text('Busca el cliente…'));
      await navegar(tester);
      await tester.tap(find.text('Elegir a Rosa'));
      await navegar(tester);
      await elegirFechaYHora(tester);
      await tester.tap(find.text('Busca el producto…'));
      await navegar(tester);
      await tester.tap(find.text('Arroz grano largo 5 kg'));
      await navegar(tester);
      await tester.enterText(campo('Cantidad'), '2');
      await tester.pump();
    }

    testWidgets('elegir el cliente propone su dirección de entrega', (tester) async {
      await abrirFormulario(tester);

      await tester.tap(find.text('Busca el cliente…'));
      await navegar(tester);
      await tester.tap(find.text('Elegir a Rosa'));
      await navegar(tester);

      expect(tester.widget<TextField>(campo('Dirección de entrega')).controller!.text, rosaOpcion.direccion);
    });

    test('una dirección escrita a mano no se pisa al cambiar de cliente', () {
      final contenedor = ProviderContainer(overrides: [productosRepositoryProvider.overrideWithValue(productos)]);
      addTearDown(contenedor.dispose);
      final viewModel = contenedor.read(nuevoPedidoProvider.notifier);
      final sub = contenedor.listen(nuevoPedidoProvider, (_, _) {});
      addTearDown(sub.close);

      viewModel.elegirCliente(rosaOpcion);
      viewModel.elegirCliente(juanOpcion);
      expect(contenedor.read(nuevoPedidoProvider).direccion, juanOpcion.direccion);

      viewModel.cambiarDireccion('Mercado Norte, puesto 12');
      viewModel.elegirCliente(rosaOpcion);
      expect(contenedor.read(nuevoPedidoProvider).direccion, 'Mercado Norte, puesto 12');
    });

    testWidgets('sin los datos obligatorios, cada error va en su campo y no se envía nada (ext. 2.1b)', (tester) async {
      await abrirFormulario(tester);

      await tester.tap(find.text('Registrar pedido'));
      await tester.pump();

      expect(find.text('Elige el cliente.'), findsOneWidget);
      expect(find.text('Escribe la dirección de entrega.'), findsOneWidget);
      expect(find.text('Elige la fecha y la hora de entrega.'), findsOneWidget);
      expect(find.text('Elige el producto.'), findsOneWidget);
      expect(pedidos.registrados, isEmpty);
    });

    testWidgets('el stock que no alcanza avisa que no se podrá entregar, sin impedir registrar', (tester) async {
      await abrirFormulario(tester);
      await llenar(tester);

      await tester.enterText(campo('Cantidad'), '500');
      await tester.pump();

      expect(find.text('Solo hay 120 en stock: con esta cantidad no se podrá entregar.'), findsOneWidget);
    });

    testWidgets('registra la entrega en la hora de Bolivia, sin zona, y abre el detalle con el aviso', (tester) async {
      await abrirFormulario(tester);
      await llenar(tester);

      await tester.tap(find.text('Registrar pedido'));
      await navegar(tester);

      final envio = pedidos.registrados.single;
      final manana = ahoraEnBolivia().add(const Duration(days: 1));
      final json = envio.datos.aJson(clave: envio.clave);
      expect(json['clienteId'], 42);
      expect(json['direccionEntrega'], rosaOpcion.direccion);
      expect(
        json['fechaEntregaEstimada'],
        '${manana.year}-${manana.month.toString().padLeft(2, '0')}-${manana.day.toString().padLeft(2, '0')}T09:00:00',
      );
      expect(json['detalles'], [
        {'productoId': 1, 'cantidad': 2.0, 'descuento': 0.0},
      ]);
      expect(json['claveIdempotencia'], isNotEmpty);
      expect(find.text('Se registró PED-2026-00037 por Bs 76,00, con entrega el 30/10/2026 09:00.'), findsOneWidget);
    });

    testWidgets('al volver, la lista de Pedidos ya tiene el pedido nuevo, sin deslizar para actualizar', (
      tester,
    ) async {
      Pedido elNuevo() =>
          pedidoDePrueba(id: 37, numero: 'PED-2026-00037', estado: EstadosDePedido.pendiente, repartidorId: null);
      var registrado = false;
      pedidos.alListar = (_, _, _) async =>
          Pagina(items: [if (registrado) resumenDePedido(elNuevo())], total: registrado ? 1 : 0, numero: 1, tamano: 25);
      pedidos.alRegistrar = (_) async {
        registrado = true;
        return elNuevo();
      };
      final router = await abrir(tester, inicio: '/');
      router.push('/pedidos');
      await navegar(tester);
      router.push('/pedidos/nuevo');
      await navegar(tester);
      await llenar(tester);

      await tester.tap(find.text('Registrar pedido'));
      await navegar(tester);
      expect(pedidos.listados, hasLength(2));

      router.pop();
      await navegar(tester);
      expect(router.state.uri.toString(), '/pedidos');
      expect(find.text('PED-2026-00037'), findsOneWidget);
    });

    testWidgets('la fecha que la API rechaza va en su campo', (tester) async {
      pedidos.alRegistrar = (_) => throw const ErrorDeApi(
        mensaje: 'La fecha y la hora de entrega ya pasaron: elige unas futuras.',
        codigo: 'fecha_en_el_pasado',
        estado: 400,
      );
      await abrirFormulario(tester);
      await llenar(tester);

      await tester.tap(find.text('Registrar pedido'));
      await tester.pump();

      expect(find.text('La fecha y la hora de entrega ya pasaron: elige unas futuras.'), findsOneWidget);
    });

    testWidgets('sin respuesta, "Reintentar" repite la misma clave; después de una respuesta, cambia', (tester) async {
      pedidos.alRegistrar = (_) => throw sinConexion;
      await abrirFormulario(tester);
      await llenar(tester);

      await tester.tap(find.text('Registrar pedido'));
      await tester.pump();
      await tester.tap(find.text('Reintentar'));
      await tester.pump();

      expect(pedidos.registrados, hasLength(2));
      expect(pedidos.registrados[1].clave, pedidos.registrados[0].clave);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrirFormulario(tester, letra: 2, alto: 800);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('el detalle (7.3.3, paso 4; 7.3.2, paso 4)', () {
    testWidgets('los datos, los productos, el monto y el historial', (tester) async {
      await abrir(tester, inicio: '/pedidos/31');
      await tester.pump();

      expect(find.text('Rosa Quispe Mamani'), findsOneWidget);
      expect(find.text('Calle 4 N.º 120, frente al Mercado Norte'), findsOneWidget);
      expect(find.text('30/10/2026 09:00'), findsOneWidget);
      expect(find.text('Carlos Mendoza Flores'), findsOneWidget);
      expect(find.text('2 × Bs 38,00'), findsOneWidget);
      expect(find.text('Historial'), findsOneWidget);
      expect(find.text('Asignado a Carlos Mendoza Flores para el 30/10/2026.'), findsOneWidget);
    });

    testWidgets('entregado: quién recibió y la venta, que se abre', (tester) async {
      pedidos.alObtener = (_) async => pedidoDePrueba(estado: EstadosDePedido.entregado);
      await abrir(tester, inicio: '/pedidos/31');
      await tester.pump();

      expect(find.text('Doña Rosa'), findsOneWidget);
      expect(find.text('HP-2026-00040, a crédito'), findsOneWidget);
      await tester.tap(find.text('Ver la venta'));
      await navegar(tester);
      expect(find.text('En /hojas-pedido/40 con null'), findsOneWidget);
    });

    testWidgets('si no carga, "Reintentar" lo vuelve a pedir', (tester) async {
      pedidos.alObtener = (_) => throw sinConexion;
      await abrir(tester, inicio: '/pedidos/31');
      await tester.pump();
      expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);

      pedidos.alObtener = (_) async => pedidoDePrueba();
      await tester.tap(find.text('Reintentar'));
      await tester.pump();
      await tester.pump();

      expect(find.text('Historial'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, inicio: '/pedidos/31', letra: 2, alto: 800);
      await tester.pump();
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  test('las rutas de los pedidos', () {
    expect(Rutas.pedido(31), '/pedidos/31');
    expect(Rutas.nuevoPedido, '/pedidos/nuevo');
  });
}
