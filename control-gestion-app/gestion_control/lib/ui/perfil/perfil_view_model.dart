import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/perfil.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';

class EstadoPerfil {
  const EstadoPerfil({
    this.perfil,
    this.errorDeCarga,
    this.reintentando = false,
    this.guardando = false,
    this.campos = const {},
    this.errorAlGuardar,
    this.guardado = false,
  });

  /// Null mientras carga o si no se pudo cargar.
  final Perfil? perfil;
  final String? errorDeCarga;
  final bool reintentando;
  final bool guardando;
  final Map<String, String> campos;
  final String? errorAlGuardar;
  final bool guardado;
}

class PerfilViewModel extends Notifier<EstadoPerfil> {
  // Errores de la API que pertenecen a un campo: se muestran junto a él.
  static const _campoDelCodigo = {'correo_duplicado': 'email'};

  @override
  EstadoPerfil build() {
    Future.microtask(cargar);
    return const EstadoPerfil();
  }

  Future<void> cargar() async {
    if (state.errorDeCarga != null) state = EstadoPerfil(errorDeCarga: state.errorDeCarga, reintentando: true);
    try {
      final perfil = await ref.read(perfilRepositoryProvider).obtener();
      if (ref.mounted) state = EstadoPerfil(perfil: perfil);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = EstadoPerfil(errorDeCarga: e.mensaje);
    }
  }

  Future<void> guardar({required String nombreCompleto, required String email}) async {
    final perfil = state.perfil;
    if (perfil == null || state.guardando) return;
    state = EstadoPerfil(perfil: perfil, guardando: true);
    try {
      final guardado = await ref
          .read(perfilRepositoryProvider)
          .editar(nombreCompleto: nombreCompleto, email: email.trim());
      if (!ref.mounted) return;
      state = EstadoPerfil(perfil: guardado, guardado: true);
      // El nombre del menú sale de la sesión.
      ref
          .read(sesionProvider.notifier)
          .refrescar()
          .catchError((Object e) => debugPrint('No se pudo actualizar la sesión después de guardar el perfil. $e'));
    } on ErrorDeApi catch (e) {
      if (!ref.mounted) return;
      final campos = e.camposCon(_campoDelCodigo);
      state = EstadoPerfil(perfil: perfil, campos: campos, errorAlGuardar: campos.isEmpty ? e.mensaje : null);
    }
  }
}

final perfilProvider = NotifierProvider.autoDispose<PerfilViewModel, EstadoPerfil>(PerfilViewModel.new);
