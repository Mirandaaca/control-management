import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/formato.dart';
import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import 'perfil_view_model.dart';

/// El perfil propio (8.3): ver el rol y la fecha de alta, y editar el nombre y el correo.
class PerfilScreen extends ConsumerStatefulWidget {
  const PerfilScreen({super.key});

  @override
  ConsumerState<PerfilScreen> createState() => _PerfilScreenState();
}

class _PerfilScreenState extends ConsumerState<PerfilScreen> {
  final _nombre = TextEditingController();
  final _correo = TextEditingController();

  @override
  void dispose() {
    _nombre.dispose();
    _correo.dispose();
    super.dispose();
  }

  void _guardar() => ref.read(perfilProvider.notifier).guardar(nombreCompleto: _nombre.text, email: _correo.text);

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(perfilProvider);
    // Los campos se llenan con lo que devolvió la API al cargar y al guardar, no en cada cambio de estado.
    ref.listen(perfilProvider.select((e) => e.perfil), (anterior, perfil) {
      if (perfil == null || identical(perfil, anterior)) return;
      _nombre.text = perfil.nombreCompleto;
      _correo.text = perfil.email;
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Mi perfil')),
      body: switch (estado) {
        EstadoPerfil(errorDeCarga: final error?) => ErrorConReintento(
          mensaje: error,
          reintentando: estado.reintentando,
          alReintentar: ref.read(perfilProvider.notifier).cargar,
        ),
        EstadoPerfil(perfil: null) => const Cargando('Cargando tu perfil…'),
        EstadoPerfil(perfil: final perfil?) => ListView(
          padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.paddingOf(context).bottom + 24),
          children: [
            const Text('Tu nombre y el correo con el que inicias sesión.', style: TextStyle(color: textoSecundario)),
            const SizedBox(height: 16),
            if (estado.guardado) ...[
              const Aviso('Se guardaron tus datos.', tipo: TipoDeAviso.exito),
              const SizedBox(height: 16),
            ],
            if (estado.errorAlGuardar != null) ...[Aviso(estado.errorAlGuardar!), const SizedBox(height: 16)],
            CampoDeTexto(
              controller: _nombre,
              etiqueta: 'Nombre completo',
              textCapitalization: TextCapitalization.words,
              autofillHints: const [AutofillHints.name],
              error: estado.campos['nombreCompleto'],
            ),
            const SizedBox(height: 12),
            CampoDeTexto(
              controller: _correo,
              etiqueta: 'Correo',
              ayuda: 'Es también tu usuario para iniciar sesión.',
              keyboardType: TextInputType.emailAddress,
              autofillHints: const [AutofillHints.email],
              error: estado.campos['email'],
              textInputAction: TextInputAction.done,
              onSubmitted: (_) => _guardar(),
            ),
            const SizedBox(height: 16),
            _Dato(titulo: 'Rol', valor: perfil.rol ?? 'Sin rol'),
            const SizedBox(height: 12),
            _Dato(titulo: 'Fecha de alta', valor: formatearFecha(perfil.fechaCreacion)),
            const SizedBox(height: 24),
            FilledButton(
              onPressed: estado.guardando ? null : _guardar,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  if (estado.guardando) ...[
                    const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2)),
                    const SizedBox(width: 12),
                  ],
                  Flexible(child: Text(estado.guardando ? 'Guardando…' : 'Guardar cambios')),
                ],
              ),
            ),
          ],
        ),
      },
    );
  }
}

class _Dato extends StatelessWidget {
  const _Dato({required this.titulo, required this.valor});

  final String titulo;
  final String valor;

  @override
  Widget build(BuildContext context) {
    return MergeSemantics(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(titulo, style: Theme.of(context).textTheme.labelMedium?.copyWith(color: const Color(0xFF334155))),
          Text(valor),
        ],
      ),
    );
  }
}
