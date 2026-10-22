import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/cobranza.dart';
import '../../data/services/camara.dart';
import '../../data/services/clave_de_idempotencia.dart';
import '../../data/services/error_de_api.dart';
import '../../proveedores.dart';
import '../core/formato.dart';

const _igual = Object();

class EstadoDelPago {
  const EstadoDelPago({
    this.cobranza,
    this.errorDeCarga,
    this.reintentando = false,
    this.metodo = MetodoPago.efectivo,
    this.foto,
    this.campos = const {},
    this.enviando = false,
    this.errorAlEnviar,
    this.registrado,
    this.errorDelComprobante,
  });

  /// Null mientras carga o si no se pudo cargar.
  final Cobranza? cobranza;
  final ErrorDeApi? errorDeCarga;
  final bool reintentando;
  final MetodoPago metodo;

  /// La foto del comprobante, que todavía no se subió: quitarla no pide confirmación.
  final FotoTomada? foto;
  final Map<String, String> campos;
  final bool enviando;
  final ErrorDeApi? errorAlEnviar;

  /// El pago ya quedó registrado y falta subir el comprobante: "Reintentar" sube solo la foto.
  final PagoRegistrado? registrado;
  final ErrorDeApi? errorDelComprobante;

  EstadoDelPago copiar({
    Object? cobranza = _igual,
    Object? errorDeCarga = _igual,
    bool? reintentando,
    MetodoPago? metodo,
    Object? foto = _igual,
    Map<String, String>? campos,
    bool? enviando,
    Object? errorAlEnviar = _igual,
    Object? registrado = _igual,
    Object? errorDelComprobante = _igual,
  }) => EstadoDelPago(
    cobranza: cobranza == _igual ? this.cobranza : cobranza as Cobranza?,
    errorDeCarga: errorDeCarga == _igual ? this.errorDeCarga : errorDeCarga as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    metodo: metodo ?? this.metodo,
    foto: foto == _igual ? this.foto : foto as FotoTomada?,
    campos: campos ?? this.campos,
    enviando: enviando ?? this.enviando,
    errorAlEnviar: errorAlEnviar == _igual ? this.errorAlEnviar : errorAlEnviar as ErrorDeApi?,
    registrado: registrado == _igual ? this.registrado : registrado as PagoRegistrado?,
    errorDelComprobante: errorDelComprobante == _igual ? this.errorDelComprobante : errorDelComprobante as ErrorDeApi?,
  );
}

/// El aviso de éxito del 8.6.12, con los textos del panel (`Cobranzas.jsx`): con el saldo que queda, o pagada.
String mensajeDePago(String numero, double saldo, {required bool conComprobante}) {
  final con = conComprobante ? ', con su comprobante' : '';
  return saldo > 0
      ? 'Se registró el pago en $numero$con: quedan ${formatearMonto(saldo)} por cobrar.'
      : 'Se registró el pago$con: $numero quedó pagada.';
}

/// Registrar un pago (3.1; 7.3.10, 8.6.7): el monto hasta el saldo, efectivo o QR con su referencia, y la foto del
/// comprobante, que se sube después del pago.
class RegistrarPagoViewModel extends Notifier<EstadoDelPago> {
  RegistrarPagoViewModel(this.cobranzaId);

  final int cobranzaId;
  final _clave = ClaveDeIdempotencia();

  @override
  EstadoDelPago build() {
    Future.microtask(cargar);
    return const EstadoDelPago();
  }

  Future<void> cargar() async {
    state = state.copiar(reintentando: state.errorDeCarga != null);
    try {
      final cobranza = await ref.read(cobranzasRepositoryProvider).obtener(cobranzaId);
      if (ref.mounted) state = state.copiar(cobranza: cobranza, errorDeCarga: null, reintentando: false);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(errorDeCarga: e, reintentando: false);
    }
  }

  void elegirMetodo(MetodoPago metodo) =>
      state = state.copiar(metodo: metodo, campos: {...state.campos}..remove('referencia'));

  Future<void> tomarFoto() async {
    final foto = await ref.read(camaraProvider).tomarFoto();
    if (foto != null && ref.mounted) state = state.copiar(foto: foto);
  }

  // Todavía no se subió: quitarla no borra nada, así que no se confirma.
  void quitarFoto() => state = state.copiar(foto: null);

  /// Registra el pago y, si hay foto, sube el comprobante. Devuelve el aviso de éxito, o null si algo lo impidió: los
  /// errores quedan en el estado y lo escrito no se pierde, ni la foto.
  Future<String?> registrar({required String monto, required String referencia, required String observaciones}) async {
    final cobranza = state.cobranza;
    if (cobranza == null || state.enviando) return null;
    // El pago ya quedó registrado: solo falta el comprobante.
    if (state.registrado != null) return reintentarComprobante();

    final valor = leerNumero(monto);
    final errores = <String, String>{
      if (valor == null || centavos(valor) <= 0)
        'monto': 'El monto debe ser mayor que cero.'
      else if (centavos(valor) > centavos(cobranza.saldo))
        'monto': 'El monto no puede superar el saldo (${formatearMonto(cobranza.saldo)}).',
      if (state.metodo == MetodoPago.qr && referencia.trim().isEmpty)
        'referencia': 'Escribe el número de referencia del pago con QR.',
    };
    state = state.copiar(campos: errores, errorAlEnviar: null);
    if (errores.isNotEmpty) return null;

    state = state.copiar(enviando: true);
    final datos = DatosDePago(
      monto: valor!,
      metodo: state.metodo,
      referencia: referencia,
      observaciones: observaciones,
    );
    final PagoRegistrado registrado;
    try {
      registrado = await ref.read(cobranzasRepositoryProvider).registrarPago(cobranzaId, datos, clave: _clave.valor);
      _clave.despuesDelEnvio(huboRespuesta: true);
    } on ErrorDeApi catch (e) {
      _clave.despuesDelEnvio(huboRespuesta: !e.sinConexion);
      if (!ref.mounted) return null;
      state = state.copiar(enviando: false, campos: e.campos, errorAlEnviar: e.campos.isEmpty ? e : null);
      // Otro usuario cambió la cobranza (7.3.10, ext. 2.4a): se muestra el saldo nuevo y el formulario queda igual.
      if (e.codigo == 'conflicto_concurrencia') await cargar();
      return null;
    }
    if (!ref.mounted) return null;
    if (state.foto == null) {
      state = state.copiar(enviando: false);
      return mensajeDePago(cobranza.numero, registrado.saldo, conComprobante: false);
    }
    state = state.copiar(registrado: registrado);
    return reintentarComprobante();
  }

  /// Sube la foto del pago ya registrado. Si falla, el pago no se repite.
  Future<String?> reintentarComprobante() async {
    final registrado = state.registrado;
    final foto = state.foto;
    final cobranza = state.cobranza;
    if (registrado == null || foto == null || cobranza == null) return null;
    state = state.copiar(enviando: true, errorDelComprobante: null);
    try {
      await ref.read(cobranzasRepositoryProvider).subirComprobante(cobranzaId, registrado.id, foto);
      if (ref.mounted) state = state.copiar(enviando: false);
      return mensajeDePago(cobranza.numero, registrado.saldo, conComprobante: true);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(enviando: false, errorDelComprobante: e);
      return null;
    }
  }

  /// El pago quedó registrado y el promotor sigue sin la foto: la adjunta después desde el historial.
  String? terminarSinComprobante() {
    final registrado = state.registrado;
    final cobranza = state.cobranza;
    if (registrado == null || cobranza == null) return null;
    return mensajeDePago(cobranza.numero, registrado.saldo, conComprobante: false);
  }
}

final registrarPagoProvider = NotifierProvider.autoDispose.family<RegistrarPagoViewModel, EstadoDelPago, int>(
  RegistrarPagoViewModel.new,
);
