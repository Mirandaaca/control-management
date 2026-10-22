import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cliente.dart';
import '../../data/models/zona.dart';
import '../../data/services/clave_de_idempotencia.dart';
import '../../data/services/error_de_api.dart';
import '../../data/services/ubicacion.dart';
import '../../domain/geometria.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';
import '../sesion/sesion_notifier.dart';

enum EnvioDeCliente { ninguno, esperandoUbicacion, enviando }

/// El celular repetido (2.2): un aviso en ámbar debajo del campo, que no impide registrar.
sealed class AvisoDeCelular {
  const AvisoDeCelular(this.telefono);

  /// El celular que se revisó: si el promotor lo cambia, el aviso ya no vale.
  final String telefono;
}

/// El repetido es de su cartera: el aviso lo nombra y deja abrirlo.
class CelularDeMiCartera extends AvisoDeCelular {
  const CelularDeMiCartera(super.telefono, this.cliente);

  final ClienteResumen cliente;
}

/// El repetido es de otra cartera: no se lo nombra, como en la API.
class CelularDeOtraCartera extends AvisoDeCelular {
  const CelularDeOtraCartera(super.telefono);
}

const _igual = Object();

class EstadoFormularioCliente {
  const EstadoFormularioCliente({
    this.zonas,
    this.original,
    this.errorDeCarga,
    this.reintentando = false,
    this.zonaId,
    this.zonaAMano = false,
    this.campos = const {},
    this.avisoCelular,
    this.envio = EnvioDeCliente.ninguno,
    this.errorAlEnviar,
    this.sinUbicacion = false,
  });

  /// Las zonas del promotor; null mientras cargan.
  final List<Zona>? zonas;

  /// El cliente que se edita; null al registrar uno nuevo.
  final Cliente? original;
  final ErrorDeApi? errorDeCarga;
  final bool reintentando;
  final int? zonaId;

  /// El promotor eligió la zona: ya no se sugiere otra.
  final bool zonaAMano;
  final Map<String, String> campos;
  final AvisoDeCelular? avisoCelular;
  final EnvioDeCliente envio;
  final ErrorDeApi? errorAlEnviar;

  /// Se intentó guardar sin ubicación: la vista va a la tarjeta, que dice qué falta.
  final bool sinUbicacion;

  Zona? get zona => zonas?.where((z) => z.id == zonaId).firstOrNull;

  EstadoFormularioCliente copiar({
    Object? zonas = _igual,
    Object? original = _igual,
    Object? errorDeCarga = _igual,
    bool? reintentando,
    Object? zonaId = _igual,
    bool? zonaAMano,
    Map<String, String>? campos,
    Object? avisoCelular = _igual,
    EnvioDeCliente? envio,
    Object? errorAlEnviar = _igual,
    bool? sinUbicacion,
  }) => EstadoFormularioCliente(
    zonas: zonas == _igual ? this.zonas : zonas as List<Zona>?,
    original: original == _igual ? this.original : original as Cliente?,
    errorDeCarga: errorDeCarga == _igual ? this.errorDeCarga : errorDeCarga as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    zonaId: zonaId == _igual ? this.zonaId : zonaId as int?,
    zonaAMano: zonaAMano ?? this.zonaAMano,
    campos: campos ?? this.campos,
    avisoCelular: avisoCelular == _igual ? this.avisoCelular : avisoCelular as AvisoDeCelular?,
    envio: envio ?? this.envio,
    errorAlEnviar: errorAlEnviar == _igual ? this.errorAlEnviar : errorAlEnviar as ErrorDeApi?,
    sinUbicacion: sinUbicacion ?? this.sinUbicacion,
  );
}

/// Registrar o editar un cliente (2.2 y 2.4; 7.3.6 y 7.3.8). Con [id] edita; sin él, registra.
class FormularioClienteViewModel extends Notifier<EstadoFormularioCliente> {
  FormularioClienteViewModel(this.id);

  final int? id;
  final _clave = ClaveDeIdempotencia();

  // Errores de la API que pertenecen a un campo.
  static const _campoDelCodigo = {'zona_fuera_de_cartera': 'zonaId', 'zona_no_existe': 'zonaId'};

  @override
  EstadoFormularioCliente build() {
    Future.microtask(cargar);
    return const EstadoFormularioCliente();
  }

  Future<void> cargar() async {
    state = state.copiar(reintentando: state.errorDeCarga != null);
    try {
      final todas = await ref.read(zonasRepositoryProvider).listar();
      final original = id == null ? null : await ref.read(clientesRepositoryProvider).obtener(id!);
      if (!ref.mounted) return;
      final zonas = _misZonas(todas, original);
      state = EstadoFormularioCliente(
        zonas: zonas,
        original: original,
        // Con una sola zona, viene elegida.
        zonaId: original?.zona.id ?? (zonas.length == 1 ? zonas.single.id : null),
      );
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = EstadoFormularioCliente(errorDeCarga: e);
    }
  }

  /// Solo sus zonas: la API no deja registrar un cliente en la zona de otro promotor. Con la cartera completa, todas. Al
  /// editar, también la zona del cliente, aunque ya no sea suya.
  List<Zona> _misZonas(List<Zona> todas, Cliente? original) {
    final sesion = ref.read(sesionProvider);
    if (sesion is! ConSesion) return const [];
    if (sesion.sesion.tiene([Permisos.seguridad.verCarteraCompleta])) return todas;
    return [
      for (final zona in todas)
        if (zona.promotorId == sesion.sesion.id || zona.id == original?.zona.id) zona,
    ];
  }

  void elegirZona(int zonaId) =>
      state = state.copiar(zonaId: zonaId, zonaAMano: true, campos: {...state.campos}..remove('zonaId'));

  /// Con varias zonas, sugiere la que contiene el punto capturado, como el panel con los puntos de referencia. Si el
  /// promotor ya eligió una, se respeta. Al editar no se sugiere: mover un cliente de zona lo cambia de promotor.
  void alCapturar(Posicion posicion) {
    final zonas = state.zonas;
    if (zonas == null || zonas.length < 2 || state.zonaAMano || state.original != null) return;
    final sugerida = zonaQueContiene(posicion.latitud, posicion.longitud, zonas);
    if (sugerida != null) state = state.copiar(zonaId: sugerida.id, campos: {...state.campos}..remove('zonaId'));
  }

  /// Al salir del campo: busca el celular en su cartera. Si está, el aviso nombra al cliente y deja abrirlo.
  Future<void> revisarCelular(String telefono) async {
    final buscado = telefono.trim();
    if (buscado.isEmpty || buscado == state.original?.telefono.trim() || buscado == state.avisoCelular?.telefono) {
      return;
    }
    try {
      final repetido = await ref.read(clientesRepositoryProvider).conCelular(buscado);
      if (!ref.mounted) return;
      if (repetido != null && repetido.id != state.original?.id) {
        state = state.copiar(avisoCelular: CelularDeMiCartera(buscado, repetido));
      }
    } on ErrorDeApi catch (e) {
      // No impide nada: si la revisión falla, la API avisa igual al registrar.
      debugPrint('No se pudo revisar si el celular ya existe. ${e.mensaje}');
    }
  }

  void cambioElCelular(String telefono) {
    if (state.avisoCelular != null && telefono.trim() != state.avisoCelular!.telefono) {
      state = state.copiar(avisoCelular: null);
    }
  }

  /// Registra o guarda el cliente, con la ubicación de la tarjeta. Devuelve el cliente guardado, o null si algo lo
  /// impidió: los errores quedan en el estado.
  Future<Cliente?> enviar(DatosDeCliente datos) async {
    if (state.envio != EnvioDeCliente.ninguno) return null;
    final faltan = _revisar(datos);
    if (faltan.isNotEmpty) {
      state = state.copiar(campos: faltan, errorAlEnviar: null, sinUbicacion: false);
      return null;
    }

    state = state.copiar(envio: EnvioDeCliente.esperandoUbicacion, campos: const {}, errorAlEnviar: null);
    final ubicacion = await ref.read(capturaDeUbicacionProvider(QueSeRegistra.cliente).notifier).paraGuardar();
    if (!ref.mounted) return null;
    if (ubicacion == null) {
      state = state.copiar(envio: EnvioDeCliente.ninguno, sinUbicacion: true);
      return null;
    }

    state = state.copiar(envio: EnvioDeCliente.enviando, sinUbicacion: false);
    // Con el aviso a la vista, registrar es confirmar que el celular se repite.
    final confirmar = state.avisoCelular != null;
    final repositorio = ref.read(clientesRepositoryProvider);
    try {
      final original = state.original;
      final guardado = original == null
          ? await repositorio.registrar(datos, ubicacion, clave: _clave.valor, confirmarTelefonoRepetido: confirmar)
          : await repositorio.editar(original.id, datos, ubicacion, confirmarTelefonoRepetido: confirmar);
      _clave.despuesDelEnvio(huboRespuesta: true);
      if (ref.mounted) state = state.copiar(envio: EnvioDeCliente.ninguno);
      return guardado;
    } on ErrorDeApi catch (e) {
      _clave.despuesDelEnvio(huboRespuesta: !e.sinConexion);
      if (!ref.mounted) return null;
      if (e.codigo == 'telefono_repetido') {
        await _avisarCelularRepetido(datos.telefono);
        return null;
      }
      final campos = e.camposCon(_campoDelCodigo);
      state = state.copiar(envio: EnvioDeCliente.ninguno, campos: campos, errorAlEnviar: campos.isEmpty ? e : null);
      return null;
    }
  }

  // La API encontró el celular. Si es de su cartera, el aviso lo nombra; si no, dice que es de otra cartera.
  Future<void> _avisarCelularRepetido(String telefono) async {
    final buscado = telefono.trim();
    ClienteResumen? deMiCartera;
    try {
      deMiCartera = await ref.read(clientesRepositoryProvider).conCelular(buscado);
    } on ErrorDeApi catch (e) {
      debugPrint('No se pudo buscar el cliente con el celular repetido. ${e.mensaje}');
    }
    if (!ref.mounted) return;
    state = state.copiar(
      envio: EnvioDeCliente.ninguno,
      avisoCelular: deMiCartera == null ? CelularDeOtraCartera(buscado) : CelularDeMiCartera(buscado, deMiCartera),
    );
  }

  // Los obligatorios, con los textos de la API: el error va en su campo antes de esperar la ubicación.
  Map<String, String> _revisar(DatosDeCliente datos) => {
    if (datos.nombreCompleto.trim().isEmpty) 'nombreCompleto': 'El nombre es obligatorio.',
    if (datos.telefono.trim().isEmpty) 'telefono': 'El celular es obligatorio.',
    if (datos.direccion.trim().isEmpty) 'direccionTexto': 'La dirección es obligatoria.',
    if (datos.zonaId == null) 'zonaId': 'Elige la zona.',
  };
}

final formularioClienteProvider = NotifierProvider.autoDispose
    .family<FormularioClienteViewModel, EstadoFormularioCliente, int?>(FormularioClienteViewModel.new);
