import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../models/camino.dart';

/// Lo que la app recuerda de una vez para otra en el celular (tarea 25): cómo fue el promotor la última vez y si
/// silenció la voz de la navegación. No son secretos: el token sigue en `AlmacenDeSesion`.
abstract interface class Preferencias {
  Future<MedioDeTransporte?> medio();

  Future<void> guardarMedio(MedioDeTransporte medio);

  Future<bool> vozSilenciada();

  Future<void> guardarVozSilenciada(bool silenciada);
}

class PreferenciasDelCelular implements Preferencias {
  final _guardadas = SharedPreferencesAsync();

  // Si no se pueden leer, la app sigue con lo de siempre: en auto y con voz.
  @override
  Future<MedioDeTransporte?> medio() async {
    try {
      return MedioDeTransporte.desde(await _guardadas.getString('navegacion.medio'));
    } catch (e) {
      debugPrint('No se pudo leer cómo va el promotor. $e');
      return null;
    }
  }

  @override
  Future<void> guardarMedio(MedioDeTransporte medio) async {
    try {
      await _guardadas.setString('navegacion.medio', medio.valor);
    } catch (e) {
      debugPrint('No se pudo guardar cómo va el promotor. $e');
    }
  }

  @override
  Future<bool> vozSilenciada() async {
    try {
      return await _guardadas.getBool('navegacion.vozSilenciada') ?? false;
    } catch (e) {
      debugPrint('No se pudo leer si la voz está silenciada. $e');
      return false;
    }
  }

  @override
  Future<void> guardarVozSilenciada(bool silenciada) async {
    try {
      await _guardadas.setBool('navegacion.vozSilenciada', silenciada);
    } catch (e) {
      debugPrint('No se pudo guardar si la voz está silenciada. $e');
    }
  }
}
