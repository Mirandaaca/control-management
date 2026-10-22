import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../menu/menu_screen.dart';
import '../perfil/cambiar_contrasena_screen.dart';
import '../perfil/perfil_screen.dart';
import '../productos/productos_screen.dart';
import '../sesion/arranque_screen.dart';
import '../sesion/inicio_de_sesion_screen.dart';
import '../sesion/sesion_notifier.dart';
import 'widgets/sin_acceso.dart';

abstract final class Rutas {
  static const arranque = '/arranque';
  static const entrar = '/entrar';
  static const menu = '/';
  static const perfil = '/perfil';
  static const contrasena = '/contrasena';
  static const productos = '/productos';
}

/// Los permisos que abren cada pantalla, como `PANTALLAS` del panel: el menú y las rutas usan la misma lista, así nunca
/// se muestra una entrada que después dice "Sin acceso". Con uno basta.
abstract final class PermisosDe {
  // Los mismos de GET /productos/opciones: quien vende también ve el catálogo.
  static final productos = [Permisos.ventas.consultarProductos, Permisos.ventas.registrarHp];
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
          ),
          GoRoute(
            path: 'contrasena',
            builder: (context, state) => const ConPermiso(child: CambiarContrasenaScreen()),
          ),
          GoRoute(
            path: 'productos',
            builder: (context, state) => ConPermiso(permisos: PermisosDe.productos, child: const ProductosScreen()),
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
