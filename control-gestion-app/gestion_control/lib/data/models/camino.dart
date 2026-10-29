/// Cómo va el promotor ese día (`MedioDeTransporte` de la API). En auto y en moto el camino es el mismo: el proveedor de
/// rutas no tiene uno para motos.
enum MedioDeTransporte {
  auto('En auto'),
  moto('En moto'),
  pie('A pie');

  const MedioDeTransporte(this.texto);

  final String texto;

  /// Lo que va en la consulta: auto, moto o pie.
  String get valor => name;

  static MedioDeTransporte? desde(String? valor) {
    for (final medio in values) {
      if (medio.name == valor) return medio;
    }
    return null;
  }
}

/// El giro de un paso (`Giro` de la API, en el mismo orden): elige el ícono de la indicación.
enum Giro {
  salida,
  recto,
  izquierda,
  derecha,
  izquierdaCerrada,
  derechaCerrada,
  izquierdaLeve,
  derechaLeve,
  mantenerIzquierda,
  mantenerDerecha,
  rotonda,
  salirDeRotonda,
  retorno,
  llegada;

  /// Un giro que esta versión de la app no conoce se muestra como seguir recto.
  static Giro desde(int valor) => valor >= 0 && valor < values.length ? values[valor] : recto;
}

typedef PuntoDelCamino = ({double latitud, double longitud});

PuntoDelCamino _punto(Object? json) {
  final punto = json as Map<String, dynamic>;
  return (latitud: (punto['latitud'] as num).toDouble(), longitud: (punto['longitud'] as num).toDouble());
}

/// Un tramo del camino (`PasoDTO`): la indicación para empezarlo ("Gira a la derecha en Calle 4"), cuánto mide y en qué
/// punto de la línea empieza ([indice]).
class Paso {
  const Paso({
    required this.indicacion,
    required this.giro,
    required this.distancia,
    required this.duracion,
    required this.indice,
    this.calle,
  });

  factory Paso.fromJson(Map<String, dynamic> json) => Paso(
    indicacion: json['indicacion'] as String,
    calle: json['calle'] as String?,
    giro: Giro.desde(json['giro'] as int),
    distancia: json['distanciaMetros'] as int,
    duracion: json['duracionSegundos'] as int,
    indice: json['indice'] as int,
  );

  final String indicacion;
  final String? calle;
  final Giro giro;

  /// En metros y en segundos.
  final int distancia;
  final int duracion;

  /// La posición, en [Camino.puntos], del punto donde empieza.
  final int indice;
}

/// Un camino por calles (`CaminoDTO`): la línea que se dibuja y sus pasos.
class Camino {
  const Camino({required this.distancia, required this.duracion, required this.puntos, required this.pasos});

  factory Camino.fromJson(Map<String, dynamic> json) => Camino(
    distancia: json['distanciaMetros'] as int,
    duracion: json['duracionSegundos'] as int,
    puntos: [for (final p in json['puntos'] as List) _punto(p)],
    pasos: [for (final p in json['pasos'] as List) Paso.fromJson(p as Map<String, dynamic>)],
  );

  /// En metros y en segundos.
  final int distancia;
  final int duracion;
  final List<PuntoDelCamino> puntos;
  final List<Paso> pasos;
}

/// El mejor camino y sus alternativas (`CaminosDTO`), que el promotor puede elegir.
class Caminos {
  const Caminos({required this.principal, this.alternativas = const []});

  factory Caminos.fromJson(Map<String, dynamic> json) => Caminos(
    principal: Camino.fromJson(json['principal'] as Map<String, dynamic>),
    alternativas: [for (final c in json['alternativas'] as List) Camino.fromJson(c as Map<String, dynamic>)],
  );

  final Camino principal;
  final List<Camino> alternativas;

  /// El principal primero: el índice que se elige en la pantalla.
  List<Camino> get todos => [principal, ...alternativas];
}

/// Una visita en el orden propuesto (`VisitaOrdenadaDTO`, parte C de la tarea 25), con el número que tendría.
/// [ausente]: estaba ausente y el promotor vuelve a pasar por ella.
class VisitaOrdenada {
  const VisitaOrdenada({required this.id, required this.orden, required this.nombreCompleto, required this.ausente});

  factory VisitaOrdenada.fromJson(Map<String, dynamic> json) => VisitaOrdenada(
    id: json['id'] as int,
    orden: json['orden'] as int,
    nombreCompleto: json['nombreCompleto'] as String,
    ausente: json['ausente'] as bool,
  );

  final int id;
  final int orden;
  final String nombreCompleto;
  final bool ausente;
}

/// El orden más corto para lo que le falta al promotor (`OrdenSugeridoDTO`): todavía no está guardado.
class OrdenSugerido {
  const OrdenSugerido({required this.visitas, required this.duracion, required this.distancia});

  factory OrdenSugerido.fromJson(Map<String, dynamic> json) => OrdenSugerido(
    visitas: [for (final v in json['visitas'] as List) VisitaOrdenada.fromJson(v as Map<String, dynamic>)],
    duracion: json['duracionSegundos'] as int,
    distancia: json['distanciaMetros'] as int,
  );

  final List<VisitaOrdenada> visitas;

  /// En segundos y en metros.
  final int duracion;
  final int distancia;
}
