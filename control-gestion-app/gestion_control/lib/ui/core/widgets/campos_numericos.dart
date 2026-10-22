import 'package:flutter/services.dart';

/// Solo dígitos y una coma decimal, con hasta [decimales] decimales. El punto se escribe como coma: en Bolivia la coma
/// es la decimal, y el teclado numérico de algunos celulares solo trae el punto.
class FormatoDecimal extends TextInputFormatter {
  FormatoDecimal({this.decimales = 2}) : _valido = RegExp('^\\d*(,\\d{0,$decimales})?\$');

  final int decimales;
  final RegExp _valido;

  @override
  TextEditingValue formatEditUpdate(TextEditingValue anterior, TextEditingValue nuevo) {
    final texto = nuevo.text.replaceAll('.', ',');
    if (!_valido.hasMatch(texto)) return anterior;
    return nuevo.copyWith(text: texto);
  }
}

/// El teclado de un monto o una cantidad: numérico y con decimales.
const tecladoDecimal = TextInputType.numberWithOptions(decimal: true);
