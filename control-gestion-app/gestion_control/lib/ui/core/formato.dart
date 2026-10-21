import 'package:intl/intl.dart';

// Bolivia está en UTC−4 todo el año: no tiene horario de verano. Se resta a mano, así la zona del celular no influye.
const _desfaseBolivia = Duration(hours: -4);
final _conZona = RegExp(r'(Z|[+-]\d{2}:?\d{2})$', caseSensitive: false);
final _dia = DateFormat('dd/MM/yyyy');
final _diaYHora = DateFormat('dd/MM/yyyy HH:mm');

/// La API guarda en UTC, pero devuelve las fechas leídas de MySQL sin la "Z" final. Sin ella, `DateTime.parse` las
/// tomaría como hora local del celular.
DateTime? leerFechaUtc(String? valor) {
  if (valor == null || valor.isEmpty) return null;
  return DateTime.tryParse(
    _conZona.hasMatch(valor) ? valor : '${valor}Z',
  )?.toUtc();
}

/// Un momento exacto (un registro, un pago, un evento) en la hora de Bolivia: dd/mm/aaaa, o dd/mm/aaaa hh:mm con
/// `hora: true`. Sin fecha, o con una que no se entiende, devuelve "—".
String formatearFecha(String? valor, {bool hora = false}) {
  final utc = leerFechaUtc(valor);
  if (utc == null) return '—';
  // Sigue marcada como UTC, así DateFormat escribe sus campos tal cual, sin pasarlos a la zona del celular.
  return (hora ? _diaYHora : _dia).format(utc.add(_desfaseBolivia));
}
