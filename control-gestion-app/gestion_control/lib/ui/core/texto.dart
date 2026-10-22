const _conTilde = 'áàäâãéèëêíìïîóòöôõúùüûñç';
const _sinTilde = 'aaaaaeeeeiiiiooooouuuunc';

/// Para buscar sin importar mayúsculas ni tildes: "maria" encuentra a "María", como `normalizar` del panel.
String normalizar(String? texto) {
  final minusculas = (texto ?? '').toLowerCase().trim();
  final buffer = StringBuffer();
  for (final letra in minusculas.split('')) {
    final i = _conTilde.indexOf(letra);
    buffer.write(i < 0 ? letra : _sinTilde[i]);
  }
  return buffer.toString();
}

/// Si alguno de los textos contiene la búsqueda. Una búsqueda vacía coincide con todo.
bool coincide(String busqueda, Iterable<String?> textos) {
  final buscado = normalizar(busqueda);
  return buscado.isEmpty || textos.any((t) => normalizar(t).contains(buscado));
}
