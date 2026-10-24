import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/router.dart';
import 'rutas/seguimiento_de_ruta.dart';
import 'core/tema.dart';
import 'core/widgets/aviso.dart';

// En español también los textos que trae Flutter, como el botón "Atrás" que lee el lector de pantalla.
const _espanol = Locale('es', 'BO');

class App extends ConsumerStatefulWidget {
  const App({super.key});

  @override
  ConsumerState<App> createState() => _AppState();
}

class _AppState extends ConsumerState<App> {
  // El seguimiento de la ruta se detiene con la app en segundo plano y sigue al volver (7.3.20, paso 4.3).
  late final _ciclo = AppLifecycleListener(
    onStateChange: (ciclo) => ref.read(seguimientoDeRutaProvider.notifier).alCambiarElCiclo(ciclo),
  );

  @override
  void initState() {
    super.initState();
    _ciclo;
  }

  @override
  void dispose() {
    _ciclo.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    // Escucharlo lo mantiene vivo mientras la app está abierta, sin reconstruir la app con cada lectura del GPS.
    ref.listen(seguimientoDeRutaProvider, (_, _) {});
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
