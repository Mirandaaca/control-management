/// Una página de una lista larga de la API (`Pagina<T>`): los registros de esta página y el total.
class Pagina<T> {
  const Pagina({required this.items, required this.total, required this.numero, required this.tamano});

  factory Pagina.fromJson(Map<String, dynamic> json, T Function(Map<String, dynamic>) leer) => Pagina(
    items: [for (final item in json['items'] as List) leer(item as Map<String, dynamic>)],
    total: json['total'] as int,
    numero: json['numeroPagina'] as int,
    tamano: json['tamano'] as int,
  );

  final List<T> items;
  final int total;
  final int numero;
  final int tamano;
}

/// Las listas de la app piden de a 25, como el panel (docs/diseno.md, "Listas").
const tamanoDePagina = 25;
