import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../proveedores.dart';
import '../core/tema.dart';

/// Un evento tiene hasta 5 fotos (7.3.14, ext. 2c), como valida la API.
const maximoDeFotos = 5;

/// La foto de un evento ya subida, para su miniatura y el visor.
final fotoDelEventoProvider = FutureProvider.autoDispose.family<Uint8List, int>(
  (ref, evidenciaId) => ref.read(eventosRepositoryProvider).archivo(evidenciaId),
);

/// "Fotos (opcional) · 2 de 5" (8.6.8): el título y cuántas hay.
class TituloDeFotos extends StatelessWidget {
  const TituloDeFotos({super.key, required this.titulo, required this.cantidad});

  final String titulo;
  final int cantidad;

  @override
  Widget build(BuildContext context) {
    return MergeSemantics(
      child: Wrap(
        spacing: 6,
        crossAxisAlignment: WrapCrossAlignment.end,
        children: [
          Text(titulo, style: Theme.of(context).textTheme.labelMedium?.copyWith(color: const Color(0xFF334155))),
          Text('· $cantidad de $maximoDeFotos', style: const TextStyle(color: textoSecundario)),
        ],
      ),
    );
  }
}

/// Las miniaturas en dos columnas, como en el 8.6.8.
class GrillaDeFotos extends StatelessWidget {
  const GrillaDeFotos({super.key, required this.fotos});

  final List<Widget> fotos;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, limites) {
        final ancho = (limites.maxWidth - 12) / 2;
        return Wrap(
          spacing: 12,
          runSpacing: 12,
          children: [for (final foto in fotos) SizedBox(width: ancho, child: foto)],
        );
      },
    );
  }
}

/// Una miniatura con su fecha debajo (7.3.14, paso 1.2). [imagen] es la foto; [alQuitar], la X de 48 dp de la esquina;
/// [alTocar] la abre a pantalla completa.
class MiniaturaDeFoto extends StatelessWidget {
  const MiniaturaDeFoto({
    super.key,
    required this.imagen,
    required this.fecha,
    required this.etiqueta,
    this.alQuitar,
    this.textoQuitar,
    this.alTocar,
  });

  final Widget imagen;
  final String fecha;

  /// Lo que dice el lector de pantalla de la foto ("Foto 1").
  final String etiqueta;
  final VoidCallback? alQuitar;
  final String? textoQuitar;
  final VoidCallback? alTocar;

  @override
  Widget build(BuildContext context) {
    final foto = ClipRRect(
      borderRadius: BorderRadius.circular(8),
      child: AspectRatio(
        aspectRatio: 4 / 3,
        child: DecoratedBox(
          decoration: BoxDecoration(
            color: const Color(0xFFE2E8F0),
            border: Border.all(color: const Color(0xFFE2E8F0)),
          ),
          child: imagen,
        ),
      ),
    );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Stack(
          children: [
            Semantics(
              image: true,
              button: alTocar != null,
              label: '$etiqueta, $fecha',
              child: alTocar == null
                  ? foto
                  : InkWell(onTap: alTocar, borderRadius: BorderRadius.circular(8), child: foto),
            ),
            if (alQuitar != null)
              // Quitarla se toca en 48 dp, aunque el círculo mida 32 (8.6.8).
              Positioned(
                top: 0,
                right: 0,
                child: IconButton(
                  onPressed: alQuitar,
                  tooltip: textoQuitar,
                  iconSize: 20,
                  style: IconButton.styleFrom(
                    fixedSize: const Size(48, 48),
                    foregroundColor: Colors.white,
                    backgroundColor: const Color(0xB30F172A),
                    shape: const CircleBorder(),
                    padding: const EdgeInsets.all(14),
                  ),
                  icon: const Icon(Icons.close),
                ),
              ),
          ],
        ),
        const SizedBox(height: 4),
        ExcludeSemantics(
          child: Text(fecha, style: const TextStyle(color: textoSecundario)),
        ),
      ],
    );
  }
}

/// La imagen de una foto ya subida: la trae de la API.
class ImagenDeFotoSubida extends ConsumerWidget {
  const ImagenDeFotoSubida(this.evidenciaId, {super.key});

  final int evidenciaId;

  @override
  Widget build(BuildContext context, WidgetRef ref) => switch (ref.watch(fotoDelEventoProvider(evidenciaId))) {
    AsyncData(:final value) => Image.memory(value, fit: BoxFit.cover, errorBuilder: (_, _, _) => const _SinImagen()),
    AsyncError() => const _SinImagen(),
    _ => const Center(child: SizedBox.square(dimension: 24, child: CircularProgressIndicator(strokeWidth: 2))),
  };
}

class _SinImagen extends StatelessWidget {
  const _SinImagen();

  @override
  Widget build(BuildContext context) => const Center(child: Icon(Icons.broken_image_outlined, color: textoSecundario));
}

/// "Tomar foto" y "Elegir de la galería" (7.3.14, paso 2 y ext. 2a), uno debajo del otro, como en el 8.6.8.
class BotonesDeFoto extends StatelessWidget {
  const BotonesDeFoto({super.key, required this.alTomar, required this.alElegir});

  final VoidCallback? alTomar;
  final VoidCallback? alElegir;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        OutlinedButton.icon(onPressed: alTomar, icon: const Icon(Icons.camera_alt), label: const Text('Tomar foto')),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: alElegir,
          icon: const Icon(Icons.photo_library),
          label: const Text('Elegir de la galería'),
        ),
      ],
    );
  }
}
