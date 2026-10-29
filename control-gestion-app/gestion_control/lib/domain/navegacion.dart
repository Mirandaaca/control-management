import 'dart:math' as math;

import '../data/models/camino.dart';

// Lo que la navegación calcula en el celular con cada lectura del GPS (tarea 25): dónde está el promotor sobre el
// camino, cuánto le falta, qué indicación sigue, si se desvió, si llegó y qué dice la voz. El camino lo calcula la API.

/// Más lejos que esto del camino, en dos lecturas seguidas, es un desvío: se pide un camino nuevo.
const umbralDeDesvio = 50.0;

/// Una lectura con más error que esto no cuenta para el desvío ni para la llegada.
const precisionMaxima = 30.0;

/// A menos que esto del cliente, el promotor llegó.
const radioDeLlegada = 30.0;

/// Una indicación cuenta como alcanzada un poco antes de su punto: en la esquina ya se muestra la siguiente.
const toleranciaDelPaso = 10.0;

const _radioDeLaTierra = 6371000.0;
const _metrosPorGrado = _radioDeLaTierra * math.pi / 180;

double _radianes(double grados) => grados * math.pi / 180;

/// La distancia en metros entre dos puntos (haversine).
double distanciaEnMetros(PuntoDelCamino a, PuntoDelCamino b) {
  final dLatitud = _radianes(b.latitud - a.latitud);
  final dLongitud = _radianes(b.longitud - a.longitud);
  final h =
      math.pow(math.sin(dLatitud / 2), 2) +
      math.cos(_radianes(a.latitud)) * math.cos(_radianes(b.latitud)) * math.pow(math.sin(dLongitud / 2), 2);
  return 2 * _radioDeLaTierra * math.asin(math.sqrt(h));
}

/// El punto a [este] y [norte] metros de [desde]. Para distancias de una ciudad, el plano alcanza.
PuntoDelCamino desplazar(PuntoDelCamino desde, {double este = 0, double norte = 0}) => (
  latitud: desde.latitud + norte / _metrosPorGrado,
  longitud: desde.longitud + este / (_metrosPorGrado * math.cos(_radianes(desde.latitud))),
);

/// Dónde cae el promotor sobre la línea: a cuántos metros de ella está y cuántos metros del camino ya recorrió.
typedef Proyeccion = ({double distancia, double avance});

/// La línea de un camino con lo que mide hasta cada punto, para no recalcularlo con cada lectura del GPS.
class Trazado {
  Trazado(this.puntos) : _acumulado = _medir(puntos);

  final List<PuntoDelCamino> puntos;
  final List<double> _acumulado;

  static List<double> _medir(List<PuntoDelCamino> puntos) {
    final acumulado = <double>[0];
    for (var i = 1; i < puntos.length; i++) {
      acumulado.add(acumulado.last + distanciaEnMetros(puntos[i - 1], puntos[i]));
    }
    return acumulado;
  }

  double get largo => _acumulado.last;

  /// Los metros desde el inicio hasta el punto [indice] de la línea.
  double avanceEn(int indice) => _acumulado[indice.clamp(0, puntos.length - 1)];

  /// El punto más cercano de la línea a [punto]: se mira cada tramo en un plano de metros centrado en el punto.
  Proyeccion proyectar(PuntoDelCamino punto) {
    if (puntos.length == 1) return (distancia: distanciaEnMetros(punto, puntos.first), avance: 0);
    final escalaEste = _metrosPorGrado * math.cos(_radianes(punto.latitud));
    (double, double) enMetros(PuntoDelCamino p) =>
        ((p.longitud - punto.longitud) * escalaEste, (p.latitud - punto.latitud) * _metrosPorGrado);

    var mejor = (distancia: double.infinity, avance: 0.0);
    var (ax, ay) = enMetros(puntos.first);
    for (var i = 0; i < puntos.length - 1; i++) {
      final (bx, by) = enMetros(puntos[i + 1]);
      final (dx, dy) = (bx - ax, by - ay);
      final largo2 = dx * dx + dy * dy;
      final t = largo2 == 0 ? 0.0 : (-(ax * dx + ay * dy) / largo2).clamp(0.0, 1.0);
      final (cx, cy) = (ax + t * dx, ay + t * dy);
      final distancia = math.sqrt(cx * cx + cy * cy);
      if (distancia < mejor.distancia) {
        mejor = (distancia: distancia, avance: _acumulado[i] + t * (_acumulado[i + 1] - _acumulado[i]));
      }
      (ax, ay) = (bx, by);
    }
    return mejor;
  }
}

/// Cómo va el promotor sobre el camino elegido.
class Progreso {
  const Progreso({
    required this.distanciaAlCamino,
    required this.metrosQueFaltan,
    required this.segundosQueFaltan,
    required this.proximo,
    required this.metrosHastaElProximo,
  });

  final double distanciaAlCamino;
  final double metrosQueFaltan;
  final double segundosQueFaltan;

  /// El paso cuya indicación se muestra (el giro que viene), o null si el camino no trae pasos.
  final int? proximo;
  final double metrosHastaElProximo;
}

/// Dónde va el promotor en [camino]. El paso actual es el último cuyo punto de inicio ya alcanzó (con
/// [toleranciaDelPaso]); la indicación que se muestra es la del paso siguiente, el giro que viene. En el último paso,
/// es la llegada.
Progreso progresoEn(Camino camino, Trazado trazado, PuntoDelCamino posicion) {
  final proyeccion = trazado.proyectar(posicion);
  final avance = proyeccion.avance;
  final faltan = math.max(0.0, trazado.largo - avance);
  final pasos = camino.pasos;
  if (pasos.isEmpty) {
    return Progreso(
      distanciaAlCamino: proyeccion.distancia,
      metrosQueFaltan: faltan,
      segundosQueFaltan: trazado.largo == 0 ? 0 : camino.duracion * faltan / trazado.largo,
      proximo: null,
      metrosHastaElProximo: faltan,
    );
  }

  final inicios = [for (final paso in pasos) trazado.avanceEn(paso.indice)];
  var actual = 0;
  for (var i = 0; i < pasos.length; i++) {
    if (inicios[i] <= avance + toleranciaDelPaso) actual = i;
  }
  final proximo = math.min(actual + 1, pasos.length - 1);

  final finDelActual = actual + 1 < pasos.length ? inicios[actual + 1] : trazado.largo;
  final largoDelActual = finDelActual - inicios[actual];
  final queda = largoDelActual <= 0 ? 0.0 : ((finDelActual - avance) / largoDelActual).clamp(0.0, 1.0);
  var segundos = queda * pasos[actual].duracion;
  for (var i = actual + 1; i < pasos.length; i++) {
    segundos += pasos[i].duracion;
  }

  return Progreso(
    distanciaAlCamino: proyeccion.distancia,
    metrosQueFaltan: faltan,
    segundosQueFaltan: segundos,
    proximo: proximo,
    metrosHastaElProximo: math.max(0.0, inicios[proximo] - avance),
  );
}

/// Cuenta como desvío estar a más de [umbralDeDesvio] del camino en dos lecturas seguidas. Las lecturas con más de
/// [precisionMaxima] de error no cuentan: ni suman ni cortan la cuenta.
class DetectorDeDesvio {
  var _seguidas = 0;

  bool get desviado => _seguidas >= 2;

  /// Anota una lectura y dice si el promotor está desviado.
  bool anotar(double distanciaAlCamino, double? precision) {
    if (precision == null || precision > precisionMaxima) return desviado;
    _seguidas = distanciaAlCamino > umbralDeDesvio ? _seguidas + 1 : 0;
    return desviado;
  }

  void reiniciar() => _seguidas = 0;
}

/// Si el promotor llegó al cliente: a menos de [radioDeLlegada], con una lectura precisa.
bool llego(PuntoDelCamino posicion, double? precision, PuntoDelCamino cliente) =>
    precision != null && precision <= precisionMaxima && distanciaEnMetros(posicion, cliente) < radioDeLlegada;

/// Cuándo la voz dice la indicación que viene: una vez al entrar a cada distancia de [umbrales] (de mayor a menor), y
/// nunca dos veces lo mismo en el mismo paso. En el umbral más chico dice solo la indicación ("Gira a la derecha en
/// Calle 4"); antes, con la distancia ("En 200 metros, gira a la derecha en Calle 4").
class Anunciador {
  Anunciador(this.umbrales);

  /// En auto y en moto, 200 y 40 m; a pie, 30 m.
  factory Anunciador.para(MedioDeTransporte medio) =>
      Anunciador(medio == MedioDeTransporte.pie ? const [30] : const [200, 40]);

  final List<double> umbrales;
  int? _paso;
  var _nivel = -1;

  // El umbral más chico en el que ya está: -1 si está más lejos que todos.
  int _nivelPara(double metros) {
    var nivel = -1;
    for (var i = 0; i < umbrales.length; i++) {
      if (metros <= umbrales[i]) nivel = i;
    }
    return nivel;
  }

  /// Lo que hay que decir ahora del paso [paso], a [metros] de él, o null si no toca.
  String? anunciar(int paso, double metros, String indicacion) {
    if (paso != _paso) {
      _paso = paso;
      _nivel = -1;
    }
    final nivel = _nivelPara(metros);
    if (nivel <= _nivel) return null;
    _nivel = nivel;
    return nivel == umbrales.length - 1 ? indicacion : 'En ${metrosHablados(metros)}, ${_minuscula(indicacion)}';
  }

  /// Al empezar un camino: la indicación que viene, siempre con la distancia. Lo que ya dijo no lo repite.
  String inicial(int paso, double metros, String indicacion) {
    _paso = paso;
    _nivel = _nivelPara(metros);
    return 'En ${metrosHablados(metros)}, ${_minuscula(indicacion)}';
  }

  void reiniciar() {
    _paso = null;
    _nivel = -1;
  }
}

String _minuscula(String texto) => texto.isEmpty ? texto : texto[0].toLowerCase() + texto.substring(1);

/// La distancia como se dice en voz alta, como se ve en la pantalla: "180 metros", "530 metros", "1,2 kilómetros".
String metrosHablados(double metros) {
  final redondeados = math.max(10, (metros / 10).round() * 10);
  if (redondeados < 1000) return '$redondeados metros';
  final kilometros = (metros / 100).round() / 10;
  final texto = kilometros == kilometros.truncateToDouble()
      ? '${kilometros.toInt()}'
      : kilometros.toStringAsFixed(1).replaceFirst('.', ',');
  return texto == '1' ? '1 kilómetro' : '$texto kilómetros';
}
