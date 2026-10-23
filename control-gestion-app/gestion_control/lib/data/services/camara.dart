import 'dart:typed_data';

import 'package:image_picker/image_picker.dart';

/// Una foto recién tomada o elegida, que todavía no se subió.
class FotoTomada {
  const FotoTomada({required this.bytes, required this.nombre, required this.tomadaUtc, this.deLaGaleria = false});

  final Uint8List bytes;
  final String nombre;

  /// Cuándo se tomó. En una de la galería, cuándo se eligió: la hora de la toma no se sabe con certeza.
  final DateTime tomadaUtc;

  /// Elegida de la galería del celular (7.3.14, ext. 2a): se sube sin el lugar ni la hora de la toma.
  final bool deLaGaleria;
}

/// La cámara y la galería del celular: las pruebas usan una falsa.
abstract interface class Camara {
  /// Null si el promotor cerró la cámara sin tomar la foto.
  Future<FotoTomada?> tomarFoto();

  /// Null si el promotor cerró la galería sin elegir una foto.
  Future<FotoTomada?> elegirDeLaGaleria();
}

/// Abre la cámara del celular (no una propia): no pide el permiso de cámara y gasta menos memoria. La galería es el
/// selector de fotos de Android, que tampoco pide permiso. En la web, las dos abren el selector de archivos, que es como
/// se verifica en Chrome.
class CamaraConImagePicker implements Camara {
  final _selector = ImagePicker();

  // Comprimida al tomarla: de unos 5 MB pasa a menos de 1 MB, sube rápido con datos móviles y se sigue leyendo.
  static const _anchoMaximo = 1600.0;
  static const _calidad = 80;

  @override
  Future<FotoTomada?> tomarFoto() async {
    final foto = await _selector.pickImage(source: ImageSource.camera, maxWidth: _anchoMaximo, imageQuality: _calidad);
    if (foto == null) return null;
    return FotoTomada(bytes: await foto.readAsBytes(), nombre: foto.name, tomadaUtc: DateTime.now().toUtc());
  }

  @override
  Future<FotoTomada?> elegirDeLaGaleria() async {
    final foto = await _selector.pickImage(source: ImageSource.gallery, maxWidth: _anchoMaximo, imageQuality: _calidad);
    if (foto == null) return null;
    return FotoTomada(
      bytes: await foto.readAsBytes(),
      nombre: foto.name,
      tomadaUtc: DateTime.now().toUtc(),
      deLaGaleria: true,
    );
  }
}
