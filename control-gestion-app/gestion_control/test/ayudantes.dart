import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/perfil.dart';
import 'package:gestion_control/data/models/sesion.dart';
import 'package:gestion_control/data/repositories/perfil_repository.dart';
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

const perfilDePrueba = Perfil(
  id: 'u1',
  email: 'promotor@test.local',
  nombreCompleto: 'Pablo Promotor',
  rol: 'Promotor',
  // 03:30 UTC del 20/10 son las 23:30 del 19/10 en Bolivia.
  fechaCreacion: '2026-10-20T03:30:00',
);

/// El perfil sin API: cada prueba dice qué responde y ve qué se le pidió.
class PerfilRepositoryFalso implements PerfilRepository {
  Future<Perfil> Function() alObtener = () async => perfilDePrueba;
  Future<Perfil> Function(String nombreCompleto, String email)? alEditar;
  Future<void> Function(String actual, String nueva)? alCambiar;
  var obtenciones = 0;
  final ediciones = <(String, String)>[];
  final cambios = <(String, String)>[];

  @override
  Future<Perfil> obtener() {
    obtenciones++;
    return alObtener();
  }

  @override
  Future<Perfil> editar({required String nombreCompleto, required String email}) async {
    ediciones.add((nombreCompleto, email));
    return (alEditar ??
        (n, e) async => Perfil(
          id: 'u1',
          email: e,
          nombreCompleto: n,
          rol: 'Promotor',
          fechaCreacion: perfilDePrueba.fechaCreacion,
        ))(nombreCompleto, email);
  }

  @override
  Future<void> cambiarContrasena({required String actual, required String nueva}) async {
    cambios.add((actual, nueva));
    await alCambiar?.call(actual, nueva);
  }
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

/// Como en un celular de 360 dp de ancho, con la letra del sistema al tamaño indicado (2.0 es el 200 %).
void tamanoDeCelular(WidgetTester tester, {double letra = 1, double alto = 800}) {
  tester.view.physicalSize = Size(360, alto) * 3;
  tester.view.devicePixelRatio = 3;
  tester.platformDispatcher.textScaleFactorTestValue = letra;
  addTearDown(() {
    tester.view.reset();
    tester.platformDispatcher.clearTextScaleFactorTestValue();
  });
}
