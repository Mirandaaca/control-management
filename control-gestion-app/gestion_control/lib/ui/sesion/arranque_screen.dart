import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../proveedores.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import 'sesion_notifier.dart';

/// Lo primero que se ve al abrir la app, mientras se revisa la sesión guardada. Si la API no responde, ofrece reintentar.
class ArranqueScreen extends ConsumerWidget {
  const ArranqueScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estado = ref.watch(sesionProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Control de Gestión')),
      body: switch (estado) {
        SesionSinConexion(:final mensaje) => ErrorConReintento(
          mensaje: mensaje,
          alReintentar: ref.read(sesionProvider.notifier).restaurar,
        ),
        _ => const Cargando('Abriendo tu sesión…'),
      },
    );
  }
}
