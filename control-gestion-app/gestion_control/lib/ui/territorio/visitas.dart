import '../../data/models/ruta.dart';
import '../core/formato.dart';

// Los textos de las visitas de un cliente (5.2 y 5.3), como components/clientes/visitas.js del panel. Los días se
// cuentan en días de Bolivia.

const _desfaseBolivia = Duration(hours: -4);

DateTime _diaEnBolivia(DateTime utc) {
  final local = utc.toUtc().add(_desfaseBolivia);
  return DateTime.utc(local.year, local.month, local.day);
}

/// Cuántos días faltan desde hoy hasta el día de [valor] (negativo si ya pasó). Null sin fecha.
int? diasDesdeHoy(String? valor, {DateTime? ahora}) {
  final utc = leerFechaUtc(valor);
  if (utc == null) return null;
  return _diaEnBolivia(utc).difference(_diaEnBolivia(ahora ?? DateTime.now())).inDays;
}

String _dias(int n) => n == 1 ? '1 día' : '$n días';

/// Cuánto falta para la próxima visita: "en 15 días", "hoy" o "vencida hace 3 días". Null si nunca se visitó.
String? cuantoFalta(String? proximaVisita, {DateTime? ahora}) {
  final n = diasDesdeHoy(proximaVisita, ahora: ahora);
  if (n == null) return null;
  if (n > 0) return 'en ${_dias(n)}';
  if (n == 0) return 'hoy';
  return 'vencida hace ${_dias(-n)}';
}

/// El atraso de la lista por visitar: "14 días", "hoy" o null si nunca se visitó ("Sin visitas").
String? textoDelAtraso(int? diasDeAtraso) {
  if (diasDeAtraso == null) return null;
  return diasDeAtraso <= 0 ? 'hoy' : _dias(diasDeAtraso);
}

/// El resultado de una visita de la ruta (8.6.9): "Visitado a las 08:40", "Ausente a las 09:20" o "Pendiente · 9 días
/// de atraso". El atraso lo calcula la API el día en que se pide la ruta.
String resultadoDeLaVisita(VisitaDeRuta visita) => switch (visita.resultado) {
  EstadoVisita.visitado => 'Visitado a las ${formatearHora(visita.hora)}',
  EstadoVisita.ausente => 'Ausente a las ${formatearHora(visita.hora)}',
  EstadoVisita.pendiente => switch (visita.diasDeAtraso) {
    null => 'Pendiente · sin visitas',
    <= 0 => 'Pendiente · sin atraso',
    final n => 'Pendiente · ${_dias(n)} de atraso',
  },
};
