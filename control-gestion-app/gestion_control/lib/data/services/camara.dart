import 'dart:typed_data';

import 'package:image_picker/image_picker.dart';

/// Una foto recién tomada, que todavía no se subió.
class FotoTomada {
  const FotoTomada({required this.bytes, required this.nombre, required this.tomadaUtc});

  final Uint8List bytes;
  final String nombre;
  final DateTime tomadaUtc;
}

/// La cámara del celular: las pruebas usan una falsa.
abstract interface class Camara {
  /// Null si el promotor cerró la cámara sin tomar la foto.
  Future<FotoTomada?> tomarFoto();
}

/// Abre la cámara del celular (no una propia): no pide el permiso de cámara y gasta menos memoria. En la web abre el
/// selector de archivos, que es como se verifica en Chrome.
class CamaraConImagePicker implements Camara {
  final _selector = ImagePicker();

  @override
  Future<FotoTomada?> tomarFoto() async {
    // Comprimida al tomarla: de unos 5 MB pasa a menos de 1 MB, sube rápido con datos móviles y se sigue leyendo.
    final foto = await _selector.pickImage(source: ImageSource.camera, maxWidth: 1600, imageQuality: 80);
    if (foto == null) return null;
    return FotoTomada(bytes: await foto.readAsBytes(), nombre: foto.name, tomadaUtc: DateTime.now().toUtc());
  }
}
