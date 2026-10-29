import 'package:flutter/foundation.dart';
import 'package:flutter_tts/flutter_tts.dart';

/// La voz que lee las indicaciones de la navegación (tarea 25). Las pruebas usan una falsa.
abstract interface class Voz {
  /// Elige una voz en español. False si el celular no tiene ninguna: la navegación sigue con el texto.
  Future<bool> preparar();

  /// Dice [texto], cortando lo que estaba diciendo: la indicación nueva es la que vale.
  Future<void> decir(String texto);

  Future<void> callar();
}

class VozDelCelular implements Voz {
  final _tts = FlutterTts();
  bool? _preparada;

  // Primero el español de América: el de España dice "gire" con otra entonación y pronuncia distinto las calles.
  static const _idiomas = ['es-US', 'es-419', 'es-MX', 'es-ES'];

  @override
  Future<bool> preparar() async {
    if (_preparada case final preparada?) return preparada;
    try {
      final disponibles = [
        for (final idioma in await _tts.getLanguages as List? ?? const []) '$idioma'.replaceAll('_', '-').toLowerCase(),
      ];
      for (final idioma in _idiomas) {
        if (disponibles.contains(idioma.toLowerCase())) {
          await _tts.setLanguage(idioma);
          return _preparada = true;
        }
      }
      debugPrint('El celular no tiene voz en español. Tiene: ${disponibles.join(', ')}');
      return _preparada = false;
    } catch (e) {
      debugPrint('No se pudo preparar la voz. $e');
      return _preparada = false;
    }
  }

  @override
  Future<void> decir(String texto) async {
    try {
      await _tts.stop();
      await _tts.speak(texto);
    } catch (e) {
      debugPrint('La voz no pudo decir la indicación. $e');
    }
  }

  @override
  Future<void> callar() async {
    try {
      await _tts.stop();
    } catch (e) {
      debugPrint('No se pudo callar la voz. $e');
    }
  }
}
