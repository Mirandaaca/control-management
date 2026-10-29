import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../proveedores.dart';
import '../core/widgets/aviso.dart';
import 'seguimiento_de_ruta.dart';

/// Sin GPS con la ruta en curso: la oficina no la sigue, las visitas no se marcan y la navegación no avanza. Lo usan la
/// ruta del día y la navegación, que escuchan el mismo GPS que el seguimiento.
Widget? avisoDelGps(WidgetRef ref, ProblemaDelGps? problema, int rutaId) {
  final gps = ref.read(servicioDeUbicacionProvider);
  final seguimiento = ref.read(seguimientoDeRutaProvider.notifier);
  const paraQue = 'para que la oficina siga tu ruta y para marcar tus visitas.';
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
      accion: AccionDeAviso('Dar permiso', () => seguimiento.empezar(rutaId, pedirPermiso: true)),
    ),
    ProblemaDelGps.sinPermisoParaSiempre => Aviso(
      'Dale permiso desde los ajustes de la app $paraQue',
      tipo: TipoDeAviso.aviso,
      titulo: 'La app no tiene permiso para usar tu ubicación.',
      accion: AccionDeAviso('Abrir los ajustes', gps.abrirAjustesDeLaApp),
    ),
  };
}
