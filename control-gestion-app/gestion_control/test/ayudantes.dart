import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:go_router/go_router.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gestion_control/data/models/cliente.dart';
import 'package:gestion_control/data/models/cobranza.dart';
import 'package:gestion_control/data/models/hoja_pedido.dart';
import 'package:gestion_control/data/models/pagina.dart';
import 'package:gestion_control/data/models/perfil.dart';
import 'package:gestion_control/data/models/producto.dart';
import 'package:gestion_control/data/models/zona.dart';
import 'package:gestion_control/data/models/sesion.dart';
import 'package:gestion_control/data/repositories/clientes_repository.dart';
import 'package:gestion_control/data/repositories/cobranzas_repository.dart';
import 'package:gestion_control/data/repositories/hojas_pedido_repository.dart';
import 'package:gestion_control/data/repositories/perfil_repository.dart';
import 'package:gestion_control/data/repositories/productos_repository.dart';
import 'package:gestion_control/data/repositories/sesion_repository.dart';
import 'package:gestion_control/data/repositories/zonas_repository.dart';
import 'package:gestion_control/data/services/camara.dart';
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

  /// Si no es null, el GPS falla así al empezar a buscar.
  Object? fallaAlEmpezar;

  @override
  Future<bool> gpsActivado() async {
    if (fallaAlEmpezar != null) throw fallaAlEmpezar!;
    return activado;
  }

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
  final excepcion = tester.takeException();
  if (excepcion != null) debugPrint('EXCEPCION: $excepcion');
  expect(excepcion, isNull);
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

/// Una pantalla dentro de un router de prueba: arranca en [inicio] y conoce [rutas], que pueden ser pantallas de mentira
/// para ver a dónde navega. Devuelve el router para leer dónde quedó.
Future<GoRouter> abrirConRouter(
  WidgetTester tester, {
  required List<RouteBase> rutas,
  String inicio = '/',
  List<Override> overrides = const [],
  double letra = 1,
  double alto = 800,
}) async {
  tamanoDeCelular(tester, letra: letra, alto: alto);
  final router = GoRouter(initialLocation: inicio, routes: rutas);
  addTearDown(router.dispose);
  await tester.pumpWidget(
    ProviderScope(
      overrides: overrides,
      child: MaterialApp.router(
        theme: tema,
        locale: const Locale('es', 'BO'),
        supportedLocales: const [Locale('es', 'BO')],
        localizationsDelegates: GlobalMaterialLocalizations.delegates,
        routerConfig: router,
      ),
    ),
  );
  await tester.pump();
  return router;
}

/// Una pantalla de mentira que muestra a dónde se navegó y con qué.
GoRoute destino(String ruta) => GoRoute(
  path: ruta,
  builder: (context, state) => Scaffold(body: Text('En ${state.uri} con ${state.extra}')),
);

// La Zona Norte de datos-ejemplo.md, con un límite que contiene sus ubicaciones, y la Zona Sur.
const zonaNorte = Zona(
  id: 1,
  codigo: 'ZN',
  nombre: 'Zona Norte',
  promotorId: 'u1',
  limite: [
    (latitud: -17.760, longitud: -63.195),
    (latitud: -17.760, longitud: -63.165),
    (latitud: -17.790, longitud: -63.165),
    (latitud: -17.790, longitud: -63.195),
  ],
);
const zonaSur = Zona(
  id: 2,
  codigo: 'ZS',
  nombre: 'Zona Sur',
  promotorId: 'u1',
  limite: [
    (latitud: -17.800, longitud: -63.200),
    (latitud: -17.800, longitud: -63.160),
    (latitud: -17.830, longitud: -63.160),
    (latitud: -17.830, longitud: -63.200),
  ],
);
const zonaDeOtro = Zona(id: 3, codigo: 'ZE', nombre: 'Zona Este', promotorId: 'otro');

const estadoActivo = EstadoCliente(id: 1, nombre: 'Activo', color: '#DC2626', delSistema: true);
const estadoDisponible = EstadoCliente(id: 2, nombre: 'Disponible para re-visita', color: '#15803D', delSistema: true);
const estadoInactivo = EstadoCliente(id: 4, nombre: 'Inactivo', color: '#475569', delSistema: false);
const zonaNorteResumen = ZonaResumen(id: 1, nombre: 'Zona Norte');

const rosaResumen = ClienteResumen(
  id: 42,
  codigo: 'CLI-00042',
  nombreCompleto: 'Rosa Quispe Mamani',
  telefono: '70010042',
  direccion: 'Calle 4 N.º 120, frente al Mercado Norte',
  zona: zonaNorteResumen,
  estado: estadoActivo,
  saldoPendiente: 140,
);

const rosa = Cliente(
  id: 42,
  codigo: 'CLI-00042',
  nombreCompleto: 'Rosa Quispe Mamani',
  telefono: '70010042',
  direccion: 'Calle 4 N.º 120, frente al Mercado Norte',
  latitud: -17.783412,
  longitud: -63.182106,
  zona: zonaNorteResumen,
  estado: estadoActivo,
  saldoPendiente: 140,
  ultimaVisita: '2026-10-08T13:30:00',
  proximaVisita: '2026-10-23T13:30:00',
);

/// Lo que se le envió al repositorio de clientes.
typedef EnvioDeClientePrueba = ({DatosDeCliente datos, Posicion ubicacion, String? clave, bool confirmar});

class ClientesRepositoryFalso implements ClientesRepository {
  Future<Pagina<ClienteResumen>> Function(String buscar, int pagina) alListar = (_, _) async =>
      const Pagina(items: [rosaResumen], total: 1, numero: 1, tamano: 25);
  Future<List<ClienteOpcion>> Function(String buscar) alBuscarOpciones = (_) async => const [];
  Future<Cliente> Function(int id) alObtener = (_) async => rosa;
  Future<ClienteResumen?> Function(String telefono) alBuscarCelular = (_) async => null;
  Future<Cliente> Function(EnvioDeClientePrueba envio)? alRegistrar;
  Future<Cliente> Function(EnvioDeClientePrueba envio)? alEditar;

  final listados = <(String, int)>[];
  final busquedas = <String>[];
  final celulares = <String>[];
  final registros = <EnvioDeClientePrueba>[];
  final ediciones = <EnvioDeClientePrueba>[];

  @override
  Future<Pagina<ClienteResumen>> listar({String buscar = '', int pagina = 1}) {
    listados.add((buscar, pagina));
    return alListar(buscar, pagina);
  }

  @override
  Future<List<ClienteOpcion>> opciones(String buscar) {
    busquedas.add(buscar);
    return alBuscarOpciones(buscar);
  }

  @override
  Future<ClienteResumen?> conCelular(String telefono) {
    celulares.add(telefono);
    return alBuscarCelular(telefono);
  }

  @override
  Future<Cliente> obtener(int id) => alObtener(id);

  @override
  Future<Cliente> registrar(
    DatosDeCliente datos,
    Posicion ubicacion, {
    required String clave,
    bool confirmarTelefonoRepetido = false,
  }) {
    final envio = (datos: datos, ubicacion: ubicacion, clave: clave, confirmar: confirmarTelefonoRepetido);
    registros.add(envio);
    return (alRegistrar ?? (_) async => _guardado(datos, ubicacion, id: 92, codigo: 'CLI-00092'))(envio);
  }

  @override
  Future<Cliente> editar(int id, DatosDeCliente datos, Posicion ubicacion, {bool confirmarTelefonoRepetido = false}) {
    final envio = (datos: datos, ubicacion: ubicacion, clave: null, confirmar: confirmarTelefonoRepetido);
    ediciones.add(envio);
    return (alEditar ?? (_) async => _guardado(datos, ubicacion, id: id, codigo: 'CLI-000$id'))(envio);
  }

  Cliente _guardado(DatosDeCliente datos, Posicion ubicacion, {required int id, required String codigo}) => Cliente(
    id: id,
    codigo: codigo,
    nombreCompleto: datos.nombreCompleto,
    telefono: datos.telefono,
    direccion: datos.direccion,
    latitud: ubicacion.latitud,
    longitud: ubicacion.longitud,
    zona: zonaNorteResumen,
    estado: estadoDisponible,
    saldoPendiente: 0,
  );
}

class ZonasRepositoryFalso implements ZonasRepository {
  Future<List<Zona>> Function() alListar = () async => const [zonaNorte, zonaDeOtro];

  @override
  Future<List<Zona>> listar() => alListar();
}

/// Una sesión de promotor con esos permisos.
Sesion promotorCon(List<String> permisos) => Sesion(
  id: 'u1',
  email: 'promotor@test.local',
  nombreCompleto: 'Carlos Mendoza Flores',
  rol: 'Promotor',
  permisos: permisos.toSet(),
);

const rosaOpcion = ClienteOpcion(
  id: 42,
  codigo: 'CLI-00042',
  nombreCompleto: 'Rosa Quispe Mamani',
  direccion: 'Calle 4 N.º 120, frente al Mercado Norte',
  estado: estadoActivo,
  inactivo: false,
);

/// La HP-2026-00018 de datos-ejemplo.md: 4 arroz con Bs 1,00 de descuento c/u y 6 detergente, menos Bs 7,00 sobre el
/// total: 148 + 99 − 7 = Bs 240,00, a crédito.
HojaPedido hojaDePrueba({
  int id = 18,
  String numero = 'HP-2026-00018',
  EstadoHP estado = EstadoHP.confirmada,
  TipoVenta tipo = TipoVenta.credito,
  String promotorId = 'u1',
}) => HojaPedido(
  id: id,
  numero: numero,
  estado: estado,
  tipoVenta: tipo,
  subTotal: 247,
  descuento: 7,
  total: 240,
  fechaRegistro: '2026-10-08T13:30:00',
  latitud: -17.783412,
  longitud: -63.182106,
  precisionGps: 8,
  cliente: const Referencia(id: '42', nombre: 'Rosa Quispe Mamani'),
  promotorId: promotorId,
  cobranza: estado == EstadoHP.confirmada && tipo == TipoVenta.credito
      ? const CobranzaDeHoja(id: 12, numero: 'COB-2026-00012')
      : null,
  lineas: const [
    LineaDeHoja(
      productoId: 1,
      sku: 'ARZ-5KG',
      descripcion: 'Arroz grano largo 5 kg',
      cantidad: 4,
      precioUnitario: 38,
      descuento: 1,
      subtotal: 148,
    ),
    LineaDeHoja(
      productoId: 2,
      sku: 'DET-1KG',
      descripcion: 'Detergente en polvo 1 kg',
      cantidad: 6,
      precioUnitario: 16.5,
      descuento: 0,
      subtotal: 99,
    ),
  ],
);

HojaPedidoResumen resumenDe(HojaPedido hoja) => HojaPedidoResumen(
  id: hoja.id,
  numero: hoja.numero,
  estado: hoja.estado,
  tipoVenta: hoja.tipoVenta,
  total: hoja.total,
  fechaRegistro: hoja.fechaRegistro,
  cliente: hoja.cliente,
  promotorId: hoja.promotorId,
);

/// Lo que se le envió al repositorio de hojas de pedido.
typedef EnvioDeHojaPrueba = ({DatosDeHoja datos, Posicion ubicacion, String clave});

class HojasPedidoRepositoryFalso implements HojasPedidoRepository {
  Future<Pagina<HojaPedidoResumen>> Function(String buscar, EstadoHP? estado, String? dia, int pagina) alListar =
      (_, _, _, _) async => Pagina(items: [resumenDe(hojaDePrueba())], total: 1, numero: 1, tamano: 25);
  Future<HojaPedido> Function(int id) alObtener = (_) async => hojaDePrueba();
  Future<HojaPedido> Function(EnvioDeHojaPrueba envio) alGuardar = (_) async =>
      hojaDePrueba(id: 19, numero: 'HP-2026-00019', estado: EstadoHP.borrador);
  Future<HojaPedido> Function(EnvioDeHojaPrueba envio) alConfirmarNueva = (_) async => hojaDePrueba();
  Future<HojaPedido> Function(int id) alConfirmar = (_) async => hojaDePrueba();

  final listados = <({String buscar, EstadoHP? estado, String? dia})>[];
  final guardadas = <EnvioDeHojaPrueba>[];
  final confirmadasNuevas = <EnvioDeHojaPrueba>[];
  final confirmadas = <int>[];

  @override
  Future<Pagina<HojaPedidoResumen>> listar({String buscar = '', EstadoHP? estado, String? dia, int pagina = 1}) {
    listados.add((buscar: buscar, estado: estado, dia: dia));
    return alListar(buscar, estado, dia, pagina);
  }

  @override
  Future<HojaPedido> obtener(int id) => alObtener(id);

  @override
  Future<HojaPedido> guardar(DatosDeHoja datos, Posicion ubicacion, {required String clave}) {
    final envio = (datos: datos, ubicacion: ubicacion, clave: clave);
    guardadas.add(envio);
    return alGuardar(envio);
  }

  @override
  Future<HojaPedido> confirmarNueva(DatosDeHoja datos, Posicion ubicacion, {required String clave}) {
    final envio = (datos: datos, ubicacion: ubicacion, clave: clave);
    confirmadasNuevas.add(envio);
    return alConfirmarNueva(envio);
  }

  @override
  Future<HojaPedido> confirmar(int id) {
    confirmadas.add(id);
    return alConfirmar(id);
  }
}

/// El primer pago de la COB-2026-00012 (datos-ejemplo.md): Lucía, en el panel, Bs 100,00 con QR y su comprobante.
const pagoDeLucia = Pago(
  id: 1,
  monto: 100,
  metodo: MetodoPago.qr,
  fecha: '2026-10-29T14:00:00',
  referencia: '48213097',
  registradoPor: 'Lucía Vargas Rojas',
  anulado: false,
  comprobanteId: 7,
);

/// La COB-2026-00012 de Rosa antes del segundo pago: 240 − 100 = 140 de saldo.
Cobranza cobranzaDeRosa({double pagado = 100, List<Pago> pagos = const [pagoDeLucia], EstadoCobranza? estado}) =>
    Cobranza(
      id: 12,
      numero: 'COB-2026-00012',
      estado: estado ?? (pagado >= 240 ? EstadoCobranza.pagada : EstadoCobranza.parcial),
      montoTotal: 240,
      montoPagado: pagado,
      saldo: 240 - pagado,
      fechaEmision: '2026-10-08T13:30:00',
      fechaVencimiento: '2026-11-07T13:30:00',
      clienteNombre: 'Rosa Quispe Mamani',
      hojaPedido: 'HP-2026-00018',
      pagos: pagos,
    );

const resumenDeRosa = CobranzaResumen(
  id: 12,
  numero: 'COB-2026-00012',
  estado: EstadoCobranza.parcial,
  montoTotal: 240,
  montoPagado: 100,
  saldo: 140,
  fechaEmision: '2026-10-08T13:30:00',
  fechaVencimiento: '2026-11-07T13:30:00',
  clienteId: 42,
  clienteNombre: 'Rosa Quispe Mamani',
  hojaPedido: 'HP-2026-00018',
);

/// La COB-2026-00007 de Marta: vencida, con 14 días de mora el 29/10/2026.
const resumenDeMarta = CobranzaResumen(
  id: 7,
  numero: 'COB-2026-00007',
  estado: EstadoCobranza.vencida,
  montoTotal: 327,
  montoPagado: 0,
  saldo: 327,
  fechaEmision: '2026-09-15T13:30:00',
  fechaVencimiento: '2026-10-15T13:30:00',
  clienteId: 80,
  clienteNombre: 'Marta Rivero Salas',
  hojaPedido: 'HP-2026-00011',
  diasDeMora: 14,
);

/// Lo que se le envió al repositorio de cobranzas al registrar un pago.
typedef EnvioDePagoPrueba = ({int cobranzaId, DatosDePago datos, String clave});

class CobranzasRepositoryFalso implements CobranzasRepository {
  Future<Pagina<CobranzaResumen>> Function(FiltroDeCobranzas filtro, int? clienteId) alListar = (_, _) async =>
      const Pagina(items: [resumenDeMarta, resumenDeRosa], total: 2, numero: 1, tamano: 25);
  Future<ResumenDeCobranzas> Function(int? clienteId) alResumir = (_) async =>
      const ResumenDeCobranzas(porCobrar: 467, vencidas: 1);
  Future<Cobranza> Function(int id) alObtener = (_) async => cobranzaDeRosa();
  Future<PagoRegistrado> Function(EnvioDePagoPrueba envio) alRegistrarPago = (envio) async =>
      PagoRegistrado(id: 2, estado: EstadoCobranza.pagada, saldo: 140 - envio.datos.monto);
  Future<Cobranza> Function(int pagoId, FotoTomada foto)? alSubir;

  final listados = <({FiltroDeCobranzas filtro, int? clienteId, String buscar})>[];
  final pagos = <EnvioDePagoPrueba>[];
  final subidas = <({int pagoId, FotoTomada foto})>[];

  @override
  Future<Pagina<CobranzaResumen>> listar({
    FiltroDeCobranzas filtro = FiltroDeCobranzas.porCobrar,
    int? clienteId,
    String buscar = '',
    int pagina = 1,
  }) {
    listados.add((filtro: filtro, clienteId: clienteId, buscar: buscar));
    return alListar(filtro, clienteId);
  }

  @override
  Future<ResumenDeCobranzas> resumen({int? clienteId}) => alResumir(clienteId);

  @override
  Future<Cobranza> obtener(int id) => alObtener(id);

  @override
  Future<PagoRegistrado> registrarPago(int cobranzaId, DatosDePago datos, {required String clave}) {
    final envio = (cobranzaId: cobranzaId, datos: datos, clave: clave);
    pagos.add(envio);
    return alRegistrarPago(envio);
  }

  @override
  Future<Cobranza> subirComprobante(int cobranzaId, int pagoId, FotoTomada foto) {
    subidas.add((pagoId: pagoId, foto: foto));
    return (alSubir ?? (_, _) async => cobranzaDeRosa())(pagoId, foto);
  }

  @override
  Future<Uint8List> comprobante(int evidenciaId) async => fotoDePrueba.bytes;
}

/// Un PNG de 1 × 1, para que Image.memory tenga algo que dibujar.
final fotoDePrueba = FotoTomada(
  bytes: Uint8List.fromList(const [
    0x89,
    0x50,
    0x4E,
    0x47,
    0x0D,
    0x0A,
    0x1A,
    0x0A,
    0x00,
    0x00,
    0x00,
    0x0D,
    0x49,
    0x48,
    0x44,
    0x52,
    0x00,
    0x00,
    0x00,
    0x01,
    0x00,
    0x00,
    0x00,
    0x01,
    0x08,
    0x06,
    0x00,
    0x00,
    0x00,
    0x1F,
    0x15,
    0xC4,
    0x89,
    0x00,
    0x00,
    0x00,
    0x0D,
    0x49,
    0x44,
    0x41,
    0x54,
    0x78,
    0x9C,
    0x63,
    0xF8,
    0xCF,
    0xC0,
    0xF0,
    0x1F,
    0x00,
    0x05,
    0x00,
    0x01,
    0xFF,
    0x89,
    0x99,
    0x3D,
    0x1D,
    0x00,
    0x00,
    0x00,
    0x00,
    0x49,
    0x45,
    0x4E,
    0x44,
    0xAE,
    0x42,
    0x60,
    0x82,
  ]),
  nombre: 'comprobante.png',
  // 13:05 UTC son las 09:05 de Bolivia, la hora del 8.6.7.
  tomadaUtc: DateTime.utc(2026, 10, 30, 13, 5),
);

/// La cámara sin celular: cada prueba dice qué foto devuelve (null: se cerró sin tomarla).
class CamaraFalsa implements Camara {
  FotoTomada? siguiente = fotoDePrueba;
  var tomas = 0;

  @override
  Future<FotoTomada?> tomarFoto() async {
    tomas++;
    return siguiente;
  }
}
