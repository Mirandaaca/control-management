import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/services/ubicacion.dart';
import '../../proveedores.dart';
import '../core/widgets/aviso.dart';
import 'seguimiento_de_ruta.dart';

/// Sin GPS con la ruta en curso: la oficina no la sigue, las visitas no se marcan y la navegación no avanza. Lo usan la
/// ruta del día y la navegación hasta una visita, que escuchan el mismo GPS que el seguimiento.
Widget? avisoDelGps(WidgetRef ref, ProblemaDelGps? problema, int rutaId) => avisoSinGps(
  ref,
  problema,
  paraQue: 'para que la oficina siga tu ruta y para marcar tus visitas.',
  darPermiso: () => ref.read(seguimientoDeRutaProvider.notifier).empezar(rutaId, pedirPermiso: true),
);

/// Sin GPS: el aviso con la acción que lo arregla. [paraQue] completa "Actívalo …"; [darPermiso] vuelve a pedir el
/// permiso (la navegación hasta un pedido, sin ruta en curso, lo pide ella).
Widget? avisoSinGps(
  WidgetRef ref,
  ProblemaDelGps? problema, {
  required String paraQue,
  required VoidCallback darPermiso,
}) {
  final gps = ref.read(servicioDeUbicacionProvider);
  return switch (problema) {
    null => null,
    ProblemaDelGps.desactivado => Aviso(
      'Actívalo $paraQue',
      tipo: TipoDeAviso.aviso,
      titulo: 'El GPS está desactivado.',
      accion: AccionDeAviso('Activar GPS', gps.abrirAjustesDeUbicacion),
    ),
    ProblemaDelGps.sinPermiso => Aviso(
      'Dale permiso $paraQue',
      tipo: TipoDeAviso.aviso,
      titulo: 'La app no tiene permiso para usar tu ubicación.',
      accion: AccionDeAviso('Dar permiso', darPermiso),
    ),
    ProblemaDelGps.sinPermisoParaSiempre => Aviso(
      'Dale permiso desde los ajustes de la app $paraQue',
      tipo: TipoDeAviso.aviso,
      titulo: 'La app no tiene permiso para usar tu ubicación.',
      accion: AccionDeAviso('Abrir los ajustes', gps.abrirAjustesDeLaApp),
    ),
  };
}
