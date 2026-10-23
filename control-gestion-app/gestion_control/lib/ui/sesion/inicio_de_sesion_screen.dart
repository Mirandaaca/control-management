import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../proveedores.dart';
import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/campo_de_texto.dart';
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
  var _verContrasena = false;

  @override
  void initState() {
    super.initState();
    ref.read(sesionRepositoryProvider).despertar();
  }

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
                        Semantics(
                          header: true,
                          container: true,
                          child: Text('Inicia sesión', style: textos.headlineSmall),
                        ),
                        const SizedBox(height: 4),
                        const Text(
                          'Con el correo y la contraseña que te dio el administrador.',
                          style: TextStyle(color: textoSecundario),
                        ),
                        const SizedBox(height: 24),
                        if (aviso != null) ...[Aviso(aviso, tipo: TipoDeAviso.aviso), const SizedBox(height: 16)],
                        CampoDeTexto(
                          controller: _correo,
                          etiqueta: 'Correo electrónico',
                          ejemplo: 'nombre@empresa.com',
                          keyboardType: TextInputType.emailAddress,
                          autofillHints: const [AutofillHints.email, AutofillHints.username],
                          error: estado.campos['email'],
                        ),
                        const SizedBox(height: 12),
                        CampoDeTexto(
                          controller: _contrasena,
                          etiqueta: 'Contraseña',
                          oculto: !_verContrasena,
                          autofillHints: const [AutofillHints.password],
                          error: estado.campos['password'],
                          textInputAction: TextInputAction.done,
                          onSubmitted: (_) => _entrar(),
                        ),
                        MostrarContrasena(
                          visible: _verContrasena,
                          alCambiar: (visible) => setState(() => _verContrasena = visible),
                        ),
                        if (estado.error != null) ...[const SizedBox(height: 16), Aviso(estado.error!)],
                        const SizedBox(height: 16),
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
