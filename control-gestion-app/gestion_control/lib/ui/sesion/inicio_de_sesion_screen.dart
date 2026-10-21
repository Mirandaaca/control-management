import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../proveedores.dart';
import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/campo_contrasena.dart';
import 'franja_del_territorio.dart';
import 'inicio_de_sesion_view_model.dart';
import 'sesion_notifier.dart';

/// El inicio de sesión (8.1): correo y contraseña, con el error de la API en un aviso.
class InicioDeSesionScreen extends ConsumerStatefulWidget {
  const InicioDeSesionScreen({super.key});

  @override
  ConsumerState<InicioDeSesionScreen> createState() => _InicioDeSesionScreenState();
}

class _InicioDeSesionScreenState extends ConsumerState<InicioDeSesionScreen> {
  final _correo = TextEditingController();
  final _contrasena = TextEditingController();

  @override
  void dispose() {
    _correo.dispose();
    _contrasena.dispose();
    super.dispose();
  }

  void _entrar() => ref.read(inicioDeSesionProvider.notifier).entrar(_correo.text, _contrasena.text);

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(inicioDeSesionProvider);
    final aviso = switch (ref.watch(sesionProvider)) {
      SinSesion(:final aviso) => aviso,
      _ => null,
    };
    // Así Android ofrece guardar la contraseña.
    ref.listen(sesionProvider, (_, sesion) {
      if (sesion is ConSesion) TextInput.finishAutofillContext();
    });
    final textos = Theme.of(context).textTheme;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light,
      child: Scaffold(
        body: ListView(
          padding: EdgeInsets.only(bottom: MediaQuery.paddingOf(context).bottom + 24),
          children: [
            const FranjaDelTerritorio(),
            Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 480),
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 24, 16, 0),
                  child: AutofillGroup(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Semantics(header: true, child: Text('Inicia sesión', style: textos.headlineSmall)),
                        const SizedBox(height: 4),
                        const Text(
                          'Con el correo y la contraseña que te dio el administrador.',
                          style: TextStyle(color: textoSecundario),
                        ),
                        const SizedBox(height: 24),
                        if (aviso != null) ...[Aviso(aviso, tipo: TipoDeAviso.aviso), const SizedBox(height: 16)],
                        TextField(
                          controller: _correo,
                          keyboardType: TextInputType.emailAddress,
                          autocorrect: false,
                          enableSuggestions: false,
                          autofillHints: const [AutofillHints.email, AutofillHints.username],
                          textInputAction: TextInputAction.next,
                          decoration: InputDecoration(
                            labelText: 'Correo electrónico',
                            hintText: 'nombre@empresa.com',
                            errorText: estado.campos['email'],
                            errorMaxLines: 3,
                          ),
                        ),
                        const SizedBox(height: 12),
                        CampoContrasena(
                          controller: _contrasena,
                          etiqueta: 'Contraseña',
                          error: estado.campos['password'],
                          textInputAction: TextInputAction.done,
                          onSubmitted: (_) => _entrar(),
                        ),
                        if (estado.error != null) ...[const SizedBox(height: 16), Aviso(estado.error!)],
                        const SizedBox(height: 24),
                        FilledButton(
                          onPressed: estado.enviando ? null : _entrar,
                          child: Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              if (estado.enviando) ...[
                                const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2)),
                                const SizedBox(width: 12),
                              ],
                              Flexible(child: Text(estado.enviando ? 'Ingresando…' : 'Ingresar')),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
