import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/perfil.dart';
import 'package:gestion_control/data/models/producto.dart';
import 'package:gestion_control/data/models/sesion.dart';
import 'package:gestion_control/data/repositories/perfil_repository.dart';
import 'package:gestion_control/data/repositories/productos_repository.dart';
import 'package:gestion_control/data/repositories/sesion_repository.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/data/services/ubicacion.dart';
import 'package:gestion_control/ui/core/tema.dart';
import 'package:gestion_control/ui/core/widgets/campo_de_texto.dart';
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

/// El TextField de un campo, buscado por la etiqueta que va encima de él.
Finder campo(String etiqueta) =>
    find.descendant(of: find.widgetWithText(CampoDeTexto, etiqueta), matching: find.byType(TextField));

/// El error que muestra un campo debajo, o null si no tiene.
String? errorDelCampo(WidgetTester tester, String etiqueta) =>
    (tester.widget<TextField>(campo(etiqueta)).decoration!.error as ErrorDeCampo?)?.mensaje;

/// Carga Inter, la letra de la app, para una prueba que mide textos: sin ella, cada letra es un cuadrado de 1 em.
Future<void> cargarInter() async {
  final inter = FontLoader('Inter');
  for (final peso in ['Regular', 'Medium', 'SemiBold']) {
    inter.addFont(rootBundle.load('assets/fuentes/inter/Inter-$peso.ttf'));
  }
  await inter.load();
}

/// El GPS sin celular: cada prueba dice si está activado, qué permiso hay y qué lee, y ve qué se le pidió.
class UbicacionFalsa implements ServicioDeUbicacion {
  var activado = true;
  var permisoActual = PermisoDeUbicacion.concedido;

  /// Lo que responde el usuario cuando se le pide el permiso. Null: lo deja como estaba.
  PermisoDeUbicacion? alPedir;
  final _lecturas = StreamController<Posicion>.broadcast();
  var pedidosDePermiso = 0;
  var ajustesDeUbicacion = 0;
  var ajustesDeLaApp = 0;

  void leer(double latitud, double longitud, double precision) => _lecturas.add(Posicion(latitud, longitud, precision));

  void fallar(Object error) => _lecturas.addError(error);

  @override
  Future<bool> gpsActivado() async => activado;

  @override
  Future<PermisoDeUbicacion> permiso() async => permisoActual;

  @override
  Future<PermisoDeUbicacion> pedirPermiso() async {
    pedidosDePermiso++;
    permisoActual = alPedir ?? permisoActual;
    return permisoActual;
  }

  @override
  Stream<Posicion> lecturas() => _lecturas.stream;

  @override
  Future<void> abrirAjustesDeUbicacion() async => ajustesDeUbicacion++;

  @override
  Future<void> abrirAjustesDeLaApp() async => ajustesDeLaApp++;
}

/// Las tres guías de accesibilidad de cada pantalla, sin excepciones de dibujo (como un texto que se desborda).
Future<void> cumpleLasGuias(WidgetTester tester) async {
  expect(tester.takeException(), isNull);
  await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
  await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
  await expectLater(tester, meetsGuideline(textContrastGuideline));
}

/// Los productos de datos-ejemplo.md, con su stock del 08/10/2026, y uno sin stock.
const arroz = Producto(
  id: 1,
  sku: 'ARZ-5KG',
  descripcion: 'Arroz grano largo 5 kg',
  categoria: 'Arroz',
  precio: 38,
  stock: 120,
);
const detergente = Producto(
  id: 2,
  sku: 'DET-1KG',
  descripcion: 'Detergente en polvo 1 kg',
  categoria: 'Limpieza',
  precio: 16.5,
  stock: 85,
);
const jabon = Producto(
  id: 3,
  sku: 'JAB-BAR',
  descripcion: 'Jabón en barra',
  categoria: 'Limpieza',
  precio: 6,
  stock: 0,
);

class ProductosRepositoryFalso implements ProductosRepository {
  Future<List<Producto>> Function() alListar = () async => const [arroz, detergente, jabon];
  var listados = 0;

  @override
  Future<List<Producto>> listar() {
    listados++;
    return alListar();
  }
}
