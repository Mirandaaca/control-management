import 'package:flutter/foundation.dart';
import 'package:wakelock_plus/wakelock_plus.dart';

/// Mantiene la pantalla encendida mientras el promotor navega (tarea 25): con la pantalla apagada, Android pausa la app
/// y se cortan el GPS, la navegación y el seguimiento de 30 s. Las pruebas usan una falsa.
abstract interface class PantallaEncendida {
  Future<void> mantener();

  /// Vuelve al ajuste del celular.
  Future<void> soltar();
}

class PantallaConWakelock implements PantallaEncendida {
  @override
  Future<void> mantener() async {
    try {
      await WakelockPlus.enable();
    } catch (e) {
      debugPrint('No se pudo mantener la pantalla encendida. $e');
    }
  }

  @override
  Future<void> soltar() async {
    try {
      await WakelockPlus.disable();
    } catch (e) {
      debugPrint('No se pudo soltar la pantalla encendida. $e');
    }
  }
}
