import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../../data/models/cliente.dart';
import '../../data/models/producto.dart';
import '../clientes/cliente_screen.dart';
import '../clientes/clientes_screen.dart';
import '../clientes/formulario_cliente_screen.dart';
import '../cobranzas/cobranza_screen.dart';
import '../cobranzas/cobranzas_screen.dart';
import '../cobranzas/registrar_pago_screen.dart';
import '../elegir/elegir_cliente_screen.dart';
import '../elegir/elegir_producto_screen.dart';
import '../eventos/evento_screen.dart';
import '../eventos/eventos_screen.dart';
import '../eventos/formulario_evento_screen.dart';
import '../hojas_pedido/hoja_pedido_screen.dart';
import '../hojas_pedido/hojas_pedido_screen.dart';
import '../hojas_pedido/nueva_hoja_screen.dart';
import '../menu/menu_screen.dart';
import '../pedidos/bandeja_screen.dart';
import '../pedidos/entrega_screen.dart';
import '../pedidos/nuevo_pedido_screen.dart';
import '../pedidos/pedido_screen.dart';
import '../pedidos/pedidos_screen.dart';
import '../perfil/cambiar_contrasena_screen.dart';
import '../perfil/perfil_screen.dart';
import '../productos/productos_screen.dart';
import '../rutas/ruta_del_dia_screen.dart';
import '../sesion/arranque_screen.dart';
import '../territorio/mapa_de_clientes_screen.dart';
import '../territorio/por_visitar_screen.dart';
import '../sesion/inicio_de_sesion_screen.dart';
import '../sesion/sesion_notifier.dart';
import 'widgets/sin_acceso.dart';

abstract final class Rutas {
  static const arranque = '/arranque';
  static const entrar = '/entrar';
  static const menu = '/';
  static const perfil = '/perfil';
  static const contrasena = '/perfil/contrasena';
  static const productos = '/productos';
  static const clientes = '/clientes';
  static const nuevoCliente = '/clientes/nuevo';
  static String cliente(int id) => '/clientes/$id';
  static String editarCliente(int id) => '/clientes/$id/editar';
  static const hojasPedido = '/hojas-pedido';
  static const nuevaHoja = '/hojas-pedido/nueva';
  static String hojaPedido(int id) => '/hojas-pedido/$id';
  static const elegirCliente = '/elegir-cliente';
  // Un evento acepta un cliente inactivo: puede explicar justo por qué quedó inactivo.
  static const elegirClienteParaEvento = '/elegir-cliente?inactivos=1';
  static const elegirProducto = '/elegir-producto';
  static const cobranzas = '/cobranzas';
  static String cobranzasDe(int clienteId) => '/cobranzas?clienteId=$clienteId';
  static String cobranza(int id) => '/cobranzas/$id';
  static String registrarPago(int cobranzaId) => '/cobranzas/$cobranzaId/pago';
  static const pedidos = '/pedidos';
  static const nuevoPedido = '/pedidos/nuevo';
  static String pedido(int id) => '/pedidos/$id';
  static const bandeja = '/bandeja';
  static String pedidoDeLaBandeja(int id) => '/bandeja/$id';
  static String entrega(int id) => '/bandeja/$id/entrega';
  static const eventos = '/eventos';
  static const nuevoEvento = '/eventos/nuevo';
  static String evento(int id) => '/eventos/$id';
  static String editarEvento(int id) => '/eventos/$id/editar';
  static const ruta = '/ruta';
  static const mapaDeClientes = '/mapa-de-clientes';
  static const porVisitar = '/por-visitar';
}

/// Los permisos que abren cada pantalla, como `PANTALLAS` del panel: el menú y las rutas usan la misma lista, así nunca
/// se muestra una entrada que después dice "Sin acceso". Con uno basta.
abstract final class PermisosDe {
  // Los mismos de GET /productos/opciones: quien vende también ve el catálogo.
  static final productos = [Permisos.ventas.consultarProductos, Permisos.ventas.registrarHp];

  // Los de `PANTALLAS.clientes` del panel. La lista y el detalle no piden permiso en la API: cada uno ve su cartera.
  static final clientes = [
    Permisos.ventas.registrarCliente,
    Permisos.ventas.editarCliente,
    Permisos.ventas.asignarEstadoCliente,
    Permisos.ventas.verUbicacionCliente,
  ];

  // Los de `PANTALLAS.hojasPedido` del panel, que son los de GET /hojas-pedido.
  static final hojasPedido = [Permisos.ventas.consultarHp, Permisos.ventas.anularHp];

  // Los de `PANTALLAS.cobranzas` del panel, que son los de GET /cobranzas.
  static final cobranzas = [Permisos.cobranzas.consultarDeudas, Permisos.cobranzas.consultarPagos];

  // Los de `PANTALLAS.pedidos` del panel: quien opera sobre un pedido también tiene que verlo, como en la API.
  static final pedidos = [
    Permisos.pedidos.consultarPedidos,
    Permisos.pedidos.modificarPedido,
    Permisos.pedidos.anularPedido,
    Permisos.pedidos.cambiarEstado,
    Permisos.pedidos.reprogramarEntrega,
    Permisos.pedidos.asignarPedidos,
    Permisos.pedidos.registrarEntrega,
  ];

  // Los de `PANTALLAS.bandeja` del panel, que son los de GET /pedidos/bandeja.
  static final bandeja = [Permisos.pedidos.verBandeja];

  // Los de `PANTALLAS.eventos` del panel: quien opera sobre un evento también tiene que verlo, como en la API.
  static final eventos = [
    Permisos.eventos.consultarEventos,
    Permisos.eventos.registrarEvento,
    Permisos.eventos.modificarEvento,
    Permisos.eventos.resolverEvento,
    Permisos.eventos.agregarFoto,
    Permisos.eventos.eliminarFoto,
  ];

  // Los de `PANTALLAS` del panel: la ruta del día es de quien la recorre (`frmRutaMovil`).
  static final ruta = [Permisos.territorial.recorrerRuta];

  // Los de `PANTALLAS.mapaClientes` y `PANTALLAS.porVisitar` del panel.
  static final mapaClientes = [Permisos.territorial.verMapaClientes];
  static final porVisitar = [Permisos.territorial.verClientesPorVisitar];
}

/// A dónde va la app según la sesión, o null si se queda donde está.
String? redirigir(EstadoDeSesion estado, String ubicacion) => switch (estado) {
  SesionCargando() || SesionSinConexion() => ubicacion == Rutas.arranque ? null : Rutas.arranque,
  SinSesion() => ubicacion == Rutas.entrar ? null : Rutas.entrar,
  ConSesion() => ubicacion == Rutas.entrar || ubicacion == Rutas.arranque ? Rutas.menu : null,
};

/// Muestra la pantalla solo si la sesión tiene alguno de sus permisos; si no, "Sin acceso". Sin permisos, la ve
/// cualquiera con sesión. Es lo que hace `PANTALLAS` en el panel: cada tarea de la app declara los de sus pantallas.
class ConPermiso extends ConsumerWidget {
  const ConPermiso({super.key, this.permisos = const [], required this.child});

  final List<String> permisos;
  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estado = ref.watch(sesionProvider);
    final permitido = estado is ConSesion && (permisos.isEmpty || estado.sesion.tiene(permisos));
    return permitido ? child : const SinAccesoScreen();
  }
}

final routerProvider = Provider<GoRouter>((ref) {
  // El router se crea una sola vez, para no perder la pila de pantallas. Esta señal le avisa que la sesión cambió, y
  // entonces vuelve a pasar por redirigir(): así un 401 en cualquier pantalla lleva al inicio de sesión.
  final sesion = ValueNotifier<EstadoDeSesion>(ref.read(sesionProvider));
  ref.listen(sesionProvider, (_, estado) => sesion.value = estado);

  final router = GoRouter(
    initialLocation: Rutas.arranque,
    refreshListenable: sesion,
    redirect: (context, state) => redirigir(sesion.value, state.matchedLocation),
    routes: [
      GoRoute(path: Rutas.arranque, builder: (context, state) => const ArranqueScreen()),
      GoRoute(path: Rutas.entrar, builder: (context, state) => const InicioDeSesionScreen()),
      GoRoute(
        path: Rutas.menu,
        builder: (context, state) => const ConPermiso(child: MenuScreen()),
        routes: [
          GoRoute(
            path: 'perfil',
            builder: (context, state) => const ConPermiso(child: PerfilScreen()),
            routes: [
              GoRoute(
                path: 'contrasena',
                builder: (context, state) => const ConPermiso(child: CambiarContrasenaScreen()),
              ),
            ],
          ),
          GoRoute(
            path: 'productos',
            builder: (context, state) => ConPermiso(permisos: PermisosDe.productos, child: const ProductosScreen()),
          ),
          GoRoute(
            path: 'hojas-pedido',
            builder: (context, state) => ConPermiso(permisos: PermisosDe.hojasPedido, child: const HojasPedidoScreen()),
            routes: [
              GoRoute(
                path: 'nueva',
                builder: (context, state) => ConPermiso(
                  permisos: [Permisos.ventas.registrarHp],
                  child: NuevaHojaScreen(cliente: state.extra as ClienteOpcion?),
                ),
              ),
              GoRoute(
                path: ':id',
                builder: (context, state) => ConPermiso(
                  permisos: PermisosDe.hojasPedido,
                  child: HojaPedidoScreen(id: int.parse(state.pathParameters['id']!), abrir: state.extra as AbrirHoja?),
                ),
              ),
            ],
          ),
          GoRoute(
            path: 'cobranzas',
            builder: (context, state) => ConPermiso(
              permisos: PermisosDe.cobranzas,
              // Desde un cliente: solo las de él, con su nombre arriba.
              child: CobranzasScreen(
                clienteId: int.tryParse(state.uri.queryParameters['clienteId'] ?? ''),
                clienteNombre: state.extra as String?,
              ),
            ),
            routes: [
              GoRoute(
                path: ':id',
                builder: (context, state) => ConPermiso(
                  permisos: PermisosDe.cobranzas,
                  child: CobranzaScreen(id: int.parse(state.pathParameters['id']!), mensaje: state.extra as String?),
                ),
                routes: [
                  GoRoute(
                    path: 'pago',
                    builder: (context, state) => ConPermiso(
                      permisos: [Permisos.cobranzas.registrarPago],
                      child: RegistrarPagoScreen(cobranzaId: int.parse(state.pathParameters['id']!)),
                    ),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: 'ruta',
            builder: (context, state) => ConPermiso(permisos: PermisosDe.ruta, child: const RutaDelDiaScreen()),
          ),
          GoRoute(
            path: 'mapa-de-clientes',
            builder: (context, state) =>
                ConPermiso(permisos: PermisosDe.mapaClientes, child: const MapaDeClientesScreen()),
          ),
          GoRoute(
            path: 'por-visitar',
            builder: (context, state) => ConPermiso(permisos: PermisosDe.porVisitar, child: const PorVisitarScreen()),
          ),
          GoRoute(
            path: 'bandeja',
            // Después de una entrega, la bandeja vuelve con su aviso.
            builder: (context, state) => ConPermiso(
              permisos: PermisosDe.bandeja,
              child: const BandejaScreen(),
            ),
            routes: [
              GoRoute(
                path: ':id',
                builder: (context, state) => ConPermiso(
                  permisos: PermisosDe.bandeja,
                  child: PedidoScreen(id: int.parse(state.pathParameters['id']!), desdeLaBandeja: true),
                ),
                routes: [
                  GoRoute(
                    path: 'entrega',
                    builder: (context, state) => ConPermiso(
                      permisos: [Permisos.pedidos.registrarEntrega],
                      child: EntregaScreen(id: int.parse(state.pathParameters['id']!)),
                    ),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: 'pedidos',
            builder: (context, state) => ConPermiso(permisos: PermisosDe.pedidos, child: const PedidosScreen()),
            routes: [
              // Antes que ':id': si no, "nuevo" se leería como un id.
              GoRoute(
                path: 'nuevo',
                builder: (context, state) =>
                    ConPermiso(permisos: [Permisos.pedidos.registrarPedido], child: const NuevoPedidoScreen()),
              ),
              GoRoute(
                path: ':id',
                builder: (context, state) => ConPermiso(
                  permisos: PermisosDe.pedidos,
                  child: PedidoScreen(id: int.parse(state.pathParameters['id']!), abrir: state.extra as AbrirPedido?),
                ),
              ),
            ],
          ),
          // Las búsquedas de una venta: vuelven con lo elegido.
          GoRoute(
            path: 'elegir-cliente',
            builder: (context, state) =>
                ElegirClienteScreen(aceptaInactivos: state.uri.queryParameters['inactivos'] == '1'),
          ),
          GoRoute(
            path: 'eventos',
            builder: (context, state) => ConPermiso(permisos: PermisosDe.eventos, child: const EventosScreen()),
            routes: [
              // Antes que ':id': si no, "nuevo" se leería como un id.
              GoRoute(
                path: 'nuevo',
                builder: (context, state) => ConPermiso(
                  permisos: [Permisos.eventos.registrarEvento],
                  child: FormularioEventoScreen(abrir: state.extra as AbrirNuevoEvento?),
                ),
              ),
              GoRoute(
                path: ':id',
                builder: (context, state) => ConPermiso(
                  permisos: PermisosDe.eventos,
                  child: EventoScreen(id: int.parse(state.pathParameters['id']!), abrir: state.extra as AbrirEvento?),
                ),
                routes: [
                  GoRoute(
                    path: 'editar',
                    builder: (context, state) => ConPermiso(
                      permisos: [Permisos.eventos.modificarEvento],
                      child: FormularioEventoScreen(eventoId: int.parse(state.pathParameters['id']!)),
                    ),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: 'elegir-producto',
            builder: (context, state) => ElegirProductoScreen(productos: state.extra as List<Producto>? ?? const []),
          ),
          GoRoute(
            path: 'clientes',
            builder: (context, state) => ConPermiso(permisos: PermisosDe.clientes, child: const ClientesScreen()),
            routes: [
              // Antes que ':id': si no, "nuevo" se leería como un id.
              GoRoute(
                path: 'nuevo',
                builder: (context, state) => ConPermiso(
                  permisos: [Permisos.ventas.registrarCliente],
                  // Desde la búsqueda de una venta, vuelve con el cliente elegido.
                  child: FormularioClienteScreen(devolver: state.extra == true),
                ),
              ),
              GoRoute(
                path: ':id',
                builder: (context, state) => ConPermiso(
                  permisos: PermisosDe.clientes,
                  child: ClienteScreen(id: int.parse(state.pathParameters['id']!), mensaje: state.extra as String?),
                ),
                routes: [
                  GoRoute(
                    path: 'editar',
                    builder: (context, state) => ConPermiso(
                      permisos: [Permisos.ventas.editarCliente],
                      child: FormularioClienteScreen(
                        id: int.parse(state.pathParameters['id']!),
                        capturarAlAbrir: state.extra == true,
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    ],
  );
  ref.onDispose(() {
    router.dispose();
    sesion.dispose();
  });
  return router;
});
