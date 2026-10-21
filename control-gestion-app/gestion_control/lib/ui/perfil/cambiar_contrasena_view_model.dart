import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';

class EstadoCambioDeContrasena {
  const EstadoCambioDeContrasena({this.enviando = false, this.campos = const {}, this.error, this.cambiada = false});

  final bool enviando;

  /// Los errores de cada campo (`actual`, `nueva`, `confirmar`), que van debajo de él.
  final Map<String, String> campos;
  final String? error;
  final bool cambiada;
}

class CambiarContrasenaViewModel extends Notifier<EstadoCambioDeContrasena> {
  // Errores de la API que pertenecen a un campo: se muestran junto a él.
  static const _campoDelCodigo = {'contrasena_actual_incorrecta': 'actual', 'contrasena_invalida': 'nueva'};

  @override
  EstadoCambioDeContrasena build() => const EstadoCambioDeContrasena();

  Future<void> cambiar({required String actual, required String nueva, required String confirmar}) async {
    if (state.enviando) return;
    // Las reglas de la nueva (8 caracteres y un número) las valida la API, así no se repiten aquí.
    final campos = {
      if (actual.isEmpty) 'actual': 'Escribe tu contraseña actual.',
      if (nueva.isEmpty) 'nueva': 'Escribe la contraseña nueva.',
      if (nueva.isNotEmpty && nueva != confirmar) 'confirmar': 'Las dos contraseñas nuevas no coinciden.',
    };
    if (campos.isNotEmpty) {
      state = EstadoCambioDeContrasena(campos: campos);
      return;
    }

    state = const EstadoCambioDeContrasena(enviando: true);
    try {
      await ref.read(perfilRepositoryProvider).cambiarContrasena(actual: actual, nueva: nueva);
      if (ref.mounted) state = const EstadoCambioDeContrasena(cambiada: true);
    } on ErrorDeApi catch (e) {
      if (!ref.mounted) return;
      final campos = e.camposCon(_campoDelCodigo);
      state = EstadoCambioDeContrasena(campos: campos, error: campos.isEmpty ? e.mensaje : null);
    }
  }
}

final cambiarContrasenaProvider = NotifierProvider.autoDispose<CambiarContrasenaViewModel, EstadoCambioDeContrasena>(
  CambiarContrasenaViewModel.new,
);
