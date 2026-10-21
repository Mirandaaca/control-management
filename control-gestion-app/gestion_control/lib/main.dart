import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'config.dart';
import 'proveedores.dart';
import 'ui/app.dart';

void main() {
  final urlDeLaApi = leerUrlDeLaApi(apiUrl);
  if (urlDeLaApi == null) {
    runApp(const AppSinConfiguracion(valor: apiUrl));
    return;
  }
  runApp(ProviderScope(overrides: [urlDeLaApiProvider.overrideWithValue(urlDeLaApi)], child: const App()));
}
