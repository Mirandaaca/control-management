import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cobranza.dart';
import '../../data/services/camara.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/formato.dart';

const _igual = Object();

/// Un comprobante que se intentó subir y falló: "Reintentar" sube la misma foto.
typedef SubidaPendiente = ({int pagoId, FotoTomada foto, bool reemplaza});

class DetalleDeCobranza {
  const DetalleDeCobranza({
    this.cobranza,
    this.error,
    this.reintentando = false,
    this.subiendo,
    this.pendiente,
    this.errorAlSubir,
    this.mensaje,
  });

  /// Null mientras carga o si no se pudo cargar.
  final Cobranza? cobranza;
  final ErrorDeApi? error;
  final bool reintentando;

  /// El pago cuyo comprobante se está subiendo.
  final int? subiendo;
  final SubidaPendiente? pendiente;
  final ErrorDeApi? errorAlSubir;
  final String? mensaje;

  DetalleDeCobranza copiar({
    Object? cobranza = _igual,
    Object? error = _igual,
    bool? reintentando,
    Object? subiendo = _igual,
    Object? pendiente = _igual,
    Object? errorAlSubir = _igual,
    Object? mensaje = _igual,
  }) => DetalleDeCobranza(
    cobranza: cobranza == _igual ? this.cobranza : cobranza as Cobranza?,
    error: error == _igual ? this.error : error as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    subiendo: subiendo == _igual ? this.subiendo : subiendo as int?,
    pendiente: pendiente == _igual ? this.pendiente : pendiente as SubidaPendiente?,
    errorAlSubir: errorAlSubir == _igual ? this.errorAlSubir : errorAlSubir as ErrorDeApi?,
    mensaje: mensaje == _igual ? this.mensaje : mensaje as String?,
  );
}

/// Una cobranza con su historial de pagos (3.1 y 3.2; 7.3.10, paso 3.2), y adjuntar o reemplazar el comprobante de un
/// pago mientras no esté anulado (ext. 2.2c y 3.2a).
class CobranzaViewModel extends Notifier<DetalleDeCobranza> {
  CobranzaViewModel(this.id);

  final int id;

  @override
  DetalleDeCobranza build() {
    Future.microtask(cargar);
    return const DetalleDeCobranza();
  }

  Future<void> cargar() async {
    state = state.copiar(reintentando: state.error != null);
    try {
      final cobranza = await ref.read(cobranzasRepositoryProvider).obtener(id);
      if (ref.mounted) state = state.copiar(cobranza: cobranza, error: null, reintentando: false);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(error: e, reintentando: false);
    }
  }

  void avisar(String mensaje) => state = state.copiar(mensaje: mensaje);

  /// Toma la foto con la cámara y la sube. Si la subida falla, la foto queda para "Reintentar".
  Future<void> adjuntar(Pago pago) async {
    final foto = await ref.read(camaraProvider).tomarFoto();
    if (foto == null || !ref.mounted) return;
    await _subir((pagoId: pago.id, foto: foto, reemplaza: pago.comprobanteId != null));
  }

  Future<void> reintentar() async {
    final pendiente = state.pendiente;
    if (pendiente != null) await _subir(pendiente);
  }

  Future<void> _subir(SubidaPendiente subida) async {
    final pago = state.cobranza?.pagos.where((p) => p.id == subida.pagoId).firstOrNull;
    state = state.copiar(subiendo: subida.pagoId, errorAlSubir: null, mensaje: null);
    try {
      final cobranza = await ref.read(cobranzasRepositoryProvider).subirComprobante(id, subida.pagoId, subida.foto);
      if (!ref.mounted) return;
      final monto = pago == null ? '' : ' de ${formatearMonto(pago.monto)}';
      state = state.copiar(
        cobranza: cobranza,
        subiendo: null,
        pendiente: null,
        mensaje: subida.reemplaza
            ? 'Se reemplazó el comprobante del pago$monto.'
            : 'Se adjuntó el comprobante del pago$monto.',
      );
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(subiendo: null, pendiente: subida, errorAlSubir: e);
    }
  }
}

final cobranzaProvider = NotifierProvider.autoDispose.family<CobranzaViewModel, DetalleDeCobranza, int>(
  CobranzaViewModel.new,
);

/// La foto de un comprobante, para verla.
final fotoDeComprobanteProvider = FutureProvider.autoDispose.family<Uint8List, int>(
  (ref, evidenciaId) => ref.read(cobranzasRepositoryProvider).comprobante(evidenciaId),
);
