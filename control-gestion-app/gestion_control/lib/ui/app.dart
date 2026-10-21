import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/router.dart';
import 'core/tema.dart';
import 'core/widgets/aviso.dart';

// En español también los textos que trae Flutter, como el botón "Atrás" que lee el lector de pantalla.
const _espanol = Locale('es', 'BO');

class App extends ConsumerWidget {
  const App({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return MaterialApp.router(
      title: 'Control de Gestión',
      debugShowCheckedModeBanner: false,
      theme: tema,
      locale: _espanol,
      supportedLocales: const [_espanol],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      routerConfig: ref.watch(routerProvider),
    );
  }
}

/// Si la app se compiló sin API_URL, o con una que no es una URL, lo dice al abrir en vez de fallar en la primera petición.
class AppSinConfiguracion extends StatelessWidget {
  const AppSinConfiguracion({super.key, required this.valor});

  final String valor;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Control de Gestión',
      debugShowCheckedModeBanner: false,
      theme: tema,
      locale: _espanol,
      supportedLocales: const [_espanol],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      home: Scaffold(
        appBar: AppBar(title: const Text('Control de Gestión')),
        body: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Aviso(valor.isEmpty ? 'Falta la URL de la API.' : 'La URL de la API no es válida: "$valor".'),
            const SizedBox(height: 16),
            const Text('Corre la app con la dirección de la PC donde corre la API, sin /api al final:'),
            const SizedBox(height: 8),
            const SelectableText('flutter run --dart-define=API_URL=http://<IP de la PC>:5124'),
          ],
        ),
      ),
    );
  }
}
