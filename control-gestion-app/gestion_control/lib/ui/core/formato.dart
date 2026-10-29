import 'package:intl/intl.dart';

// Bolivia está en UTC−4 todo el año: no tiene horario de verano. Se resta a mano, así la zona del celular no influye.
const _desfaseBolivia = Duration(hours: -4);
final _conZona = RegExp(r'(Z|[+-]\d{2}:?\d{2})$', caseSensitive: false);
final _dia = DateFormat('dd/MM/yyyy');
final _diaYHora = DateFormat('dd/MM/yyyy HH:mm');
final _hora = DateFormat('HH:mm');
final _miles = RegExp(r'\B(?=(\d{3})+(?!\d))');

/// La API guarda en UTC, pero devuelve las fechas leídas de MySQL sin la "Z" final. Sin ella, `DateTime.parse` las
/// tomaría como hora local del celular.
DateTime? leerFechaUtc(String? valor) {
  if (valor == null || valor.isEmpty) return null;
  return DateTime.tryParse(_conZona.hasMatch(valor) ? valor : '${valor}Z')?.toUtc();
}

/// Un momento exacto (un registro, un pago, un evento) en la hora de Bolivia: dd/mm/aaaa, o dd/mm/aaaa hh:mm con
/// `hora: true`. Sin fecha, o con una que no se entiende, devuelve "—".
String formatearFecha(String? valor, {bool hora = false}) {
  final utc = leerFechaUtc(valor);
  if (utc == null) return '—';
  return formatearMomento(utc, hora: hora);
}

/// Solo la hora de un momento, en la hora de Bolivia: "09:00", como en la bandeja del 8.6.10. Sin fecha, "—".
String formatearHora(String? valor) {
  final utc = leerFechaUtc(valor);
  return utc == null ? '—' : _hora.format(utc.add(_desfaseBolivia));
}

/// Un momento en UTC, en la hora de Bolivia.
String formatearMomento(DateTime utc, {bool hora = false}) =>
    // Sigue marcada como UTC, así DateFormat escribe sus campos tal cual, sin pasarlos a la zona del celular.
    (hora ? _diaYHora : _dia).format(utc.toUtc().add(_desfaseBolivia));

/// "Bs 1.234,50": punto de miles, coma decimal y dos decimales, como en Bolivia (docs/diseno.md, "Formato").
String formatearMonto(num monto) => 'Bs ${_numero(monto, decimales: 2)}';

/// Una cantidad sin ceros de más: "120", "1.200" o "12,5".
String formatearCantidad(num cantidad) {
  final texto = _numero(cantidad, decimales: 3);
  return texto.contains(',') ? texto.replaceFirst(RegExp(r',?0+$'), '') : texto;
}

/// Una distancia del camino (tarea 25): "150 m", de 10 en 10, o "1,2 km" desde el kilómetro.
String formatearDistancia(double metros) {
  final redondeados = (metros / 10).round() * 10;
  if (redondeados < 1000) return '$redondeados m';
  return '${_numero(metros / 1000, decimales: 1).replaceFirst(RegExp(r',0$'), '')} km';
}

/// Un tiempo del camino (tarea 25): "8 min" o "1 h 5 min". Lo que falta nunca baja de 1 minuto hasta llegar.
String formatearDuracion(double segundos) {
  final minutos = (segundos / 60).ceil().clamp(1, 1 << 30);
  if (minutos < 60) return '$minutos min';
  final resto = minutos % 60;
  return resto == 0 ? '${minutos ~/ 60} h' : '${minutos ~/ 60} h $resto min';
}

/// Una coordenada con sus 6 decimales (unos 10 cm), con coma decimal: "-17,783412".
String formatearCoordenada(double valor) => valor.toStringAsFixed(6).replaceFirst('.', ',');

String _numero(num valor, {required int decimales}) {
  final partes = valor.abs().toStringAsFixed(decimales).split('.');
  final entero = partes[0].replaceAll(_miles, '.');
  final signo = valor < 0 && partes.any((p) => p.contains(RegExp('[1-9]'))) ? '-' : '';
  return '$signo$entero${partes.length > 1 ? ',${partes[1]}' : ''}';
}

/// Un monto o una cantidad escritos por el usuario, con coma o con punto decimal ("140,00", "12.5"). Null si está vacío
/// o no es un número.
double? leerNumero(String texto) {
  final limpio = texto.trim().replaceAll(' ', '');
  if (limpio.isEmpty) return null;
  return double.tryParse(limpio.replaceAll(',', '.'));
}

/// En centavos: así los montos se comparan sin los errores de redondeo de un double (140,00 contra 139,999…).
int centavos(num monto) => (monto * 100).round();
