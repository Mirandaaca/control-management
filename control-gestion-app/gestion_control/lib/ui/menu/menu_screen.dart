import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/sesion.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../sesion/sesion_notifier.dart';

/// Una entrada del menú principal, con los permisos que la abren (sin permisos, la ve cualquiera con sesión). Cada tarea
/// de la app agrega aquí la suya, con las mismas claves que `PANTALLAS` del panel y que su ruta en `router.dart`.
class EntradaDelMenu {
  const EntradaDelMenu({
    required this.titulo,
    required this.descripcion,
    required this.icono,
    required this.ruta,
    this.permisos = const [],
  });

  final String titulo;
  final String descripcion;
  final IconData icono;
  final String ruta;
  final List<String> permisos;
}

final entradasDelMenu = [
  EntradaDelMenu(
    titulo: 'Ruta del día',
    descripcion: 'Los clientes que visitas hoy, en orden: iniciarla, marcar cada visita y terminarla.',
    icono: Icons.route_outlined,
    ruta: Rutas.ruta,
    permisos: PermisosDe.ruta,
  ),
  EntradaDelMenu(
    titulo: 'Bandeja del día',
    descripcion: 'Los pedidos que tienes que entregar hoy.',
    icono: Icons.inbox_outlined,
    ruta: Rutas.bandeja,
    permisos: PermisosDe.bandeja,
  ),
  EntradaDelMenu(
    titulo: 'Pedidos',
    descripcion: 'Los de tus clientes y los que te asignan: registrarlos y ver su estado.',
    icono: Icons.shopping_bag_outlined,
    ruta: Rutas.pedidos,
    permisos: PermisosDe.pedidos,
  ),
  EntradaDelMenu(
    titulo: 'Clientes',
    descripcion: 'Tu cartera: registrarlos, editarlos y ver dónde están.',
    icono: Icons.storefront_outlined,
    ruta: Rutas.clientes,
    permisos: PermisosDe.clientes,
  ),
  EntradaDelMenu(
    titulo: 'Mapa de clientes',
    descripcion: 'Tus clientes en el mapa, con el color de su estado.',
    icono: Icons.map_outlined,
    ruta: Rutas.mapaDeClientes,
    permisos: PermisosDe.mapaClientes,
  ),
  EntradaDelMenu(
    titulo: 'Clientes por visitar',
    descripcion: 'Los clientes a los que ya les toca visita, del más atrasado al más reciente.',
    icono: Icons.event_repeat_outlined,
    ruta: Rutas.porVisitar,
    permisos: PermisosDe.porVisitar,
  ),
  EntradaDelMenu(
    titulo: 'Hojas de pedido',
    descripcion: 'Tus ventas: registrarlas, confirmarlas y verlas.',
    icono: Icons.receipt_long_outlined,
    ruta: Rutas.hojasPedido,
    permisos: PermisosDe.hojasPedido,
  ),
  EntradaDelMenu(
    titulo: 'Cobranzas',
    descripcion: 'Lo que deben tus clientes, la mora y los pagos.',
    icono: Icons.request_quote_outlined,
    ruta: Rutas.cobranzas,
    permisos: PermisosDe.cobranzas,
  ),
  EntradaDelMenu(
    titulo: 'Eventos',
    descripcion: 'Lo que pasa en la calle, con sus fotos y su ubicación.',
    icono: Icons.report_outlined,
    ruta: Rutas.eventos,
    permisos: PermisosDe.eventos,
  ),
  EntradaDelMenu(
    titulo: 'Productos',
    descripcion: 'El catálogo, con el precio y el stock de cada producto.',
    icono: Icons.category_outlined,
    ruta: Rutas.productos,
    permisos: PermisosDe.productos,
  ),
  // "Cambiar contraseña" va dentro de Mi perfil, como en el panel (colaboración 7.4.31).
  const EntradaDelMenu(
    titulo: 'Mi perfil',
    descripcion: 'Tu nombre, tu correo y tu contraseña.',
    icono: Icons.person_outline,
    ruta: Rutas.perfil,
  ),
];

/// El menú principal: quién entró y las opciones que sus permisos abren.
class MenuScreen extends ConsumerStatefulWidget {
  const MenuScreen({super.key, this.entradas});

  /// Las del menú: las pruebas pasan otras.
  final List<EntradaDelMenu>? entradas;

  @override
  ConsumerState<MenuScreen> createState() => _MenuScreenState();
}

class _MenuScreenState extends ConsumerState<MenuScreen> {
  var _cerrando = false;

  // Deslizar hacia abajo trae la sesión de nuevo: así un cambio de permisos se ve sin volver a entrar.
  Future<void> _refrescar() async {
    try {
      await ref.read(sesionProvider.notifier).refrescar();
    } on ErrorDeApi catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e.mensaje),
          action: SnackBarAction(label: 'Reintentar', onPressed: _refrescar),
        ),
      );
    }
  }

  // Con la sesión cerrada, el router lleva al inicio de sesión.
  Future<void> _cerrarSesion() async {
    setState(() => _cerrando = true);
    await ref.read(sesionProvider.notifier).cerrar();
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(sesionProvider);
    if (estado is! ConSesion) return const SizedBox.shrink();
    final sesion = estado.sesion;
    final visibles = (widget.entradas ?? entradasDelMenu).where((e) => e.permisos.isEmpty || sesion.tiene(e.permisos));

    return Scaffold(
      appBar: AppBar(title: const Text('Control de Gestión')),
      body: RefreshIndicator(
        onRefresh: _refrescar,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            _Usuario(sesion),
            const SizedBox(height: 16),
            for (final entrada in visibles) ...[
              Card(
                margin: EdgeInsets.zero,
                clipBehavior: Clip.antiAlias,
                child: ListTile(
                  contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  leading: Icon(entrada.icono, color: marca600),
                  title: Text(entrada.titulo, style: const TextStyle(fontWeight: FontWeight.w600)),
                  subtitle: Text(entrada.descripcion),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => context.go(entrada.ruta),
                ),
              ),
              const SizedBox(height: 12),
            ],
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _cerrando ? null : _cerrarSesion,
              icon: _cerrando
                  ? const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(Icons.logout),
              label: Text(_cerrando ? 'Cerrando sesión…' : 'Cerrar sesión'),
            ),
          ],
        ),
      ),
    );
  }
}

class _Usuario extends StatelessWidget {
  const _Usuario(this.sesion);

  final Sesion sesion;

  @override
  Widget build(BuildContext context) {
    final iniciales = sesion.nombreCompleto
        .split(' ')
        .where((p) => p.isNotEmpty)
        .take(2)
        .map((p) => p[0].toUpperCase())
        .join();
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            ExcludeSemantics(
              child: CircleAvatar(
                radius: 24,
                backgroundColor: const Color(0xFFDCE8F5),
                foregroundColor: const Color(0xFF1C3A66),
                child: Text(iniciales, style: const TextStyle(fontWeight: FontWeight.w600)),
              ),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Semantics(
                    header: true,
                    container: true,
                    child: Text(sesion.nombreCompleto, style: Theme.of(context).textTheme.titleMedium),
                  ),
                  Text(sesion.rol ?? 'Sin rol', style: const TextStyle(color: textoSecundario)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
