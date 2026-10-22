import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/foundation.dart';

/// Si el celular tiene una red (datos móviles o wifi). Solo se pregunta cuando una petición se quedó sin respuesta: así
/// la app distingue "No tienes conexión a internet." de "No se pudo conectar con el servidor." (8.6.12).
abstract interface class VerificadorDeConexion {
  Future<bool> hayRed();
}

class VerificadorConConnectivity implements VerificadorDeConexion {
  @override
  Future<bool> hayRed() async {
    try {
      final redes = await Connectivity().checkConnectivity();
      return redes.any((red) => red != ConnectivityResult.none);
    } catch (e) {
      // Si no se puede saber, el error queda como "No se pudo conectar con el servidor.", que también pide reintentar.
      debugPrint('No se pudo saber si el celular tiene red. $e');
      return true;
    }
  }
}
