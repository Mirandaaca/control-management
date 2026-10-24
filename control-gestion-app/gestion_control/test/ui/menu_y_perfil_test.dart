import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:gestion_control/data/models/sesion.dart';
import 'package:gestion_control/data/services/error_de_api.dart';
import 'package:gestion_control/domain/permisos.dart';
import 'package:gestion_control/proveedores.dart';
import 'package:gestion_control/ui/menu/menu_screen.dart';
import 'package:gestion_control/ui/perfil/cambiar_contrasena_screen.dart';
import 'package:gestion_control/ui/perfil/perfil_screen.dart';
import 'package:gestion_control/ui/sesion/sesion_notifier.dart';

import '../ayudantes.dart';

void main() {
  late SesionRepositoryFalso sesiones;
  late PerfilRepositoryFalso perfiles;

  setUp(() {
    sesiones = SesionRepositoryFalso(token: 'abc');
    perfiles = PerfilRepositoryFalso();
  });

  Future<void> abrir(WidgetTester tester, Widget pantalla, {double letra = 1, Sesion sesion = sesionDePrueba}) async {
    tamanoDeCelular(tester, letra: letra);
    await tester.pumpWidget(
      conLaApp(
        pantalla,
        overrides: [
          sesionRepositoryProvider.overrideWithValue(sesiones),
          perfilRepositoryProvider.overrideWithValue(perfiles),
          sesionProvider.overrideWith(() => SesionFija(ConSesion(sesion))),
        ],
      ),
    );
    await tester.pump();
  }

  Future<void> cumpleLasGuias(WidgetTester tester) async {
    expect(tester.takeException(), isNull);
    await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
    await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
    await expectLater(tester, meetsGuideline(textContrastGuideline));
  }

  group('menú', () {
    testWidgets('muestra quién entró y lo que ya funciona en la app', (tester) async {
      await abrir(tester, const MenuScreen());

      expect(find.text('Pablo Promotor'), findsOneWidget);
      expect(find.text('Promotor'), findsOneWidget);
      expect(find.text('Mi perfil'), findsOneWidget);
      // Va dentro de Mi perfil (colaboración 7.4.31), no en el menú.
      expect(find.text('Cambiar contraseña'), findsNothing);
      expect(find.text('Cerrar sesión'), findsOneWidget);
    });

    testWidgets('una entrada con permisos no se ve sin ninguno de ellos', (tester) async {
      await abrir(tester, const MenuScreen());

      expect(find.text('Bandeja del día'), findsNothing);
      expect(find.text('Pedidos'), findsNothing);
      expect(find.text('Eventos'), findsNothing);
    });

    testWidgets('una entrada con permisos se ve con uno de ellos', (tester) async {
      await abrir(
        tester,
        const MenuScreen(),
        sesion: Sesion(
          id: 'u1',
          email: 'a@test.local',
          nombreCompleto: 'Pablo Promotor',
          rol: 'Promotor',
          permisos: {Permisos.pedidos.verBandeja},
        ),
      );

      expect(find.text('Bandeja del día'), findsOneWidget);
    });

    testWidgets('la ruta, el mapa de clientes y los clientes por visitar, cada uno con su permiso (tarea 17)', (
      tester,
    ) async {
      await abrir(tester, const MenuScreen());
      expect(find.text('Ruta del día'), findsNothing);
      expect(find.text('Mapa de clientes'), findsNothing);
      expect(find.text('Clientes por visitar'), findsNothing);
    });

    testWidgets('con los tres permisos del promotor, las tres entradas', (tester) async {
      await abrir(
        tester,
        const MenuScreen(),
        sesion: Sesion(
          id: 'u1',
          email: 'a@test.local',
          nombreCompleto: 'Pablo Promotor',
          rol: 'Promotor',
          permisos: {
            Permisos.territorial.recorrerRuta,
            Permisos.territorial.verMapaClientes,
            Permisos.territorial.verClientesPorVisitar,
          },
        ),
      );

      expect(find.text('Ruta del día'), findsOneWidget);
      expect(find.text('Mapa de clientes'), findsOneWidget);
      expect(find.text('Clientes por visitar'), findsOneWidget);
    });

    testWidgets('cerrar sesión llama a la API y deja la app sin sesión', (tester) async {
      await abrir(tester, const MenuScreen());

      await tester.tap(find.text('Cerrar sesión'));
      await tester.pump();

      expect(sesiones.cierres, 1);
    });

    testWidgets('con la letra al 200 % cumple las guías de accesibilidad', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, const MenuScreen(), letra: 2);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('perfil (8.3)', () {
    testWidgets('muestra los datos, con la fecha de alta en la hora de Bolivia', (tester) async {
      await abrir(tester, const PerfilScreen());

      expect(tester.widget<TextField>(campo('Nombre completo')).controller!.text, 'Pablo Promotor');
      expect(tester.widget<TextField>(campo('Correo')).controller!.text, 'promotor@test.local');
      expect(find.text('Promotor'), findsOneWidget);
      expect(find.text('19/10/2026'), findsOneWidget);
    });

    testWidgets('si no carga, muestra el error y "Reintentar" lo recupera', (tester) async {
      perfiles.alObtener = () => throw sinConexion;
      await abrir(tester, const PerfilScreen());
      expect(find.text('No se pudo conectar con el servidor.'), findsOneWidget);

      perfiles.alObtener = () async => perfilDePrueba;
      await tester.tap(find.text('Reintentar'));
      await tester.pump();
      await tester.pump();

      expect(perfiles.obtenciones, 2);
      expect(campo('Nombre completo'), findsOneWidget);
    });

    testWidgets('un correo repetido muestra el error en su campo', (tester) async {
      perfiles.alEditar = (_, _) => throw const ErrorDeApi(
        mensaje: 'Ya existe un usuario con ese correo.',
        codigo: 'correo_duplicado',
        estado: 409,
      );
      await abrir(tester, const PerfilScreen());

      await tester.enterText(campo('Correo'), 'otro@test.local');
      await tester.tap(find.text('Guardar cambios'));
      await tester.pump();

      expect(errorDelCampo(tester, 'Correo'), 'Ya existe un usuario con ese correo.');
    });

    testWidgets('al guardar, avisa y trae la sesión de nuevo para el nombre del menú', (tester) async {
      await abrir(tester, const PerfilScreen());

      await tester.enterText(campo('Nombre completo'), 'Pablo Promotor Pérez');
      await tester.tap(find.text('Guardar cambios'));
      await tester.pump();

      expect(perfiles.ediciones.single, ('Pablo Promotor Pérez', 'promotor@test.local'));
      expect(find.text('Se guardaron tus datos.'), findsOneWidget);
      expect(sesiones.pedidosDeYo, 1);
    });

    testWidgets('"Cambiar contraseña" está en el perfil y abre su pantalla (7.3.31, paso 3)', (tester) async {
      await abrirConRouter(
        tester,
        inicio: '/perfil',
        rutas: [
          GoRoute(
            path: '/perfil',
            builder: (_, _) => const PerfilScreen(),
            routes: [GoRoute(path: 'contrasena', builder: (_, state) => Text('En ${state.uri}'))],
          ),
        ],
        overrides: [
          perfilRepositoryProvider.overrideWithValue(perfiles),
          sesionProvider.overrideWith(() => SesionFija(const ConSesion(sesionDePrueba))),
        ],
      );
      await tester.pump();

      await tester.scrollUntilVisible(find.text('Cambiar contraseña'), 200, scrollable: find.byType(Scrollable).first);
      await tester.tap(find.text('Cambiar contraseña'));
      await tester.pumpAndSettle();

      expect(find.text('En /perfil/contrasena'), findsOneWidget);
    });

    testWidgets('con la letra al 200 % cumple las guías de accesibilidad', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, const PerfilScreen(), letra: 2);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });

  group('cambiar contraseña (8.1)', () {
    Future<void> escribir(
      WidgetTester tester, {
      String actual = 'Actual2026',
      String nueva = 'Nueva2026',
      String? repetida,
    }) async {
      await tester.enterText(campo('Contraseña actual'), actual);
      await tester.enterText(campo('Contraseña nueva'), nueva);
      await tester.enterText(campo('Repite la contraseña nueva'), repetida ?? nueva);
      await tester.tap(find.byType(FilledButton));
      await tester.pump();
    }

    String? errorDe(WidgetTester tester, String etiqueta) => errorDelCampo(tester, etiqueta);

    testWidgets('si las dos nuevas no coinciden, lo dice sin enviar nada', (tester) async {
      await abrir(tester, const CambiarContrasenaScreen());

      await escribir(tester, repetida: 'Otra2026');

      expect(errorDe(tester, 'Repite la contraseña nueva'), 'Las dos contraseñas nuevas no coinciden.');
      expect(perfiles.cambios, isEmpty);
    });

    testWidgets('la contraseña actual incorrecta va en su campo', (tester) async {
      perfiles.alCambiar = (_, _) => throw const ErrorDeApi(
        mensaje: 'La contraseña actual no es correcta.',
        codigo: 'contrasena_actual_incorrecta',
        estado: 400,
      );
      await abrir(tester, const CambiarContrasenaScreen());

      await escribir(tester);

      expect(errorDe(tester, 'Contraseña actual'), 'La contraseña actual no es correcta.');
    });

    testWidgets('las reglas de la nueva que rechaza la API van en su campo', (tester) async {
      perfiles.alCambiar = (_, _) => throw const ErrorDeApi(
        mensaje: 'La contraseña debe tener al menos un número.',
        codigo: 'validacion',
        estado: 400,
        campos: {'nueva': 'La contraseña debe tener al menos un número.'},
      );
      await abrir(tester, const CambiarContrasenaScreen());

      await escribir(tester, nueva: 'SinNumeros');

      expect(errorDe(tester, 'Contraseña nueva'), 'La contraseña debe tener al menos un número.');
    });

    testWidgets('al cambiarla, avisa que se cerraron las otras sesiones y vacía los campos', (tester) async {
      await abrir(tester, const CambiarContrasenaScreen());

      await escribir(tester);

      expect(perfiles.cambios.single, ('Actual2026', 'Nueva2026'));
      expect(find.text('Tu contraseña cambió. Se cerraron tus otras sesiones abiertas.'), findsOneWidget);
      expect(tester.widget<TextField>(campo('Contraseña nueva')).controller!.text, isEmpty);
    });

    testWidgets('con la letra al 200 % cumple las guías de accesibilidad', (tester) async {
      final semantica = tester.ensureSemantics();
      await abrir(tester, const CambiarContrasenaScreen(), letra: 2);
      await cumpleLasGuias(tester);
      semantica.dispose();
    });
  });
}
