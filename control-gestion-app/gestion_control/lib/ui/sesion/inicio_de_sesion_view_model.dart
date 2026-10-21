import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';

/// Lo que muestra el inicio de sesión mientras envía o cuando algo falla.
class EstadoInicioDeSesion {
  const EstadoInicioDeSesion({this.enviando = false, this.error, this.campos = const {}});

  final bool enviando;

  /// El error de la API que no es de un campo, como "Credenciales incorrectas.".
  final String? error;

  /// Los errores de cada campo (`email`, `password`), que van debajo de él.
  final Map<String, String> campos;
}

class InicioDeSesionViewModel extends Notifier<EstadoInicioDeSesion> {
  @override
  EstadoInicioDeSesion build() => const EstadoInicioDeSesion();

  Future<void> entrar(String email, String password) async {
    if (state.enviando) return;
    final campos = {
      if (email.trim().isEmpty) 'email': 'Escribe tu correo.',
      if (password.isEmpty) 'password': 'Escribe tu contraseña.',
    };
    if (campos.isNotEmpty) {
      state = EstadoInicioDeSesion(campos: campos);
      return;
    }

    state = const EstadoInicioDeSesion(enviando: true);
    try {
      // Con la sesión iniciada, el router lleva al menú.
      await ref.read(sesionProvider.notifier).iniciar(email.trim(), password);
    } on ErrorDeApi catch (e) {
      if (!ref.mounted) return;
      state = EstadoInicioDeSesion(error: e.campos.isEmpty ? e.mensaje : null, campos: e.campos);
    }
  }
}

final inicioDeSesionProvider = NotifierProvider.autoDispose<InicioDeSesionViewModel, EstadoInicioDeSesion>(
  InicioDeSesionViewModel.new,
);
