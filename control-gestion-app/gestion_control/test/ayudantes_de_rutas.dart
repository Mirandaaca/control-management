import 'package:gestion_control/data/models/camino.dart';
import 'package:gestion_control/data/models/cliente.dart';
import 'package:gestion_control/data/models/ruta.dart';
import 'package:gestion_control/data/repositories/rutas_repository.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/data/services/ubicacion.dart';

import 'ayudantes.dart';

// Los datos de la tarea 17: la ruta del 30/10/2026 de datos-ejemplo.md (RUT-2026-00008, la del 8.6.9) y los clientes
// del mapa y de la lista por visitar.

Map<String, dynamic> _visita(
  int id,
  int orden,
  int resultado,
  String nombre,
  String direccion,
  double latitud,
  double longitud, {
  String? hora,
  int? atraso,
  String? observacion,
}) => {
  'id': id,
  'orden': orden,
  'resultado': resultado,
  'horaVisita': hora,
  'latitud': hora == null ? null : latitud,
  'longitud': hora == null ? null : longitud,
  'observacion': observacion,
  'cliente': {
    'id': 60 + orden,
    'codigo': 'CLI-0006$orden',
    'nombreCompleto': nombre,
    'direccionTexto': direccion,
    'latitud': latitud,
    'longitud': longitud,
    'ultimaVisita': null,
    'proximaVisita': null,
  },
  'diasDeAtraso': atraso,
};

/// La ruta como la manda la API (`RutaDTO`). [estado]: 0 planificada, 1 en curso, 2 completada. En curso tiene 3 de 5
/// marcadas, como el 8.6.9; planificada, ninguna; [todasMarcadas] marca también la 4 y la 5.
Map<String, dynamic> rutaJson({int estado = 1, bool todasMarcadas = false}) {
  final marcadas = estado != 0;
  final visitas = [
    _visita(
      1,
      1,
      marcadas ? 1 : 0,
      'Elena Gutiérrez Vaca',
      'Calle 2 N.º 88',
      -17.7700,
      -63.1800,
      hora: marcadas ? '2026-10-30T12:40:00' : null,
      atraso: marcadas ? null : 14,
    ),
    _visita(
      2,
      2,
      marcadas ? 2 : 0,
      'Pedro Mamani Cruz',
      'Calle 5 N.º 214, esquina con la avenida',
      -17.7750,
      -63.1780,
      hora: marcadas ? '2026-10-30T13:20:00' : null,
      atraso: marcadas ? null : 11,
    ),
    _visita(
      3,
      3,
      marcadas ? 1 : 0,
      'Carmen Rojas Saucedo',
      'Calle 7 N.º 35, a media cuadra de la Plaza Norte',
      -17.7765,
      -63.1740,
      hora: marcadas ? '2026-10-30T14:05:00' : null,
      atraso: marcadas ? null : 6,
    ),
    _visita(
      4,
      4,
      todasMarcadas ? 1 : 0,
      'Marco Ortiz Peña',
      'Calle 8 N.º 150',
      -17.7730,
      -63.1690,
      hora: todasMarcadas ? '2026-10-30T15:00:00' : null,
      atraso: todasMarcadas ? null : 9,
    ),
    _visita(
      5,
      5,
      todasMarcadas ? 2 : 0,
      'Luis Fernández Áñez',
      'Calle 3 N.º 402',
      -17.7710,
      -63.1710,
      hora: todasMarcadas ? '2026-10-30T15:30:00' : null,
      atraso: todasMarcadas ? null : 4,
    ),
  ];
  final pendientes = visitas.where((v) => v['resultado'] == 0).length;
  return {
    'id': 8,
    'numero': 'RUT-2026-00008',
    'nombre': 'Zona Norte · 30/10/2026',
    'fecha': '2026-10-30',
    'estado': estado,
    'promotor': {'id': 'u1', 'nombreCompleto': 'Carlos Mendoza Flores'},
    'zona': {'id': 1, 'nombre': 'Zona Norte'},
    'fechaInicio': estado == 0 ? null : '2026-10-30T12:15:00',
    'fechaFin': estado == 2 ? '2026-10-30T20:10:00' : null,
    'total': 5,
    'marcadas': 5 - pendientes,
    'visitadas': visitas.where((v) => v['resultado'] == 1).length,
    'pendientes': pendientes,
    'visitas': visitas,
    'ultimaUbicacion': null,
  };
}

Ruta rutaDePrueba({EstadoRuta estado = EstadoRuta.enCurso, bool todasMarcadas = false}) =>
    Ruta.fromJson(rutaJson(estado: estado.index, todasMarcadas: todasMarcadas));

/// La misma ruta con una visita marcada de otra forma, como la devuelve la API al marcarla.
Ruta conVisitaMarcada(Ruta ruta, int visitaId, EstadoVisita resultado, {String hora = '2026-10-30T15:00:00'}) {
  final json = rutaJson(estado: ruta.estado.index);
  final visitas = [
    for (final v in ruta.visitas)
      if (v.id == visitaId)
        {
          ...(json['visitas'] as List).cast<Map<String, dynamic>>()[v.orden - 1],
          'resultado': resultado.index,
          'horaVisita': hora,
        }
      else
        (json['visitas'] as List).cast<Map<String, dynamic>>()[v.orden - 1],
  ];
  final pendientes = visitas.where((v) => v['resultado'] == 0).length;
  return Ruta.fromJson({...json, 'visitas': visitas, 'pendientes': pendientes, 'marcadas': 5 - pendientes});
}

// El camino de la tarea 25: desde Carmen (la 3, recién visitada) hasta Marco Ortiz Peña (la 4, la siguiente), 530 m al
// este por la Calle 7 y 390 m al norte por la Calle 8. La alternativa va primero al norte y tarda 3 minutos más.
const desdeCarmen = (latitud: -17.7765, longitud: -63.1740);
const esquina = (latitud: -17.7765, longitud: -63.1690);
const casaDeMarco = (latitud: -17.7730, longitud: -63.1690);

Map<String, dynamic> _puntoJson(PuntoDelCamino p) => {'latitud': p.latitud, 'longitud': p.longitud};

Map<String, dynamic> _pasoJson(String indicacion, int giro, int indice, int distancia, int duracion, {String? calle}) =>
    {
      'indicacion': indicacion,
      'calle': calle,
      'giro': giro,
      'distanciaMetros': distancia,
      'duracionSegundos': duracion,
      'inicio': {'latitud': 0, 'longitud': 0},
      'indice': indice,
    };

/// Los caminos como los manda la API (`CaminosDTO`).
Map<String, dynamic> caminosJson() => {
  'principal': {
    'distanciaMetros': 919,
    'duracionSegundos': 105,
    'puntos': [
      for (final p in [desdeCarmen, esquina, casaDeMarco]) _puntoJson(p),
    ],
    'pasos': [
      _pasoJson('Sigue por Calle 7', 0, 0, 530, 60, calle: 'Calle 7'),
      _pasoJson('Gira a la izquierda en Calle 8', 2, 1, 389, 45, calle: 'Calle 8'),
      _pasoJson('Llegas a tu destino', 13, 2, 0, 0),
    ],
  },
  'alternativas': [
    {
      'distanciaMetros': 919,
      'duracionSegundos': 285,
      'puntos': [
        for (final p in [desdeCarmen, (latitud: -17.7730, longitud: -63.1740), casaDeMarco]) _puntoJson(p),
      ],
      'pasos': [
        _pasoJson('Sigue el camino marcado', 0, 0, 389, 140),
        _pasoJson('Gira a la derecha', 3, 1, 530, 145),
        _pasoJson('Llegas a tu destino', 13, 2, 0, 0),
      ],
    },
  ],
};

Caminos caminosDePrueba() => Caminos.fromJson(caminosJson());

/// Lo que se le pidió al pedir un camino.
typedef PedidoDeCamino = ({int rutaId, int visitaId, Posicion desde, MedioDeTransporte medio});

/// Lo que se le mandó al marcar.
typedef MarcaDePrueba = ({int rutaId, int visitaId, EstadoVisita resultado, Posicion ubicacion, String observacion});

class RutasRepositoryFalso implements RutasRepository {
  Future<Ruta?> Function() alPedirDelDia = () async => rutaDePrueba();
  Future<Ruta> Function(int id)? alIniciar;
  Future<Ruta> Function(MarcaDePrueba marca)? alMarcar;
  Future<void> Function(int id, Posicion ubicacion)? alEnviar;
  Future<Ruta> Function(int id)? alTerminar;
  Future<Caminos> Function(PedidoDeCamino pedido)? alPedirCamino;

  var pedidosDelDia = 0;
  final inicios = <int>[];
  final marcas = <MarcaDePrueba>[];
  final ubicaciones = <(int, Posicion)>[];
  final finales = <int>[];
  final caminos = <PedidoDeCamino>[];

  @override
  Future<Ruta?> delDia() {
    pedidosDelDia++;
    return alPedirDelDia();
  }

  @override
  Future<Ruta> iniciar(int id) {
    inicios.add(id);
    return (alIniciar ?? (_) async => rutaDePrueba())(id);
  }

  @override
  Future<Ruta> marcar(
    int id,
    int visitaId, {
    required EstadoVisita resultado,
    required Posicion ubicacion,
    String observacion = '',
  }) {
    final marca = (
      rutaId: id,
      visitaId: visitaId,
      resultado: resultado,
      ubicacion: ubicacion,
      observacion: observacion,
    );
    marcas.add(marca);
    return (alMarcar ?? (m) async => conVisitaMarcada(rutaDePrueba(), m.visitaId, m.resultado))(marca);
  }

  @override
  Future<void> enviarUbicacion(int id, Posicion ubicacion) {
    ubicaciones.add((id, ubicacion));
    return (alEnviar ?? (_, _) async {})(id, ubicacion);
  }

  @override
  Future<Caminos> camino(int id, int visitaId, {required Posicion desde, required MedioDeTransporte medio}) {
    final pedido = (rutaId: id, visitaId: visitaId, desde: desde, medio: medio);
    caminos.add(pedido);
    return (alPedirCamino ?? (_) async => caminosDePrueba())(pedido);
  }

  @override
  Future<Ruta> terminar(int id) {
    finales.add(id);
    return (alTerminar ?? (_) async => rutaDePrueba(estado: EstadoRuta.completada))(id);
  }
}

const rutaNoEnCurso = ErrorDeApi(mensaje: 'La ruta ya se completó.', codigo: 'ruta_no_en_curso', estado: 400);

/// Los clientes del mapa: Elena disponible (sin visitas), Carmen activa, Silvia inactiva y uno de la Zona Sur.
const clientesDelMapa = [
  ClienteEnElMapa(
    id: 61,
    codigo: 'CLI-00061',
    nombreCompleto: 'Elena Gutiérrez Vaca',
    latitud: -17.7700,
    longitud: -63.1800,
    telefono: '70010061',
    direccion: 'Calle 2 N.º 88',
    zonaId: 1,
    estado: estadoDisponible,
  ),
  ClienteEnElMapa(
    id: 68,
    codigo: 'CLI-00068',
    nombreCompleto: 'Carmen Rojas Saucedo',
    latitud: -17.7765,
    longitud: -63.1740,
    telefono: '70010068',
    direccion: 'Calle 7 N.º 35, a media cuadra de la Plaza Norte',
    zonaId: 1,
    estado: estadoActivo,
    ultimaVisita: '2026-10-30T14:05:00',
    proximaVisita: '2026-11-14T14:05:00',
  ),
  ClienteEnElMapa(
    id: 92,
    codigo: 'CLI-00092',
    nombreCompleto: 'Silvia Torrico Méndez',
    latitud: -17.7860,
    longitud: -63.1900,
    telefono: '70010092',
    direccion: 'Calle 6 N.º 310, a media cuadra del Mercado Norte',
    zonaId: 1,
    estado: estadoInactivo,
  ),
  ClienteEnElMapa(
    id: 80,
    codigo: 'CLI-00080',
    nombreCompleto: 'Marta Rivero Salas',
    latitud: -17.8100,
    longitud: -63.1800,
    telefono: '70010080',
    direccion: 'Calle 12 N.º 60',
    zonaId: 2,
    estado: estadoDisponible,
  ),
];

/// Los clientes por visitar de datos-ejemplo.md, como los ordena la API.
const porVisitarDePrueba = [
  ClientePorVisitar(
    id: 92,
    codigo: 'CLI-00092',
    nombreCompleto: 'Silvia Torrico Méndez',
    direccion: 'Calle 6 N.º 310, a media cuadra del Mercado Norte',
    latitud: -17.7718,
    longitud: -63.1763,
    zona: zonaNorteResumen,
  ),
  ClientePorVisitar(
    id: 61,
    codigo: 'CLI-00061',
    nombreCompleto: 'Elena Gutiérrez Vaca',
    direccion: 'Calle 2 N.º 88',
    latitud: -17.7700,
    longitud: -63.1800,
    zona: zonaNorteResumen,
    ultimaVisita: '2026-10-02T14:00:00',
    proximaVisita: '2026-10-17T14:00:00',
    diasDeAtraso: 14,
  ),
  ClientePorVisitar(
    id: 71,
    codigo: 'CLI-00071',
    nombreCompleto: 'Luis Fernández Áñez',
    direccion: 'Calle 3 N.º 402',
    latitud: -17.7710,
    longitud: -63.1710,
    zona: zonaNorteResumen,
    ultimaVisita: '2026-10-15T14:00:00',
    proximaVisita: '2026-10-30T14:00:00',
    diasDeAtraso: 0,
  ),
];
