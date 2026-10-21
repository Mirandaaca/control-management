import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/sesion.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';

const avisoSesionTerminada = 'Tu sesión terminó: vuelve a entrar.';

/// En qué estado está la sesión. El router decide con él a qué pantalla va la app.
sealed class EstadoDeSesion {
  const EstadoDeSesion();
}

/// Al abrir la app, mientras se revisa el token guardado.
class SesionCargando extends EstadoDeSesion {
  const SesionCargando();
}

/// Sin sesión: va al inicio de sesión. Con [aviso] cuando la API la dio por terminada.
class SinSesion extends EstadoDeSesion {
  const SinSesion({this.aviso});

  final String? aviso;
}

class ConSesion extends EstadoDeSesion {
  const ConSesion(this.sesion);

  final Sesion sesion;
}

/// Al abrir la app, la API no respondió: se muestra el error con "Reintentar". No se usan permisos guardados, porque la
/// app no trabaja sin conexión.
class SesionSinConexion extends EstadoDeSesion {
  const SesionSinConexion(this.mensaje);

  final String mensaje;
}

class SesionNotifier extends Notifier<EstadoDeSesion> {
  @override
  EstadoDeSesion build() {
    Future.microtask(restaurar);
    return const SesionCargando();
  }

  /// Al abrir la app: con un token guardado, pide la sesión a la API, así los permisos llegan al día.
  Future<void> restaurar() async {
    state = const SesionCargando();
    final repositorio = ref.read(sesionRepositoryProvider);
    if (await repositorio.tokenGuardado() == null) {
      state = const SinSesion();
      return;
    }
    try {
      state = ConSesion(await repositorio.yo());
    } on ErrorDeApi catch (e) {
      if (e.estado == 401) {
        await repositorio.olvidar();
        state = const SinSesion(aviso: avisoSesionTerminada);
      } else {
        state = SesionSinConexion(e.mensaje);
      }
    }
  }

  /// Los errores ([ErrorDeApi]) los muestra la pantalla de inicio de sesión.
  Future<void> iniciar(String email, String password) async {
    state = ConSesion(await ref.read(sesionRepositoryProvider).iniciar(email, password));
  }

  /// Trae la sesión de nuevo: el nombre o los permisos pudieron cambiar.
  Future<void> refrescar() async {
    final sesion = await ref.read(sesionRepositoryProvider).yo();
    if (state is ConSesion) state = ConSesion(sesion);
  }

  Future<void> cerrar() async {
    await ref.read(sesionRepositoryProvider).cerrar();
    state = const SinSesion();
  }

  /// La API respondió 401 a una petición con la sesión actual (el interceptor de dio).
  void terminada() {
    unawaited(ref.read(sesionRepositoryProvider).olvidar());
    state = const SinSesion(aviso: avisoSesionTerminada);
  }
}
