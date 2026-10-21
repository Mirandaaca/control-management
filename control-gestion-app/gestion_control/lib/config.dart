/// La URL de la API, que se pasa al compilar y nunca va escrita en el código:
/// `flutter run --dart-define=API_URL=http://<IP de la PC>:5124`. Es la raíz del servidor, sin `/api`.
const apiUrl = String.fromEnvironment('API_URL');

/// La URL base para dio (con `/api` al final), o null si falta o no es una URL http(s) con su servidor.
String? leerUrlDeLaApi(String valor) {
  final uri = Uri.tryParse(valor.trim());
  if (uri == null || !(uri.isScheme('http') || uri.isScheme('https')) || uri.host.isEmpty) {
    return null;
  }
  final raiz = uri.toString().replaceAll(RegExp(r'/+$'), '');
  // Si alguien la pasa ya con /api, no se repite.
  return raiz.endsWith('/api') ? raiz : '$raiz/api';
}
