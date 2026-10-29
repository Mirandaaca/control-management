import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/camino.dart';
import '../../data/models/ruta.dart';
import '../../data/services/error_de_api.dart';
import '../../data/services/ubicacion.dart';
import '../../domain/navegacion.dart';
import '../../proveedores.dart';

/// El límite de la API: un camino cada 10 s por promotor. La app no lo pasa, así no recibe un 503 de más.
const esperaEntrePedidos = Duration(seconds: 10);

/// Un desvío pide un camino nuevo como mucho cada 20 s.
const esperaEntreRecalculos = Duration(seconds: 20);

/// La visita a la que se navega, desde la ruta del día.
typedef AbrirCamino = ({int rutaId, VisitaDeRuta visita});

/// La indicación de un paso. La llegada nombra al cliente: "Llegas a Elena Gutiérrez Vaca".
String indicacionDe(Paso paso, String cliente) => paso.giro == Giro.llegada ? 'Llegas a $cliente' : paso.indicacion;

const _igual = Object();

class EstadoDeLaNavegacion {
  const EstadoDeLaNavegacion({
    this.medio = MedioDeTransporte.auto,
    this.posicion,
    this.caminos,
    this.elegido = 0,
    this.progreso,
    this.calculando = true,
    this.recalculando = false,
    this.error,
    this.errorDelRecalculo,
    this.llegaste = false,
    this.vozSilenciada = false,
    this.sinVoz = false,
  });

  final MedioDeTransporte medio;

  /// La última lectura del GPS; null mientras busca.
  final Posicion? posicion;
  final Caminos? caminos;

  /// El camino elegido en [Caminos.todos]: 0 es el principal.
  final int elegido;
  final Progreso? progreso;

  /// Esperando el primer camino, o uno nuevo porque cambió el medio.
  final bool calculando;

  /// Pidiendo un camino nuevo porque se desvió.
  final bool recalculando;

  /// No hay ningún camino: el mapa muestra al cliente y la pantalla ofrece "Reintentar".
  final ErrorDeApi? error;

  /// Un camino nuevo no llegó: se queda el anterior.
  final ErrorDeApi? errorDelRecalculo;
  final bool llegaste;
  final bool vozSilenciada;

  /// El celular no tiene voz en español: la navegación sigue con el texto.
  final bool sinVoz;

  Camino? get camino => caminos?.todos[elegido];

  EstadoDeLaNavegacion copiar({
    MedioDeTransporte? medio,
    Object? posicion = _igual,
    Object? caminos = _igual,
    int? elegido,
    Object? progreso = _igual,
    bool? calculando,
    bool? recalculando,
    Object? error = _igual,
    Object? errorDelRecalculo = _igual,
    bool? llegaste,
    bool? vozSilenciada,
    bool? sinVoz,
  }) => EstadoDeLaNavegacion(
    medio: medio ?? this.medio,
    posicion: posicion == _igual ? this.posicion : posicion as Posicion?,
    caminos: caminos == _igual ? this.caminos : caminos as Caminos?,
    elegido: elegido ?? this.elegido,
    progreso: progreso == _igual ? this.progreso : progreso as Progreso?,
    calculando: calculando ?? this.calculando,
    recalculando: recalculando ?? this.recalculando,
    error: error == _igual ? this.error : error as ErrorDeApi?,
    errorDelRecalculo: errorDelRecalculo == _igual ? this.errorDelRecalculo : errorDelRecalculo as ErrorDeApi?,
    llegaste: llegaste ?? this.llegaste,
    vozSilenciada: vozSilenciada ?? this.vozSilenciada,
    sinVoz: sinVoz ?? this.sinVoz,
  );
}

/// La navegación hasta una visita de la ruta en curso (tarea 25): pide el camino a la API, sigue al promotor con el GPS,
/// recalcula si se desvía y avisa cuando llega. Mientras está abierta, la pantalla no se apaga. No manda nada más al
/// servidor: la ubicación la sigue mandando el seguimiento de 30 s.
class NavegacionViewModel extends Notifier<EstadoDeLaNavegacion> {
  NavegacionViewModel(this.abrir);

  final AbrirCamino abrir;

  StreamSubscription<Posicion>? _lecturas;
  Timer? _esperaDelPedido;
  Timer? _esperaDelRecalculo;
  var _pidiendo = false;

  /// Hay que pedir un camino apenas se pueda: cambió el medio o se tocó "Reintentar" antes de los 10 s.
  var _pendiente = false;
  Trazado? _trazado;
  final _desvio = DetectorDeDesvio();
  var _anunciador = Anunciador.para(MedioDeTransporte.auto);

  /// Desde dónde se pidió el camino que se sigue. Quieto lejos de la calle (un estacionamiento, un mercado), el camino
  /// nuevo empieza en la calle y el promotor sigue "desviado": sin moverse, no se vuelve a pedir.
  PuntoDelCamino? _desdeDelCamino;

  /// La indicación que la voz dijo al empezar el último camino: si un camino nuevo empieza igual, no la repite.
  String? _ultimaInicial;

  PuntoDelCamino get _cliente => (latitud: abrir.visita.cliente.latitud, longitud: abrir.visita.cliente.longitud);

  @override
  EstadoDeLaNavegacion build() {
    final pantalla = ref.read(pantallaEncendidaProvider);
    final voz = ref.read(vozProvider);
    pantalla.mantener();
    ref.onDispose(() {
      _lecturas?.cancel();
      _esperaDelPedido?.cancel();
      _esperaDelRecalculo?.cancel();
      pantalla.soltar();
      voz.callar();
    });
    // Si la navegación se cerró y se abrió otra vez enseguida, el primer pedido espera lo que falte de los 10 s.
    if (ref.read(ultimoPedidoDeCaminoProvider) case final ultimo?) {
      final falta = esperaEntrePedidos - DateTime.now().difference(ultimo);
      if (falta > Duration.zero) _esperaDelPedido = Timer(falta, _alTerminarLaEspera);
    }
    Future.microtask(_empezar);
    return const EstadoDeLaNavegacion();
  }

  Future<void> _empezar() async {
    final preferencias = ref.read(preferenciasProvider);
    final (medio, silenciada, conVoz) = await (
      preferencias.medio(),
      preferencias.vozSilenciada(),
      ref.read(vozProvider).preparar(),
    ).wait;
    if (!ref.mounted) return;
    final elegido = medio ?? MedioDeTransporte.auto;
    _anunciador = Anunciador.para(elegido);
    state = state.copiar(medio: elegido, vozSilenciada: silenciada, sinVoz: !conVoz);
    // Los problemas del GPS (apagado, sin permiso) los muestra el seguimiento, que escucha el mismo flujo.
    _lecturas = ref
        .read(servicioDeUbicacionProvider)
        .lecturas()
        .listen(_alLeer, onError: (Object e) => debugPrint('El GPS de la navegación falló. $e'));
  }

  void _alLeer(Posicion lectura) {
    final punto = (latitud: lectura.latitud, longitud: lectura.longitud);
    state = state.copiar(posicion: lectura);
    if (state.llegaste) return;
    if (llego(punto, lectura.precision, _cliente)) return _llegar();
    if (state.caminos == null) {
      // El primer camino se pide con la primera lectura; después de un error, solo con "Reintentar".
      if (state.error == null) _pedirCuandoSePueda();
      return;
    }
    final progreso = _avanzar(punto);
    if (_desvio.anotar(progreso.distanciaAlCamino, lectura.precision) && _puedeRecalcular && _seMovio(punto)) {
      _pedir(recalculo: true);
    }
  }

  bool _seMovio(PuntoDelCamino punto) => switch (_desdeDelCamino) {
    final desde? => distanciaEnMetros(desde, punto) > umbralDeDesvio,
    null => true,
  };

  bool get _puedeRecalcular =>
      !_pidiendo && !(_esperaDelRecalculo?.isActive ?? false) && !(_esperaDelPedido?.isActive ?? false);

  void _pedirCuandoSePueda() {
    if (_pidiendo || (_esperaDelPedido?.isActive ?? false) || state.posicion == null) {
      _pendiente = true;
      return;
    }
    _pedir();
  }

  void _alTerminarLaEspera() {
    if (_pendiente && !_pidiendo && state.posicion != null) _pedir();
  }

  Future<void> _pedir({bool recalculo = false}) async {
    final desde = state.posicion!;
    final medio = state.medio;
    _pidiendo = true;
    _pendiente = false;
    ref.read(ultimoPedidoDeCaminoProvider.notifier).anotar();
    _esperaDelPedido?.cancel();
    _esperaDelPedido = Timer(esperaEntrePedidos, _alTerminarLaEspera);
    _esperaDelRecalculo?.cancel();
    _esperaDelRecalculo = Timer(esperaEntreRecalculos, () {});
    if (recalculo) {
      state = state.copiar(recalculando: true, errorDelRecalculo: null);
      _decir('Recalculando');
    } else {
      state = state.copiar(calculando: true, error: null);
    }

    try {
      final caminos = await ref
          .read(rutasRepositoryProvider)
          .camino(abrir.rutaId, abrir.visita.id, desde: desde, medio: medio);
      if (!ref.mounted) return;
      _pidiendo = false;
      // Cambió el medio mientras esperaba: este camino ya no sirve, y el del medio nuevo sale cuando se pueda.
      if (medio != state.medio) {
        _pendiente = true;
      } else if (!state.llegaste) {
        _desdeDelCamino = (latitud: desde.latitud, longitud: desde.longitud);
        state = state.copiar(caminos: caminos, elegido: 0, calculando: false, recalculando: false, error: null);
        _seguirCamino();
      }
    } on ErrorDeApi catch (e) {
      if (!ref.mounted) return;
      _pidiendo = false;
      // Sin camino, el promotor sigue el mapa; con uno, se queda el anterior.
      state = state.caminos == null
          ? state.copiar(calculando: false, recalculando: false, error: e)
          : state.copiar(calculando: false, recalculando: false, errorDelRecalculo: e);
    }
    if (_pendiente && !(_esperaDelPedido?.isActive ?? false)) _alTerminarLaEspera();
  }

  /// Empieza a seguir el camino elegido: la voz dice la indicación que viene, con su distancia.
  void _seguirCamino() {
    final camino = state.camino!;
    _trazado = Trazado(camino.puntos);
    _desvio.reiniciar();
    _anunciador.reiniciar();
    final posicion = state.posicion;
    if (posicion == null) return;
    final progreso = _avanzar((latitud: posicion.latitud, longitud: posicion.longitud), anunciar: false);
    if (progreso.proximo case final proximo?) {
      final indicacion = indicacionDe(camino.pasos[proximo], abrir.visita.cliente.nombreCompleto);
      final texto = _anunciador.inicial(proximo, progreso.metrosHastaElProximo, indicacion);
      if (indicacion != _ultimaInicial) _decir(texto);
      _ultimaInicial = indicacion;
    }
  }

  Progreso _avanzar(PuntoDelCamino punto, {bool anunciar = true}) {
    final camino = state.camino!;
    final progreso = progresoEn(camino, _trazado!, punto);
    state = state.copiar(progreso: progreso);
    if (anunciar && progreso.proximo != null) {
      final proximo = progreso.proximo!;
      final indicacion = indicacionDe(camino.pasos[proximo], abrir.visita.cliente.nombreCompleto);
      if (_anunciador.anunciar(proximo, progreso.metrosHastaElProximo, indicacion) case final texto?) _decir(texto);
    }
    return progreso;
  }

  void _llegar() {
    state = state.copiar(llegaste: true, calculando: false, recalculando: false, errorDelRecalculo: null);
    _decir('Llegaste a ${abrir.visita.cliente.nombreCompleto}');
  }

  void _decir(String texto) {
    if (!state.vozSilenciada && !state.sinVoz) ref.read(vozProvider).decir(texto);
  }

  /// Elige otro de los caminos: el principal (0) o una alternativa.
  void elegir(int indice) {
    final caminos = state.caminos;
    if (caminos == null || indice == state.elegido || indice < 0 || indice >= caminos.todos.length) return;
    state = state.copiar(elegido: indice);
    _seguirCamino();
  }

  /// Cómo va el promotor: la app lo recuerda para la próxima vez, y pide el camino de ese medio.
  void elegirMedio(MedioDeTransporte medio) {
    if (medio == state.medio) return;
    ref.read(preferenciasProvider).guardarMedio(medio);
    _anunciador = Anunciador.para(medio);
    state = state.copiar(medio: medio, calculando: true, error: null, errorDelRecalculo: null);
    _pedirCuandoSePueda();
  }

  /// Después de un error sin camino: lo pide otra vez, apenas se pueda.
  void reintentar() {
    state = state.copiar(calculando: true, error: null);
    _pedirCuandoSePueda();
  }

  void silenciar(bool silenciada) {
    state = state.copiar(vozSilenciada: silenciada);
    ref.read(preferenciasProvider).guardarVozSilenciada(silenciada);
    if (silenciada) ref.read(vozProvider).callar();
  }
}

final navegacionProvider = NotifierProvider.autoDispose.family<NavegacionViewModel, EstadoDeLaNavegacion, AbrirCamino>(
  NavegacionViewModel.new,
);

/// Cuándo se pidió el último camino, en toda la app: al volver a abrir la navegación, no se pasa el límite de la API.
class UltimoPedidoDeCamino extends Notifier<DateTime?> {
  @override
  DateTime? build() => null;

  void anotar() => state = DateTime.now();
}

final ultimoPedidoDeCaminoProvider = NotifierProvider<UltimoPedidoDeCamino, DateTime?>(UltimoPedidoDeCamino.new);
