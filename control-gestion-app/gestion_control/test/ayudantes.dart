import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/sesion.dart';
import 'package:gestion_control/data/repositories/sesion_repository.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/ui/core/tema.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';

const sesionDePrueba = Sesion(
  id: 'u1',
  email: 'promotor@test.local',
  nombreCompleto: 'Pablo Promotor',
  rol: 'Promotor',
  permisos: <String>{},
);

const sinConexion = ErrorDeApi(mensaje: 'No se pudo conectar con el servidor.', sinConexion: true);

/// La sesión sin API: cada prueba dice qué responde y ve qué se le pidió.
class SesionRepositoryFalso implements SesionRepository {
  SesionRepositoryFalso({this.token, Future<Sesion> Function()? alPedirYo, this.alIniciar})
    : alPedirYo = alPedirYo ?? (() async => sesionDePrueba);

  String? token;
  Future<Sesion> Function() alPedirYo;
  Future<Sesion> Function(String email, String password)? alIniciar;
  final inicios = <(String, String)>[];
  var pedidosDeYo = 0;
  var cierres = 0;

  @override
  Future<String?> tokenGuardado() async => token;

  @override
  Future<Sesion> iniciar(String email, String password) async {
    inicios.add((email, password));
    final sesion = await (alIniciar ?? (_, _) async => sesionDePrueba)(email, password);
    token = 'token';
    return sesion;
  }

  @override
  Future<Sesion> yo() {
    pedidosDeYo++;
    return alPedirYo();
  }

  @override
  Future<void> cerrar() async {
    cierres++;
    token = null;
  }

  @override
  Future<void> olvidar() async => token = null;
}

/// La sesión en un estado fijo, para probar una pantalla sin pasar por el arranque.
class SesionFija extends SesionNotifier {
  SesionFija(this.inicial);

  final EstadoDeSesion inicial;

  @override
  EstadoDeSesion build() => inicial;
}

/// Una pantalla con el tema y los textos en español de la app.
Widget conLaApp(Widget pantalla, {List<Override> overrides = const []}) => ProviderScope(
  overrides: overrides,
  child: MaterialApp(
    theme: tema,
    locale: const Locale('es', 'BO'),
    supportedLocales: const [Locale('es', 'BO')],
    localizationsDelegates: GlobalMaterialLocalizations.delegates,
    home: pantalla,
  ),
);

/// Como en un celular de 360 × 640 dp, con la letra del sistema al tamaño indicado (2.0 es el 200 %).
void tamanoDeCelular(WidgetTester tester, {double letra = 1}) {
  tester.view.physicalSize = const Size(360, 640) * 3;
  tester.view.devicePixelRatio = 3;
  tester.platformDispatcher.textScaleFactorTestValue = letra;
  addTearDown(() {
    tester.view.reset();
    tester.platformDispatcher.clearTextScaleFactorTestValue();
  });
}
