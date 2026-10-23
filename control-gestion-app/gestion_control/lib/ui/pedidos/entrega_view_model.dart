import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/hoja_pedido.dart';
import '../../data/models/pedido.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';

const _igual = Object();

class EstadoDeLaEntrega {
  const EstadoDeLaEntrega({
    this.pedido,
    this.errorDeCarga,
    this.reintentando = false,
    this.tipoVenta,
    this.campos = const {},
    this.enviando = false,
    this.esperandoUbicacion = false,
    this.errorAlEnviar,
    this.sinUbicacion = false,
  });

  /// Null mientras carga o si no se pudo cargar.
  final Pedido? pedido;
  final ErrorDeApi? errorDeCarga;
  final bool reintentando;

  /// Sin valor por defecto, como en el panel: así no se registra un contado por descuido.
  final TipoVenta? tipoVenta;

  /// Los errores por campo, con las claves de la API: `recibidoPor`, `tipoVenta` y el stock de cada línea
  /// (`detalles[i].cantidad`), en el orden en que se guardaron.
  final Map<String, String> campos;
  final bool enviando;

  /// Se tocó el botón antes de tener una ubicación: espera la primera lectura.
  final bool esperandoUbicacion;
  final ErrorDeApi? errorAlEnviar;
  final bool sinUbicacion;

  EstadoDeLaEntrega copiar({
    Object? pedido = _igual,
    Object? errorDeCarga = _igual,
    bool? reintentando,
    Object? tipoVenta = _igual,
    Map<String, String>? campos,
    bool? enviando,
    bool? esperandoUbicacion,
    Object? errorAlEnviar = _igual,
    bool? sinUbicacion,
  }) => EstadoDeLaEntrega(
    pedido: pedido == _igual ? this.pedido : pedido as Pedido?,
    errorDeCarga: errorDeCarga == _igual ? this.errorDeCarga : errorDeCarga as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    tipoVenta: tipoVenta == _igual ? this.tipoVenta : tipoVenta as TipoVenta?,
    campos: campos ?? this.campos,
    enviando: enviando ?? this.enviando,
    esperandoUbicacion: esperandoUbicacion ?? this.esperandoUbicacion,
    errorAlEnviar: errorAlEnviar == _igual ? this.errorAlEnviar : errorAlEnviar as ErrorDeApi?,
    sinUbicacion: sinUbicacion ?? this.sinUbicacion,
  );
}

/// El aviso de éxito de la entrega, con los textos del panel (`EntregarPedido.jsx`).
String mensajeDeEntrega(Pedido pedido) {
  final venta = pedido.venta;
  if (venta == null) return '${pedido.numero} quedó entregado.';
  final cobranza = venta.cobranza;
  return cobranza != null
      ? '${pedido.numero} quedó entregado: nació la venta ${venta.numero}, se descontó el stock y nació la deuda '
            '${cobranza.numero}.'
      : '${pedido.numero} quedó entregado: nació la venta ${venta.numero} al contado y se descontó el stock.';
}

/// Registrar la entrega (1.2; 7.3.2, paso 3): quién recibió, contado o crédito y la ubicación del GPS. La entrega es la
/// venta: la API crea y confirma la HP con los productos del pedido.
class EntregaViewModel extends Notifier<EstadoDeLaEntrega> {
  EntregaViewModel(this.id);

  final int id;

  /// El último envío no tuvo respuesta: la entrega pudo haber quedado registrada.
  var _sinRespuesta = false;

  @override
  EstadoDeLaEntrega build() {
    Future.microtask(cargar);
    return const EstadoDeLaEntrega();
  }

  Future<void> cargar() async {
    state = state.copiar(reintentando: state.errorDeCarga != null);
    try {
      final pedido = await ref.read(pedidosRepositoryProvider).obtener(id);
      if (ref.mounted) state = state.copiar(pedido: pedido, errorDeCarga: null, reintentando: false);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(errorDeCarga: e, reintentando: false);
    }
  }

  void elegirTipo(TipoVenta? tipo) =>
      state = state.copiar(tipoVenta: tipo, campos: {...state.campos}..remove('tipoVenta'));

  void cambioQuienRecibio() {
    if (state.campos.containsKey('recibidoPor')) state = state.copiar(campos: {...state.campos}..remove('recibidoPor'));
  }

  /// Revisa lo obligatorio antes de pedir la confirmación (7.3.2, ext. 3.1a): cada error va en su campo.
  bool revisar(String recibidoPor) {
    final errores = <String, String>{
      if (recibidoPor.trim().isEmpty) 'recibidoPor': 'Escribe quién recibió el pedido.',
      if (state.tipoVenta == null) 'tipoVenta': 'Elige si la venta es al contado o a crédito.',
    };
    state = state.copiar(campos: errores, errorAlEnviar: null, sinUbicacion: false);
    return errores.isEmpty;
  }

  /// Registra la entrega con la ubicación de la tarjeta. Devuelve el aviso de éxito, o null si algo lo impidió: los
  /// errores quedan en el estado y lo escrito no se pierde.
  Future<String?> entregar({required String recibidoPor, required String observacion}) async {
    if (state.enviando || !revisar(recibidoPor)) return null;
    state = state.copiar(enviando: true, esperandoUbicacion: true);

    // Sin respuesta la vez anterior: si la entrega ya quedó, no se envía otra vez (la API diría que ya se entregó).
    if (_sinRespuesta) {
      final ya = await _yaEntregado();
      if (!ref.mounted) return null;
      if (ya != null) {
        state = state.copiar(pedido: ya, enviando: false, esperandoUbicacion: false);
        return mensajeDeEntrega(ya);
      }
    }

    final ubicacion = await ref.read(capturaDeUbicacionProvider(QueSeRegistra.entrega).notifier).paraGuardar();
    if (!ref.mounted) return null;
    if (ubicacion == null) {
      state = state.copiar(enviando: false, esperandoUbicacion: false, sinUbicacion: true);
      return null;
    }

    state = state.copiar(esperandoUbicacion: false);
    final datos = DatosDeEntrega(recibidoPor: recibidoPor, tipoVenta: state.tipoVenta!, observacion: observacion);
    try {
      final pedido = await ref.read(pedidosRepositoryProvider).entregar(id, datos, ubicacion);
      _sinRespuesta = false;
      if (ref.mounted) state = state.copiar(pedido: pedido, enviando: false);
      return mensajeDeEntrega(pedido);
    } on ErrorDeApi catch (e) {
      _sinRespuesta = e.sinConexion;
      if (!ref.mounted) return null;
      // El stock va en su línea; el aviso de abajo dice igual qué pasó, porque el botón está lejos de las líneas.
      final enCampos = e.campos.keys.any((c) => c == 'recibidoPor' || c == 'tipoVenta');
      state = state.copiar(enviando: false, campos: e.campos, errorAlEnviar: enCampos ? null : e);
      return null;
    }
  }

  Future<Pedido?> _yaEntregado() async {
    try {
      final pedido = await ref.read(pedidosRepositoryProvider).obtener(id);
      return pedido.estado.id == EstadosDePedido.entregado ? pedido : null;
    } on ErrorDeApi catch (e) {
      debugPrint('No se pudo revisar si la entrega ya quedó registrada. ${e.mensaje}');
      return null;
    }
  }
}

final entregaProvider = NotifierProvider.autoDispose.family<EntregaViewModel, EstadoDeLaEntrega, int>(
  EntregaViewModel.new,
);
