import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/campo_contrasena.dart';
import 'cambiar_contrasena_view_model.dart';

/// Cambiar la contraseña (8.1): pide la actual. Al cambiarla, la API cierra las demás sesiones; esta sigue abierta.
class CambiarContrasenaScreen extends ConsumerStatefulWidget {
  const CambiarContrasenaScreen({super.key});

  @override
  ConsumerState<CambiarContrasenaScreen> createState() => _CambiarContrasenaScreenState();
}

class _CambiarContrasenaScreenState extends ConsumerState<CambiarContrasenaScreen> {
  final _actual = TextEditingController();
  final _nueva = TextEditingController();
  final _confirmar = TextEditingController();

  @override
  void dispose() {
    _actual.dispose();
    _nueva.dispose();
    _confirmar.dispose();
    super.dispose();
  }

  void _cambiar() => ref
      .read(cambiarContrasenaProvider.notifier)
      .cambiar(actual: _actual.text, nueva: _nueva.text, confirmar: _confirmar.text);

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(cambiarContrasenaProvider);
    // Ya cambiada, los campos quedan vacíos: las contraseñas no se dejan a la vista.
    ref.listen(cambiarContrasenaProvider.select((e) => e.cambiada), (_, cambiada) {
      if (!cambiada) return;
      for (final campo in [_actual, _nueva, _confirmar]) {
        campo.clear();
      }
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Cambiar contraseña')),
      body: AutofillGroup(
        child: ListView(
          padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.paddingOf(context).bottom + 24),
          children: [
            const Text('Para cambiarla necesitas la actual.', style: TextStyle(color: textoSecundario)),
            const SizedBox(height: 16),
            if (estado.cambiada) ...[
              const Aviso('Tu contraseña cambió. Se cerraron tus otras sesiones abiertas.', tipo: TipoDeAviso.exito),
              const SizedBox(height: 16),
            ],
            if (estado.error != null) ...[Aviso(estado.error!), const SizedBox(height: 16)],
            CampoContrasena(controller: _actual, etiqueta: 'Contraseña actual', error: estado.campos['actual']),
            const SizedBox(height: 12),
            CampoContrasena(
              controller: _nueva,
              etiqueta: 'Contraseña nueva',
              ayuda: '8 caracteres o más, con al menos un número.',
              error: estado.campos['nueva'],
              autofillHints: const [AutofillHints.newPassword],
            ),
            const SizedBox(height: 12),
            CampoContrasena(
              controller: _confirmar,
              etiqueta: 'Repite la contraseña nueva',
              error: estado.campos['confirmar'],
              autofillHints: const [AutofillHints.newPassword],
              textInputAction: TextInputAction.done,
              onSubmitted: (_) => _cambiar(),
            ),
            const SizedBox(height: 24),
            FilledButton(
              onPressed: estado.enviando ? null : _cambiar,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  if (estado.enviando) ...[
                    const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2)),
                    const SizedBox(width: 12),
                  ],
                  Flexible(child: Text(estado.enviando ? 'Cambiando…' : 'Cambiar contraseña')),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
